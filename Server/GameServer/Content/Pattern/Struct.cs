using System.Collections.Generic;

public enum TargetType : byte
{
    ClosestPlayer = 0,
    LowestHpPlayer = 1,
    RandomPlayer = 2
}

public enum AreaDirectionType : byte
{
    None = 0,          // Diamond/Cells 처럼 방향 불필요
    SelfToTarget = 1,  // self -> target 벡터
    Fixed = 2          // 고정 방향(Up/Down/Left/Right)
}

public enum FixedDir : byte
{
    Up = 0, Right = 1, Down = 2, Left = 3
}

public enum PhaseTransitionType : byte
{
    HpPercentLE = 0,   // HP% <= value
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

public sealed class MonsterPatternSet
{
    public List<MonsterPatternDef> Monsters = new();
    public MonsterPatternDef GetMonster(string monsterType)
        => Monsters.Find(m => m.MonsterType == monsterType);
}

public sealed class MonsterPatternDef
{
    public string MonsterType = "";
    public string DefaultPhase = "P1";
    public List<PhaseDef> Phases = new();
    public List<PhaseTransitionDef> Transitions = new();

    public PhaseDef GetPhase(string id)
        => Phases.Find(p => p.Id == id);
}

public sealed class PhaseDef
{
    public string Id = "P1";
    public List<SelectorDef> Selectors = new();
}
public sealed class PhaseTransitionDef
{
    public string FromPhaseId = "P1";
    public string ToPhaseId = "P2";

    public PhaseTransitionType Type;
    public int Value; // HpPercentLE: 30, TimeSinceSpawnBeatsGE: 64 등
}
public sealed class SelectorDef
{
    public string Id = "";
    public int Weight = 1;
    public int CooldownBeats = 0;
    public WhenGroup When = new();
    public List<ActionDef> Timeline = new();
}

public sealed class WhenGroup
{
    public List<ConditionDef> All = new();
}

public sealed class ConditionDef
{
    public ConditionType Type;
    public int Value;
}

public enum ConditionType
{
    DistanceToClosestPlayerLE,
    DistanceToClosestPlayerGT
}



public sealed class ActionDef
{
    public int AtBeatOffset;
    public ActionType Type;

    //  Runner에서 act.Target 쓰니까 반드시 필요
    public TargetDef Target = new TargetDef();
    public string SkillId = ""; // Type==Attack일 때 사용

    public int TelegraphBeats;
    public byte TelegraphStyleId;

    public AreaDef Area = new();

    // -- Extended for New Pattern System --
    public int MoveDistance = 1;
    public MoveStrategy MoveStrategy = MoveStrategy.Random;
}

public enum ActionType
{
    Wait,
    MoveStepToward,
    Attack,
    CastSkill,
    Move
}

public enum MoveStrategy 
{ 
    Random, 
    Flee, 
    Forward, 
    Backward 
}

public sealed class TargetDef
{
    public TargetType Type = TargetType.ClosestPlayer;

    // 옵션 파라미터 (필요한 것만 사용)
    public int MaxRange = 999;   // 타겟 탐색 제한(거리)
    public bool RequireAlive = true;
}

public sealed class AreaDef
{
    public TelegraphShape Shape = TelegraphShape.Diamond;
    public TelegraphOriginType OriginType = TelegraphOriginType.Target;

    public int OriginX;
    public int OriginY;

    // Shape 파라미터(명세 고정)
    // Diamond: ParamA=radius
    // Rect:    ParamA=width, ParamB=height
    // Line:    ParamA=length, ParamB=thickness(옵션)
    public int ParamA;
    public int ParamB;

    // v1.1 추가: 방향
    public AreaDirectionType DirType = AreaDirectionType.None;
    public FixedDir FixedDir = FixedDir.Up; // DirType==Fixed일 때만

    public List<GridPos> Cells = new();
}

