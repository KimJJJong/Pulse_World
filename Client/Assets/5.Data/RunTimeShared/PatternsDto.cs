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
    Move       // Generic Move
}

public enum TargetType : byte { ClosestPlayer = 0, LowestHpPlayer = 1, RandomPlayer = 2 }

[Serializable]
public sealed class TargetDef
{
    public TargetType Type = TargetType.ClosestPlayer;
    public int MaxRange = 999;
    public bool RequireAlive = true;
}

// Pattern쪽 Area는 "origin 결정을 위한 힌트 + Cells 편집" 정도로만 유지(룰은 SkillDef)
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

    public string SkillId = ""; // CastSkill일 때 필수

    public int TelegraphBeats;
    public byte TelegraphStyleId;

    public AreaDef Area = new();

    // -- Extended for New Pattern System --
    public int MoveDistance = 1;
    public MoveStrategy MoveStrategy = MoveStrategy.Random;

    /// <summary>
    /// [방향 기반 이동] None이 아니면 MoveStrategy 대신 이 방향으로 1칸씩 MoveDistance번 이동.
    /// 절대 위치 예약이 아니므로 순간이동/대각선이동/2칸 점프가 발생하지 않는다.
    /// </summary>
    public MoveDirection MoveDirection = MoveDirection.None;

#if UNITY_EDITOR
    // Editor-only reference for convenience
    public Client.Data.NewSkillSO SkillRef;
#endif
}

public enum MoveStrategy
{
    Random,
    Flee,    // Run away from Target
    Forward, // Based on Facing? Or absolute? Keep simplistic for now.
    Backward
}

/// <summary>
/// [방향 기반 이동] 절대 위치 예약 대신 방향+칸수로 AI 이동을 정의.
/// 이렇게 하면 플레이어 위치와 무관하게 매 Beat 1칸씩 독립 판정되어
/// 순간이동/대각선이동/2칸 점프 문제를 원천 차단한다.
/// </summary>
public enum MoveDirection
{
    None = 0,
    Up = 1,             // +Y
    Down = 2,           // -Y
    Left = 3,           // -X
    Right = 4,          // +X
    TowardTarget = 5,   // 매 Beat 타겟 방향으로 1칸 (Manhattan 우선)
    AwayFromTarget = 6, // 매 Beat 타겟 반대 방향으로 1칸
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
