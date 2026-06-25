using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GameShared.Data; // Ensure this namespace matches NewSkillDto.cs location

public static class NewSkillLoader
{
    private static readonly JsonSerializerOptions Opt = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        IncludeFields = true // [FIX] NewSkillDef uses fields
    };

    public sealed class LoadReport
    {
        public int FilesScanned;
        public int SkillsLoaded;
        public int Warnings;
        public int Errors;
        public List<string> ErrorLines = new();
        public List<string> LoadedSkillIds = new();
    }

    public static List<NewSkillDef> LoadFromDirectory(string skillsDir, out LoadReport report)
    {
        report = new LoadReport();
        var result = new List<NewSkillDef>();

        if (!Directory.Exists(skillsDir))
        {
            Console.WriteLine($"[NewSkillLoader] Directory not found: {skillsDir}");
            return result;
        }

        var files = Directory.GetFiles(skillsDir, "*.json", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            report.FilesScanned++;
            try
            {
                var json = File.ReadAllText(file);
                
                // [Check] Legacy Format Detection
                if (IsLegacyFormat(json))
                {
                    report.Warnings++;
                    report.ErrorLines.Add($"[Skip] Legacy format detected: {Path.GetFileName(file)}");
                    continue;    
                }

                // Try single object
                var def = JsonSerializer.Deserialize<NewSkillDef>(json, Opt);
                if (def != null)
                {
                    // If SkillId is empty, it might be a serialization failure or empty file
                    if (string.IsNullOrWhiteSpace(def.SkillId))
                    {
                        report.Warnings++; // Warn but don't count as Critical Error for migration phase
                        report.ErrorLines.Add($"[Warn] SkillId is empty in {Path.GetFileName(file)} (Check IncludeFields or JSON format)");
                        continue;
                    }

                    Validate(def, file);
                    result.Add(def);
                    report.SkillsLoaded++;
                    report.LoadedSkillIds.Add(def.SkillId);
                }
            }
            catch (Exception ex)
            {
                report.Errors++;
                report.ErrorLines.Add($"[Error] {file}: {ex.Message}");
            }
        }

        return result;
    }

    private static bool IsLegacyFormat(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            // Legacy starts with { "Skills": [ ... ] }
            return doc.RootElement.ValueKind == JsonValueKind.Object 
                   && doc.RootElement.TryGetProperty("Skills", out _);
        }
        catch 
        { 
            return false; 
        }
    }



    private static void Validate(NewSkillDef def, string file)
    {
        if (string.IsNullOrEmpty(def.SkillId))
            throw new Exception($"SkillId is empty in {file}");
        
        // Add more validation if needed
    }
}
