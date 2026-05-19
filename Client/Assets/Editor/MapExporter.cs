#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MapExportUtility
{
    private const string ServerMapJsonRelativePath = "../Server/GameServer/Content/01.Game/Map/Json";

    [MenuItem("RhythmRPG/Editors/World/Export MapAsset to JSON", true)]
    private static bool Validate()
        => Selection.activeObject is MapAsset;

    [MenuItem("RhythmRPG/Editors/World/Export MapAsset to JSON")]
    private static void ExportSelected()
    {
        var asset = (MapAsset)Selection.activeObject;
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string path = Path.GetFullPath(
            Path.Combine(projectRoot, ServerMapJsonRelativePath, $"{asset.name}.json"));
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var jsonObj = new MapJson
        {
            appearancePalette = GetResourcesPath(asset.AppearancePalette),
            width = asset.Width,
            height = asset.Height,
            cells = new MapJson.Cell[asset.Width * asset.Height]
        };

        if (asset.AppearancePalette != null && string.IsNullOrEmpty(jsonObj.appearancePalette))
        {
            Debug.LogWarning(
                $"[MapExport] AppearancePalette '{asset.AppearancePalette.name}' is not under a Resources folder. " +
                "Server JSON was exported without a runtime palette path.");
        }

        asset.RebuildAppearanceAutoTiles();
        EditorUtility.SetDirty(asset);

        for (int i = 0; i < jsonObj.cells.Length; i++)
        {
            var c = asset.Cells[i];
            var a = asset.AppearanceCells[i];
            jsonObj.cells[i] = new MapJson.Cell
            {
                k = (byte)c.Kind,
                v = c.Variant,
                a = (byte)a.Kind,
                av = a.Variant
            };
        }

        // pretty print(사람이 보기 좋게)
        var json = JsonUtility.ToJson(jsonObj, prettyPrint: true);

        File.WriteAllText(path, json, Encoding.UTF8);

        Debug.Log($"[MapExport] Exported: {path}");
        AssetDatabase.Refresh();
    }

    private static string GetResourcesPath(UnityEngine.Object asset)
    {
        if (asset == null)
            return "";

        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(assetPath))
            return "";

        const string marker = "/Resources/";
        int markerIndex = assetPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return "";

        string resourcePath = assetPath.Substring(markerIndex + marker.Length);
        string extension = Path.GetExtension(resourcePath);
        return string.IsNullOrEmpty(extension)
            ? resourcePath
            : resourcePath.Substring(0, resourcePath.Length - extension.Length);
    }
}
#endif
