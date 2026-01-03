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
    public List<MonsterPatternDef> Monsters { get; set; } = new();
    public MonsterPatternDef GetMonster(string monsterType)
        => Monsters.Find(m => m.MonsterType == monsterType);
}

public sealed class MonsterPatternDef
{
    public string MonsterType { get; set; } = "";
    public string DefaultPhase { get; set; } = "P1";
    public List<PhaseDef> Phases { get; set; } = new();
    public List<PhaseTransitionDef> Transitions { get; set; } = new();

    public PhaseDef GetPhase(string id)
        => Phases.Find(p => p.Id == id);
}

public sealed class PhaseDef
{
    public string Id { get; set; } = "P1";
    public List<SelectorDef> Selectors { get; set; } = new();
}
public sealed class PhaseTransitionDef
{
    public string FromPhaseId { get; set; } = "P1";
    public string ToPhaseId { get; set; } = "P2";

    public PhaseTransitionType Type { get; set; }
    public int Value { get; set; } // HpPercentLE: 30, TimeSinceSpawnBeatsGE: 64 등
}
public sealed class SelectorDef
{
    public string Id { get; set; } = "";
    public int Weight { get; set; } = 1;
    public int CooldownBeats { get; set; } = 0;
    public WhenGroup When { get; set; } = new();
    public List<ActionDef> Timeline { get; set; } = new();
}

public sealed class WhenGroup
{
    public List<ConditionDef> All { get; set; } = new();
}

public sealed class ConditionDef
{
    public ConditionType Type { get; set; }
    public int Value { get; set; }
}

public enum ConditionType
{
    DistanceToClosestPlayerLE,
    DistanceToClosestPlayerGT
}

public sealed class ActionDef
{
    public int AtBeatOffset { get; set; }
    public ActionType Type { get; set; }

    //  Runner에서 act.Target 쓰니까 반드시 필요
    public TargetDef Target { get; set; } = new TargetDef();
    public string SkillId { get; set; } = ""; // Type==Attack일 때 사용


    public int TelegraphBeats { get; set; }
    public byte TelegraphStyleId { get; set; }

    public AreaDef Area { get; set; } = new();
}

public enum ActionType
{
    Wait,
    MoveStepToward,
    Attack
}

public sealed class TargetDef
{
    public TargetType Type { get; set; } = TargetType.ClosestPlayer;

    // 옵션 파라미터 (필요한 것만 사용)
    public int MaxRange { get; set; } = 999;   // 타겟 탐색 제한(거리)
    public bool RequireAlive { get; set; } = true;
}

public sealed class AreaDef
{
    public TelegraphShape Shape { get; set; } = TelegraphShape.Diamond;
    public TelegraphOriginType OriginType { get; set; } = TelegraphOriginType.Target;

    public int OriginX { get; set; }
    public int OriginY { get; set; }

    // Shape 파라미터(명세 고정)
    // Diamond: ParamA=radius
    // Rect:    ParamA=width, ParamB=height
    // Line:    ParamA=length, ParamB=thickness(옵션)
    public int ParamA { get; set; }
    public int ParamB { get; set; }

    // v1.1 추가: 방향
    public AreaDirectionType DirType { get; set; } = AreaDirectionType.None;
    public FixedDir FixedDir { get; set; } = FixedDir.Up; // DirType==Fixed일 때만

    public List<GridPos> Cells { get; set; } = new();
}

