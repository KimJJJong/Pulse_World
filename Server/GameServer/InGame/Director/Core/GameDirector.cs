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

        public Dictionary<int, int> MonsterGroupDeadCounts { get; } = new();

        public GameDirector(GameSession session)
        {
            _session = session;
        }

        public void LoadScenario(StageScenario scenario)
        {
            _currentScenario = scenario;
            MonsterGroupDeadCounts.Clear();
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

            _session.SpawnEntityInternal(data.MonsterId, EntityType.Monster, mapX, mapY, data.GroupId, data.AI);
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

            _session.SpawnEntityInternal(data.EntityId, (EntityType)data.EntityType, mapX, mapY, data.GroupId, data.Pattern);
        }

        public void BroadcastMessage(string msg)
        {
            Console.WriteLine($"[Generate Broadcast] {msg}");
        }

        public void ReturnToTown()
        {
            Console.WriteLine("[GameDirector] Triggering ReturnToTown");
            _session.BroadcastReturnToTown();
        }

        public void OpenGate(int x, int y)
        {
            var map = _session.Map;
            if (map == null)
                return;

            map.Set(x, y, TileKind.Floor);
            Console.WriteLine($"[GameDirector] OpenGate at ({x},{y})");
        }

        private static int ResolveMapY(int legacyY, int unityZ)
        {
            if (unityZ != 0)
                return unityZ;

            return legacyY;
        }
    }
}
