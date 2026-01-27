using GameServer.InGame.Director.Core;
using System;

namespace GameServer.InGame.Director.Events
{
    // ==========================================
    // Conditions
    // ==========================================
    public class ConditionMonsterAllDead : EventCondition
    {
        public override bool Check(GameDirector director, GameEventContext context)
        {
            // Only check on Dead event
            if (context.Type != EventType.Dead) return false;

            // TargetId in Data is the GroupId we want to check
            int requiredGroupId = _data.TargetId;
            int requiredCount = _data.Count;

            if (director.MonsterGroupDeadCounts.TryGetValue(requiredGroupId, out int currentCount))
            {
                return currentCount >= requiredCount;
            }
            return false;
        }
    }

    public class ConditionAreaEnter : EventCondition
    {
        public override bool Check(GameDirector director, GameEventContext context)
        {
            if (context.Type != EventType.Move) return false;

            // Check if context X,Y is inside Rect
            var r = _data.Area;
            if (r == null) return false;

            return (context.X >= r.X && context.X < r.X + r.W &&
                    context.Y >= r.Y && context.Y < r.Y + r.H);
        }
    }

    public class ConditionTimeElapsed : EventCondition
    {
        public override bool Check(GameDirector director, GameEventContext context)
        {
            // Simple check: if current time > start + delay
            // But we need State in Director to know "StartTime".
            // For now, let's assume Director handles timer events or we poll.
            return false; // TODO: Implement Time Logic
        }
    }

    // ==========================================
    // Actions
    // ==========================================
    public class ActionSpawnMonster : EventAction
    {
        public override void Execute(GameDirector director)
        {
            var spawnData = new GameServer.InGame.Director.Data.SpawnData
            {
                MonsterId = _data.ParamId,
                X = _data.X,
                Y = _data.Y,
                AI = _data.StringVal, // Assuming AI Key stored here
                GroupId = 0 // Default for spawned ones
            };
            director.SpawnMonster(spawnData);
        }
    }

    public class ActionBroadcast : EventAction
    {
        public override void Execute(GameDirector director)
        {
            director.BroadcastMessage(_data.StringVal);
        }
    }
}
