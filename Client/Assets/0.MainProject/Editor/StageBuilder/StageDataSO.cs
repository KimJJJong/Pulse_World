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

        [Header("Rhythm Settings")]
        public RhythmSettingsSO Rhythm = new RhythmSettingsSO();

        [Header("Content")]
        public List<SpawnInfoSO> InitialSpawns = new List<SpawnInfoSO>();
        public List<EventInfoSO> Events = new List<EventInfoSO>();

        // [ContextMenu("Export JSON")]
        // public void Export() => StageExporter.Export(this);
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
        public int MonsterId;
        public Vector2Int Position;
        public string AI = "Default";
        public int GroupId;
    }

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
        public int ParamId;
        public Vector2Int Position;
        public string StringVal;
        public int GroupId;
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
