using System;
using System.Collections.Generic;

[Serializable]
public sealed class SkillSet
{
    public List<SkillDef> Skills { get; set; } = new();
}

public enum SkillAoeShape : byte { Cells = 0, Diamond = 1, Rect = 2, Line = 3 }
public enum SkillDirType : byte { None = 0, SelfToTarget = 1, Fixed = 2 }
public enum FixedDir : byte { Up = 0, Right = 1, Down = 2, Left = 3 }

[Serializable]
public sealed class SkillDef
{
    public string SkillId { get; set; } = "";

    public int CooldownBeats { get; set; } = 0;
    public int Damage { get; set; } = 10;
    public bool BlockedByWall { get; set; } = false;

    public SkillAoeShape Shape { get; set; } = SkillAoeShape.Diamond;
    public int ParamA { get; set; } = 1;
    public int ParamB { get; set; } = 0;

    public SkillDirType DirType { get; set; } = SkillDirType.None;
    public FixedDir FixedDir { get; set; } = FixedDir.Up;

    public bool HitPlayers { get; set; } = true;
    public bool HitMonsters { get; set; } = false;

    // (옵션) 다단/지속
    public int Ticks { get; set; } = 1;
    public int TickIntervalBeats { get; set; } = 0;
}
