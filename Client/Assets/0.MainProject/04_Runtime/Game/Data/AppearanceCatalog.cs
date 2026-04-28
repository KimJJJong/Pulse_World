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

    private static void EnsureLoaded()
    {
        if (_options != null && _optionsById != null)
            return;

        var options = new List<AppearanceOption>
        {
            new(AutoAppearanceId, "자동(장비기반)", "Data/Entity_10_Player_Barbarian")
        };

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
