using GameServer.InGame.Manager.Entity;

public enum SkillAoeShape : byte { Cells = 0, Diamond = 1, Rect = 2, Line = 3 }
// Diamond/Cells 같은 방향 불필요
public enum SkillDirType : byte{ None = 0, SelfToTarget = 1, Fixed = 2 }



public sealed class SkillDef
{
    public string SkillId { get; set; } = "";
    public SkillAoeShape Shape { get; set; } = SkillAoeShape.Diamond;
    public int ParamA { get; set; } = 1;
    public int ParamB { get; set; } = 0;

    public SkillDirType DirType { get; set; } = SkillDirType.None;
    public FixedDir FixedDir { get; set; } = FixedDir.Up;

    public bool BlockedByWall { get; set; } = false;
    public int Damage { get; set; } = 10;
    public int CooldownBeats { get; set; } = 0;
    public bool CanHit(MapEntity attacker, MapEntity target)
    {
        // 예시: 몬스터는 플레이어만 공격, 플레이어는 몬스터만 공격
        if (attacker.Type == EntityType.Monster && target.Type == EntityType.Player) return true;
        if (attacker.Type == EntityType.Player && target.Type == EntityType.Monster) return true;
        return false;
    }
}