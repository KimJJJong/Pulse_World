using GameServer.InGame.Director.Data;

namespace GameServer.InGame.Director.Core
{
    // Context passed when an event occurs in the game
    public struct GameEventContext
    {
        public EventType Type;
        public int SourceActorId; // Who caused it?
        public int TargetId;      // What was affected? (MonsterId, etc.)
        public int X;
        public int Y;
        public long TimeMs;
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

    // Base class for Conditions
    public abstract class EventCondition
    {
        protected ConditionData _data;

        public void Init(ConditionData data)
        {
            _data = data;
        }

        public abstract bool Check(GameDirector director, GameEventContext context);
    }

    // Base class for Actions
    public abstract class EventAction
    {
        protected ActionData _data;

        public void Init(ActionData data)
        {
            _data = data;
        }

        public abstract void Execute(GameDirector director);
    }
}
