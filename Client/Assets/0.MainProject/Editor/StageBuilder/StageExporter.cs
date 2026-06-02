using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace RhythmRPG.Editor.StageBuilder
{
    public static class StageExporter
    {
        private const string ClientStageJsonFolder = "Resources/Data/Stage";
        private const string ServerStageJsonRelativePath = "../Server/GameServer/Content/01.Game/Stage/Json";
        private const string ServerMapJsonRelativePath = "../Server/GameServer/Content/01.Game/Map/Json";

        public static void Export(StageDataSO stage)
        {
            if (stage == null || string.IsNullOrEmpty(stage.MapId))
            {
                Debug.LogError("Stage Data is null or MapId is empty.");
                return;
            }

            var dto = new StageScenarioDTO();
            dto.MapId = stage.MapId;
            dto.Description = stage.Description;
            dto.RhythmSettings = new RhythmSettingsDTO 
            {
                SongKey = stage.Rhythm.SongKey,
                Bpm = stage.Rhythm.Bpm,
                BaseBeatDivision = stage.Rhythm.BaseBeatDivision,
                ActionWindowMs = stage.Rhythm.ActionWindowMs,
                StartDelayMs = stage.Rhythm.StartDelayMs
            };
            
            // 1. Build Registry Map
            var registryMap = new Dictionary<string, StageRegisteredEntity>();
            foreach(var reg in stage.Registry)
            {
                if (!string.IsNullOrEmpty(reg.Key) && !registryMap.ContainsKey(reg.Key))
                {
                    registryMap.Add(reg.Key, reg);
                }
            }
            
            // 2. Process Initial Spawns (Unified List -> Split by Type)
            // Combine InitialSpawns and InitialObjects (both are SpawnInfoSO now)
            var allSpawns = new List<SpawnInfoSO>();
            allSpawns.AddRange(stage.InitialSpawns);
            allSpawns.AddRange(stage.InitialObjects);

            foreach(var s in allSpawns)
            {
                if (string.IsNullOrEmpty(s.EntityKey) || !registryMap.TryGetValue(s.EntityKey, out var reg))
                {
                    Debug.LogWarning($"[StageExporter] EntityKey '{s.EntityKey}' not found in Registry!");
                    continue;
                }

                if (reg.EntityDef == null) continue;

                int groupId = (s.OverrideGroupId != -1) ? s.OverrideGroupId : reg.DefaultGroupId;
                
                string ai_pattern = "";
                // [Refactor] Use Registry Pattern Reference
                if (reg.PatternRef != null)
                {
                    if (reg.PatternRef.Data != null && !string.IsNullOrEmpty(reg.PatternRef.Data.MonsterType))
                    {
                        ai_pattern = reg.PatternRef.Data.MonsterType;
                    }
                    else
                    {
                        // Fallback to Asset Name if internal ID is missing
                        ai_pattern = reg.PatternRef.name;
                        Debug.LogWarning($"[StageExporter] PatternRef '{reg.PatternRef.name}' has empty MonsterType. Using Asset Name as ID.");
                    }
                }

                if (reg.EntityDef.Type == EntityType.Monster)
                {
                    dto.InitialSpawns.Add(new SpawnDataDTO { 
                        MonsterId = reg.EntityDef.EntityId,
                        X = (int)s.Position.x,
                        Y = (int)s.Position.y,
                        Z = (int)s.Position.z, // [NEW]
                        AI = ai_pattern,
                        GroupId = groupId
                    });
                }
                else // Object, etc.
                {
                    dto.InitialObjects.Add(new SpawnObjectDTO {
                        EntityId = reg.EntityDef.EntityId,
                        EntityType = (int)reg.EntityDef.Type,
                        X = (int)s.Position.x,
                        Y = (int)s.Position.y,
                        Z = (int)s.Position.z, // [NEW]
                        GroupId = groupId,
                        Pattern = ai_pattern
                    });
                }
            }

            // 3. Process Events
            foreach(var e in stage.Events)
            {
                if (e == null || !e.Enabled)
                {
                    continue;
                }

                var evtDto = new EventDataDTO 
                {
                    EventId = e.EventId,
                    IsOneShot = e.IsOneShot
                };
                
                foreach(var c in e.Conditions)
                {
                    evtDto.Conditions.Add(new ConditionDataDTO {
                        Type = c.Type.ToString(),
                        TargetId = c.TargetId,
                        SecondaryTargetId = c.SecondaryTargetId,
                        TargetKey = c.TargetKey,
                        Count = c.Count,
                        Area = new RectDTO { X=c.Area.x, Y=c.Area.y, W=c.Area.width, H=c.Area.height }
                    });
                }

                foreach(var a in e.Actions)
                {
                    int paramId = a.ParamId;
                    string ai_pattern = a.StringVal; // Default to existing value
                    
                    // Resolve EntityKey for Spawn/Action if HeaderParam is used
                    if (!string.IsNullOrEmpty(a.HeaderParam) && registryMap.TryGetValue(a.HeaderParam, out var reg))
                    {
                        // Insert ID from Registry
                         paramId = reg.EntityDef.EntityId;

                         // [Refactor] Extract Pattern from Registry for SpawnMonster Action
                         if (a.Type == ActionType.SpawnMonster || a.Type == ActionType.SpawnObject)
                         {
                             if (reg.PatternRef != null)
                             {
                                if (reg.PatternRef.Data != null && !string.IsNullOrEmpty(reg.PatternRef.Data.MonsterType))
                                {
                                    ai_pattern = reg.PatternRef.Data.MonsterType;
                                }
                                else
                                {
                                    ai_pattern = reg.PatternRef.name;
                                    Debug.LogWarning($"[StageExporter] Action PatternRef '{reg.PatternRef.name}' has empty MonsterType. Using Asset Name.");
                                }
                             }
                         }
                    }

                    evtDto.Actions.Add(new ActionDataDTO {
                        Type = a.Type.ToString(),
                        ParamId = paramId,
                        X = (int)a.Position.x,
                        Y = (int)a.Position.y,
                        Z = (int)a.Position.z, // [NEW]
                        StringVal = ai_pattern, // Use resolved pattern
                        GroupId = a.GroupId,
                        GuideTitle = a.GuideTitle,
                        GuideBody = a.GuideBody,
                        GuideImageResource = a.GuideImageResource,
                        DurationMs = a.DurationMs,
                        VfxKey = a.VfxKey
                    });
                }

                dto.Events.Add(evtDto);
            }

            string finalJson = JsonUtility.ToJson(dto, true);

            string clientRuntimePath = Path.Combine(Application.dataPath, ClientStageJsonFolder, $"{stage.MapId}.json");
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string serverPath = Path.GetFullPath(
                Path.Combine(projectRoot, ServerStageJsonRelativePath, $"{stage.MapId}.json"));

            ExportToFile(clientRuntimePath, finalJson);
            ExportToFile(serverPath, finalJson);
            WarnIfMapJsonMissing(projectRoot, stage.MapId);

            AssetDatabase.Refresh();
            Debug.Log($"<b>[StageExporter]</b> Exported to runtime and server paths for '{stage.MapId}'.");
        }

        private static void ExportToFile(string path, string content)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, content);
            Debug.Log($"<b>[StageExporter]</b> Exported to {path}");
        }

        private static void WarnIfMapJsonMissing(string projectRoot, string mapId)
        {
            string serverMapPath = Path.GetFullPath(
                Path.Combine(projectRoot, ServerMapJsonRelativePath, $"{mapId}.json"));

            if (File.Exists(serverMapPath))
                return;

            Debug.LogWarning(
                $"<b>[StageExporter]</b> Map JSON is missing for '{mapId}': {serverMapPath}. " +
                "Export a MapAsset whose asset name matches this MapId.");
        }
        
        // --- DTO Definition (Mirrors Server) ---
        
        [System.Serializable]
        public class StageScenarioDTO
        {
            public string MapId;
            public string Description;
            public RhythmSettingsDTO RhythmSettings;
            public List<SpawnDataDTO> InitialSpawns = new List<SpawnDataDTO>();
            public List<SpawnObjectDTO> InitialObjects = new List<SpawnObjectDTO>(); // [NEW]
            public List<EventDataDTO> Events = new List<EventDataDTO>();
        }

        [System.Serializable]
        public class RhythmSettingsDTO
        {
            public string SongKey;
            public int Bpm;
            public int BaseBeatDivision;
            public int ActionWindowMs;
            public int StartDelayMs;
        }

        [System.Serializable]
        public class SpawnDataDTO
        {
            public int MonsterId;
            public int X;
            public int Y;
            public int Z; // [NEW] Unity Z
            public string AI;
            public int GroupId;
        }

        [System.Serializable]
        public class SpawnObjectDTO
        {
            public int EntityId;
            public int EntityType;
            public int X;
            public int Y;
            public int Z; // [NEW] Unity Z
            public int GroupId;
            public string Pattern;
        }

        [System.Serializable]
        public class EventDataDTO
        {
            public int EventId;
            public bool IsOneShot;
            public List<ConditionDataDTO> Conditions = new List<ConditionDataDTO>();
            public List<ActionDataDTO> Actions = new List<ActionDataDTO>();
        }

        [System.Serializable]
        public class ConditionDataDTO
        {
            public string Type;
            public int TargetId;
            public int SecondaryTargetId;
            public string TargetKey;
            public int Count;
            public RectDTO Area;
        }

        [System.Serializable]
        public class ActionDataDTO
        {
            public string Type;
            public int ParamId;
            public int X;
            public int Y;
            public int Z; // [NEW] Unity Z
            public string StringVal;
            public int GroupId;
            public string GuideTitle;
            public string GuideBody;
            public string GuideImageResource;
            public int DurationMs;
            public string VfxKey;
        }

        [System.Serializable]
        public class RectDTO
        {
            public int X, Y, W, H;
        }
    }
}
