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
        private int _startBeat;
        private bool _isRunning;
        
        // [Fix] Snapshot for Ground Targeting consistency
        private List<GridPos> _cachedWarningCells;

        public bool IsRunning => _isRunning;
        private readonly int _casterId;
        private readonly GameServer.Content.Map.Interface.IGameWorld _map; // for Position check only
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

        public void StartSkill(NewSkillDef skill, int currentBeat)
        {
            _currentSkill = skill;
            _startBeat = currentBeat;
            _isRunning = true;
        }

        public void Update(int currentBeat)
        {
            if (!_isRunning || _currentSkill == null) return;

            int beatOffset = currentBeat - _startBeat;
            //Console.WriteLine($"[SkillRunner] Update: current={currentBeat}, start={_startBeat}, offset={beatOffset}");

            if (beatOffset < 0) return;
            if (beatOffset > _currentSkill.TotalDurationBeats)
            {
                Finish();
                return;
            }

            ProcessEvents(currentBeat, beatOffset);
        }

        private void ProcessEvents(int currentBeat, int beatOffset)
        {
            foreach (var track in _currentSkill.Tracks)
            {
                foreach (var evt in track.Events)
                {
                    // [Debug] checking event trigger
                    if (evt.TriggerBeat == beatOffset)
                    {
                        //Console.WriteLine($"[SkillRunner] Event Triggered! Type={evt.Action?.GetSkillActionType()} at offset {beatOffset}");
                        ExecuteAction(evt.Action, currentBeat);
                    }
                }
            }
        }

        private void ExecuteAction(BaseAction action, int currentBeat)
        {
            if (action == null) return;
            //Console.WriteLine($"[SkillRunner] ExecuteAction: {action.GetSkillActionType()} at Beat {currentBeat}");

            switch (action.GetSkillActionType())
            {
                case SkillActionType.Warning:
                    ProcessWarning((WarningAction)action, currentBeat);
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

        private void ProcessWarning(WarningAction action, int currentBeat)
        {
            if (!_map.TryGetActorPosition(_casterId, out GridPos casterPos))
                return;

            List<GridPos> cells = CalculateShapeCells(action.Shape, casterPos);
            
            // [Debug] CustomCells Validation
            if (action.Shape.GetShapeType() == ShapeType.CustomCells)
                Console.WriteLine($"[SkillRunner] Warning CustomCells: {cells.Count} cells generated from Shape.");

            // [Fix] Snapshot the warned cells to ensure Damage hits the exact warned area (Ground Targeting),
            // even if the caster moves afterwards.
            _cachedWarningCells = cells;

            // Build Telegraph Packet
            var tg = new SC_BeatTelegraphs.Telegraphs
            {
                CasterId = _casterId,
                StyleId = 1, // Default Style (or add style to WarningAction)
                DurationBeats = 1, // Warning duration (needs checking action duration if available)
                Shape = (byte)TelegraphShape.Cells,
                OriginType = (byte)TelegraphOriginType.Point,
                OriginX = 0,
                OriginY = 0,
                ParamA = 0,
                ParamB = 0,
                cellss = new List<SC_BeatTelegraphs.Telegraphs.Cells>()
            };

            foreach (var c in cells)
            {
                tg.cellss.Add(new SC_BeatTelegraphs.Telegraphs.Cells { X = c.X, Y = c.Y });
            }

            // Schedule immediate broadcast or for next beat
            _telegraph.Schedule(currentBeat, tg);
        }

        private void ProcessDamage(DamageAction action, int currentBeat)
        {
            if (!_map.TryGetActorPosition(_casterId, out GridPos casterPos))
                return;

            List<GridPos> targetCells;
            
            // [Fix] If we have a cached warning area, use it to match the visual telegraph exactly.
            if (_cachedWarningCells != null && _cachedWarningCells.Count > 0)
            {
                 targetCells = _cachedWarningCells;
                 // Note: We don't clear it immediately in case of multi-tick damage. 
                 // It will be overwritten by next Warning or cleared on Finish.
                 // Console.WriteLine($"[SkillRunner] Damage using Cached Warning Cells ({targetCells.Count})");
            }
            else
            {
                targetCells = CalculateShapeCells(action.Shape, casterPos);
            }

            // [Debug] CustomCells Validation
            if (action.Shape.GetShapeType() == ShapeType.CustomCells)
                //Console.WriteLine($"[SkillRunner] Damage CustomCells: {targetCells.Count} cells generated from Shape.");

            // 1. FrozenRegistry에 커스텀 데미지와 범위 등록
            _frozen.PutRaw(_casterId, currentBeat, action.Amount, targetCells);

            // 2. 예약 (BeatActionManager가 해당 비트에 실행)
            var cmd = new PlayerActionCmd
            {
                ActorId = _casterId,
                Kind = ActionKind.Skill,
                SkillId = "NewSkill", // Dummy ID (FrozenRegistry has the real data)
                TargetCell = casterPos, // Origin
                ExecuteBeat = currentBeat,
                ClientSendTimeMs = 0,
                ServerReceiveTimeMs = 0
            };

            _beatActionManager.ScheduleServerCommand(currentBeat, cmd);
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
        private List<GridPos> CalculateShapeCells(IShapeDef shape, GridPos origin)
        {
            // ... (Same as before) ...
            List<GridPos> cells = new List<GridPos>();

            if (shape is DiamondShape diamond)
            {
                int r = diamond.Radius;
                for (int x = -r; x <= r; x++)
                {
                    for (int y = -r; y <= r; y++)
                    {
                        if (Math.Abs(x) + Math.Abs(y) <= r)
                            cells.Add(new GridPos(origin.X + x, origin.Y + y));
                    }
                }
            }
            else if (shape is RectShape rect)
            {
                int halfW = rect.Width / 2;
                int halfH = rect.Height / 2;
                for (int x = -halfW; x <= halfW; x++)
                {
                    for (int y = -halfH; y <= halfH; y++)
                    {
                        cells.Add(new GridPos(origin.X + x, origin.Y + y));
                    }
                }
            }
            else if (shape is CustomCellsShape custom)
            {
                foreach (var p in custom.Cells)
                {
                    cells.Add(new GridPos(origin.X + p.X, origin.Y + p.Y));
                }
            }
            return cells;
        }
    }
}
