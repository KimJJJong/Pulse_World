using System;
using System.Collections.Generic;
using GameShared.Data;
using GameServer.Content.Map;
using GameServer.InGame.Manager.Beat;
using GameServer.InGame.Manager.Entity;

namespace GameServer.Content.Skill
{
    public class SkillRunner
    {
        private readonly struct TimedSkillEvent
        {
            public readonly SkillEvent Event;
            public readonly int TrackIndex;
            public readonly int EventIndex;

            public TimedSkillEvent(SkillEvent evt, int trackIndex, int eventIndex)
            {
                Event = evt;
                TrackIndex = trackIndex;
                EventIndex = eventIndex;
            }
        }

        private NewSkillDef? _currentSkill;
        private long _startTick;
        private bool _isRunning;
        private readonly List<TimedSkillEvent> _runtimeEvents = new(16);
        private int _nextRuntimeEventIndex;

        private float _casterRotationSnapshot;

        public bool IsRunning => _isRunning;
        private readonly int _casterId;
        private readonly GameServer.Content.Map.Interface.IGameWorld _map;
        private readonly BeatActionManager _beatActionManager;
        private readonly FrozenAttackRegistry _frozen;
        private readonly TelegraphScheduler _telegraph;

        public SkillRunner(int casterId, GameServer.Content.Map.Interface.IGameWorld map, BeatActionManager beatActionManager, FrozenAttackRegistry frozen, TelegraphScheduler telegraph)
        {
            _casterId = casterId;
            _map = map;
            _beatActionManager = beatActionManager;
            _frozen = frozen;
            _telegraph = telegraph;
        }

        public void StartSkillTick(NewSkillDef skill, long currentTick, float rotation)
        {
            _currentSkill = skill;
            _startTick = currentTick;
            _isRunning = true;
            BuildSortedEvents(skill, _runtimeEvents, includeAllEvents: true, elapsedTick: 0);
            _nextRuntimeEventIndex = 0;
            _casterRotationSnapshot = rotation;

            _beatActionManager.BroadcastActionInstant(_casterId, ActionKind.Skill, skill.SkillId, currentTick, rotation);
        }

        /// <summary>
        /// [즉시 실행] StartSkillTick 없이 스킬을 즉시 실행.
        /// - TriggerTick == 0 : 즉시 처리 (Damage, Warning, InputLock 등)
        /// - TriggerTick  > 0 : Beat 단위 예약 (Damage만, InputLock도 처리)
        /// </summary>
        public void ExecuteInstant(NewSkillDef skill, long currentTick, float rotation, List<HpUpdate> hpUpdates)
        {
            _currentSkill = skill;
            _startTick = currentTick;
            _isRunning = true;
            _runtimeEvents.Clear();
            _nextRuntimeEventIndex = 0;
            _casterRotationSnapshot = rotation;

            long currentBeat = currentTick / 480;

            var sortedEvents = CollectSortedEvents(skill, includeAllEvents: true, elapsedTick: currentTick - _startTick);

            GridPos simulatedOrigin = default;
            bool hasSimulatedOrigin = _map.TryGetActorPosition(_casterId, out simulatedOrigin);

            foreach (var entry in sortedEvents)
            {
                var evt = entry.Event;

                if (evt.TriggerTick == 0)
                {
                    ExecuteActionInstant(evt.Action, (int)currentBeat, evt.DurationTicks, currentTick, hpUpdates);

                    if (evt.Action is MoveAction && _map.TryGetActorPosition(_casterId, out var movedPos))
                    {
                        simulatedOrigin = movedPos;
                        hasSimulatedOrigin = true;
                    }

                    continue;
                }

                if (!hasSimulatedOrigin && !_map.TryGetActorPosition(_casterId, out simulatedOrigin))
                    continue;

                ExecuteActionDelayed(evt.Action, (int)currentBeat, evt.TriggerTick, evt.DurationTicks, ref simulatedOrigin);
                hasSimulatedOrigin = true;
            }

            Finish();
        }

        /// <summary>
        /// TriggerTick > 0 인 이벤트를 Beat 단위로 예약.
        /// Damage/Move → 해당 비트에 맞춰 예약
        /// InputLock → 시전자 Entity에 즉시 세팅 (잠금 시작은 스킬 시전 시점)
        /// </summary>
        private void ExecuteActionDelayed(BaseAction action, int baseBeat, int triggerTick, int durationTicks, ref GridPos simulatedOrigin)
        {
            if (action == null) return;

            var type = action.GetSkillActionType();
            long targetBeat = baseBeat + (triggerTick / 480);

            // InputLock: TriggerTick > 0이어도 잠금 시작은 스킬 시전(Beat N) 기준
            if (type == SkillActionType.InputLock)
            {
                ProcessInputLock((InputLockAction)action, durationTicks);
                return;
            }

            if (type == SkillActionType.Warning && action is WarningAction warningAction)
            {
                ProcessWarningAt(warningAction, (int)targetBeat, durationTicks, simulatedOrigin);
                return;
            }

            if (type == SkillActionType.Move && action is MoveAction moveAction)
            {
                if (!TryResolveMoveTarget(simulatedOrigin, moveAction, out var targetPos))
                    targetPos = simulatedOrigin;

                var cmd = new PlayerActionCmd
                {
                    ActorId = _casterId,
                    Kind = ActionKind.Move,
                    SkillId = _currentSkill?.SkillId ?? "",
                    TargetCell = targetPos,
                    ExecuteBeat = targetBeat,
                    Rotation = _casterRotationSnapshot,
                    ClientSendTimeMs = 0,
                    ServerReceiveTimeMs = 0
                };

                _beatActionManager.ScheduleServerCommand(targetBeat, cmd);
                simulatedOrigin = targetPos;
                Console.WriteLine($"[SkillRunner] Delayed Move scheduled: Caster={_casterId} Skill={_currentSkill?.SkillId} Beat={targetBeat} Target={targetPos}");
                return;
            }

            if (type == SkillActionType.SummonDecoy && action is SummonDecoyAction summonAction)
            {
                ProcessSummonDecoyAt(summonAction, (int)targetBeat, simulatedOrigin);
                return;
            }

            // Damage만 Beat 예약
            if (type != SkillActionType.Damage) return;
            if (action is not DamageAction damageAction) return;

            List<GridPos> targetCells = CalculateShapeCells(damageAction.Shape, simulatedOrigin);

            _frozen.PutRaw(_casterId, (int)targetBeat, damageAction.Amount, targetCells,
                damageAction.StunDurationTicks, damageAction.KnockbackDistance,
                damageAction.HitPlayers, damageAction.HitMonsters);

            var damageCmd = new PlayerActionCmd
            {
                ActorId = _casterId,
                Kind = ActionKind.Skill,
                SkillId = _currentSkill?.SkillId ?? "",
                TargetCell = simulatedOrigin,
                ExecuteBeat = targetBeat,
                Rotation = _casterRotationSnapshot,
                ClientSendTimeMs = 0,
                ServerReceiveTimeMs = 0
            };

            _beatActionManager.ScheduleServerCommand(targetBeat, damageCmd);
            Console.WriteLine($"[SkillRunner] Delayed Damage scheduled: Caster={_casterId} Skill={_currentSkill?.SkillId} Beat={targetBeat} DMG={damageAction.Amount} Origin={simulatedOrigin}");
        }

        public void UpdateTick(long currentTick)
        {
            if (!_isRunning || _currentSkill == null) return;

            long elapsedTick = currentTick - _startTick;

            if (elapsedTick < 0) return;
            if (elapsedTick > _currentSkill.TotalDurationTicks)
            {
                Finish();
                return;
            }

            ProcessEventsTick(currentTick, elapsedTick);
        }

        private void ProcessEventsTick(long currentTick, long elapsedTick)
        {
            while (_nextRuntimeEventIndex < _runtimeEvents.Count)
            {
                var entry = _runtimeEvents[_nextRuntimeEventIndex];
                var evt = entry.Event;
                if (evt.TriggerTick > elapsedTick)
                    break;

                _nextRuntimeEventIndex++;
                long currentBeat = currentTick / 480;
                ExecuteAction(evt.Action, (int)currentBeat, evt.DurationTicks);
            }
        }

        private void ExecuteAction(BaseAction action, int currentBeat, int durationTicks)
        {
            if (action == null) return;

            switch (action.GetSkillActionType())
            {
                case SkillActionType.Warning:
                    ProcessWarning((WarningAction)action, currentBeat, durationTicks);
                    break;
                case SkillActionType.Damage:
                    ProcessDamage((DamageAction)action, currentBeat);
                    break;
                case SkillActionType.Move:
                    ProcessMove((MoveAction)action, currentBeat);
                    break;
                case SkillActionType.InputLock:
                    ProcessInputLock((InputLockAction)action, durationTicks);
                    break;
                case SkillActionType.Sound:
                    break;
                case SkillActionType.SummonDecoy:
                    ProcessSummonDecoy((SummonDecoyAction)action, currentBeat);
                    break;
            }
        }

        /// <summary>
        /// ExecuteInstant 용 즉시 액션 처리.
        /// Damage → 직접 TryUseCustomSkill, Warning → 텔레그래프, InputLock → Entity 세팅
        /// </summary>
        private void ExecuteActionInstant(BaseAction action, int currentBeat, int durationTicks, long currentTick, List<HpUpdate> hpUpdates)
        {
            if (action == null) return;

            switch (action.GetSkillActionType())
            {
                case SkillActionType.Warning:
                    ProcessWarning((WarningAction)action, currentBeat, durationTicks);
                    break;
                case SkillActionType.Damage:
                    ProcessDamageInstant((DamageAction)action, currentTick, hpUpdates);
                    break;
                case SkillActionType.Move:
                    ProcessMove((MoveAction)action, currentBeat);
                    break;
                case SkillActionType.InputLock:
                    ProcessInputLock((InputLockAction)action, durationTicks);
                    break;
                case SkillActionType.Sound:
                    break;
                case SkillActionType.SummonDecoy:
                    ProcessSummonDecoy((SummonDecoyAction)action, currentBeat);
                    break;
            }
        }

        private void Finish()
        {
            _isRunning = false;
            _currentSkill = null;
            _runtimeEvents.Clear();
            _nextRuntimeEventIndex = 0;
        }

        private List<TimedSkillEvent> CollectSortedEvents(NewSkillDef skill, bool includeAllEvents, long elapsedTick)
        {
            var events = new List<TimedSkillEvent>();
            BuildSortedEvents(skill, events, includeAllEvents, elapsedTick);
            return events;
        }

        private static void BuildSortedEvents(NewSkillDef skill, List<TimedSkillEvent> events, bool includeAllEvents, long elapsedTick)
        {
            events.Clear();
            if (skill == null) return;

            for (int t = 0; t < skill.Tracks.Count; t++)
            {
                var track = skill.Tracks[t];
                for (int e = 0; e < track.Events.Count; e++)
                {
                    var evt = track.Events[e];
                    if (includeAllEvents || evt.TriggerTick <= elapsedTick)
                        events.Add(new TimedSkillEvent(evt, t, e));
                }
            }

            events.Sort((a, b) =>
            {
                int cmp = a.Event.TriggerTick.CompareTo(b.Event.TriggerTick);
                if (cmp != 0) return cmp;

                cmp = GetActionPriority(a.Event.Action).CompareTo(GetActionPriority(b.Event.Action));
                if (cmp != 0) return cmp;

                cmp = a.TrackIndex.CompareTo(b.TrackIndex);
                if (cmp != 0) return cmp;

                return a.EventIndex.CompareTo(b.EventIndex);
            });
        }

        private static int GetActionPriority(BaseAction action)
        {
            if (action == null) return int.MaxValue;

            return action.GetSkillActionType() switch
            {
                SkillActionType.Move => 0,
                SkillActionType.SummonDecoy => 1,
                SkillActionType.Warning => 2,
                SkillActionType.InputLock => 3,
                SkillActionType.Damage => 4,
                SkillActionType.Sound => 5,
                SkillActionType.Wait => 6,
                _ => 6
            };
        }

        private bool TryResolveMoveTarget(GridPos fromPos, MoveAction action, out GridPos targetPos)
        {
            var rotated = RotateDirForMove(action.DirectionX, action.DirectionY, _casterRotationSnapshot);
            int dirX = rotated.X;
            int dirY = rotated.Y;

            switch (action.MoveType)
            {
                case MoveType.Dash:
                    return _map.TryPreviewDash(fromPos, dirX, dirY, action.Distance, out targetPos);
                case MoveType.Blink:
                    return _map.TryPreviewBlink(fromPos, dirX, dirY, action.Distance, out targetPos);
                default:
                    targetPos = new GridPos(
                        fromPos.X + dirX * action.Distance,
                        fromPos.Y + dirY * action.Distance);
                    return true;
            }
        }

        // --------------------------------------------------------------------
        // Action Processors
        // --------------------------------------------------------------------

        private void ProcessWarning(WarningAction action, int currentBeat, int durationTicks)
        {
            if (!_map.TryGetActorPosition(_casterId, out GridPos casterPos))
                return;

            ProcessWarningAt(action, currentBeat, durationTicks, casterPos);
        }

        private void ProcessWarningAt(WarningAction action, int currentBeat, int durationTicks, GridPos casterPos)
        {
            List<GridPos> cells = CalculateShapeCells(action.Shape, casterPos);

            var tg = new SC_BeatTelegraphs.Telegraphs
            {
                CasterId = _casterId,
                StyleId = 1,
                DurationTicks = durationTicks,
                Shape = (byte)TelegraphShape.Cells,
                OriginType = (byte)TelegraphOriginType.Point,
                OriginX = 0,
                OriginY = 0,
                ParamA = 0,
                ParamB = 0,
                cellss = new List<SC_BeatTelegraphs.Telegraphs.Cells>()
            };

            foreach (var c in cells)
                tg.cellss.Add(new SC_BeatTelegraphs.Telegraphs.Cells { X = c.X, Y = c.Y });

            _telegraph.Schedule(currentBeat, tg);
        }

        private void ProcessDamage(DamageAction action, int currentBeat)
        {
            if (!_map.TryGetActorPosition(_casterId, out GridPos casterPos))
                return;

            List<GridPos> targetCells = CalculateShapeCells(action.Shape, casterPos);

            _frozen.PutRaw(_casterId, currentBeat, action.Amount, targetCells, action.StunDurationTicks, action.KnockbackDistance, action.HitPlayers, action.HitMonsters);

            var cmd = new PlayerActionCmd
            {
                ActorId = _casterId,
                Kind = ActionKind.Skill,
                SkillId = _currentSkill?.SkillId ?? "",
                TargetCell = casterPos,
                ExecuteBeat = currentBeat,
                ClientSendTimeMs = 0,
                ServerReceiveTimeMs = 0
            };

            _beatActionManager.ScheduleServerCommand(currentBeat, cmd);
        }

        private void ProcessDamageInstant(DamageAction action, long currentTick, List<HpUpdate> hpUpdates)
        {
            if (!_map.TryGetActorPosition(_casterId, out GridPos casterPos))
                return;

            List<GridPos> targetCells = CalculateShapeCells(action.Shape, casterPos);

            if (targetCells.Count > 0)
            {
                Console.WriteLine($"[Skill_Sync] [ProcessInstant] Caster={_casterId} Rot={_casterRotationSnapshot} Origin=({casterPos.X},{casterPos.Y}) FirstCell=({targetCells[0].X},{targetCells[0].Y}) Rel=({targetCells[0].X - casterPos.X},{targetCells[0].Y - casterPos.Y})");
            }

            var frozen = new FrozenAttackRegistry.FrozenAttack
            {
                SkillId = _currentSkill?.SkillId ?? "",
                Cells = targetCells,
                CustomDamage = action.Amount,
                StunDurationTicks = action.StunDurationTicks,
                KnockbackDistance = action.KnockbackDistance,
                HitPlayers = action.HitPlayers,
                HitMonsters = action.HitMonsters
            };

            _map.TryUseCustomSkill(_casterId, currentTick, frozen, hpUpdates);
        }

        private void ProcessMove(MoveAction action, int currentBeat)
        {
            if (!_map.TryGetActorPosition(_casterId, out GridPos currentPos))
                return;

            // Move용 회전: 스킬 JSON Dir은 Forward=+Y 기준, +180 offset 없이 순수 회전
            var rotated = RotateDirForMove(action.DirectionX, action.DirectionY, _casterRotationSnapshot);
            int dirX = rotated.X;
            int dirY = rotated.Y;

            GridPos targetPos;
            bool success;

            switch (action.MoveType)
            {
                case MoveType.Dash:
                    success = _map.TryDash(_casterId, dirX, dirY, action.Distance, out targetPos);
                    if (!success) targetPos = currentPos;
                    break;

                case MoveType.Blink:
                    success = _map.TryBlink(_casterId, dirX, dirY, action.Distance, out targetPos);
                    if (!success) targetPos = currentPos;
                    break;

                default: // Walk
                    targetPos = new GridPos(
                        currentPos.X + dirX * action.Distance,
                        currentPos.Y + dirY * action.Distance);
                    success = true;
                    break;
            }

            if (action.MoveType == MoveType.Walk)
            {
                // Walk: BeatScheduler를 통해 이동 (TryMove)
                var cmd = new PlayerActionCmd
                {
                    ActorId = _casterId,
                    Kind = ActionKind.Move,
                    TargetCell = targetPos,
                    ExecuteBeat = currentBeat,
                    Rotation = _casterRotationSnapshot,
                    ClientSendTimeMs = 0,
                    ServerReceiveTimeMs = 0
                };
                _beatActionManager.ScheduleServerCommand(currentBeat, cmd);
            }
            else
            {
                // Dash/Blink: 이미 직접 이동 완료 — 클라이언트에 Move 결과 브로드캐스트
                // 클라이언트 OnBeatAction(Move) 통해 StartMove 호출되어 애니메이션 실행
                _beatActionManager.BroadcastMoveResult(
                    _casterId,
                    currentBeat,
                    fromX: currentPos.X, fromY: currentPos.Y,
                    toX: targetPos.X,   toY: targetPos.Y,
                    rotation: _casterRotationSnapshot,
                    accepted: success);

                Console.WriteLine($"[SkillRunner] {action.MoveType} Caster={_casterId} ({currentPos.X},{currentPos.Y})->({targetPos.X},{targetPos.Y}) success={success}");
            }
        }

        private void ProcessSummonDecoy(SummonDecoyAction action, int currentBeat)
        {
            if (!_map.TryGetActorPosition(_casterId, out GridPos casterPos))
                return;

            ProcessSummonDecoyAt(action, currentBeat, casterPos);
        }

        private void ProcessSummonDecoyAt(SummonDecoyAction action, int currentBeat, GridPos origin)
        {
            int durationTicks = Math.Max(480, action.DurationTicks);
            long expireBeat = currentBeat + Math.Max(1, (durationTicks + 479) / 480);

            foreach (var candidate in BuildDecoyCandidates(action, origin))
            {
                if (_map.TrySpawnDecoy(_casterId, action.AppearanceId, action.Hp, candidate, currentBeat, expireBeat))
                    return;
            }
        }

        private IEnumerable<GridPos> BuildDecoyCandidates(SummonDecoyAction action, GridPos origin)
        {
            var candidates = new List<GridPos>(6);
            var offset = action.RotateWithCaster
                ? RotateDirForMove(action.OffsetX, action.OffsetY, _casterRotationSnapshot)
                : new GridPoint(action.OffsetX, action.OffsetY);

            AddDecoyCandidate(candidates, new GridPos(origin.X + offset.X, origin.Y + offset.Y));
            AddDecoyCandidate(candidates, new GridPos(origin.X, origin.Y - 1));
            AddDecoyCandidate(candidates, new GridPos(origin.X + 1, origin.Y));
            AddDecoyCandidate(candidates, new GridPos(origin.X - 1, origin.Y));
            AddDecoyCandidate(candidates, new GridPos(origin.X, origin.Y + 1));
            return candidates;
        }

        private static void AddDecoyCandidate(List<GridPos> candidates, GridPos candidate)
        {
            foreach (var existing in candidates)
            {
                if (existing.X == candidate.X && existing.Y == candidate.Y)
                    return;
            }

            candidates.Add(candidate);
        }

        /// <summary>
        /// Move 전용 회전: +180 offset 없이 순수하게 방향 회전
        /// 에디터에서 Forward=+Y 기준으로 설정한 Dir을 실제 바라보는 방향으로 변환
        /// </summary>
        private GridPoint RotateDirForMove(int x, int y, float rotation)
        {
            int deg = (int)((rotation + 45) / 90) * 90;
            deg = (deg % 360 + 360) % 360;

            if (deg == 90)  return new GridPoint( y, -x); // East
            if (deg == 180) return new GridPoint(-x, -y); // South
            if (deg == 270) return new GridPoint(-y,  x); // West
            return new GridPoint(x, y);                  // North (0)
        }

        /// <summary>Shape 회전에 사용 (Attack 방향 보정 +180 offset 포함)</summary>
        private GridPoint RotateDir(int x, int y, float rotation)
        {
            return RotateGridPoint(x, y, rotation);
        }

        /// <summary>
        /// InputLock: 시전자 Entity에 입력 봉인 Beat 세팅.
        /// durationTicks 기준으로 lockEndBeat 계산.
        /// 더 늦게 끝나는 Lock이 이미 세팅된 경우 갱신(최대값 유지).
        /// </summary>
        private void ProcessInputLock(InputLockAction action, int durationTicks)
        {
            if (!_map.TryGetEntity(_casterId, out var caster)) return;

            // _startTick + durationTicks 까지 잠금 (Beat 올림 변환)
            long lockEndBeat = (_startTick + durationTicks + 479) / 480;

            if (lockEndBeat > caster.InputLockEndBeat)
            {
                caster.InputLockEndBeat = lockEndBeat;
                Console.WriteLine($"[SkillRunner] InputLock: Caster={_casterId} LockEndBeat={lockEndBeat} (Skill={_currentSkill?.SkillId}, DurationTicks={durationTicks})");
            }
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        private float GetCasterRotation() => _casterRotationSnapshot;

        private List<GridPos> CalculateShapeCells(IShapeDef shape, GridPos origin)
        {
            List<GridPos> cells = new List<GridPos>();
            float rotation = GetCasterRotation();

            if (shape is DiamondShape diamond)
            {
                int r = diamond.Radius;
                for (int x = -r; x <= r; x++)
                    for (int y = -r; y <= r; y++)
                        if (Math.Abs(x) + Math.Abs(y) <= r)
                        {
                            var pt = RotateGridPoint(x, y, shape.RotateWithCaster ? rotation : 0);
                            cells.Add(new GridPos(origin.X + pt.X, origin.Y + pt.Y));
                        }
            }
            else if (shape is RectShape rect)
            {
                int halfW = rect.Width / 2;
                int halfH = rect.Height / 2;
                for (int x = -halfW; x <= halfW; x++)
                    for (int y = -halfH; y <= halfH; y++)
                    {
                        var pt = RotateGridPoint(x, y, shape.RotateWithCaster ? rotation : 0);
                        cells.Add(new GridPos(origin.X + pt.X, origin.Y + pt.Y));
                    }
            }
            else if (shape is CustomCellsShape custom)
            {
                foreach (var p in custom.Cells)
                {
                    var pt = RotateGridPoint(p.X, p.Y, shape.RotateWithCaster ? rotation : 0);
                    cells.Add(new GridPos(origin.X + pt.X, origin.Y + pt.Y));
                }
            }
            return cells;
        }

        private GridPoint RotateGridPoint(int x, int y, float rotation)
        {
            float corrected = rotation + 180f;
            int deg = (int)((corrected + 45) / 90) * 90;
            deg = (deg % 360 + 360) % 360;

            if (deg == 90)  return new GridPoint( y, -x);
            if (deg == 180) return new GridPoint(-x, -y);
            if (deg == 270) return new GridPoint(-y,  x);
            return new GridPoint(x, y);
        }
    }
}
