using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class P2PServerContentResolver
{
    private static readonly string[] PatternDirectories =
    {
        "Server/GameServer/Content/01.Game/Pattern/Json",
        "Server/GameServer/Content/Pattern/Json"
    };

    public static bool TryLoadMapJson(string mapId, out MapJson mapJson)
    {
        mapJson = null;
        if (!TryReadServerText($"Server/GameServer/Content/01.Game/Map/Json/{mapId}.json", out var json))
            return false;

        try
        {
            mapJson = JsonUtility.FromJson<MapJson>(json);
            return mapJson != null && mapJson.width > 0 && mapJson.height > 0;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[P2PServerContentResolver] Map json parse failed: {mapId} / {ex.Message}");
            mapJson = null;
            return false;
        }
    }

    public static bool TryLoadStageJson(string mapId, out string json)
        => TryReadServerText($"Server/GameServer/Content/01.Game/Stage/Json/{mapId}.json", out json);

    public static IEnumerable<string> EnumeratePatternJsonTexts()
    {
        foreach (var relativeDir in PatternDirectories)
        {
            string dir = GetServerPath(relativeDir);
            if (!Directory.Exists(dir))
                continue;

            var files = Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                string text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[P2PServerContentResolver] Pattern read failed: {file} / {ex.Message}");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(text))
                    yield return text;
            }
        }
    }

    private static bool TryReadServerText(string relativePath, out string json)
    {
        json = string.Empty;
        string fullPath = GetServerPath(relativePath);
        if (!File.Exists(fullPath))
            return false;

        try
        {
            json = File.ReadAllText(fullPath);
            return !string.IsNullOrWhiteSpace(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[P2PServerContentResolver] Read failed: {fullPath} / {ex.Message}");
            json = string.Empty;
            return false;
        }
    }

    private static string GetServerPath(string relativePath)
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        string normalizedRelative = relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(repoRoot, normalizedRelative));
    }
}
