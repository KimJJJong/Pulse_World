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
            
            foreach(var s in stage.InitialSpawns)
            {
                dto.InitialSpawns.Add(new SpawnDataDTO { 
                    MonsterId = s.MonsterId,
                    X = s.Position.x,
                    Y = s.Position.y,
                    AI = s.AI,
                    GroupId = s.GroupId
                });
            }

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
                    evtDto.Actions.Add(new ActionDataDTO {
                        Type = a.Type.ToString(),
                        ParamId = a.ParamId,
                        X = a.Position.x,
                        Y = a.Position.y,
                        StringVal = a.StringVal,
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
