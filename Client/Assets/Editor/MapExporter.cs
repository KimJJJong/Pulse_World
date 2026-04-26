#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MapExportUtility
{
    [MenuItem("RhythmRPG/Editors/World/Export MapAsset to JSON", true)]
    private static bool Validate()
        => Selection.activeObject is MapAsset;

    [MenuItem("RhythmRPG/Editors/World/Export MapAsset to JSON")]
    private static void ExportSelected()
    {
        var asset = (MapAsset)Selection.activeObject;

        // 저장 경로 선택
        var defaultName = $"{asset.name}.json";
        var path = EditorUtility.SaveFilePanel(
            title: "Export Map JSON",
            directory: Application.dataPath,
            defaultName: defaultName,
            extension: "json"
        );

        if (string.IsNullOrWhiteSpace(path))
            return;

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
