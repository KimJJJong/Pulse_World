using GameServer.Content.Map;
using GameServer.InGame.Director.Data;
using GameServer.InGame.Manager.Entity;
using System;
using System.Collections.Generic;

namespace GameServer.InGame.Director.Core
{
    public sealed class GameDirector : IStageActionHost
    {
        private readonly GameSession _session;
        private readonly StageRuntimeEngine _stageEngine = new();
        private StageScenario _currentScenario;
        private long _startTime;
        private long _lastServerTimeMs;
        private readonly Dictionary<int, int> _objectStates = new();

        public Dictionary<int, int> MonsterGroupDeadCounts { get; } = new();

        public GameDirector(GameSession session)
        {
            _session = session;
        }

        public void LoadScenario(StageScenario scenario)
        {
            _currentScenario = scenario;
            MonsterGroupDeadCounts.Clear();
            _objectStates.Clear();
            _startTime = 0;
            _lastServerTimeMs = 0;
            _stageEngine.LoadScenario(scenario);

            if (_currentScenario == null)
                return;

            Console.WriteLine($"[GameDirector] Loading Scenario: {scenario.MapId}");

            foreach (var spawn in _currentScenario.InitialSpawns ?? new List<SpawnData>())
                SpawnMonster(spawn);

            foreach (var obj in _currentScenario.InitialObjects ?? new List<SpawnObjectData>())
                SpawnObject(obj);
        }

        public void NotifyEvent(GameEventContext context)
        {
            if (_currentScenario == null)
                return;

            _lastServerTimeMs = context.TimeMs != 0 ? context.TimeMs : _lastServerTimeMs;

            if (context.Type == EventType.GameStart && _startTime == 0)
                _startTime = context.TimeMs != 0 ? context.TimeMs : _lastServerTimeMs;

            if (context.Type == EventType.Dead)
            {
                if (!MonsterGroupDeadCounts.ContainsKey(context.TargetId))
                    MonsterGroupDeadCounts[context.TargetId] = 0;
                MonsterGroupDeadCounts[context.TargetId]++;
                Console.WriteLine($"[MonsterGroupDeadCount] {MonsterGroupDeadCounts[context.TargetId]}");
            }

            _stageEngine.NotifyEvent(context, this);
        }

        public void Update(long currentTick)
        {
            _lastServerTimeMs = currentTick;
            if (_startTime == 0)
                _startTime = currentTick;

            _stageEngine.NotifyEvent(new GameEventContext(EventType.TimeTick, timeMs: currentTick), this);
        }

        public long GetElapsedTime(long currentTick)
        {
            if (_startTime == 0)
                return 0;

            return currentTick - _startTime;
        }

        public long GetElapsedTimeMs()
        {
            if (_startTime == 0)
                return 0;

            return _lastServerTimeMs - _startTime;
        }

        public int GetDeadMonsterCount(int groupId)
        {
            return MonsterGroupDeadCounts.TryGetValue(groupId, out var count) ? count : 0;
        }

        public int GetObjectState(int targetId)
        {
            return _objectStates.TryGetValue(targetId, out var state) ? state : 0;
        }

        public int GetParticipantPlayerCount()
        {
            return Math.Max(1, _session?.Players?.Count ?? 0);
        }

        public int CountAlivePlayersInArea(RectData area)
        {
            if (area == null || _session?.Players == null)
                return 0;

            int count = 0;
            foreach (var player in _session.Players)
            {
                if (player == null || !player.IsAlive || player.Type != EntityType.Player)
                    continue;

                if (StageAreaUtility.Contains(area, player.Position.X, player.Position.Y))
                    count++;
            }

            return count;
        }

        public void SpawnMonster(SpawnData data)
        {
            if (data == null)
                return;

            var map = _session.Map;
            int mapX = data.X;
            int mapY = ResolveMapY(data.Y, data.Z);

            if (map != null && !map.IsWalkable(mapX, mapY))
            {
                Console.WriteLine($"[GameDirector] Spawn Failed. Invalid Pos ({mapX},{mapY}) for Monster {data.MonsterId}");
                return;
            }

            _session.SpawnEntityInternal(data.MonsterId, EntityType.Monster, mapX, mapY, data.GroupId, data.ResolvePatternKey());
        }

        public void SpawnObject(SpawnObjectData data)
        {
            if (data == null)
                return;

            var map = _session.Map;
            int mapX = data.X;
            int mapY = ResolveMapY(data.Y, data.Z);

            if (map != null && !map.IsWalkable(mapX, mapY))
            {
                Console.WriteLine($"[GameDirector] Spawn Object Failed. Invalid Pos ({mapX},{mapY})");
                return;
            }

            _session.SpawnEntityInternal(
                data.EntityId,
                (EntityType)data.EntityType,
                mapX,
                mapY,
                data.GroupId,
                data.Pattern,
                data.SizeX,
                data.SizeY);
        }

        public void BroadcastMessage(string msg)
        {
            Console.WriteLine($"[Generate Broadcast] {msg}");
            ShowGuide(new StageGuideData
            {
                Body = msg ?? string.Empty,
                DurationMs = 3000
            });
        }

        public void ReturnToTown()
        {
            Console.WriteLine("[GameDirector] Triggering ReturnToTown");
            _session.BroadcastReturnToTown();
        }

        public void FinGame()
        {
            var data = new StageClearResultData
            {
                MapId = _currentScenario?.MapId ?? string.Empty,
                Title = "STAGE CLEAR",
                Subtitle = "Purification Complete - Deepwood Gate Stabilized",
                ClearTimeMs = (int)Math.Min(int.MaxValue, Math.Max(0, GetElapsedTimeMs()))
            };

            Console.WriteLine("[GameDirector] Triggering FinGame result UI");
            _session.BroadcastStageSignal(StageSignalCodec.StageClearWarnCode, StageSignalCodec.EncodeStageClear(data));
        }

        public void OpenGate(int x, int y)
        {
            var map = _session.Map;
            if (map == null)
                return;

            map.Set(x, y, TileKind.Floor);
            Console.WriteLine($"[GameDirector] OpenGate at ({x},{y})");
        }

        public void SetObjectState(int targetId, int state)
        {
            if (targetId <= 0)
                return;

            _objectStates[targetId] = state;
            Console.WriteLine($"[GameDirector] SetObjectState target={targetId} state={state}");
        }

        public void RemoveEntityGroup(int groupId, int delayMs = 0)
        {
            if (groupId <= 0)
                return;

            int removed = _session.RemoveEntityGroupInternal(groupId);
            Console.WriteLine($"[GameDirector] RemoveEntityGroup group={groupId} removed={removed}");
        }

        public void SetSceneObjectActive(StageSceneObjectData data)
        {
            data ??= new StageSceneObjectData();
            Console.WriteLine($"[GameDirector] SetSceneObjectActive key='{data.TargetKey}' group={data.GroupId} visible={data.Visible}");
            _session.BroadcastStageSignal(StageSignalCodec.SceneObjectWarnCode, StageSignalCodec.EncodeSceneObject(data));
        }

        public void SetSummonPortalActive(StageSummonPortalData data)
        {
            data ??= new StageSummonPortalData();
            Console.WriteLine(
                $"[GameDirector] SetSummonPortalActive key='{data.PortalKey}' active={data.Active} group={data.SpawnGroupId} " +
                $"pos=({data.SpawnX},{ResolveMapY(data.SpawnY, data.SpawnZ)}) maxAlive={data.MaxAlive} interval={data.IntervalBeats}");
        }

        public void SetGateDoorOpen(StageGateDoorData data)
        {
            data ??= new StageGateDoorData();
            Console.WriteLine($"[GameDirector] SetGateDoorOpen key='{data.TargetKey}' group={data.GroupId} open={data.Open} duration={data.DurationMs}");
            _session.BroadcastStageSignal(StageSignalCodec.GateDoorWarnCode, StageSignalCodec.EncodeGateDoor(data));
        }

        public void ShowGuide(StageGuideData data)
        {
            data ??= new StageGuideData();
            Console.WriteLine($"[GameDirector] ShowGuide title='{data.Title}' body='{data.Body}'");
            _session.BroadcastStageSignal(StageSignalCodec.GuideWarnCode, StageSignalCodec.EncodeGuide(data));
        }

        public void ShowAreaProgress(StageAreaProgressData data)
        {
            data ??= new StageAreaProgressData();
            Console.WriteLine($"[GameDirector] ShowAreaProgress label='{data.Label}' progress={data.CurrentCount}/{data.RequiredCount}");
            _session.BroadcastStageSignal(StageSignalCodec.AreaProgressWarnCode, StageSignalCodec.EncodeAreaProgress(data));
        }

        public void ShowTutorialPanel(StageTutorialPanelData data)
        {
            SendTutorialPanel(data, visible: true);
        }

        public void HideTutorialPanel(StageTutorialPanelData data)
        {
            SendTutorialPanel(data, visible: false);
        }

        public void PlayStageVfx(StageVfxData data)
        {
            data ??= new StageVfxData();
            Console.WriteLine($"[GameDirector] PlayStageVfx key='{data.VfxKey}' pos=({data.X},{ResolveMapY(data.Y, data.Z)})");
            _session.BroadcastStageSignal(StageSignalCodec.VfxWarnCode, StageSignalCodec.EncodeVfx(data));
        }

        private void SendTutorialPanel(StageTutorialPanelData data, bool visible)
        {
            data ??= new StageTutorialPanelData();
            data.Visible = visible;

            Console.WriteLine($"[GameDirector] {(visible ? "Show" : "Hide")}TutorialPanel panelId='{data.PanelId}' image='{data.ImageResource}'");
            _session.BroadcastStageSignal(StageSignalCodec.TutorialPanelWarnCode, StageSignalCodec.EncodeTutorialPanel(data));
        }

        private static int ResolveMapY(int legacyY, int unityZ)
        {
            if (unityZ != 0)
                return unityZ;

            return legacyY;
        }
    }
}
