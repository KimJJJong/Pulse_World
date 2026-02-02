using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace RhythmRPG.Editor.StageBuilder
{
    public static class StageExporter
    {
        public static void Export(StageDataSO stage)
        {
            if (stage == null || string.IsNullOrEmpty(stage.MapId))
            {
                Debug.LogError("Stage Data is null or MapId is empty.");
                return;
            }

            // Convert SO to DTO
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

            // Convert SO to DTO
            //var dto = new StageScenarioDTO();
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
                string ai_pattern = (!string.IsNullOrEmpty(s.OverrideAI_Pattern)) ? s.OverrideAI_Pattern : reg.DefaultAI_Pattern;

                if (reg.EntityDef.Type == EntityType.Monster)
                {
                    dto.InitialSpawns.Add(new SpawnDataDTO { 
                        MonsterId = reg.EntityDef.EntityId,
                        X = s.Position.x,
                        Y = s.Position.y,
                        AI = ai_pattern,
                        GroupId = groupId
                    });
                }
                else // Object, etc.
                {
                    dto.InitialObjects.Add(new SpawnObjectDTO {
                        EntityId = reg.EntityDef.EntityId,
                        EntityType = (int)reg.EntityDef.Type,
                        X = s.Position.x,
                        Y = s.Position.y,
                        GroupId = groupId,
                        Pattern = ai_pattern
                    });
                }
            }

            // 3. Process Events
            foreach(var e in stage.Events)
            {
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
                        Count = c.Count,
                        Area = new RectDTO { X=c.Area.x, Y=c.Area.y, W=c.Area.width, H=c.Area.height }
                    });
                }

                foreach(var a in e.Actions)
                {
                    int paramId = a.ParamId;
                    
                    // Resolve EntityKey for Spawn/Action if HeaderParam is used
                    if (!string.IsNullOrEmpty(a.HeaderParam) && registryMap.TryGetValue(a.HeaderParam, out var reg))
                    {
                        // Insert ID from Registry
                         paramId = reg.EntityDef.EntityId;
                    }

                    evtDto.Actions.Add(new ActionDataDTO {
                        Type = a.Type.ToString(),
                        ParamId = paramId,
                        X = a.Position.x,
                        Y = a.Position.y,
                        StringVal = a.StringVal, // Keep if still used, or could use HeaderParam?
                        GroupId = a.GroupId
                    });
                }

                dto.Events.Add(evtDto);
            }

            string finalJson = JsonUtility.ToJson(dto, true);

            // Save
            string path = "Assets/Resources/Data/Stage";
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            string fullPath = $"{path}/{stage.MapId}.json";
            File.WriteAllText(fullPath, finalJson);

            AssetDatabase.Refresh();
            Debug.Log($"<b>[StageExporter]</b> Exported to {fullPath}");
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
            public string StringVal;
            public int GroupId;
        }

        [System.Serializable]
        public class RectDTO
        {
            public int X, Y, W, H;
        }
    }
}
