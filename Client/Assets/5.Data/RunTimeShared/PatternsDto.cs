using System;
using System.Collections.Generic;

[Serializable]
public sealed class MonsterPatternSet
{
    public List<MonsterPatternDef> Monsters { get; set; } = new();
    public MonsterPatternDef GetMonster(string monsterType)
        => Monsters.Find(m => m.MonsterType == monsterType);
}

[Serializable]
public sealed class MonsterPatternDef
{
    public string MonsterType { get; set; } = "";
    public string DefaultPhase { get; set; } = "P1";
    public List<PhaseDef> Phases { get; set; } = new();

    public List<PhaseTransitionDef> Transitions { get; set; } = new();
}

[Serializable]
public sealed class PhaseDef
{
    public string Id { get; set; } = "P1";
    public List<SelectorDef> Selectors { get; set; } = new();
}

[Serializable]
public sealed class SelectorDef
{
    public string Id { get; set; } = "";
    public int Weight { get; set; } = 1;
    public int CooldownBeats { get; set; } = 0;
    public WhenGroup When { get; set; } = new();
    public List<ActionDef> Timeline { get; set; } = new();
}

[Serializable]
public sealed class WhenGroup
{
    public List<ConditionDef> All { get; set; } = new();
}

public enum ConditionType
{
    DistanceToClosestPlayerLE,
    DistanceToClosestPlayerGT
}

[Serializable]
public sealed class ConditionDef
{
    public ConditionType Type { get; set; }
    public int Value { get; set; }
}

public enum ActionType { Wait, MoveStepToward, Attack }

public enum TargetType : byte { ClosestPlayer = 0, LowestHpPlayer = 1, RandomPlayer = 2 }

[Serializable]
public sealed class TargetDef
{
    public TargetType Type { get; set; } = TargetType.ClosestPlayer;
    public int MaxRange { get; set; } = 999;
    public bool RequireAlive { get; set; } = true;
}

// Pattern쪽 Area는 “origin 결정을 위한 힌트 + Cells 편집” 정도로만 유지(룰은 SkillDef)
public enum TelegraphOriginType : byte { Self = 0, Target = 1, Point = 2 }
public enum TelegraphShape : byte { Cells = 0, Diamond = 1, Rect = 2, Line = 3 }

[Serializable]
public sealed class AreaDef
{
    public TelegraphShape Shape { get; set; } = TelegraphShape.Diamond;
    public TelegraphOriginType OriginType { get; set; } = TelegraphOriginType.Target;

    public int OriginX { get; set; }
    public int OriginY { get; set; }

    public int ParamA { get; set; }
    public int ParamB { get; set; }

    public List<GridPos> Cells { get; set; } = new();
}

[Serializable]
public sealed class ActionDef
{
    public int AtBeatOffset { get; set; }
    public ActionType Type { get; set; }

    public TargetDef Target { get; set; } = new();

    public string SkillId { get; set; } = ""; // Attack일 때 필수

    public int TelegraphBeats { get; set; }
    public byte TelegraphStyleId { get; set; }

    public AreaDef Area { get; set; } = new();
}

public enum PhaseTransitionType : byte { HpPercentLE = 0, TimeSinceSpawnBeatsGE = 1 }

[Serializable]
public sealed class PhaseTransitionDef
{
    public string FromPhaseId { get; set; } = "P1";
    public string ToPhaseId { get; set; } = "P2";
    public PhaseTransitionType Type { get; set; }
    public int Value { get; set; }
}

// 너 프로젝트 GridPos와 동일하게 맞추기
[Serializable]
public struct GridPos
{
    public int X;
    public int Y;
    public GridPos(int x, int y) { X = x; Y = y; }
}
