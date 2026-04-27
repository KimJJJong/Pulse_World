using System.Collections.Generic;

public static class AppearanceCatalog
{
    public sealed class AppearanceOption
    {
        public AppearanceOption(int id, string displayName, string prefabName)
        {
            Id = id;
            DisplayName = displayName;
            PrefabName = prefabName;
        }

        public int Id { get; }
        public string DisplayName { get; }
        public string PrefabName { get; }
    }

    public static readonly IReadOnlyList<AppearanceOption> Options = new[]
    {
        new AppearanceOption(0, "자동(장비기반)", "Players/Barbarian"),
        new AppearanceOption(10, "Barbarian", "Players/Barbarian"),
        new AppearanceOption(11, "Mage", "Players/Mage"),
        new AppearanceOption(12, "Rogu", "Players/Rogue"),
    };

    public static bool TryGetPrefabName(int appearanceId, out string prefabName)
    {
        foreach (var option in Options)
        {
            if (option.Id != appearanceId)
                continue;

            prefabName = option.PrefabName;
            return !string.IsNullOrEmpty(prefabName);
        }

        prefabName = "";
        return false;
    }

    public static string GetDisplayName(int appearanceId)
    {
        foreach (var option in Options)
        {
            if (option.Id == appearanceId)
                return option.DisplayName;
        }

        return $"Unknown({appearanceId})";
    }
}
