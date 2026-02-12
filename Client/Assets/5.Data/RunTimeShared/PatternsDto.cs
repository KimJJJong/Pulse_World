using System;
using System.Collections.Generic;

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
}

[Serializable]
public sealed class PhaseDef
{
    public string Id = "P1";
    public List<SelectorDef> Selectors = new();
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
    CastSkill, // New!
    Move // Generic Move
}

public enum TargetType : byte { ClosestPlayer = 0, LowestHpPlayer = 1, RandomPlayer = 2 }

[Serializable]
public sealed class TargetDef
{
    public TargetType Type = TargetType.ClosestPlayer;
    public int MaxRange = 999;
    public bool RequireAlive = true;
}

// Pattern쪽 Area는 “origin 결정을 위한 힌트 + Cells 편집” 정도로만 유지(룰은 SkillDef)
public enum TelegraphOriginType : byte { Self = 0, Target = 1, Point = 2 }
public enum TelegraphShape : byte { Cells = 0, Diamond = 1, Rect = 2, Line = 3 }

[Serializable]
public sealed class AreaDef
{
    public TelegraphShape Shape = TelegraphShape.Diamond;
    public TelegraphOriginType OriginType = TelegraphOriginType.Target;

    public int OriginX;
    public int OriginY;

    public int ParamA;
    public int ParamB;

    public List<GridPos> Cells = new();
}

[Serializable]
public sealed class ActionDef
{
    public int AtBeatOffset;
    public ActionType Type;

    public TargetDef Target = new();

    public string SkillId = ""; // Attack일 때 필수

    public int TelegraphBeats;
    public byte TelegraphStyleId;

    public AreaDef Area = new();

    // -- Extended for New Pattern System --
    public int MoveDistance = 1;
    public MoveStrategy MoveStrategy = MoveStrategy.Random;
    
#if UNITY_EDITOR
    // Editor-only reference for convenience
    public Client.Data.NewSkillSO SkillRef; 
#endif
}

public enum MoveStrategy 
{ 
    Random, 
    Flee, // Run away from Target
    Forward, // Based on Facing? Or absolute? Keep simplistic for now.
    Backward 
}

public enum PhaseTransitionType : byte { HpPercentLE = 0, TimeSinceSpawnBeatsGE = 1 }

[Serializable]
public sealed class PhaseTransitionDef
{
    public string FromPhaseId = "P1";
    public string ToPhaseId = "P2";
    public PhaseTransitionType Type;
    public int Value;
}

// 너 프로젝트 GridPos와 동일하게 맞추기
[Serializable]
public struct GridPos
{
    public int X;
    public int Y;
    public GridPos(int x, int y) { X = x; Y = y; }
}
