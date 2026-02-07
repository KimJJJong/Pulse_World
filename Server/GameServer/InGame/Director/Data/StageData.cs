using System;
using System.Collections.Generic;

namespace GameServer.InGame.Director.Data
{
    [Serializable]
    public class StageScenario
    {
        public string MapId;
        public string Description;
        
        public RhythmSettingsData RhythmSettings = new RhythmSettingsData();
        
        public List<SpawnData> InitialSpawns = new List<SpawnData>();
        public List<SpawnObjectData> InitialObjects = new List<SpawnObjectData>(); // [NEW]
        
        public List<EventData> Events = new List<EventData>();
    }

    [Serializable]
    public class RhythmSettingsData
    {
        public string SongKey = "DefaultSong";
        public int Bpm = 120;
        public int BaseBeatDivision = 1;
        public int ActionWindowMs = 100;
        public int StartDelayMs = 2000;
    }

    [Serializable]
    public class SpawnData
    {
        public int MonsterId;
        public int X;
        public int Y;
        public int Z; // [NEW] From Unity Z
        public string AI = "Default"; // AI Type Key
        public int GroupId = 0; // For event triggering (e.g., "Group 1 All Dead")
    }

    [Serializable]
    public class SpawnObjectData
    {
        public int EntityId;
        public int EntityType; // 3=Object
        public int X;
        public int Y;
        public int Z; // [NEW] From Unity Z
        public int GroupId;
        public string Pattern; // Interaction/State Pattern
    }

    [Serializable]
    public class EventData
    {
        public int EventId;
        public bool IsOneShot = true;
        
        public List<ConditionData> Conditions = new List<ConditionData>();
        public List<ActionData> Actions = new List<ActionData>();
    }

    [Serializable]
    public class ConditionData
    {
        public string Type; // "MonsterAllDead", "AreaEnter", "TimeElapsed"
        
        // Parameters (Generic storage, can be expanded)
        public int TargetId; // MonsterId, GroupId
        public int Count;
        public RectData Area;
    }

    [Serializable]
    public class ActionData
    {
        public string Type; // "SpawnMonster", "OpenGate", "Broadcast"
        
        // Parameters
        public int ParamId; // MonsterId, GateId
        public int X;
        public int Y;
        public int Z; // [NEW] From Unity Z
        public string StringVal; // Message, AI Key
        public int GroupId;
    }

    [Serializable]
    public class RectData
    {
        public int X;
        public int Y;
        public int W;
        public int H;
    }
}
