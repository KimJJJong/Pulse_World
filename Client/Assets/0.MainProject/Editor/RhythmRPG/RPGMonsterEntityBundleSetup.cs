#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RhythmRPG.Editor.StageBuilder;
using UnityEditor;
using UnityEngine;

namespace RhythmRPG.Editor
{
    public static class RPGMonsterEntityBundleSetup
    {
        private const string EnemyPrefabFolder = "Assets/Resources/Entity/Enemy";
        private const string EntityDataFolder = "Assets/Resources/Data";
        private const string EntityDataJsonPath = EntityDataFolder + "/EntityData.json";

        private static readonly MonsterSpec[] Monsters =
        {
            new(1020, "Beholder", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave02/CharacterPBR/BeholderPBRDefault.prefab", "BeholderPBRDefault.prefab"),
            new(1021, "BlackKnight", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave02/CharacterPBR/BlackKnightPBRDefault.prefab", "BlackKnightPBRDefault.prefab", 250),
            new(1022, "ChestMonster", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave02/CharacterPBR/ChestMonsterPBRDefault.prefab", "ChestMonsterPBRDefault.prefab"),
            new(1023, "CrabMonster", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave02/CharacterPBR/CrabMonsterPBRDefault.prefab", "CrabMonsterPBRDefault.prefab"),
            new(1024, "FlyingDemon", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave02/CharacterPBR/FylingDemonPBRDefault.prefab", "FlyingDemonPBRDefault.prefab"),
            new(1025, "LizardWarrior", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave02/CharacterPBR/LizardWarriorPBRDefault.prefab", "LizardWarriorPBRDefault.prefab"),
            new(1026, "RatAssassin", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave02/CharacterPBR/RatAssassinPBRDefault.prefab", "RatAssassinPBRDefault.prefab"),
            new(1027, "Specter", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave02/CharacterPBR/SpecterPBRDefault.prefab", "SpecterPBRDefault.prefab", 50),
            new(1028, "Werewolf", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave02/CharacterPBR/WerewolfPBRDefault.prefab", "WerewolfPBRDefault.prefab"),
            new(1029, "WormMonster", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave02/CharacterPBR/WormMonsterPBRDefault.prefab", "WormMonsterPBRDefault.prefab"),
            new(1030, "BattleBee", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave03/CharacterPBR/BattleBeePBRDefault.prefab", "BattleBeePBRDefault.prefab"),
            new(1031, "BishopKnight", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave03/CharacterPBR/BishopKnightPBRDefault.prefab", "BishopKnightPBRDefault.prefab"),
            new(1032, "Cactus", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave03/CharacterPBR/CactusPBRDefault.prefab", "CactusPBRDefault.prefab"),
            new(1033, "Cyclops", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave03/CharacterPBR/CyclopsPBRDefault.prefab", "CyclopsPBRDefault.prefab"),
            new(1034, "DemonKing", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave03/CharacterPBR/DemonKingPBRDefault.prefab", "DemonKingPBRDefault.prefab", 180),
            new(1035, "Fishman", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave03/CharacterPBR/FishmanPBRDefault.prefab", "FishmanPBRDefault.prefab"),
            new(1036, "MushroomAngry", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave03/CharacterPBR/MushroomAngryPBRDefault.prefab", "MushroomAngryPBRDefault.prefab"),
            new(1037, "MushroomSmile", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave03/CharacterPBR/MushroomSmilePBRDefault.prefab", "MushroomSmilePBRDefault.prefab"),
            new(1038, "NagaWizard", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave03/CharacterPBR/NagaWizardPBRDefault.prefab", "NagaWizardPBRDefault.prefab"),
            new(1039, "Salamander", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave03/CharacterPBR/SalamanderPBRDefault.prefab", "SalamanderPBRDefault.prefab"),
            new(1040, "StingRay", "Assets/RPGMonsterBundlePBR/CommonStuffs/Prefab/Wave03/CharacterPBR/StingRayPBRDefault.prefab", "StingRayPBRDefault.prefab"),
        };

        [MenuItem("RhythmRPG/Editors/Setup/RPG Monster Entity Bundle")]
        public static void Setup()
        {
            EnsureFolder(EnemyPrefabFolder);
            EnsureFolder(EntityDataFolder);

            int copiedPrefabs = 0;
            int createdDefinitions = 0;
            int updatedDefinitions = 0;

            foreach (MonsterSpec monster in Monsters)
            {
                string targetPrefabPath = monster.TargetPrefabPath;
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(monster.SourcePrefabPath))
                {
                    throw new FileNotFoundException("Source monster prefab is missing.", monster.SourcePrefabPath);
                }

                if (!AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabPath))
                {
                    if (!AssetDatabase.CopyAsset(monster.SourcePrefabPath, targetPrefabPath))
                    {
                        throw new InvalidOperationException($"Failed to copy prefab: {monster.SourcePrefabPath} -> {targetPrefabPath}");
                    }

                    copiedPrefabs++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (MonsterSpec monster in Monsters)
            {
                string targetPrefabPath = monster.TargetPrefabPath;
                ConfigurePrefabRoot(targetPrefabPath);

                var targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabPath);
                if (targetPrefab == null)
                    throw new FileNotFoundException("Target monster prefab was not imported.", targetPrefabPath);

                string definitionPath = monster.DefinitionPath;
                var definition = AssetDatabase.LoadAssetAtPath<EntityDefinitionSO>(definitionPath);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<EntityDefinitionSO>();
                    AssetDatabase.CreateAsset(definition, definitionPath);
                    createdDefinitions++;
                }
                else
                {
                    updatedDefinitions++;
                }

                definition.EntityId = monster.EntityId;
                definition.EntityName = monster.DefinitionName;
                definition.Type = StageBuilder.EntityType.Monster;
                definition.MaxHp = monster.MaxHp;
                definition.Prefab = targetPrefab;
                definition.AnimatorController = null;
                EditorUtility.SetDirty(definition);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EntityAnimationProfileGenerator.Generate();
            ExportEntityDataJson();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[RPGMonsterEntityBundleSetup] Complete. Prefabs copied={copiedPrefabs}, " +
                $"definitions created={createdDefinitions}, definitions updated={updatedDefinitions}, " +
                $"monsters={Monsters.Length}, firstId={Monsters.Min(m => m.EntityId)}, lastId={Monsters.Max(m => m.EntityId)}");
        }

        private static void ConfigurePrefabRoot(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                string expectedName = Path.GetFileNameWithoutExtension(prefabPath);
                if (!string.Equals(root.name, expectedName, StringComparison.Ordinal))
                    root.name = expectedName;

                var animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.runtimeAnimatorController == null)
                    Debug.LogWarning($"[RPGMonsterEntityBundleSetup] Prefab has no animator controller: {prefabPath}");

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ExportEntityDataJson()
        {
            var root = new EntityDataRoot();
            string[] guids = AssetDatabase.FindAssets("t:EntityDefinitionSO", new[] { EntityDataFolder });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<EntityDefinitionSO>(assetPath);
                if (definition == null)
                    continue;

                root.Entities.Add(new EntityDataDto
                {
                    EntityId = definition.EntityId,
                    Name = definition.EntityName,
                    EntityType = (int)definition.Type,
                    MaxHp = definition.MaxHp,
                    ResourcePath = GetResourcePath(assetPath)
                });
            }

            root.Entities = root.Entities
                .OrderBy(entity => entity.EntityId)
                .ThenBy(entity => entity.Name, StringComparer.Ordinal)
                .ToList();

            string json = JsonUtility.ToJson(root, true);
            WriteText(EntityDataJsonPath, json);
            WriteText(GetServerEntityDataJsonPath(), json);
        }

        private static string GetResourcePath(string assetPath)
        {
            const string marker = "/Resources/";
            int resourcesIndex = assetPath.IndexOf(marker, StringComparison.Ordinal);
            if (resourcesIndex < 0)
                return string.Empty;

            string subPath = assetPath.Substring(resourcesIndex + marker.Length);
            return Path.ChangeExtension(subPath, null).Replace('\\', '/');
        }

        private static string GetServerEntityDataJsonPath()
        {
            string clientRoot = Directory.GetParent(Application.dataPath)?.FullName;
            string repoRoot = Directory.GetParent(clientRoot ?? string.Empty)?.FullName;
            if (string.IsNullOrWhiteSpace(repoRoot))
                throw new DirectoryNotFoundException("Could not resolve repository root from Application.dataPath.");

            return Path.Combine(repoRoot, "Server", "GameServer", "Content", "01.Game", "Entity", "Json", "EntityData.json");
        }

        private static void WriteText(string path, string content)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, content);
            Debug.Log($"[RPGMonsterEntityBundleSetup] Exported: {path}");
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private readonly struct MonsterSpec
        {
            public MonsterSpec(int entityId, string name, string sourcePrefabPath, string targetPrefabFileName, int maxHp = 10)
            {
                EntityId = entityId;
                Name = name;
                SourcePrefabPath = sourcePrefabPath;
                TargetPrefabFileName = targetPrefabFileName;
                MaxHp = maxHp;
            }

            public int EntityId { get; }
            public string Name { get; }
            public string SourcePrefabPath { get; }
            public string TargetPrefabFileName { get; }
            public int MaxHp { get; }
            public string DefinitionName => $"Entity_{EntityId}_Monster_{Name}";
            public string DefinitionPath => $"{EntityDataFolder}/{DefinitionName}.asset";
            public string TargetPrefabPath => $"{EnemyPrefabFolder}/{TargetPrefabFileName}";
        }

        [Serializable]
        private sealed class EntityDataRoot
        {
            public List<EntityDataDto> Entities = new();
        }

        [Serializable]
        private sealed class EntityDataDto
        {
            public int EntityId;
            public string Name;
            public int EntityType;
            public int MaxHp;
            public string ResourcePath;
        }
    }
}
#endif
