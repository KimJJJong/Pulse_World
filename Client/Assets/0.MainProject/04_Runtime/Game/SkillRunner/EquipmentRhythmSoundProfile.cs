using System;

internal sealed class EquipmentRhythmSoundProfile
{
    private const float DefaultVolume = 0.85f;

    public string EventPath { get; }
    public int Count => _ticks.Length;

    private readonly int[] _ticks;
    private readonly float[] _volumes;

    private EquipmentRhythmSoundProfile(string eventPath, int[] ticks, float[] volumes = null)
    {
        EventPath = eventPath;
        _ticks = ticks ?? Array.Empty<int>();
        _volumes = volumes;
    }

    public string GetEventPath(int index)
    {
        if (string.IsNullOrEmpty(EventPath))
            return string.Empty;

        if (EventPath.EndsWith("_Skill", StringComparison.OrdinalIgnoreCase))
        {
            if (EventPath.Contains("Sword"))
            {
                if (index == _ticks.Length - 1)
                    return EventPath + "_Final";
            }
            else if (EventPath.Contains("Axe"))
            {
                if (index >= _ticks.Length - 2)
                    return EventPath + "_Final";
            }
            else if (EventPath.Contains("Staff"))
            {
                if (index >= _ticks.Length - 2)
                    return EventPath + "_Final";
            }
        }
        return EventPath;
    }

    public bool TryGetEvent(int index, int totalDurationTicks, out int triggerTick, out float volume)
    {
        triggerTick = 0;
        volume = DefaultVolume;

        if (index < 0 || index >= _ticks.Length)
            return false;

        triggerTick = _ticks[index];
        if (triggerTick < 0 || triggerTick > totalDurationTicks)
            return false;

        if (_volumes != null && index < _volumes.Length)
            volume = _volumes[index];

        return true;
    }

    public static EquipmentRhythmSoundProfile Resolve(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return null;

        switch (skillId.Trim().ToLowerInvariant())
        {
            case "swordattack":
            case "ironswordattack":
                return WeaponNormal("Forest_Sword_Normal", new[] { 1, 0, 1, 0 });

            case "novicesword":
            case "ironswordskill":
                return WeaponSkill("Forest_Sword_Skill", new[] { 1, 0, 1, 1, 0, 1, 0, 1 });

            case "axeattack":
                return WeaponNormal("Forest_Axe_Normal", new[] { 1, 0, 0, 0, 1, 0, 0, 0 });

            case "noviceaxe":
                return WeaponSkill("Forest_Axe_Skill", new[] { 1, 0, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0 });

            case "bowattack":
                return WeaponNormal("Forest_Bow_Normal", new[] { 1, 0, 1, 0 });

            case "bowskill":
                return WeaponSkill("Forest_Bow_Skill", new[] { 1, 1, 0, 1, 1, 0, 1, 1 });

            case "daggerattack":
                return WeaponNormal("Forest_Dagger_Normal", new[] { 1, 1, 1, 0 });

            case "novicedagger":
                return WeaponSkill("Forest_Dagger_Skill", new[] { 1, 1, 1, 1, 1, 1, 0, 1 });

            case "staffattack":
                return WeaponNormal("Forest_Staff_Normal", new[] { 1, 0, 0, 1 });

            case "novicestaff":
            case "staffskill":
                return WeaponSkill("Forest_Staff_Skill", new[] { 1, 0, 1, 0, 1, 1, 0, 1, 0, 1, 1, 1 });

            case "moveskill":
                return new EquipmentRhythmSoundProfile("Forest_Shoes_Boots", new[] { 120, 600 }, new[] { 1.25f, 1.3f });

            case "backstepskill":
                return new EquipmentRhythmSoundProfile("Forest_Shoes_Heavy", new[] { 120, 600 }, new[] { 1.3f, 1.3f });

            case "hatdecoyskill":
                return new EquipmentRhythmSoundProfile("Forest_Hat_Helm", new[] { 0, 1920 }, new[] { 0.85f, 0.78f });

            case "beatorbskill":
                return new EquipmentRhythmSoundProfile("Forest_Accessory_Ring", new[] { 720 }, new[] { 0.82f });

            default:
                return null;
        }
    }

    private static EquipmentRhythmSoundProfile WeaponNormal(string eventPath, int[] pattern)
    {
        return new EquipmentRhythmSoundProfile(eventPath, TicksFromPattern(pattern));
    }

    private static EquipmentRhythmSoundProfile WeaponSkill(string eventPath, int[] pattern)
    {
        return new EquipmentRhythmSoundProfile(eventPath, TicksFromPattern(pattern));
    }

    private static int[] TicksFromPattern(int[] pattern)
    {
        if (pattern == null || pattern.Length == 0)
            return Array.Empty<int>();

        int count = 0;
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != 0)
                count++;
        }

        int[] ticks = new int[count];
        int write = 0;
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != 0)
                ticks[write++] = i * 120;
        }

        return ticks;
    }
}
