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
        [HideInInspector]
        [Range(60, 240)] public int Bpm = 120;
        [HideInInspector]
        public int BaseBeatDivision = 1;
        public int ActionWindowMs = 100;
        public int StartDelayMs = 2000;
    }

    [System.Serializable]
    public class SpawnInfoSO
    {
        public string EntityKey; // [Refactor] Reference Registry Key
        public string Label;
        public Vector3 Position;
        public Vector3 EulerAngles;
        public Vector3 Scale = Vector3.one;
        public GameObject PreviewPrefabOverride;
        public bool PlaceInScene = true;
        
        // Optional Overrides (-1 or Empty means use Default)
        public int OverrideGroupId = -1; 
    }

    // [System.Serializable]
    // public class SpawnObjectInfoSO { ... } // Merged into SpawnInfoSO

    [System.Serializable]
    public class EventInfoSO
    {
        public string Title = "New Event";
        [TextArea(2, 4)] public string Notes;
        public bool Enabled = true;
        public int EventId;
        public bool IsOneShot = true;
        public StageEventVisualSO Visual = new StageEventVisualSO();
        public List<ConditionInfoSO> Conditions = new List<ConditionInfoSO>();
        public List<ActionInfoSO> Actions = new List<ActionInfoSO>();
    }

    [System.Serializable]
    public class StageEventVisualSO
    {
        public Color SceneColor = new Color(0.67f, 0.44f, 0.93f, 1f);
        public string VfxKey;
        public bool DrawSceneLinks = true;
    }

    [System.Serializable]
    public class ConditionInfoSO
    {
        public ConditionType Type;
        public int TargetId;
        public int SecondaryTargetId;
        public string TargetKey;
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
        public string GuideTitle;
        [TextArea(2, 5)] public string GuideBody;
        public string GuideImageResource;
        public int DurationMs = 3500;
        public string VfxKey;
    }

    public enum ConditionType
    {
        MonsterAllDead,
        AreaEnter,
        TimeElapsed,
        ObjectInteracted,
        ObjectPairInteracted,
        ObjectStateEquals,
        AreaExit
    }

    public enum ActionType
    {
        SpawnMonster,
        Broadcast,
        OpenGate,
        ReturnToTown,
        SpawnObject,
        ShowGuide,
        SetObjectState,
        PlayVfx,
        FinGame,
        ShowTutorialPanel,
        HideTutorialPanel
    }
}
