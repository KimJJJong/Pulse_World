using System;
using System.Collections.Generic;

namespace GameServer.InGame.Director.Data
{
    [Serializable]
    public class StageScenario
    {
        public string MapId = string.Empty;
        public string Description = string.Empty;

        public RhythmSettingsData RhythmSettings = new RhythmSettingsData();

        public List<SpawnData> InitialSpawns = new List<SpawnData>();
        public List<SpawnObjectData> InitialObjects = new List<SpawnObjectData>();

        public List<EventData> Events = new List<EventData>();
    }

    [Serializable]
    public class RhythmSettingsData
    {
        public string SongKey = "DefaultSong";
        public int Bpm = 120;
        public int BaseBeatDivision = 1;
        public int ActionWindowMs = 100;
        public int StartDelayMs = 2000;
    }

    [Serializable]
    public class SpawnData
    {
        public int MonsterId;
        public int X;
        public int Y;
        public int Z;
        public string AI = "Default";
        public int GroupId = 0;
    }

    [Serializable]
    public class SpawnObjectData
    {
        public int EntityId;
        public int EntityType;
        public int X;
        public int Y;
        public int Z;
        public int GroupId;
        public string Pattern = string.Empty;
    }

    [Serializable]
    public class EventData
    {
        public int EventId;
        public bool IsOneShot = true;

        public List<ConditionData> Conditions = new List<ConditionData>();
        public List<ActionData> Actions = new List<ActionData>();
    }

    [Serializable]
    public class ConditionData
    {
        public string Type = string.Empty;
        public int TargetId;
        public int Count;
        public RectData Area;
    }

    [Serializable]
    public class ActionData
    {
        public string Type = string.Empty;
        public int ParamId;
        public int X;
        public int Y;
        public int Z;
        public string StringVal = string.Empty;
        public int GroupId;
    }

    [Serializable]
    public class RectData
    {
        public int X;
        public int Y;
        public int W;
        public int H;
    }
}

namespace GameServer.InGame.Director.Core
{
    using GameServer.InGame.Director.Data;

    public struct GameEventContext
    {
        public EventType Type;
        public int SourceActorId;
        public int TargetId;
        public int X;
        public int Y;
        public long TimeMs;

        public GameEventContext(EventType type, int sourceActorId = 0, int targetId = 0, int x = 0, int y = 0, long timeMs = 0)
        {
            Type = type;
            SourceActorId = sourceActorId;
            TargetId = targetId;
            X = x;
            Y = y;
            TimeMs = timeMs;
        }
    }

    public enum EventType
    {
        None = 0,
        GameStart,
        Beat,
        Move,
        Dead,
        Interact,
        TimeTick
    }

    public interface IStageActionHost
    {
        int GetDeadMonsterCount(int groupId);
        long GetElapsedTimeMs();

        void SpawnMonster(SpawnData data);
        void SpawnObject(SpawnObjectData data);
        void BroadcastMessage(string msg);
        void ReturnToTown();
        void OpenGate(int x, int y);
    }

    public abstract class EventCondition
    {
        protected ConditionData _data = new();

        public void Init(ConditionData data)
        {
            _data = data ?? new ConditionData();
        }

        public abstract bool Check(IStageActionHost host, GameEventContext context);
    }

    public abstract class EventAction
    {
        protected ActionData _data = new();

        public void Init(ActionData data)
        {
            _data = data ?? new ActionData();
        }

        public abstract void Execute(IStageActionHost host);
    }

    public sealed class StageRuntimeEngine
    {
        private sealed class RuntimeEvent
        {
            public EventData Data;
            public List<EventCondition> Conditions = new();
            public List<EventAction> Actions = new();
        }

        private StageScenario _scenario;
        private readonly HashSet<int> _executedEventIds = new();
        private readonly List<RuntimeEvent> _runtimeEvents = new();

        public void Reset()
        {
            _scenario = null;
            _executedEventIds.Clear();
            _runtimeEvents.Clear();
        }

        public void LoadScenario(StageScenario scenario)
        {
            Reset();
            _scenario = scenario;

            if (_scenario == null)
                return;

            foreach (var evtData in _scenario.Events ?? new List<EventData>())
            {
                if (evtData == null)
                    continue;

                var rtEvent = new RuntimeEvent { Data = evtData };

                foreach (var condData in evtData.Conditions ?? new List<ConditionData>())
                {
                    var condition = CreateCondition(condData);
                    if (condition != null)
                        rtEvent.Conditions.Add(condition);
                }

                foreach (var actData in evtData.Actions ?? new List<ActionData>())
                {
                    var action = CreateAction(actData);
                    if (action != null)
                        rtEvent.Actions.Add(action);
                }

                _runtimeEvents.Add(rtEvent);
            }
        }

        public void NotifyEvent(GameEventContext context, IStageActionHost host)
        {
            if (_scenario == null || host == null)
                return;

            foreach (var evt in _runtimeEvents)
            {
                if (evt.Data == null)
                    continue;

                if (evt.Data.IsOneShot && _executedEventIds.Contains(evt.Data.EventId))
                    continue;

                bool allMet = true;
                foreach (var cond in evt.Conditions)
                {
                    if (!cond.Check(host, context))
                    {
                        allMet = false;
                        break;
                    }
                }

                if (!allMet)
                    continue;

                foreach (var action in evt.Actions)
                    action.Execute(host);

                if (evt.Data.IsOneShot)
                    _executedEventIds.Add(evt.Data.EventId);
            }
        }

        private static EventCondition CreateCondition(ConditionData data)
        {
            if (data == null)
                return null;

            EventCondition cond = data.Type switch
            {
                "MonsterAllDead" => new ConditionMonsterAllDead(),
                "AreaEnter" => new ConditionAreaEnter(),
                "TimeElapsed" => new ConditionTimeElapsed(),
                _ => null
            };

            cond?.Init(data);
            return cond;
        }

        private static EventAction CreateAction(ActionData data)
        {
            if (data == null)
                return null;

            EventAction act = data.Type switch
            {
                "SpawnMonster" => new ActionSpawnMonster(),
                "SpawnObject" => new ActionSpawnObject(),
                "Broadcast" => new ActionBroadcast(),
                "ReturnToTown" => new ActionReturnToTown(),
                "OpenGate" => new ActionOpenGate(),
                _ => null
            };

            act?.Init(data);
            return act;
        }

        private sealed class ConditionMonsterAllDead : EventCondition
        {
            public override bool Check(IStageActionHost host, GameEventContext context)
            {
                int requiredGroupId = _data.TargetId;
                int requiredCount = _data.Count;
                return host.GetDeadMonsterCount(requiredGroupId) >= requiredCount;
            }
        }

        private sealed class ConditionAreaEnter : EventCondition
        {
            public override bool Check(IStageActionHost host, GameEventContext context)
            {
                if (context.Type != EventType.Move)
                    return false;

                var r = _data.Area;
                if (r == null)
                    return false;

                return context.X >= r.X && context.X < r.X + r.W &&
                       context.Y >= r.Y && context.Y < r.Y + r.H;
            }
        }

        private sealed class ConditionTimeElapsed : EventCondition
        {
            public override bool Check(IStageActionHost host, GameEventContext context)
                => host.GetElapsedTimeMs() >= _data.Count;
        }

        private sealed class ActionSpawnMonster : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                var spawnData = new SpawnData
                {
                    MonsterId = _data.ParamId,
                    X = _data.X,
                    Y = _data.Y,
                    Z = _data.Z,
                    AI = _data.StringVal,
                    GroupId = _data.GroupId
                };
                host.SpawnMonster(spawnData);
            }
        }

        private sealed class ActionSpawnObject : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                var spawnData = new SpawnObjectData
                {
                    EntityId = _data.ParamId,
                    EntityType = 3,
                    X = _data.X,
                    Y = _data.Y,
                    Z = _data.Z,
                    GroupId = _data.GroupId,
                    Pattern = _data.StringVal
                };
                host.SpawnObject(spawnData);
            }
        }

        private sealed class ActionBroadcast : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.BroadcastMessage(_data.StringVal);
            }
        }

        private sealed class ActionReturnToTown : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.ReturnToTown();
            }
        }

        private sealed class ActionOpenGate : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.OpenGate(_data.X, _data.Z);
            }
        }
    }
}
