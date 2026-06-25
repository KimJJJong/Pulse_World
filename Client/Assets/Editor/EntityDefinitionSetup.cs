#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ET = RhythmRPG.Editor.StageBuilder.EntityType;

/// <summary>
/// EntityData.json을 원본으로 EntityDefinitionSO를 생성/업데이트하는 동기화 도구입니다.
/// 이 도구의 책임은 "코드에 박힌 목록 생성"이 아니라 "JSON의 ResourcePath를 기준으로 SO를 반영"하는 것입니다.
/// Menu: RhythmRPG/Editors/Setup/Entity Definitions
/// </summary>
public class EntityDefinitionSetup
{
    private const string EntityJsonAssetPath = "Assets/Resources/Data/EntityData.json";
    private const string ResourcesRootFolder = "Assets/Resources";
    private const string TargetSoFolder = "Assets/Resources/Data";
    private const string LegacySoFolder = "Assets/0.MainProject/Resources/Data";

    [MenuItem("RhythmRPG/Editors/Setup/Entity Definitions")]
    public static void SetupAll()
    {
        EntityDataRootDto root = LoadEntityData();
        if (root?.Entities == null || root.Entities.Count == 0)
        {
            Debug.LogError($"[EntityDefinitionSetup] No entity entries found in {EntityJsonAssetPath}.");
            EditorUtility.DisplayDialog(
                "Entity Definitions",
                $"JSON이 비어 있거나 읽을 수 없습니다.\n\n경로: {EntityJsonAssetPath}",
                "OK");
            return;
        }

        int processed = 0;
        int created = 0;
        int updated = 0;
        int migrated = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var dto in root.Entities.OrderBy(e => e.EntityId))
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
                {
                    continue;
                }

                string soPath = GetTargetSoPath(dto);
                EnsureFolderForAssetPath(soPath);

                var so = AssetDatabase.LoadAssetAtPath<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>(soPath);
                if (so == null)
                {
                    string legacyPath = FindLegacyAssetPath(dto) ?? GetLegacySoPath(dto);
                    if (!string.IsNullOrWhiteSpace(legacyPath))
                    {
                        string moveResult = AssetDatabase.MoveAsset(legacyPath, soPath);
                        if (string.IsNullOrEmpty(moveResult))
                        {
                            migrated++;
                            Debug.Log($"[EntityDefinitionSetup] Migrated legacy asset: {legacyPath} -> {soPath}");
                            so = AssetDatabase.LoadAssetAtPath<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>(soPath);
                        }
                    }
                }

                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>();
                    AssetDatabase.CreateAsset(so, soPath);
                    created++;
                    Debug.Log($"[EntityDefinitionSetup] Created: {soPath}");
                }
                else
                {
                    updated++;
                }

                so.EntityId = dto.EntityId;
                so.EntityName = ResolveEntityName(dto, soPath);
                so.Type = (ET)dto.EntityType;
                if (dto.MaxHp > 0)
                {
                    so.MaxHp = dto.MaxHp;
                }

                EditorUtility.SetDirty(so);
                processed++;
            }

            CleanupLegacyFolder(root.Entities);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[EntityDefinitionSetup] Sync complete. Processed={processed}, Created={created}, Updated={updated}, Migrated={migrated}, Target={TargetSoFolder}");
        EditorUtility.DisplayDialog(
            "완료",
            $"EntityDefinitionSO 동기화 완료!\n\n" +
            $"Source: {EntityJsonAssetPath}\n" +
            $"Target: {TargetSoFolder}\n" +
            $"Processed: {processed}, Created: {created}, Updated: {updated}, Migrated: {migrated}\n" +
            $"Prefab는 기존 SO에 있던 값을 유지하고, 새로 생성된 항목은 필요 시 수동 지정합니다.",
            "OK");
    }

    private static EntityDataRootDto LoadEntityData()
    {
        string json = null;

        var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(EntityJsonAssetPath);
        if (textAsset != null)
        {
            json = textAsset.text;
        }
        else
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", "Data", "EntityData.json"));
            if (File.Exists(fullPath))
            {
                json = File.ReadAllText(fullPath);
            }
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<EntityDataRootDto>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EntityDefinitionSetup] Failed to parse EntityData.json: {ex.Message}");
            return null;
        }
    }

    private static void CleanupLegacyFolder(List<EntityDataDto> entities)
    {
        if (!AssetDatabase.IsValidFolder(LegacySoFolder))
        {
            return;
        }

        foreach (var dto in entities.Where(e => e != null))
        {
            string legacyPath = GetLegacySoPath(dto);
            string targetPath = GetTargetSoPath(dto);

            bool legacyExists = AssetDatabase.LoadAssetAtPath<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>(legacyPath) != null;
            if (!legacyExists)
            {
                continue;
            }

            bool targetExists = AssetDatabase.LoadAssetAtPath<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>(targetPath) != null;
            if (targetExists)
            {
                if (AssetDatabase.DeleteAsset(legacyPath))
                {
                    Debug.Log($"[EntityDefinitionSetup] Removed legacy duplicate: {legacyPath}");
                }
                continue;
            }

            string moveResult = AssetDatabase.MoveAsset(legacyPath, targetPath);
            if (string.IsNullOrEmpty(moveResult))
            {
                Debug.Log($"[EntityDefinitionSetup] Migrated legacy asset: {legacyPath} -> {targetPath}");
            }
            else
            {
                Debug.LogWarning($"[EntityDefinitionSetup] Failed to migrate {legacyPath}: {moveResult}");
            }
        }
    }

    private static string GetTargetSoPath(EntityDataDto dto)
    {
        string resourcePath = NormalizeResourcePath(dto?.ResourcePath);
        if (!string.IsNullOrWhiteSpace(resourcePath))
        {
            return $"{ResourcesRootFolder}/{resourcePath}.asset";
        }

        string fallbackName = !string.IsNullOrWhiteSpace(dto?.Name)
            ? dto.Name
            : $"Entity_{dto?.EntityId}";

        return $"{TargetSoFolder}/{fallbackName}.asset";
    }

    private static string GetLegacySoPath(EntityDataDto dto)
    {
        string resourcePath = NormalizeResourcePath(dto?.ResourcePath);
        if (!string.IsNullOrWhiteSpace(resourcePath))
        {
            return $"Assets/0.MainProject/Resources/{resourcePath}.asset";
        }

        string fallbackName = !string.IsNullOrWhiteSpace(dto?.Name)
            ? dto.Name
            : $"Entity_{dto?.EntityId}";

        return $"{LegacySoFolder}/{fallbackName}.asset";
    }

    private static string FindLegacyAssetPath(EntityDataDto dto)
    {
        if (!AssetDatabase.IsValidFolder(LegacySoFolder))
        {
            return null;
        }

        foreach (var guid in AssetDatabase.FindAssets("t:EntityDefinitionSO", new[] { LegacySoFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>(path);
            if (so != null && so.EntityId == dto.EntityId)
            {
                return path;
            }
        }

        return null;
    }

    private static string NormalizeResourcePath(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return string.Empty;
        }

        string normalized = resourcePath.Replace('\\', '/').Trim();
        normalized = normalized.TrimStart('/');
        if (normalized.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring("Assets/Resources/".Length);
        }

        if (normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            normalized = Path.ChangeExtension(normalized, null);
        }

        return normalized;
    }

    private static string ResolveEntityName(EntityDataDto dto, string soPath)
    {
        string pathName = Path.GetFileNameWithoutExtension(soPath);
        if (string.IsNullOrWhiteSpace(pathName))
        {
            return dto?.Name ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(dto?.ResourcePath) &&
            !string.IsNullOrWhiteSpace(dto.Name) &&
            !string.Equals(dto.Name, pathName, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning(
                $"[EntityDefinitionSetup] JSON name mismatch for EntityId={dto.EntityId}. " +
                $"Name='{dto.Name}', ResourcePath='{dto.ResourcePath}'. Using '{pathName}' from the resource path.");
        }

        return pathName;
    }

    private static void EnsureFolderForAssetPath(string assetPath)
    {
        string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        EnsureFolder(folder);
    }

    private static void EnsureFolder(string folder)
    {
        folder = folder.Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = Path.GetFileName(folder);

        if (string.IsNullOrWhiteSpace(parent))
        {
            parent = "Assets";
        }

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(parent, name);
            Debug.Log($"[EntityDefinitionSetup] Created folder: {folder}");
        }
    }

    [Serializable]
    public class EntityDataRootDto
    {
        public List<EntityDataDto> Entities = new List<EntityDataDto>();
    }

    [Serializable]
    public class EntityDataDto
    {
        public int EntityId;
        public string Name;
        public int EntityType;
        public int MaxHp;
        public string ResourcePath;
    }
}
#endif
