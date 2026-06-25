using UnityEngine;

namespace RhythmRPG.Game.Stage
{
    public enum StageSummonPortalKind
    {
        NormalRing,
        EliteGate
    }

    [DisallowMultipleComponent]
    public sealed class StageSummonPortalMarker : MonoBehaviour
    {
        public StageSummonPortalKind Kind = StageSummonPortalKind.NormalRing;
        public string PortalKey = string.Empty;
        public int SceneGroupId;
        public int SpawnGroupId;
        public StageSummonSpawnPointMarker SpawnPoint;
        public Vector3Int SpawnCell;
        public int MaxAlive = 2;
        public int SpawnIntervalBeats = 8;
        public int InitialDelayBeats = 1;
        public string MonsterIdsCsv = "1027";
        public string MonsterPattern = "Enemy_Specter";
        public bool DrawEditorLink = true;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(PortalKey))
                PortalKey = gameObject.name;

            if (SpawnPoint != null)
                SpawnCell = SpawnPoint.GetCell();

            if (MaxAlive < 1)
                MaxAlive = 1;

            if (SpawnIntervalBeats < 1)
                SpawnIntervalBeats = 1;

            if (InitialDelayBeats < 0)
                InitialDelayBeats = 0;
        }

        private void OnDrawGizmos()
        {
            if (!DrawEditorLink)
                return;

            Vector3 spawnPosition = SpawnPoint != null
                ? SpawnPoint.transform.position
                : new Vector3(SpawnCell.x, SpawnCell.y, SpawnCell.z);

            Gizmos.color = Kind == StageSummonPortalKind.EliteGate
                ? new Color(0.25f, 1f, 0.72f, 0.65f)
                : new Color(0.12f, 1f, 0.58f, 0.55f);
            Gizmos.DrawLine(transform.position, spawnPosition + Vector3.up * 0.12f);
        }
    }
}
