using System;
using System.Collections.Generic;

public enum TargetType : byte
{
    ClosestPlayer = 0,
    LowestHpPlayer = 1,
    RandomPlayer = 2
}

public enum AreaDirectionType : byte
{
    None = 0,
    SelfToTarget = 1,
    Fixed = 2
}

#if !UNITY_5_3_OR_NEWER
public enum FixedDir : byte
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3
}
#endif

public enum PhaseTransitionType : byte
{
    HpPercentLE = 0,
    TimeSinceSpawnBeatsGE = 1
}

public enum TelegraphShape : byte
{
    Cells = 0,
    Diamond = 1,
    Rect = 2,
    Line = 3
}

public enum TelegraphOriginType : byte
{
    Self = 0,
    Target = 1,
    Point = 2
}

public enum MoveDirection
{
    None = 0,
    Up = 1,
    Down = 2,
    Left = 3,
    Right = 4,
    TowardTarget = 5,
    AwayFromTarget = 6,
}

[Serializable]
public sealed class MonsterPatternSet
{
    public List<MonsterPatternDef> Monsters = new();
    public MonsterPatternDef GetMonster(string monsterType)
        => Monsters.Find(m => m.MonsterType == monsterType);
}

[Serializable]
public sealed class MonsterPatternDef
{
    public string MonsterType = "";
    public string DefaultPhase = "P1";
    public List<PhaseDef> Phases = new();
    public List<PhaseTransitionDef> Transitions = new();

    public PhaseDef GetPhase(string id)
        => Phases.Find(p => p.Id == id);
}

[Serializable]
public sealed class PhaseDef
{
    public string Id = "P1";
    public List<SelectorDef> Selectors = new();
}

[Serializable]
public sealed class PhaseTransitionDef
{
    public string FromPhaseId = "P1";
    public string ToPhaseId = "P2";
    public PhaseTransitionType Type;
    public int Value;
}

[Serializable]
public sealed class SelectorDef
{
    public string Id = "";
    public int Weight = 1;
    public int CooldownBeats = 0;
    public WhenGroup When = new();
    public List<ActionDef> Timeline = new();
}

[Serializable]
public sealed class WhenGroup
{
    public List<ConditionDef> All = new();
}

public enum ConditionType
{
    DistanceToClosestPlayerLE,
    DistanceToClosestPlayerGT
}

[Serializable]
public sealed class ConditionDef
{
    public ConditionType Type;
    public int Value;
}

public enum ActionType
{
    Wait,
    MoveStepToward,
    Attack,
    CastSkill,
    Move
}

[Serializable]
public sealed class TargetDef
{
    public TargetType Type = TargetType.ClosestPlayer;
    public int MaxRange = 999;
    public bool RequireAlive = true;
}

[Serializable]
public sealed class AreaDef
{
    public TelegraphShape Shape = TelegraphShape.Diamond;
    public TelegraphOriginType OriginType = TelegraphOriginType.Target;

    public int OriginX;
    public int OriginY;

    public int ParamA;
    public int ParamB;

    public AreaDirectionType DirType = AreaDirectionType.None;
    public FixedDir FixedDir = FixedDir.Up;

    public List<GridPos> Cells = new();
}

[Serializable]
public sealed class ActionDef
{
    public int AtBeatOffset;
    public ActionType Type;

    public TargetDef Target = new TargetDef();
    public string SkillId = "";
    public bool LockRotation;

    public int TelegraphBeats;
    public byte TelegraphStyleId;

    public AreaDef Area = new();

    public int MoveDistance = 1;
    public MoveStrategy MoveStrategy = MoveStrategy.Random;
    public MoveDirection MoveDirection = MoveDirection.None;

#if UNITY_EDITOR
    public Client.Data.NewSkillSO SkillRef;
#endif
}

public enum MoveStrategy
{
    Random,
    Flee,
    Forward,
    Backward
}

#if UNITY_5_3_OR_NEWER
[Serializable]
public struct GridPos
{
    public int X;
    public int Y;

    public GridPos(int x, int y)
    {
        X = x;
        Y = y;
    }
}
#endif
