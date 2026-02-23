using GameServer.Content.Map;
using GameServer.InGame.Director.Data;
using GameServer.InGame.Director.Events;
using GameServer.InGame.Manager.Entity; // [NEW] for EntityType enum
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameServer.InGame.Director.Core
{
    public class GameDirector
    {
        private readonly GameSession _session;
        private StageScenario _currentScenario;
        
        // Runtime State
        private readonly HashSet<int> _executedEvents = new();
        private readonly List<RuntimeEvent> _runtimeEvents = new();
        
        private long _startTime;

        
        // Tracking for Conditions (e.g. Monster Kill Count)
        // GroupId -> Dead Count
        public Dictionary<int, int> MonsterGroupDeadCounts { get; private set; } = new();

        public GameDirector(GameSession session)
        {
            _session = session;
        }

        public void LoadScenario(StageScenario scenario)
        {
            _currentScenario = scenario;
            _executedEvents.Clear();
            _runtimeEvents.Clear();
            MonsterGroupDeadCounts.Clear();
            _startTime = 0;


            if (_currentScenario == null) return;

            Console.WriteLine($"[GameDirector] Loading Scenario: {scenario.MapId}");

            // 1. Convert Data to Runtime Objects
            foreach (var evtData in _currentScenario.Events)
            {
                var rtEvent = new RuntimeEvent { Data = evtData };
                
                foreach (var condData in evtData.Conditions)
                {
                    var condition = CreateCondition(condData);
                    if (condition != null) rtEvent.Conditions.Add(condition);
                }

                foreach (var actData in evtData.Actions)
                {
                    var action = CreateAction(actData);
                    if (action != null) rtEvent.Actions.Add(action);
                }

                _runtimeEvents.Add(rtEvent);
            }

            // 2. Initial Spawns
            foreach (var spawn in _currentScenario.InitialSpawns)
            {
                SpawnMonster(spawn);
            }
        }

        public void NotifyEvent(GameEventContext context)
        {
            if (_currentScenario == null) return;

            // Pre-process context for state tracking
            if (context.Type == EventType.Dead)
            {
                // Assuming TargetId is relevant group or monster logic
                // For now, let's say we check if the dead monster belonged to a group.
                // This requires GameSession to tell us the GroupId of the dead actor, 
                // OR we pass it in TargetId. 
                // Let's assume TargetId IS the MonsterId/GroupId or we track it.
                
                // Simple implementation: Just increment context.TargetId if it's treated as GroupId
                if (!MonsterGroupDeadCounts.ContainsKey(context.TargetId))
                    MonsterGroupDeadCounts[context.TargetId] = 0;
                MonsterGroupDeadCounts[context.TargetId]++;
                Console.WriteLine($"[MonsterGroupDeadCount] {MonsterGroupDeadCounts[context.TargetId]}");
            }

            // Check Triggers
            foreach (var evt in _runtimeEvents)
            {
                if (evt.Data.IsOneShot && _executedEvents.Contains(evt.Data.EventId))
                    continue;

                bool allMet = true;
                foreach (var cond in evt.Conditions)
                {
                    if (!cond.Check(this, context))
                    {
                        allMet = false;
                        break;
                    }
                }

                if (allMet)
                {
                    Console.WriteLine($"[GameDirector] Event {evt.Data.EventId} {evt.Data.Actions} Triggered!");
                    
                    foreach (var action in evt.Actions)
                    {
                        action.Execute(this);
                    }

                    if (evt.Data.IsOneShot)
                        _executedEvents.Add(evt.Data.EventId);
                }
            }
        }

        public void Update(long currentTick)
        {
            if (_startTime == 0) _startTime = currentTick;
            
            // Re-trigger checking for TimeElapsed events
            NotifyEvent(new GameEventContext { Type = EventType.None });
        }
        
        public long GetElapsedTime(long currentTick)
        {
             if (_startTime == 0) return 0;
             return currentTick - _startTime;
        }


        // ========================================================
        //  Helper Methods for Actions
        // ========================================================
        public void SpawnMonster(SpawnData data)
        {
            // Validate with Map
            var map = _session.Map; // Access Map via Session
            int mapX = data.X;
            int mapY = data.Z; // [Unified] Use Z as Map Y

            if (map != null)
            {
                if (!map.IsWalkable(mapX, mapY))
                {
                    Console.WriteLine($"[GameDirector] Spawn Failed. Invalid Pos ({mapX},{mapY}) for Monster {data.MonsterId}");
                    return;
                }
            }

            _session.SpawnEntityInternal(data.MonsterId, EntityType.Monster, mapX, mapY, data.GroupId, data.AI);
        }

        public void SpawnObject(SpawnObjectData data)
        {
             var map = _session.Map;
             int mapX = data.X;
             int mapY = data.Z; // [Unified] Use Z as Map Y

             if (map != null && !map.IsWalkable(mapX, mapY))
             {
                 // Object는 벽에 생성될 수도 있으니 체크 완화 필요할 수도 있음
                 Console.WriteLine($"[GameDirector] Spawn Object Failed. Invalid Pos ({mapX},{mapY})");
                 return;
             }
             _session.SpawnEntityInternal(data.EntityId, (EntityType)data.EntityType, mapX, mapY, data.GroupId, data.Pattern);
        }

        public void BroadcastMessage(string msg)
        {
            // _session.Broadcast(...) 
            // Need public method in GameSession
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
            if (map != null)
            {
                // Gate logic: Set tile to Floor to make it walkable
                map.Set(x, y, TileKind.Floor);
                Console.WriteLine($"[GameDirector] OpenGate at ({x},{y})");
                
                // Optional: Broadcast map update to clients if needed? 
                // Currently Map is static on client, but we might need a packet for TileChange.
                // For now, server logic allows movement.
            }
        }


        // ========================================================
        //  Factory Methods (Reflection or Switch)
        // ========================================================
        private EventCondition CreateCondition(ConditionData data)
        {
            EventCondition cond = null;
            switch (data.Type)
            {
                case "MonsterAllDead": cond = new ConditionMonsterAllDead(); break;
                case "AreaEnter": cond = new ConditionAreaEnter(); break;
                case "TimeElapsed": cond = new ConditionTimeElapsed(); break;
            }
            cond?.Init(data);
            return cond;
        }

        private EventAction CreateAction(ActionData data)
        {
            EventAction act = null;
            switch (data.Type)
            {
                case "SpawnMonster": act = new ActionSpawnMonster(); break;
                case "Broadcast": act = new ActionBroadcast(); break;
                case "ReturnToTown": act = new ActionReturnToTown(); break;
                case "OpenGate": act = new ActionOpenGate(); break;

            }
            act?.Init(data);
            return act;
        }

        private class RuntimeEvent
        {
            public EventData Data;
            public List<EventCondition> Conditions = new();
            public List<EventAction> Actions = new();
        }

        /*
        private class ActionReturnToTown : EventAction
        {
            public override void Execute(GameDirector director)
            {
                director.ReturnToTown();
            }
        }
        */

    }
}
