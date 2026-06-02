using GameServer.Content.Map;
using GameServer.Content.Map.Interface;
using GameServer.Content.Skill;
using GameServer.InGame.Manager.Entity;
using GameServer.InGame.System.Rhythm;
using System;
using System.Collections.Generic;



namespace GameServer.InGame.Manager.Beat
{
    public sealed class BeatActionManager
    {
        private readonly IServerTime _time;
        private readonly IGameBroadcaster _broadcaster;
        private readonly IBeatClock _clock;
        private readonly IGameWorld _world;

        private readonly BeatScheduler _scheduler = new();
        private readonly BeatScheduler _delayedScheduler = new(); // Attack/Skill용 지연 큐
        private readonly FrozenAttackRegistry _frozen;
        private readonly TelegraphScheduler _telegraph;

        private readonly double _actionWindowMs;
        private readonly int _maxBeatLookAhead;

        public event Action<PlayerActionCmd, int, int>? InteractResolved;

        // Move Frequency Limiter
        private readonly Dictionary<int, long> _lastActionBeat = new();
        private readonly Dictionary<int, int> _actionCountInBeat = new();

        // Optimized Buffer
        private readonly List<PlayerActionCmd> _cmdBuffer = new(64);

        public BeatActionManager(
            IServerTime time,
            IGameBroadcaster broadcaster,
            IBeatClock clock,
            IGameWorld world,
            FrozenAttackRegistry frozenAttackRegistry,
            TelegraphScheduler telegraphScheduler,
            double actionWindowMs,
            int maxBeatLookAhead
            )
        {
            _time = time;
            _broadcaster = broadcaster;
            _clock = clock;
            _world = world;
            _frozen = frozenAttackRegistry;
            _telegraph = telegraphScheduler;
            _actionWindowMs = actionWindowMs;
            _maxBeatLookAhead = maxBeatLookAhead;

            if (_clock is RhythmSystem rhythmSys)
            {
                rhythmSys.OnJudgeWindowEnd += OnJudgeWindowEnd;
            }
        }

        public void BroadcastCancelAction(int actorId)
        {
            _broadcaster.Broadcast(new SC_CancelAction { ActorId = actorId });
        }

        /// <summary>
        /// 클라이언트 입력 도착 시 호출.
        /// CS_ActionRequest를 PlayerActionCmd로 변환하고, Beat 판정 후 스케줄에 등록.
        /// </summary>
        public void OnClientActionRequest(int actorId, CS_ActionRequest req)
        {
            long now = req.ClientSendTimeMs;

            if (!_clock.TryComputeJudge(
                    nowMs: now,
                    actionWindowMs: _actionWindowMs,
                    out var judge))
                return;

            if (!judge.IsAccepted)
                return;

            if (!ActionRequestTranslator.TryBuildCmd(actorId, req, judge.ExecuteBeat, judge.DiffMs, now, out var cmd, out var reason))
            {
                Console.WriteLine($"[BeatActionManager] [Reject] Invalid action payload. reason={reason}");
                return;
            }

            // Entity의 바라보는 방향 업데이트
            if (_world.TryGetEntity(actorId, out var actor))
            {
                // InputLock 체크: 스킬 시전 중에는 Move를 포함한 모든 입력 차단
                if (actor.IsInputLocked(judge.ExecuteBeat))
                {
                    Console.WriteLine($"[BeatActionManager] [Reject] InputLocked Actor={actorId} Beat={judge.ExecuteBeat} LockEndBeat={actor.InputLockEndBeat}");
                    return;
                }

                if (Math.Abs(actor.Rotation - cmd.Rotation) > 0.1f)
                {
                    Console.WriteLine($"[BeatActionManager] Actor {actorId} Rotation: {actor.Rotation} -> {cmd.Rotation}");
                    actor.Rotation = cmd.Rotation;
                }
            }

            // --- Move: 즉시 실행 ---
            // --- Action Limit: 1 action per beat ---
            if (!TryConsumeActionLimit(actorId, judge.ExecuteBeat))
            {
                Console.WriteLine($"[BeatActionManager] [Reject] Action limit exceeded for Actor {actorId} at Beat {judge.ExecuteBeat}");
                return; 
            }

            if (cmd.Kind == ActionKind.Move)
            {
                ProcessImmediateMove(cmd, judge.ExecuteBeat);
            }
            else if (cmd.Kind == ActionKind.Interact)
            {
                ProcessImmediateInteract(cmd, judge.ExecuteBeat);
            }
            // --- Skill: 지연 실행 (OnJudgeWindowEnd) ---
            else
            {
                // [Fix] SlotIndex -1 (일반공격 Space키) 포함 SkillId 결정
                ResolveSkillId(cmd);

                _delayedScheduler.Enqueue(cmd);
                Console.WriteLine($"[BeatActionManager] [Resolve] Kind={cmd.Kind} Slot={cmd.SlotIndex} -> SkillId={cmd.SkillId}");

                // 즉각적인 공격 액션 브로드캐스트 (클라이언트 선행 애니메이션용)
                // [Fix] Attack→Skill 통일 후 Skill만 체크
                if (cmd.Kind == ActionKind.Skill)
                {
                    _broadcaster.Broadcast(new SC_ActionInstantBroadcast
                    {
                        ActorId = cmd.ActorId,
                        ActionKind = (int)cmd.Kind,
                        SkillId = cmd.SkillId ?? "Attack",
                        Rotation = cmd.Rotation,
                        StartTick = judge.ExecuteBeat * 480
                    });
                }
            }
        }

        private readonly Dictionary<int, string[]> _activeSkillSlots = new();
        private readonly Dictionary<int, string> _normalAttackSkillId = new();

        public void InjectSkillSlots(int actorId, string[] activeSkills, string normalAttackSkillId)
        {
            _activeSkillSlots[actorId] = activeSkills;
            _normalAttackSkillId[actorId] = normalAttackSkillId;
        }

        /// <summary>
        /// [Fix] SlotIndex 기반 SkillId 결정.
        /// - Attack kind : Skill 호환 경로로 변환
        /// - Skill kind, SlotIndex == -1 : 일반공격이므로 "Attack"으로 처리
        /// - Skill kind, SlotIndex >= 0  : "_activeSkillSlots" 참조
        /// </summary>
        private void ResolveSkillId(PlayerActionCmd cmd)
        {
            string normalAttack = _normalAttackSkillId.TryGetValue(cmd.ActorId, out var natk) && !string.IsNullOrEmpty(natk) ? natk : "Attack";

            if (cmd.Kind == ActionKind.Attack)
            {
                cmd.SkillId = string.IsNullOrWhiteSpace(cmd.SkillId) ? normalAttack : cmd.SkillId;
                cmd.Kind = ActionKind.Skill; // [Fix] Attack→Skill 통일
            }
            else if (cmd.Kind == ActionKind.Skill)
            {
                if (!string.IsNullOrWhiteSpace(cmd.SkillId))
                    return;

                if (cmd.SlotIndex < 0)
                {
                    // SlotIndex -1 = 일반 공격 (Space키)
                    cmd.SkillId = normalAttack;
                    cmd.Kind = ActionKind.Skill; // [Fix] Attack case 제거, 모든 공격은 Skill로 통일
                }
                else
                {
                    if (_activeSkillSlots.TryGetValue(cmd.ActorId, out var slots) && cmd.SlotIndex >= 0 && cmd.SlotIndex < slots.Length)
                    {
                        cmd.SkillId = string.IsNullOrEmpty(slots[cmd.SlotIndex]) ? normalAttack : slots[cmd.SlotIndex];
                    }
                    else
                    {
                        cmd.SkillId = $"Skill{cmd.SlotIndex}"; // Fallback
                    }
                }
            }
        }

        private void ProcessImmediateMove(PlayerActionCmd cmd, long beatIndex)
        {
            if (!_world.TryGetActorPosition(cmd.ActorId, out var fromPos)) return;

            var toPos = cmd.TargetCell;
            bool accepted = _world.TryMove(cmd.ActorId, toPos);
            
            if (!accepted)
                toPos = fromPos;

            _broadcaster.Broadcast(new SC_BeatActions
            {
                BeatIndex = beatIndex,
                beatActionResults = new List<SC_BeatActions.BeatActionResult>
                {
                    new SC_BeatActions.BeatActionResult
                    {
                        ActorId = cmd.ActorId,
                        ActionKind = (int)cmd.Kind,
                        FromX = fromPos.X,
                        FromY = fromPos.Y,
                        ToX = toPos.X,
                        ToY = toPos.Y,
                        Rotation = cmd.Rotation,
                        Accepted = accepted,
                        hpUpdates = new List<SC_BeatActions.BeatActionResult.HpUpdate>()
                    }
                }
            });
        }

        /// <summary>
        /// Town 채널 클라이언트 입력 처리
        /// </summary>
        public void OnTownClientActionRequest(int actorId, CS_TownActionRequest req)
        {
            long now = req.ClientSendTimeMs;

            if (!_clock.TryComputeJudge(
                    nowMs: now,
                    actionWindowMs: _actionWindowMs,
                    out var judge))
                return;

            PrintJudgeBar(judge, now);

            if (!judge.IsAccepted)
            {
                Console.WriteLine($"[ServerAction] REJECT | ClientTime={now} ServerRecv={_time.NowMs} Diff={judge.DiffMs}ms (Window=±{_actionWindowMs})");
                return;
            }

            if (!ActionRequestTranslator.TryBuildCmd(actorId, req, judge.ExecuteBeat, judge.DiffMs, now, out var cmd, out var reason))
            {
                Console.WriteLine($"[Reject] invalid action payload. reason={reason}");
                return;
            }

            if (cmd.Kind == ActionKind.Move)
            {
                ProcessImmediateMove(cmd, judge.ExecuteBeat);
            }
            else if (cmd.Kind == ActionKind.Interact)
            {
                ProcessImmediateInteract(cmd, judge.ExecuteBeat);
            }
            else
            {
                ResolveSkillId(cmd);
                _delayedScheduler.Enqueue(cmd);
                
                if (cmd.Kind == ActionKind.Skill)
                {
                    _broadcaster.Broadcast(new SC_ActionInstantBroadcast
                    {
                        ActorId = cmd.ActorId,
                        ActionKind = (int)cmd.Kind,
                        SkillId = cmd.SkillId ?? "Attack",
                        Rotation = cmd.Rotation,
                        StartTick = judge.ExecuteBeat * 480
                    });
                }
            }

            Console.WriteLine($"[ServerAction] ACCEPT | ClientTime={now} ServerRecv={_time.NowMs} Diff={judge.DiffMs}ms Kind={cmd.Kind}");
        }

        /// <summary>
        /// 캘리브레이션 요청 처리
        /// </summary>
        public void OnClientCalibRequest(int actorId, CS_CalibHit req)
        {
            var now = req.ClientSendTimeMs;

            var currBeat = _clock.GetCurrentBeatIndex(now);
            if (currBeat < 0)
            {
                Console.WriteLine("[OnClientActionRequest] song not started yet");
                return;
            }

            var nearestBeat = _clock.GetNearestBeatIndex(now);
            var judgeTimeMs = _clock.GetBeatTimeMs(nearestBeat);

            int diff = (int)(now - judgeTimeMs);
            int halfSpanMs = (int)Math.Round(_clock.GetBeatDurationMs() * 0.5); 
            
            Console.WriteLine(
                RhythmSystem.FormatJudgeBar(
                    currBeat: currBeat,
                    nextBeat: currBeat + 1,
                    nowMs: now,
                    judgeCenterMs: judgeTimeMs,
                    windowMs: (int)_actionWindowMs,
                    halfSpanMs: halfSpanMs,
                    width: 36,
                    marker: '^'
                )
            );
            
            _broadcaster.Broadcast(new SC_CalibResult
            {
                DiffMs = diff,
                ServerNowMs = now,
                BeatIndex = nearestBeat
            });
        }

        private void ProcessImmediateInteract(PlayerActionCmd cmd, long beatIndex)
        {
            if (!_world.TryGetActorPosition(cmd.ActorId, out var fromPos)) return;

            bool accepted = TryResolveInteractTarget(cmd.TargetCell, out int targetEntityId, out int targetGroupId);
            if (accepted)
                InteractResolved?.Invoke(cmd, targetEntityId, targetGroupId);

            _broadcaster.Broadcast(new SC_BeatActions
            {
                BeatIndex = beatIndex,
                beatActionResults = new List<SC_BeatActions.BeatActionResult>
                {
                    new SC_BeatActions.BeatActionResult
                    {
                        ActorId = cmd.ActorId,
                        ActionKind = (int)ActionKind.Interact,
                        FromX = fromPos.X,
                        FromY = fromPos.Y,
                        ToX = cmd.TargetCell.X,
                        ToY = cmd.TargetCell.Y,
                        Rotation = cmd.Rotation,
                        Accepted = accepted,
                        hpUpdates = new List<SC_BeatActions.BeatActionResult.HpUpdate>()
                    }
                }
            });
        }

        /// <summary>
        /// 서버에서 직접 예약 (몬스터 AI 등).
        /// Move/Wait → _scheduler (OnBeat)
        /// Skill → _delayedScheduler (OnJudgeWindowEnd)
        /// </summary>
        public void ScheduleServerCommand(long beatIndex, PlayerActionCmd cmd)
        {
            cmd.ExecuteBeat = beatIndex;
            ResolveSkillId(cmd);

            if (cmd.Kind == ActionKind.Move || cmd.Kind == ActionKind.Wait)
                _scheduler.Enqueue(cmd);
            else
                _delayedScheduler.Enqueue(cmd);
        }

        public void BroadcastActionInstant(int actorId, ActionKind kind, string skillId, long startTick)
        {
            _broadcaster.Broadcast(new SC_ActionInstantBroadcast
            {
                ActorId = actorId,
                ActionKind = (int)kind,
                SkillId = skillId ?? "Attack",
                Rotation = 0,
                StartTick = startTick
            });
        }

        /// <summary>
        /// Dash/Blink 등 스킬 이동 결과를 SC_BeatActions(Move kind)로 클라이언트에 전달.
        /// 클라이언트 OnBeatAction의 Move 처리경로(StartMove)가 타서 애니메이션이 실행됨.
        /// </summary>
        public void BroadcastMoveResult(int actorId, long beatIndex, int fromX, int fromY, int toX, int toY, float rotation, bool accepted)
        {
            _broadcaster.Broadcast(new SC_BeatActions
            {
                BeatIndex = beatIndex,
                beatActionResults = new List<SC_BeatActions.BeatActionResult>
                {
                    new SC_BeatActions.BeatActionResult
                    {
                        ActorId   = actorId,
                        ActionKind = (int)ActionKind.Move,
                        FromX = fromX, FromY = fromY,
                        ToX   = toX,   ToY   = toY,
                        Rotation  = rotation,
                        Accepted  = accepted,
                        hpUpdates = new List<SC_BeatActions.BeatActionResult.HpUpdate>()
                    }
                }
            });
        }

        /// <summary>
        /// Beat 시작마다 호출 (AI Move 등 처리)
        /// </summary>
        public void OnBeat(long beatIndex)
        {
            _telegraph.OnBeat(beatIndex);

            _scheduler.PopActions(beatIndex, _cmdBuffer);
            if (_cmdBuffer.Count > 0)
            {
                ProcessBatchActions(beatIndex, _cmdBuffer);
                _scheduler.DropBefore(beatIndex - 4);
                _frozen?.DropBefore(beatIndex - 16);
            }
        }

        /// <summary>
        /// Beat 판정 윈도우 종료마다 호출 (플레이어 Attack/Skill 실제 데미지 적용)
        /// </summary>
        public void OnJudgeWindowEnd(long beatIndex)
        {
            _delayedScheduler.PopActions(beatIndex, _cmdBuffer);
            if (_cmdBuffer.Count > 0)
                ProcessBatchActions(beatIndex, _cmdBuffer);
        }

        private void ProcessBatchActions(long beatIndex, List<PlayerActionCmd> cmds)
        {
            var results = new List<SC_BeatActions.BeatActionResult>(cmds.Count);

            foreach (var cmd in cmds)
            {
                if (!_world.TryGetActorPosition(cmd.ActorId, out var fromPos))
                {
                    Console.WriteLine($"[BeatAction] unknown actorId={cmd.ActorId} kind={cmd.Kind}");
                    results.Add(new SC_BeatActions.BeatActionResult
                    {
                        ActorId = cmd.ActorId,
                        ActionKind = (int)cmd.Kind,
                        FromX = 0, FromY = 0, ToX = 0, ToY = 0,
                        Accepted = false
                    });
                    continue;
                }

                var toPos = fromPos;
                bool accepted;
                var hpUpdates = new List<HpUpdate>(4);

                switch (cmd.Kind)
                {
                    case ActionKind.Move:
                        toPos = cmd.TargetCell;
                        accepted = _world.TryMove(cmd.ActorId, toPos);
                        if (!accepted) toPos = fromPos;
                        break;

                    case ActionKind.Interact:
                        accepted = TryResolveInteractTarget(cmd.TargetCell, out int targetEntityId, out int targetGroupId);
                        if (accepted)
                            InteractResolved?.Invoke(cmd, targetEntityId, targetGroupId);
                        toPos = cmd.TargetCell;
                        break;

                    case ActionKind.Skill:
                    {
                        // [Fix] FrozenPop을 최우선으로 체크 (몬스터 AI 패턴Runner가 사전에 PutRaw 해둔 경우)
                        if (_frozen.TryPop(cmd.ActorId, beatIndex, out var frozen))
                        {
                            accepted = _world.TryUseCustomSkill(cmd.ActorId, beatIndex * 480, frozen, hpUpdates);
                        }
                        else if (!string.IsNullOrEmpty(cmd.SkillId)
                            && NewSkillDatabase.TryGet(cmd.SkillId, out var skillDef))
                        {
                            var runner = new SkillRunner(cmd.ActorId, _world, this, _frozen, _telegraph);
                            runner.ExecuteInstant(skillDef, beatIndex * 480, cmd.Rotation, hpUpdates);
                            Console.WriteLine($"[BeatAction] Skill ExecuteInstant: actor={cmd.ActorId} skill={cmd.SkillId} rot={cmd.Rotation} hpUpdates={hpUpdates.Count}");
                            accepted = true;
                        }
                        else
                        {
                            Console.WriteLine($"[BeatAction] Skill not found: actor={cmd.ActorId} skill={cmd.SkillId}");
                            accepted = false;
                        }
                        toPos = fromPos;
                        break;
                    }

                    case ActionKind.Wait:
                    default:
                        accepted = true;
                        toPos = fromPos;
                        break;
                }

                var pktHpUpdates = new List<SC_BeatActions.BeatActionResult.HpUpdate>(hpUpdates.Count);
                if (accepted && hpUpdates.Count > 0)
                {
                    foreach (var u in hpUpdates)
                    {
                        pktHpUpdates.Add(new SC_BeatActions.BeatActionResult.HpUpdate
                        {
                            EntityId = u.EntityId,
                            NewHp = u.NewHp
                        });
                    }
                }

                results.Add(new SC_BeatActions.BeatActionResult
                {
                    ActorId = cmd.ActorId,
                    ActionKind = (int)cmd.Kind,
                    FromX = fromPos.X,
                    FromY = fromPos.Y,
                    ToX = toPos.X,
                    ToY = toPos.Y,
                    Rotation = cmd.Rotation,
                    Accepted = accepted,
                    hpUpdates = pktHpUpdates
                });
            }

            if (results.Count == 0) return;

            _broadcaster.Broadcast(new SC_BeatActions
            {
                BeatIndex = beatIndex,
                beatActionResults = results
            });
        }

        private bool TryResolveInteractTarget(GridPos targetCell, out int targetEntityId, out int targetGroupId)
        {
            targetEntityId = 0;
            targetGroupId = 0;

            foreach (var entity in _world.GetEntitiesAt(targetCell))
            {
                if (entity == null || !entity.IsAlive || entity.Type != EntityType.Object)
                    continue;

                targetEntityId = entity.Id;
                targetGroupId = entity.GetState<int>("GroupId");
                return true;
            }

            return false;
        }

        public void CancelActor(int actorId)
        {
            _scheduler.RemoveByActor(actorId);
        }

        // ============== Util ====================
        private void PrintJudgeBar(JudgeResult judge, long nowMs)
        {
            int halfSpanMs = (int)Math.Round(_clock.GetBeatDurationMs() * 0.5);
            Console.WriteLine(
                RhythmSystem.FormatJudgeBar(
                    currBeat: judge.CurrBeat,
                    nextBeat: judge.NextBeat,
                    nowMs: nowMs,
                    judgeCenterMs: judge.CenterMs,
                    windowMs: (int)_actionWindowMs,
                    halfSpanMs: halfSpanMs,
                    width: 36,
                    marker: '^'
                )
            );
        }

        // --------------------------------------------------------------------
        // Move Frequency Limiter
        // --------------------------------------------------------------------
        private bool TryConsumeActionLimit(int actorId, long beatIndex)
        {
            if (!_lastActionBeat.TryGetValue(actorId, out long lastBeat))
                lastBeat = -1;

            int count = (lastBeat == beatIndex && _actionCountInBeat.TryGetValue(actorId, out int c)) ? c : 0;

            if (count >= GetMaxActionCount(actorId))
                return false;

            _lastActionBeat[actorId] = beatIndex;
            _actionCountInBeat[actorId] = count + 1;
            return true;
        }

        private int GetMaxActionCount(int actorId) => 1;
    }
}



public readonly struct JudgeResult
{
    public readonly long CurrBeat;
    public readonly long NextBeat;
    public readonly long CenterMs;
    public readonly int DiffMs;
    public readonly bool IsAccepted;
    public readonly long ExecuteBeat;

    public JudgeResult(long currBeat, long nextBeat, long centerMs, int diffMs, bool accepted, long executeBeat)
    {
        CurrBeat = currBeat;
        NextBeat = nextBeat;
        CenterMs = centerMs;
        DiffMs = diffMs;
        IsAccepted = accepted;
        ExecuteBeat = executeBeat;
    }
}
