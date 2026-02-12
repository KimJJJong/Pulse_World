using System.Collections.Generic;
using UnityEngine;

namespace RhythmRPG.Editor.StageBuilder
{
    [CreateAssetMenu(fileName = "NewStageData", menuName = "RhythmRPG/Stage Data", order = 1)]
    public class StageDataSO : ScriptableObject
    {
        [Header("Basic Info")]
        public string MapId;
        [TextArea] public string Description;
        public GameObject MapPrefab; // [NEW] For Scene View Editing

        [Header("Rhythm Settings")]
        public RhythmSettingsSO Rhythm = new RhythmSettingsSO();

        [Header("Init (Entity Registry)")]
        public List<StageRegisteredEntity> Registry = new List<StageRegisteredEntity>();

        [Header("Content")]
        public List<SpawnInfoSO> InitialSpawns = new List<SpawnInfoSO>();
        public List<SpawnInfoSO> InitialObjects = new List<SpawnInfoSO>(); // [Refactor] Unified Type
        public List<EventInfoSO> Events = new List<EventInfoSO>();

        // [ContextMenu("Export JSON")]
        // public void Export() => StageExporter.Export(this);
    }

    [System.Serializable]
    public class StageRegisteredEntity
    {
        public string Key; // e.g. "Slime_A"
        public EntityDefinitionSO EntityDef;
        public int DefaultGroupId;
        
        // [Refactor] Decoupling: Explicit Pattern Reference (Registry Level)
        public MonsterPatternSO PatternRef;
    }

    [System.Serializable]
    public class RhythmSettingsSO
    {
        public string SongKey = "DefaultSong";
        [Range(60, 240)] public int Bpm = 120;
        public int BaseBeatDivision = 1;
        public int ActionWindowMs = 100;
        public int StartDelayMs = 2000;
    }

    [System.Serializable]
    public class SpawnInfoSO
    {
        public string EntityKey; // [Refactor] Reference Registry Key
        public Vector3 Position;
        
        // Optional Overrides (-1 or Empty means use Default)
        public int OverrideGroupId = -1; 
    }

    // [System.Serializable]
    // public class SpawnObjectInfoSO { ... } // Merged into SpawnInfoSO

    [System.Serializable]
    public class EventInfoSO
    {
        public int EventId;
        public bool IsOneShot = true;
        public List<ConditionInfoSO> Conditions = new List<ConditionInfoSO>();
        public List<ActionInfoSO> Actions = new List<ActionInfoSO>();
    }

    [System.Serializable]
    public class ConditionInfoSO
    {
        public ConditionType Type;
        public int TargetId;
        public int Count;
        public RectInt Area;
    }

    [System.Serializable]
    public class ActionInfoSO
    {
        public ActionType Type;
        public string HeaderParam; // EntityKey, MapId, etc.
        public int ParamId; // (Deprecated or for specific IDs?)
        public Vector3 Position;
        public string StringVal; // (Deprecated or Extra)
        public int GroupId; // Override?
    }

    public enum ConditionType
    {
        MonsterAllDead,
        AreaEnter,
        TimeElapsed
    }

    public enum ActionType
    {
        SpawnMonster,
        Broadcast,
        OpenGate,
        ReturnToTown
    }
}
