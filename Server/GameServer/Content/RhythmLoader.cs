using Shared.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GameServer.Content
{
    public static class RhythmLoader
    {
        public static Dictionary<string, RhythmStageData> LoadFromDirectory(string dirPath)
        {
            var result = new Dictionary<string, RhythmStageData>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(dirPath))
            {
                return result;
            }

            // 기존 JSON 파일들과 겹치지 않게 유니티 툴이 뱉어내는 규격(*_Rhythm.json)만 스캔
            var files = Directory.GetFiles(dirPath, "*_Rhythm.json", SearchOption.AllDirectories);
            Console.WriteLine($"[RhythmLoader] Loading {files.Length} rhythm files from {dirPath}...");

            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var rhythmData = JsonSerializer.Deserialize<RhythmStageData>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        IncludeFields = true
                    });

                    if (rhythmData != null && !string.IsNullOrWhiteSpace(rhythmData.StageId))
                    {
                        result[rhythmData.StageId] = rhythmData;
                        Console.WriteLine($"[RhythmLoader] Loaded Rhythm: {rhythmData.StageId} (Blocks: {rhythmData.Blocks.Count})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RhythmLoader] Failed to load {file}: {ex.Message}");
                }
            }

            return result;
        }
    }
}
