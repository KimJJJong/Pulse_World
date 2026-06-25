using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class GameOptionsPanelPrefabBuilder
{
    private const string PrefabFolder = "Assets/0.MainProject/Resources/UI/Options";
    private const string PrefabPath = PrefabFolder + "/PF_GameOptionsPanel.prefab";

    [MenuItem("RhythmRPG/Editors/UI/Create Shared Options Panel Prefab")]
    public static void CreatePrefab()
    {
        EnsureFolder(PrefabFolder);

        var root = new GameObject("UI_Home_Options", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(GameOptionsPanel));
        try
        {
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var panel = root.GetComponent<GameOptionsPanel>();
            panel.RebuildDefaultLayout();
            root.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GameOptionsPanelPrefabBuilder] Created shared options prefab: {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void EnsureFolder(string folder)
    {
        var normalized = folder.Replace("\\", "/");
        var parts = normalized.Split('/');
        var current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }

        Directory.CreateDirectory(normalized);
    }
}
