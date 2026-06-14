using System;
using System.Collections.Generic;
using UnityEngine;

public static class AppearanceCatalog
{
    public sealed class AppearanceOption
    {
        public AppearanceOption(int id, string displayName, string definitionPath)
        {
            Id = id;
            DisplayName = displayName;
            DefinitionPath = definitionPath;
        }

        public int Id { get; }
        public string DisplayName { get; }
        public string DefinitionPath { get; }
    }

    private const int AutoAppearanceId = 0;
    private static IReadOnlyList<AppearanceOption> _options;
    private static Dictionary<int, AppearanceOption> _optionsById;

    public static IReadOnlyList<AppearanceOption> Options
    {
        get
        {
            EnsureLoaded();
            return _options;
        }
    }

    public static bool IsSelectableAppearanceId(int appearanceId)
    {
        EnsureLoaded();

        foreach (var option in _options)
        {
            if (option.Id == appearanceId)
                return true;
        }

        return false;
    }

    public static int GetDefaultAppearanceId()
    {
        EnsureLoaded();
        return _options.Count > 0 ? _options[0].Id : AutoAppearanceId;
    }

    public static int NormalizeSelectableAppearanceId(int preferredAppearanceId, int fallbackAppearanceId = AutoAppearanceId)
    {
        if (IsSelectableAppearanceId(preferredAppearanceId))
            return preferredAppearanceId;

        if (IsSelectableAppearanceId(fallbackAppearanceId))
            return fallbackAppearanceId;

        return GetDefaultAppearanceId();
    }

    public static bool TryGetDefinitionPath(int appearanceId, out string definitionPath)
    {
        EnsureLoaded();

        if (_optionsById.TryGetValue(appearanceId, out var option))
        {
            definitionPath = option.DefinitionPath;
            return !string.IsNullOrEmpty(definitionPath);
        }

        definitionPath = string.Empty;
        return false;
    }

    public static string GetDisplayName(int appearanceId)
    {
        EnsureLoaded();

        if (_optionsById.TryGetValue(appearanceId, out var option))
            return option.DisplayName;

        return $"Unknown({appearanceId})";
    }

    public static string GetPortraitResourcePath(int appearanceId)
    {
        EnsureLoaded();

        if (!_optionsById.TryGetValue(appearanceId, out var option))
            return "";

        string key = $"{option.DisplayName} {option.DefinitionPath}".ToLowerInvariant();
        if (key.Contains("mage") || key.Contains("magician"))
            return "UI/UI_Appear/Magician";

        if (key.Contains("rog") || key.Contains("roug"))
            return "UI/UI_Appear/Rouge";

        if (key.Contains("barbar") || appearanceId == AutoAppearanceId)
            return "UI/UI_Appear/Babarian";

        return "";
    }

    private static void EnsureLoaded()
    {
        if (_options != null && _optionsById != null)
            return;

        var options = new List<AppearanceOption>();

        var textAsset = Resources.Load<TextAsset>("Data/EntityData");
        if (textAsset != null)
        {
            try
            {
                var root = JsonUtility.FromJson<EntityDataRoot>(textAsset.text);
                if (root?.Entities != null)
                {
                    foreach (var entity in root.Entities)
                    {
                        if (entity == null || entity.EntityType != (int)RhythmRPG.Editor.StageBuilder.EntityType.Player)
                            continue;

                        options.Add(new AppearanceOption(
                            entity.EntityId,
                            ResolveDisplayName(entity),
                            entity.ResourcePath ?? string.Empty));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AppearanceCatalog] Failed to parse EntityData.json: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[AppearanceCatalog] Data/EntityData.json not found.");
        }

        _options = options;
        _optionsById = new Dictionary<int, AppearanceOption>(options.Count);
        foreach (var option in options)
            _optionsById[option.Id] = option;

        if (!_optionsById.ContainsKey(AutoAppearanceId))
        {
            var defaultOption = options.Count > 0
                ? options[0]
                : new AppearanceOption(10, "Barbarian", "Data/Entity_10_Player_Barbarian");
            _optionsById[AutoAppearanceId] = new AppearanceOption(
                AutoAppearanceId,
                defaultOption.DisplayName,
                defaultOption.DefinitionPath);
        }
    }

    private static string ResolveDisplayName(EntityDataEntry entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.Name))
        {
            var parts = entity.Name.Split('_');
            if (parts.Length > 0)
            {
                var last = parts[parts.Length - 1];
                if (!string.IsNullOrWhiteSpace(last))
                    return last;
            }

            return entity.Name;
        }

        return $"Appearance {entity.EntityId}";
    }

    [Serializable]
    private sealed class EntityDataRoot
    {
        public List<EntityDataEntry> Entities;
    }

    [Serializable]
    private sealed class EntityDataEntry
    {
        public int EntityId;
        public string Name;
        public int EntityType;
        public string ResourcePath;
    }
}
