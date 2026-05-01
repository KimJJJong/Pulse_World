using System;
using System.Collections.Generic;

public sealed partial class P2PContentDirector
{
    private struct StageMonsterTemplate
    {
        public int AppearanceId;
        public string MonsterType;
        public int GroupId;
        public int MaxHp;
    }

    [Serializable]
    private sealed class EntityDataRoot
    {
        public List<EntityDataRecord> Entities = new();
    }

    [Serializable]
    private sealed class EntityDataRecord
    {
        public int EntityId;
        public string Name;
        public int EntityType;
        public int MaxHp;
        public string ResourcePath;
    }

    private sealed class MonsterRuntimeState
    {
        public int EntityId;
        public int AppearanceId;
        public string MonsterType = "";
        public int GroupId;
        public int MaxHp = 1;
        public long SpawnBeat;
        public string PhaseId = "P1";
        public long LockedUntilBeat = -1;
        public bool IsAlive = true;
        public int LastKnownHp;
        public float Rotation;
        public readonly Dictionary<string, long> Cooldowns = new(StringComparer.OrdinalIgnoreCase);
    }
}
