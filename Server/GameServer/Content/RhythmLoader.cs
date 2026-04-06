using Shared.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            // *_Rhythm.json 우선 스캔, 일반 .json은 Stage Json과 겹치지 않는 Sound 폴더 하위에서만 추가 스캔
            var rhythmFiles = Directory.GetFiles(dirPath, "*_Rhythm.json", SearchOption.AllDirectories);
            var allJsonFiles = Directory.GetFiles(dirPath, "*.json", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith("_Rhythm.json", StringComparison.OrdinalIgnoreCase));
            var files = rhythmFiles.Concat(allJsonFiles).ToArray();
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
