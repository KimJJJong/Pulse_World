using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace RhythmRPG.Editor.StageBuilder
{
    public static class EntityExporter
    {
        private static readonly string[] SearchFolders =
        {
            "Assets/Resources/Data",
        };

        [MenuItem("RhythmRPG/Editors/Data/Export Entity Data")]
        public static void Export()
        {
            // 1. Find all EntityDefinitionSO assets
            string[] guids = AssetDatabase.FindAssets("t:EntityDefinitionSO", SearchFolders);
            if (guids == null || guids.Length == 0)
            {
                Debug.LogError("[EntityExporter] No EntityDefinitionSO found!");
                return;
            }

            var root = new EntityDataRoot();
            
            // 2. Process each asset
            foreach (var g in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(g);
                var so = AssetDatabase.LoadAssetAtPath<EntityDefinitionSO>(assetPath);
                
                if (so == null) continue;

                // Calculate Resource Path (relative to Resources folder)
                // e.g. Assets/Resources/Entities/Entity_3001.asset -> Entities/Entity_3001
                // Calculate Resource Path (relative to Resources folder)
                // e.g. Assets/Resources/Entities/Entity_3001.asset -> Entities/Entity_3001
                string resourcePath = "";
                if (assetPath.Contains("/Resources/"))
                {
                    int resIndex = assetPath.IndexOf("/Resources/") + 11; // 11 is len of "/Resources/"
                    string subPath = assetPath.Substring(resIndex);
                    // Remove extension
                    resourcePath = System.IO.Path.ChangeExtension(subPath, null);
                }
                else
                {
                    Debug.LogWarning($"[EntityExporter] Entity {so.name} is not in a Resources folder! Path: {assetPath}");
                }

                root.Entities.Add(new EntityDataDTO
                {
                    EntityId = so.EntityId,
                    Name = so.EntityName,
                    EntityType = (int)so.Type,
                    MaxHp = so.MaxHp,
                    ResourcePath = resourcePath // [NEW]
                });
            }

            string json = JsonUtility.ToJson(root, true);

            // Path 1: Client Runtime Map (Assets/Resources/Data/EntityData.json)
            string runtimePath = "Assets/Resources/Data/EntityData.json";
            ExportToFile(runtimePath, json);

            // Path 2: Server
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string gameServerRoot = Path.Combine(projectRoot, "Server", "GameServer", "Content", "01.Game");
            string serverPath = Path.Combine(gameServerRoot, "Entity", "Json", "EntityData.json");
            ExportToFile(serverPath, json);

            AssetDatabase.Refresh();
        }

        private static void ExportToFile(string path, string content)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, content);
                Debug.Log($"[EntityExporter] Exported to: {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EntityExporter] Failed to export to {path}: {ex.Message}");
            }
        }

        [System.Serializable]
        public class EntityDataRoot
        {
            public List<EntityDataDTO> Entities = new List<EntityDataDTO>();
        }

        [System.Serializable]
        public class EntityDataDTO
        {
            public int EntityId;
            public string Name;
            public int EntityType;
            public int MaxHp;
            public string ResourcePath; // [NEW] Path for Lazy Loading
        }
    }
}
