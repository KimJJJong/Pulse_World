using Newtonsoft.Json;

public static class PlayerStateDtos
{
    public sealed class PlayerStateResponse
    {
        [JsonProperty("BaseHp")] public int BaseHp;
        [JsonProperty("BaseAtk")] public int BaseAtk;
        [JsonProperty("BaseDef")] public int BaseDef;
        [JsonProperty("TotalHp")] public int TotalHp;
        [JsonProperty("TotalAtk")] public int TotalAtk;
        [JsonProperty("TotalDef")] public int TotalDef;

        [JsonProperty("Gears")] public EquippedGearDto[] Gears;

        [JsonProperty("NormalAttackSkillId")] public string NormalAttackSkillId;
        [JsonProperty("ActiveSkillSlots")] public string[] ActiveSkillSlots;
        [JsonProperty("SavedAppearanceId")] public int SavedAppearanceId;
        [JsonProperty("AppearanceId")] public int AppearanceId;
    }

    public sealed class EquippedGearDto
    {
        [JsonProperty("TemplateId")] public int TemplateId;
        [JsonProperty("SlotType")] public int SlotType;
        [JsonProperty("SkillId")] public string SkillId;
        [JsonProperty("BonusHp")] public int BonusHp;
        [JsonProperty("BonusAtk")] public int BonusAtk;
        [JsonProperty("BonusDef")] public int BonusDef;
    }

    public sealed class SetAppearanceRequest
    {
        [JsonProperty("AppearanceId")] public int AppearanceId;

        public SetAppearanceRequest(int appearanceId) => AppearanceId = appearanceId;
    }
}
