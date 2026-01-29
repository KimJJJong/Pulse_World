using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json; // or Newtonsoft.Json. Using System.Text.Json for standard.

namespace GameServer.InGame.Director.Data
{
    public static class StageDataManager
    {
        private static Dictionary<string, StageScenario> _stages = new Dictionary<string, StageScenario>(StringComparer.OrdinalIgnoreCase);

        public static void Register(StageScenario stage)
        {
            if (stage == null || string.IsNullOrWhiteSpace(stage.MapId)) return;
            _stages[stage.MapId] = stage;
        }

        public static StageScenario Get(string mapId)
        {
            if (_stages.TryGetValue(mapId, out var stage))
                return stage;
            return null;
        }

        public static void Clear() => _stages.Clear();
    }

    public static class StageLoader
    {
        public static Dictionary<string, StageScenario> LoadFromDirectory(string dirPath)
        {
            var result = new Dictionary<string, StageScenario>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(dirPath))
            {
                Console.WriteLine($"[StageLoader] Dictory not found: {dirPath}");
                return result;
            }

            var files = Directory.GetFiles(dirPath, "*.json", SearchOption.AllDirectories);
            Console.WriteLine($"[StageLoader] Loading {files.Length} files from {dirPath}...");

            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    // Use System.Text.Json or Newtonsoft.Json. 
                    // Since Unity Exporter uses JsonUtility, the format is simple public fields.
                    // Assuming we have proper deserializer or Newtonsoft.
                    // For now, let's use Newtonsoft if available or System.Text.Json if simple.
                    // Given the project references, let's look for what's common.
                    // Assuming Newtonsoft.Json is typical. If not, System.Text.Json.
                    
                    var scenario = JsonSerializer.Deserialize<StageScenario>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        IncludeFields = true 
                        // AllowTrailingCommas = true
                    });

                    if (scenario != null && !string.IsNullOrWhiteSpace(scenario.MapId))
                    {
                        // Validation logic could go here
                        result[scenario.MapId] = scenario;
                        Console.WriteLine($"[StageLoader] Loaded {scenario.MapId} (Events: {scenario.Events.Count})");
                    }
                    else
                    {
                        Console.WriteLine($"[StageLoader] Skipped {file}: deserialized object is null or MapId is empty.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StageLoader] Failed to load {file}: {ex.Message}");
                }
            }

            return result;
        }
    }
}
