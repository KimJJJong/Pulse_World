#if UNITY_EDITOR
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
            width = asset.Width,
            height = asset.Height,
            cells = new MapJson.Cell[asset.Width * asset.Height]
        };

        for (int i = 0; i < jsonObj.cells.Length; i++)
        {
            var c = asset.Cells[i];
            jsonObj.cells[i] = new MapJson.Cell
            {
                k = (byte)c.Kind,
                v = c.Variant
            };
        }

        // pretty print(사람이 보기 좋게)
        var json = JsonUtility.ToJson(jsonObj, prettyPrint: true);

        File.WriteAllText(path, json, Encoding.UTF8);

        Debug.Log($"[MapExport] Exported: {path}");
        AssetDatabase.Refresh();
    }
}
#endif
