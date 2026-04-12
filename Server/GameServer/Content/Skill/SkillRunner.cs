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
        private NewSkillDef _currentSkill;
        private long _startTick;
        private bool _isRunning;
        private HashSet<object> _executedEvents = new HashSet<object>();
        
        // [Fix] Snapshot for Ground Targeting consistency
        private List<GridPos> _cachedWarningCells;
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
            _executedEvents.Clear();
            _cachedWarningCells = null;
            _casterRotationSnapshot = rotation;

            // Immediately broadcast to clients so they can start visual simulation precisely at currentTick
            _beatActionManager.BroadcastActionInstant(_casterId, ActionKind.Skill, skill.SkillId, currentTick);
        }

        /// <summary>
        /// [즉시 실행] StartSkillTick 없이 스킬을 즉시 실행하고 데미지 결과를 hpUpdates에 담아 반환.
        /// BeatActionManager의 Attack/Skill 케이스에서 2단계(PutRaw→ScheduleServerCommand) 대신 직접 호출.
        /// Warning 이벤트는 텔레그래프 패킷으로 처리, Damage 이벤트는 즉시 _map.TryUseCustomSkill로 적용.
        /// </summary>
        public void ExecuteInstant(NewSkillDef skill, long currentTick, float rotation, List<HpUpdate> hpUpdates)
        {
            _currentSkill = skill;
            _startTick = currentTick;
            _isRunning = true;
            _executedEvents.Clear();
            _cachedWarningCells = null;
            _casterRotationSnapshot = rotation;

            long currentBeat = currentTick / 480;

            // 모든 TriggerTick == 0인 이벤트를 즉시 처리 (1-Beat 스킬 기준)
            foreach (var track in skill.Tracks)
            {
                foreach (var evt in track.Events)
                {
                    if (evt.TriggerTick == 0)
                    {
                        ExecuteActionInstant(evt.Action, (int)currentBeat, evt.DurationTicks, currentTick, hpUpdates);
                    }
                }
            }

            Finish();
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
            foreach (var track in _currentSkill.Tracks)
            {
                foreach (var evt in track.Events)
                {
                    if (evt.TriggerTick <= elapsedTick)
                    {
                        if (!_executedEvents.Contains(evt))
                        {
                            _executedEvents.Add(evt);
                            long currentBeat = currentTick / 480; 
                            ExecuteAction(evt.Action, (int)currentBeat, evt.DurationTicks);
                        }
                    }
                }
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
                    ProcessInputLock((InputLockAction)action);
                    break;
                case SkillActionType.Sound:
                    break;
            }
        }

        /// <summary>
        /// ExecuteInstant 용 즉시 액션 처리.
        /// Damage → 직접 _map.TryUseCustomSkill 호출 (ScheduleServerCommand 사용 안 함).
        /// Warning → 텔레그래프만 전송.
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
                    ProcessInputLock((InputLockAction)action);
                    break;

                case SkillActionType.Sound:
                    break;
            }
        }

        private void Finish()
        {
            _isRunning = false;
            _currentSkill = null;
            _cachedWarningCells = null;
        }

        // --------------------------------------------------------------------
        // Action Processors
        // --------------------------------------------------------------------

        private void ProcessWarning(WarningAction action, int currentBeat, int durationTicks)
        {
            if (!_map.TryGetActorPosition(_casterId, out GridPos casterPos))
                return;

            List<GridPos> cells = CalculateShapeCells(action.Shape, casterPos);
            _cachedWarningCells = cells;

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

        /// <summary>
        /// [기존 2단계 방식] PutRaw → ScheduleServerCommand.
        /// MonsterAI 패턴 스킬 등 비동기 실행이 필요한 경우에 사용.
        /// </summary>
        private void ProcessDamage(DamageAction action, int currentBeat)
        {
            if (!_map.TryGetActorPosition(_casterId, out GridPos casterPos))
                return;

            List<GridPos> targetCells = (_cachedWarningCells != null && _cachedWarningCells.Count > 0)
                ? _cachedWarningCells
                : CalculateShapeCells(action.Shape, casterPos);

            if (targetCells.Count > 0)
            {
                Console.WriteLine($"[Skill_Sync] [ProcessDamage] Caster={_casterId} Rot={_casterRotationSnapshot} Origin=({casterPos.X},{casterPos.Y}) FirstCell=({targetCells[0].X},{targetCells[0].Y}) Rel=({targetCells[0].X - casterPos.X},{targetCells[0].Y - casterPos.Y})");
            }

            // 1. FrozenRegistry에 등록
            _frozen.PutRaw(_casterId, currentBeat, action.Amount, targetCells, action.StunDurationTicks, action.KnockbackDistance, action.HitPlayers, action.HitMonsters);

            // 2. 다음 OnJudgeWindowEnd에서 실행될 Skill 커맨드 예약
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

        /// <summary>
        /// [즉시 실행 방식] 플레이어 공격 OnJudgeWindowEnd 시점에 직접 데미지 적용.
        /// PutRaw + ScheduleServerCommand의 2단계 구조를 거치지 않아 Beat 오차 없음.
        /// </summary>
        private void ProcessDamageInstant(DamageAction action, long currentTick, List<HpUpdate> hpUpdates)
        {
            if (!_map.TryGetActorPosition(_casterId, out GridPos casterPos))
                return;

            List<GridPos> targetCells = (_cachedWarningCells != null && _cachedWarningCells.Count > 0)
                ? _cachedWarningCells
                : CalculateShapeCells(action.Shape, casterPos);

            if (targetCells.Count > 0)
            {
                Console.WriteLine($"[Skill_Sync] [ProcessInstant] Caster={_casterId} Rot={_casterRotationSnapshot} Origin=({casterPos.X},{casterPos.Y}) FirstCell=({targetCells[0].X},{targetCells[0].Y}) Rel=({targetCells[0].X - casterPos.X},{targetCells[0].Y - casterPos.Y})");
            }

            // FrozenAttack 구조를 재활용해서 _map.TryUseCustomSkill 호출
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

            GridPos targetPos = new GridPos(
                currentPos.X + action.DirectionX * action.Distance,
                currentPos.Y + action.DirectionY * action.Distance
            );

            var cmd = new PlayerActionCmd
            {
                ActorId = _casterId,
                Kind = ActionKind.Move,
                TargetCell = targetPos,
                ExecuteBeat = currentBeat,
                ClientSendTimeMs = 0,
                ServerReceiveTimeMs = 0
            };

            _beatActionManager.ScheduleServerCommand(currentBeat, cmd);
        }

        private void ProcessInputLock(InputLockAction action)
        {
            // InputLock 로직 구현
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
            // [Sync] Match ClientSkillRunner's logic (including +180 offset for model forward Z-)
            float corrected = rotation + 180f;
            int deg = (int)((corrected + 45) / 90) * 90;
            deg = (deg % 360 + 360) % 360;

            if (deg == 90)  return new GridPoint( y, -x); // Right
            if (deg == 180) return new GridPoint(-x, -y); // Down
            if (deg == 270) return new GridPoint(-y,  x); // Left
            return new GridPoint(x, y);                  // Up (0)
        }
    }
}
