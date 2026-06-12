using System.IO;
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using Shared.Data;

namespace RhythmRPG.Editor.StageBuilder
{
    public static class StageExporter
    {
        private const string ClientStageJsonFolder = "Resources/Data/Stage";
        private const string ServerStageJsonRelativePath = "../Server/GameServer/Content/01.Game/Stage/Json";
        private const string ServerMapJsonRelativePath = "../Server/GameServer/Content/01.Game/Map/Json";
        private const string ServerSoundJsonRelativePath = "../Server/GameServer/Content/01.Game/Sound/Json";
        internal const string DefaultSongKey = "DefaultSong";

        public static void Export(StageDataSO stage)
        {
            if (stage == null || string.IsNullOrEmpty(stage.MapId))
            {
                Debug.LogError("Stage Data is null or MapId is empty.");
                return;
            }

            var rhythmSettings = stage.Rhythm ?? new RhythmSettingsSO();
            string rhythmKey = ResolveRhythmKey(stage.MapId, rhythmSettings.SongKey);
            int exportedBpm = rhythmSettings.Bpm;
            if (TryLoadRhythmAudioData(rhythmKey, out var rhythmAudio))
            {
                exportedBpm = rhythmAudio.Bpm;
            }
            else
            {
                Debug.LogWarning(
                    $"<b>[StageExporter]</b> Rhythm Audio Data not found for '{rhythmKey}'. " +
                    $"Using legacy StageData BPM fallback ({exportedBpm}).");
            }

            var dto = new StageScenarioDTO();
            dto.MapId = stage.MapId;
            dto.Description = stage.Description;
            dto.DisableAutoClearOnMonsterWipe = stage.DisableAutoClearOnMonsterWipe;
            dto.RhythmSettings = new RhythmSettingsDTO 
            {
                SongKey = rhythmKey,
                Bpm = exportedBpm,
                BaseBeatDivision = Mathf.Max(1, rhythmSettings.BaseBeatDivision),
                ActionWindowMs = rhythmSettings.ActionWindowMs,
                StartDelayMs = rhythmSettings.StartDelayMs
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
                        Pattern = ai_pattern,
                        GroupId = groupId,
                        Rotation = s.EulerAngles.y
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
                        SizeX = Mathf.Max(1, s.ObjectSize.x),
                        SizeY = Mathf.Max(1, s.ObjectSize.y),
                        Pattern = ai_pattern,
                        Rotation = s.EulerAngles.y
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
                        UseParticipantCount = c.CountRequirement == StageCountRequirementMode.ParticipantCount,
                        ShowProgressUi = c.ShowProgressUi,
                        ShowAreaOutline = c.ShowAreaOutline,
                        ProgressLabel = c.ProgressLabel,
                        ProgressDurationMs = c.ProgressDurationMs,
                        Area = BuildAreaDTO(c)
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
                        SizeX = Mathf.Max(1, a.ObjectSize.x),
                        SizeY = Mathf.Max(1, a.ObjectSize.y),
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

        private static RectDTO BuildAreaDTO(ConditionInfoSO condition)
        {
            var area = new RectDTO
            {
                X = condition.Area.x,
                Y = condition.Area.y,
                W = condition.Area.width,
                H = condition.Area.height,
                Shape = condition.AreaShape.ToString()
            };

            if (condition.AreaShape == StageAreaShapeType.CustomCells && condition.AreaCells != null)
            {
                var seen = new HashSet<Vector2Int>();
                foreach (var cell in condition.AreaCells)
                {
                    if (!seen.Add(cell))
                        continue;

                    area.Cells.Add(new GridPointDTO { X = cell.x, Y = cell.y });
                }
            }

            return area;
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

        internal static string ResolveRhythmKey(string mapId, string songKey)
        {
            if (!string.IsNullOrWhiteSpace(songKey) &&
                !string.Equals(songKey, DefaultSongKey, StringComparison.OrdinalIgnoreCase))
            {
                return songKey.Trim();
            }

            return string.IsNullOrWhiteSpace(mapId) ? DefaultSongKey : mapId.Trim();
        }

        internal static string GetServerSoundJsonDirectory()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, ServerSoundJsonRelativePath));
        }

        internal static bool TryLoadRhythmAudioData(string rhythmKey, out RhythmAudioSummary summary)
        {
            summary = null;
            if (string.IsNullOrWhiteSpace(rhythmKey))
                return false;

            string dir = GetServerSoundJsonDirectory();
            if (!Directory.Exists(dir))
                return false;

            string trimmedKey = rhythmKey.Trim();
            string rhythmPath = Path.Combine(dir, $"{trimmedKey}_Rhythm.json");
            if (TryLoadRhythmAudioDataFromPath(rhythmPath, trimmedKey, out summary))
                return true;

            string plainPath = Path.Combine(dir, $"{trimmedKey}.json");
            if (TryLoadRhythmAudioDataFromPath(plainPath, trimmedKey, out summary))
                return true;

            foreach (string path in Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
            {
                if (TryLoadRhythmAudioDataFromPath(path, trimmedKey, out summary))
                    return true;
            }

            return false;
        }

        private static bool TryLoadRhythmAudioDataFromPath(string path, string rhythmKey, out RhythmAudioSummary summary)
        {
            summary = null;
            if (!File.Exists(path))
                return false;

            try
            {
                var data = JsonUtility.FromJson<RhythmStageData>(File.ReadAllText(path));
                if (data == null || string.IsNullOrWhiteSpace(data.StageId))
                    return false;

                if (!string.Equals(data.StageId, rhythmKey, StringComparison.OrdinalIgnoreCase))
                    return false;

                summary = new RhythmAudioSummary
                {
                    Path = path,
                    StageId = data.StageId,
                    Bpm = data.Bpm,
                    TimeSignatureNum = data.TimeSignatureNum,
                    TimeSignatureDenom = data.TimeSignatureDenom,
                    TicksPerBeat = data.TicksPerBeat,
                    BlockCount = data.Blocks?.Count ?? 0
                };
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<b>[StageExporter]</b> Failed to read Rhythm Audio Data '{path}': {ex.Message}");
                return false;
            }
        }

        internal sealed class RhythmAudioSummary
        {
            public string Path;
            public string StageId;
            public int Bpm;
            public int TimeSignatureNum;
            public int TimeSignatureDenom;
            public int TicksPerBeat;
            public int BlockCount;
        }
        
        // --- DTO Definition (Mirrors Server) ---
        
        [System.Serializable]
        public class StageScenarioDTO
        {
            public string MapId;
            public string Description;
            public bool DisableAutoClearOnMonsterWipe;
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
            public string Pattern;
            public int GroupId;
            public float Rotation;
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
            public int SizeX = 1;
            public int SizeY = 1;
            public string Pattern;
            public float Rotation;
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
            public bool UseParticipantCount;
            public bool ShowProgressUi = true;
            public bool ShowAreaOutline = true;
            public string ProgressLabel;
            public int ProgressDurationMs;
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
            public int SizeX = 1;
            public int SizeY = 1;
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
            public string Shape;
            public List<GridPointDTO> Cells = new List<GridPointDTO>();
        }

        [System.Serializable]
        public class GridPointDTO
        {
            public int X;
            public int Y;
        }
    }
}
