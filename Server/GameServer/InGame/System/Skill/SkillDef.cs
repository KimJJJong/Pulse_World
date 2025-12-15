using GameServer.InGame.Manager.Entity;

public enum SkillAoeShape : byte { Diamond = 1, Rect = 2, Line = 3, Cells = 0 }

public sealed class SkillDef
{
    public string SkillId { get; set; } = "";

    // 룰
    public int CooldownBeats { get; set; } = 0;
    public int Damage { get; set; } = 10;
    public bool BlockedByWall { get; set; } = false;

    // 판정 Shape(서버 권위)
    public SkillAoeShape Shape { get; set; } = SkillAoeShape.Diamond;
    public int ParamA { get; set; } = 1; // radius/width/len
    public int ParamB { get; set; } = 0; // height/thickness 등

    // 타겟 규칙
    public bool HitPlayers { get; set; } = true;
    public bool HitMonsters { get; set; } = false;

    // (선택) 다단/지속
    public int Ticks { get; set; } = 1;          // 1이면 단타
    public int TickIntervalBeats { get; set; } = 0; // 0이면 즉시 1회
    public bool CanHit(MapEntity attacker, MapEntity target)
    {
        // 예시: 몬스터는 플레이어만 공격, 플레이어는 몬스터만 공격
        if (attacker.Type == EntityType.Monster && target.Type == EntityType.Player) return true;
        if (attacker.Type == EntityType.Player && target.Type == EntityType.Monster) return true;
        return false;
    }
}