using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RhythmRPG.Game.Stage
{
    [DisallowMultipleComponent]
    public sealed class StageSummonSpawnPointMarker : MonoBehaviour
    {
        public string PortalKey = string.Empty;
        public int SpawnGroupId;
        public int MaxAlive = 2;
        public int SpawnIntervalBeats = 8;
        public int InitialDelayBeats = 1;
        public string MonsterIdsCsv = "1027";
        public string MonsterPattern = "Enemy_Specter";
        public Color GizmoColor = new Color(0.12f, 1f, 0.58f, 0.92f);
        public float GizmoRadius = 0.45f;

        public Vector3Int GetCell()
        {
            Vector3 position = transform.position;
            return new Vector3Int(
                Mathf.RoundToInt(position.x),
                Mathf.RoundToInt(position.y),
                Mathf.RoundToInt(position.z));
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(PortalKey))
                PortalKey = gameObject.name;

            if (MaxAlive < 1)
                MaxAlive = 1;

            if (SpawnIntervalBeats < 1)
                SpawnIntervalBeats = 1;

            if (InitialDelayBeats < 0)
                InitialDelayBeats = 0;

            if (GizmoRadius < 0.05f)
                GizmoRadius = 0.05f;
        }

        private void OnDrawGizmos()
        {
            Vector3Int cell = GetCell();
            Vector3 center = new Vector3(cell.x, transform.position.y + 0.12f, cell.z);
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(center, GizmoRadius);
            Gizmos.DrawLine(center + Vector3.left * GizmoRadius, center + Vector3.right * GizmoRadius);
            Gizmos.DrawLine(center + Vector3.forward * GizmoRadius, center + Vector3.back * GizmoRadius);

#if UNITY_EDITOR
            Handles.color = GizmoColor;
            Handles.Label(center + Vector3.up * 0.55f, $"{PortalKey}\nGroup {SpawnGroupId} / Alive {MaxAlive} / {SpawnIntervalBeats} Beat");
#endif
        }
    }
}
