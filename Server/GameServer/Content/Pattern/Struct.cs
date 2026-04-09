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

// ─────────────────────────────────────────────────────────────
//  [방향 기반 이동] MoveDirection
//  절대 위치 예약 대신 "현재 위치 기준 방향 + N칸" 으로 AI 이동을 정의.
//  ActionDef.MoveDirection != None 이면 MoveStrategy 대신 이 값이 우선 적용되며,
//  서버는 매 Beat 마다 1칸씩 MoveDistance번 독립 판정하므로
//  순간이동 · 대각선이동 · 2칸 점프가 발생하지 않는다.
// ─────────────────────────────────────────────────────────────
public enum MoveDirection
{
    None = 0,
    Up = 1,             // +Y
    Down = 2,           // -Y
    Left = 3,           // -X
    Right = 4,          // +X
    TowardTarget = 5,   // 매 Beat 타겟 방향으로 1칸 (Manhattan 우선, X거리 >= Y거리 이면 X축)
    AwayFromTarget = 6, // 매 Beat 타겟 반대 방향으로 1칸
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
    public int Value;
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

    public TargetDef Target = new TargetDef();
    public string SkillId = "";

    public int TelegraphBeats;
    public byte TelegraphStyleId;

    public AreaDef Area = new();

    // -- Extended for New Pattern System --
    public int MoveDistance = 1;
    public MoveStrategy MoveStrategy = MoveStrategy.Random;

    /// <summary>
    /// 방향 기반 이동: None이 아니면 MoveStrategy 대신 이 방향으로 1칸씩 MoveDistance번 이동.
    /// 각 Beat마다 독립 판정하므로 순간이동/대각선/2칸 점프 없음.
    /// </summary>
    public MoveDirection MoveDirection = MoveDirection.None;
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
    public int MaxRange = 999;
    public bool RequireAlive = true;
}

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
