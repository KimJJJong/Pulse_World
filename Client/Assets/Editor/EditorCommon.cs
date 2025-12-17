#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;

public static class EditorCommon
{
    public static void ExportJson<T>(T obj, string defaultName)
    {
        var path = EditorUtility.SaveFilePanel(
            "Export JSON",
            Application.dataPath,
            defaultName,
            "json");

        if (string.IsNullOrEmpty(path))
            return;

        var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
        File.WriteAllText(path, json);
        AssetDatabase.Refresh();
        Debug.Log($"Exported JSON: {path}");
    }
}
#endif
