using System;
using System.IO;
using System.Text.Json;

public static class JsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true, // 대, 소문자 구분X
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static T LoadOrThrow<T>(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"JSON file not found: {path}");

        var json = File.ReadAllText(path);
        var obj = JsonSerializer.Deserialize<T>(json, Options);
        if (obj == null)
            throw new Exception($"Failed to deserialize JSON: {path}");

        return obj;
    }
}
