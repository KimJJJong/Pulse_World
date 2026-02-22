using GameServer.Content.Map;
using GameServer.Content.Map.Interface;
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

        //private readonly int _leadBeat;
        private readonly double _actionWindowMs;
        private readonly int _maxBeatLookAhead;

        // Move Frequency Limiter
        private readonly Dictionary<int, long> _lastMoveBeat = new();
        private readonly Dictionary<int, int> _moveCountInBeat = new();

        // Optimized Buffer
        private readonly List<PlayerActionCmd> _cmdBuffer = new(64);

        public BeatActionManager(
            IServerTime time,
            IGameBroadcaster broadcaster,
            IBeatClock clock,
            IGameWorld world,
            FrozenAttackRegistry frozenAttackRegistry,
            double actionWindowMs,
            int maxBeatLookAhead
            )
        {
            _time = time;
            _broadcaster = broadcaster;
            _clock = clock;
            _world = world;

            _frozen = frozenAttackRegistry;

            _actionWindowMs = actionWindowMs;
            _maxBeatLookAhead = maxBeatLookAhead;

            // RhythmSystem 이벤트 구독 (만약 clock이 RhythmSystem이라면)
            if (_clock is RhythmSystem rhythmSys)
            {
                rhythmSys.OnJudgeWindowEnd += OnJudgeWindowEnd;
            }
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
            {
                // TryComputeJudge 안에서 song not started / out of range 등을 판단
                return;
            }

            // 디버그 바 //Debug
            //PrintJudgeBar(judge, now);

            if (!judge.IsAccepted)
            {   //Debug
                //Console.WriteLine($"[Reject] out of window. diff={judge.DiffMs}ms (±{_actionWindowMs}ms)");
                return;
            }

            // Request -> Cmd 변환 (액션별 파싱/검증)
            if (!ActionRequestTranslator.TryBuildCmd(actorId, req, judge.ExecuteBeat, judge.DiffMs, now, out var cmd, out var reason))
            {
                Console.WriteLine($"[Reject] invalid action payload. reason={reason}");
                return;
            }
            //Debug
            //Console.WriteLine($"[Accept] kind={cmd.Kind} currBeat={judge.CurrBeat} executeBeat={cmd.ExecuteBeat} diff={judge.DiffMs}ms");

            // --- Move: 즉시 실행 ---
            if (cmd.Kind == ActionKind.Move)
            {
                // [Fix] Prevent double move in single beat
                if (!TryConsumeMoveLimit(actorId, judge.ExecuteBeat))
                {
                    // Optional: Log rejection
                    // Console.WriteLine($"[Reject] Move limit exceeded for Actor {actorId} at Beat {judge.ExecuteBeat}");
                    return; 
                }

                ProcessImmediateMove(cmd, judge.ExecuteBeat);
            }
            // --- Attack/Skill: 지연 실행 (OnJudgeWindowEnd) ---
            else
            {
                _delayedScheduler.Enqueue(cmd);
                
                //  즉각적인 공격 액션 브로드캐스트 전송
                if (cmd.Kind == ActionKind.Attack || cmd.Kind == ActionKind.Skill)
                {
                    _broadcaster.Broadcast(new SC_ActionInstantBroadcast
                    {
                        ActorId = cmd.ActorId,
                        ActionKind = (int)cmd.Kind
                    });
                }
            }
        }

        private void ProcessImmediateMove(PlayerActionCmd cmd, long beatIndex)
        {
            if (!_world.TryGetActorPosition(cmd.ActorId, out var fromPos)) return;

            var toPos = cmd.TargetCell;
            bool accepted = _world.TryMove(cmd.ActorId, toPos);
            
            if (!accepted)
            {
                toPos = fromPos;
            }

            // 즉시 브로드캐스트
            var result = new SC_BeatActions.BeatActionResult
            {
                ActorId = cmd.ActorId,
                ActionKind = (int)cmd.Kind,
                FromX = fromPos.X,
                FromY = fromPos.Y,
                ToX = toPos.X,
                ToY = toPos.Y,
                Accepted = accepted,
                hpUpdates = new List<SC_BeatActions.BeatActionResult.HpUpdate>()
            };

            // Move는 단건으로 바로 보냄 (혹은 BeatActions에 담아서 보냄 -> 구조상 배칭이 나을 수도 있으나 '즉시' 요구사항 따름)
            // 여기서는 BeatActions 패킷 포맷을 재활용
             _broadcaster.Broadcast(new SC_BeatActions
            {
                BeatIndex = beatIndex, // 이 비트에 대한 움직임임
                beatActionResults = new List<SC_BeatActions.BeatActionResult> { result }
            });
        }

        /// <summary>
        /// Town
        /// </summary>
        /// <param name="actorId"></param>
        /// <param name="req"></param>
        public void OnTownClientActionRequest(int actorId, CS_TownActionRequest req)
        {
            long now = req.ClientSendTimeMs;

            if (!_clock.TryComputeJudge(
                    nowMs: now,
                    actionWindowMs: _actionWindowMs,
                    out var judge))
            {
                // TryComputeJudge 안에서 song not started / out of range 등을 판단
                return;
            }

            // 디버그 바 (항상 출력)
            PrintJudgeBar(judge, now);

            if (!judge.IsAccepted)
            {
                // [Debug] Log Server Action Reject
                Console.WriteLine($"[ServerAction] REJECT | ClientTime={now} ServerRecv={_time.NowMs} Diff={judge.DiffMs}ms (Window=±{_actionWindowMs})");
                return;
            }

            // Request -> Cmd 변환 (액션별 파싱/검증)
            if (!ActionRequestTranslator.TryBuildCmd(actorId, req, judge.ExecuteBeat, judge.DiffMs, now, out var cmd, out var reason))
            {
                Console.WriteLine($"[Reject] invalid action payload. reason={reason}");
                return;
            }

            // Town에서도 Move 즉시? -> 보통 Town은 전투가 없으므로 비슷하게 처리하거나, 그냥 기존 scheduler 써도 무방하나 일관성 위해 즉시 처리
            if (cmd.Kind == ActionKind.Move)
            {
                ProcessImmediateMove(cmd, judge.ExecuteBeat);
            }
            else
            {
                _delayedScheduler.Enqueue(cmd);
                
                // [NEW] 즉각적인 공격 액션 브로드캐스트 전송
                if (cmd.Kind == ActionKind.Attack || cmd.Kind == ActionKind.Skill)
                {
                    _broadcaster.Broadcast(new SC_ActionInstantBroadcast
                    {
                        ActorId = cmd.ActorId,
                        ActionKind = (int)cmd.Kind
                    });
                }
            }

            // [Debug] Log Server Action Accept
            Console.WriteLine($"[ServerAction] ACCEPT | ClientTime={now} ServerRecv={_time.NowMs} Diff={judge.DiffMs}ms Kind={cmd.Kind}");
        }




        /// <summary>
        /// sync Setting
        /// </summary>
        /// <param name="actorId"></param>
        /// <param name="req"></param>
        public void OnClientCalibRequest(int actorId, CS_CalibHit req)
        {
            var now = req.ClientSendTimeMs;//_time.NowMs;

            // ---- Beat 계산 ----
            var currBeat = _clock.GetCurrentBeatIndex(now);
            if (currBeat < 0)
            {
                Console.WriteLine("[OnClientActionRequest] song not started yet");
                return;
            }

            // [변경] Nearest Beat 기준
            var nearestBeat = _clock.GetNearestBeatIndex(now);
            var judgeTimeMs = _clock.GetBeatTimeMs(nearestBeat); // 이것이 Target Time

            // [Debug] Verify logic
            // Console.WriteLine($"[Calib] Now={now} NearestBeat={nearestBeat} TargetTime={judgeTimeMs}");

            int diff = (int)(now - judgeTimeMs);

            int halfSpanMs = (int)Math.Round(_clock.GetBeatDurationMs() * 0.5); 
            
            // DebugBar: currBeat ~ currBeat+1 사이에서의 위치를 보여주되, Target은 nearestBeat임을 인지
            Console.WriteLine(
                RhythmSystem.FormatJudgeBar(
                    currBeat: currBeat,
                    nextBeat: currBeat + 1,
                    nowMs: now,
                    judgeCenterMs: judgeTimeMs, // Center가 이제 BeatTime
                    windowMs: (int)_actionWindowMs,
                    halfSpanMs: halfSpanMs,
                    width: 36,
                    marker: '^'
                )
            );
            
            var send = new SC_CalibResult();

            send.DiffMs = diff;
            send.ServerNowMs = now;
            send.BeatIndex = nearestBeat; // 판정 대상 Beat

            _broadcaster.Broadcast(/*actorId, */send);

        }





        // 서버에서 직접 예약하고 싶은 명령 (몬스터 AI 등)
        // Move → _scheduler (OnBeat: Beat 시작점에 위치 갱신)
        // Attack/Skill → _delayedScheduler (OnJudgeWindowEnd: 입력 윈도우 종료 후 데미지 적용)
        //   → 플레이어가 Warning을 보고 회피 입력할 시간을 보장
        public void ScheduleServerCommand(long beatIndex, PlayerActionCmd cmd)
        {
            cmd.ExecuteBeat = beatIndex;

            if (cmd.Kind == ActionKind.Move || cmd.Kind == ActionKind.Wait)
            {
                _scheduler.Enqueue(cmd);
            }
            else
            {
                // Attack/Skill: 입력 윈도우가 닫힌 후 실행 (OnJudgeWindowEnd)
                _delayedScheduler.Enqueue(cmd);
            }
        }


        /// <summary>
        /// RhythmSystem에서 Beat가 도래할 때마다 호출.
        /// AI 이동 등 스케줄된 액션 처리. (플레이어 Move는 이미 처리됨)
        /// </summary>
        public void OnBeat(long beatIndex)
        {
            _scheduler.PopActions(beatIndex, _cmdBuffer);
            if (_cmdBuffer.Count > 0)
            {
                ProcessBatchActions(beatIndex, _cmdBuffer);
                
                // Original logic: DropBefore only if processed? 
                // Actually, original code had `if (cmds.Count == 0) return;` before DropBefore.
                // So yes, only if actions existed.
                _scheduler.DropBefore(beatIndex - 4);
                _frozen?.DropBefore(beatIndex - 16);
            }
        }

        public void OnJudgeWindowEnd(long beatIndex)
        {
             _delayedScheduler.PopActions(beatIndex, _cmdBuffer);
             if (_cmdBuffer.Count > 0)
             {
                 ProcessBatchActions(beatIndex, _cmdBuffer);
             }
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
                        FromX = 0,
                        FromY = 0,
                        ToX = 0,
                        ToY = 0,
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
                        // 스케줄러에 들어있던 Move라면 여기서 처리 (AI 등)
                        toPos = cmd.TargetCell;
                        accepted = _world.TryMove(cmd.ActorId, toPos);
                        if (!accepted)
                        {
                            toPos = fromPos;
                        }
                        break;
                    case ActionKind.Attack:
                        {
                            accepted = _world.TryUseAttack(cmd.ActorId, cmd.TargetCell.X, cmd.TargetCell.Y, hpUpdates);
                            break;
                        }

                    case ActionKind.Skill:
                        {
                            if (_frozen.TryPop(cmd.ActorId, beatIndex, out var frozen))
                            {
                                // Logic Extension for NewSkill System
                                if (frozen.CustomDamage.HasValue)
                                {
                                    accepted = _world.TryUseCustomSkill(cmd.ActorId, frozen.CustomDamage.Value, frozen.Cells, hpUpdates);
                                }
                                else
                                {
                                    accepted = _world.TryUseSkillArea(cmd.ActorId, frozen.SkillId, frozen.Cells, hpUpdates);
                                }
                            }
                            else
                            {
                                accepted = _world.TryUseSkill(cmd.ActorId, cmd.SkillId, cmd.TargetCell.X, cmd.TargetCell.Y, hpUpdates);
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

        public void CancelActor(int actorId)
        {
            _scheduler.RemoveByActor(actorId);
        }
   

    ///============== Util ====================
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
        // Move Frequency Limiter Logic
        // --------------------------------------------------------------------

        private bool TryConsumeMoveLimit(int actorId, long beatIndex)
        {
            if (!_lastMoveBeat.TryGetValue(actorId, out long lastBeat))
            {
                lastBeat = -1;
            }

            int count = 0;
            if (lastBeat == beatIndex)
            {
                _moveCountInBeat.TryGetValue(actorId, out count);
            }
            else
            {
                // New beat, reset
                count = 0;
            }

            int max = GetMaxMoveCount(actorId);

            if (count >= max)
            {
                return false;
            }

            // Update State
            _lastMoveBeat[actorId] = beatIndex;
            _moveCountInBeat[actorId] = count + 1;

            return true;
        }

        private int GetMaxMoveCount(int actorId)
        {
            // TODO: Retrieve from Skill/Buff/Stat if needed
            // Currently fixed to 1 per beat
            return 1;
        }
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
