using System;
using GameServer.InGame.Director.Data;
using UnityEngine;

namespace RhythmRPG.Game.Stage
{
    [DisallowMultipleComponent]
    public sealed class StageSceneObjectTarget : MonoBehaviour
    {
        public string TargetKey = string.Empty;
        public int GroupId;

        public static int SetActive(StageSceneObjectData data)
        {
            data ??= new StageSceneObjectData();

            string targetKey = data.TargetKey?.Trim() ?? string.Empty;
            int groupId = data.GroupId;
            if (string.IsNullOrWhiteSpace(targetKey) && groupId <= 0)
            {
                Debug.LogWarning("[StageSceneObjectTarget] Missing TargetKey and GroupId.");
                return 0;
            }

            int changed = 0;
            var targets = FindObjectsByType<StageSceneObjectTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var target in targets)
            {
                if (target == null || !target.Matches(targetKey, groupId))
                    continue;

                if (target.gameObject.activeSelf != data.Visible)
                    changed++;

                target.gameObject.SetActive(data.Visible);
            }

            Debug.Log($"[StageSceneObjectTarget] SetActive visible={data.Visible} key='{targetKey}' group={groupId} changed={changed}");
            return changed;
        }

        private bool Matches(string targetKey, int groupId)
        {
            bool hasKey = !string.IsNullOrWhiteSpace(targetKey);
            bool hasGroup = groupId > 0;

            if (hasKey && string.Equals(TargetKey?.Trim(), targetKey, StringComparison.OrdinalIgnoreCase))
                return true;

            return hasGroup && GroupId == groupId;
        }
    }
}
