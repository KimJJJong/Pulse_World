using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Client.Data; // For NewSkillSO, MonsterPatternSO
using RhythmRPG.Editor.StageBuilder; // For StageExporter

public static class BatchDataExporter
{
    [MenuItem("RhythmRPG/Export All Data (Skills, Patterns, Stages)")]
    public static void ExportAll()
    {
        Debug.Log("<b>[BatchExporter]</b> Starting Batch Export...");

        //ExportSkills();
        //ExportPatterns();
        //ExportStages();

        AssetDatabase.Refresh();
        Debug.Log("<b>[BatchExporter]</b> Batch Export Complete!");
    }

    public static Newtonsoft.Json.JsonSerializerSettings GetJsonSettings()
    {
        return new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
            ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None, // Start clean, no $type
            ContractResolver = new IgnoreEditorFieldsResolver()
        };
    }
    
    // ... (rest of the file) ...

    // Helper from PatternEditorWindow
    public class IgnoreEditorFieldsResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
    {
        protected override Newtonsoft.Json.Serialization.JsonProperty CreateProperty(System.Reflection.MemberInfo member, Newtonsoft.Json.MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            if (member.Name == "SkillRef")
            {
                property.Ignored = true;
            }
            if (member.Name == "Type" || member.Name == "ShapeType")
            {
                property.Order = -2;
            }
            return property;
        }
    }
    
    // Formatting helper
    private static class Formatting 
    {
        public const Newtonsoft.Json.Formatting Indented = Newtonsoft.Json.Formatting.Indented;
    }
}
