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
                return WeaponNormal("Greatsword", new[] { 1, 0, 1, 0 });

            case "novicesword":
            case "ironswordskill":
                return WeaponSkill("Greatsword", new[] { 1, 0, 1, 1, 0, 1, 0, 1 });

            case "axeattack":
                return WeaponNormal("Parry", new[] { 1, 0, 0, 0, 1, 0, 0, 0 });

            case "noviceaxe":
                return WeaponSkill("Parry", new[] { 1, 0, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0 });

            case "bowattack":
                return WeaponNormal("Bow", new[] { 1, 0, 1, 0 });

            case "bowskill":
                return WeaponSkill("Bow", new[] { 1, 1, 0, 1, 1, 0, 1, 1 });

            case "daggerattack":
                return WeaponNormal("Dagger", new[] { 1, 1, 1, 0 });

            case "novicedagger":
                return WeaponSkill("Dagger", new[] { 1, 1, 1, 1, 1, 1, 0, 1 });

            case "staffattack":
                return WeaponNormal("Staff", new[] { 1, 0, 0, 1 });

            case "staffskill":
                return WeaponSkill("Staff", new[] { 1, 0, 1, 0, 1, 1, 0, 1, 0, 1, 1, 1 });

            case "moveskill":
                return new EquipmentRhythmSoundProfile("Staff", new[] { 120, 600 }, new[] { 0.75f, 0.8f });

            case "backstepskill":
                return new EquipmentRhythmSoundProfile("Staff", new[] { 120 }, new[] { 0.8f });

            case "hatdecoyskill":
                return new EquipmentRhythmSoundProfile("Staff", new[] { 0, 1920 }, new[] { 0.85f, 0.78f });

            case "beatorbskill":
                return new EquipmentRhythmSoundProfile("Parry", new[] { 720 }, new[] { 0.82f });

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
