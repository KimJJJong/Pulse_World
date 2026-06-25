using System.Collections.Generic;

namespace GameServer.Infrastructure.Api.Dto;

public class PlayerStateResponse
{
    public int BaseHp { get; set; }
    public int BaseAtk { get; set; }
    public int BaseDef { get; set; }
    public int TotalHp { get; set; }
    public int TotalAtk { get; set; }
    public int TotalDef { get; set; }

    public List<EquippedGearDto> Gears { get; set; } = new();

    public string NormalAttackSkillId { get; set; } = "Attack";
    public string[] ActiveSkillSlots { get; set; } = new string[4];

    public int AppearanceId { get; set; }
}

public class EquippedGearDto
{
    public int TemplateId { get; set; }
    public int SlotType { get; set; }
    public string SkillId { get; set; } = "";
    public int BonusHp { get; set; }
    public int BonusAtk { get; set; }
    public int BonusDef { get; set; }
}
