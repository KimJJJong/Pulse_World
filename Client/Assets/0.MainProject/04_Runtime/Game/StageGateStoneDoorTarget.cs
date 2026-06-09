using System;
using System.Collections;
using GameServer.InGame.Director.Data;
using UnityEngine;

namespace RhythmRPG.Game.Stage
{
    [DisallowMultipleComponent]
    public sealed class StageGateStoneDoorTarget : MonoBehaviour
    {
        public string TargetKey = string.Empty;
        public int GroupId;
        public Transform DoorTransform;
        public Transform PivotTransform;
        public Vector3 WorldAxis = Vector3.up;
        public float OpenAngleDegrees = 95f;
        public bool DisableDoorCollidersWhenOpen = true;
        public bool StartOpen;

        private Vector3 _closedPosition;
        private Quaternion _closedRotation;
        private bool _hasClosedPose;
        private float _openAmount;
        private Collider[] _doorColliders = Array.Empty<Collider>();
        private Coroutine _openRoutine;

        private void Reset()
        {
            TargetKey = gameObject.name;
            PivotTransform = transform;
            DoorTransform = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }

        private void Awake()
        {
            CacheClosedPose();
            if (StartOpen)
                ApplyOpenAmount(1f, ResolveAngle(0));
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(TargetKey))
                TargetKey = gameObject.name;

            if (PivotTransform == null)
                PivotTransform = transform;

            if (DoorTransform == null && transform.childCount > 0)
                DoorTransform = transform.GetChild(0);

            if (WorldAxis.sqrMagnitude <= 0.0001f)
                WorldAxis = Vector3.up;
        }

        public static int SetOpen(StageGateDoorData data)
        {
            data ??= new StageGateDoorData();

            string targetKey = data.TargetKey?.Trim() ?? string.Empty;
            int groupId = data.GroupId;
            if (string.IsNullOrWhiteSpace(targetKey) && groupId <= 0)
            {
                Debug.LogWarning("[StageGateStoneDoorTarget] Missing TargetKey and GroupId.");
                return 0;
            }

            int changed = 0;
            var targets = FindObjectsByType<StageGateStoneDoorTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var target in targets)
            {
                if (target == null || !target.Matches(targetKey, groupId))
                    continue;

                target.SetOpen(data.Open, data.DurationMs, data.AngleDegrees);
                changed++;
            }

            Debug.Log($"[StageGateStoneDoorTarget] SetOpen open={data.Open} key='{targetKey}' group={groupId} changed={changed}");
            return changed;
        }

        public void SetOpen(bool open, int durationMs, int angleOverride)
        {
            CacheClosedPose();

            float targetAmount = open ? 1f : 0f;
            float angle = ResolveAngle(angleOverride);
            if (_openRoutine != null)
            {
                StopCoroutine(_openRoutine);
                _openRoutine = null;
            }

            if (!isActiveAndEnabled || durationMs <= 0)
            {
                ApplyOpenAmount(targetAmount, angle);
                return;
            }

            _openRoutine = StartCoroutine(AnimateOpen(targetAmount, Math.Max(0.01f, durationMs / 1000f), angle));
        }

        private IEnumerator AnimateOpen(float targetAmount, float durationSeconds, float angle)
        {
            float startAmount = _openAmount;
            float elapsed = 0f;

            while (elapsed < durationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / durationSeconds);
                float eased = t * t * (3f - 2f * t);
                ApplyOpenAmount(Mathf.Lerp(startAmount, targetAmount, eased), angle);
                yield return null;
            }

            ApplyOpenAmount(targetAmount, angle);
            _openRoutine = null;
        }

        private void CacheClosedPose()
        {
            if (DoorTransform == null)
                DoorTransform = transform.childCount > 0 ? transform.GetChild(0) : transform;

            if (PivotTransform == null)
                PivotTransform = transform;

            if (_hasClosedPose)
                return;

            _closedPosition = DoorTransform.position;
            _closedRotation = DoorTransform.rotation;
            _doorColliders = DoorTransform.GetComponentsInChildren<Collider>(true);
            _hasClosedPose = true;
        }

        private void ApplyOpenAmount(float amount, float angle)
        {
            CacheClosedPose();

            Vector3 axis = WorldAxis.sqrMagnitude > 0.0001f ? WorldAxis.normalized : Vector3.up;
            Vector3 pivot = PivotTransform != null ? PivotTransform.position : transform.position;
            Quaternion delta = Quaternion.AngleAxis(angle * amount, axis);

            DoorTransform.SetPositionAndRotation(
                pivot + delta * (_closedPosition - pivot),
                delta * _closedRotation);

            _openAmount = amount;
            SetDoorCollidersEnabled(!DisableDoorCollidersWhenOpen || amount <= 0.02f);
        }

        private void SetDoorCollidersEnabled(bool enabled)
        {
            if (_doorColliders == null)
                return;

            foreach (var doorCollider in _doorColliders)
            {
                if (doorCollider != null)
                    doorCollider.enabled = enabled;
            }
        }

        private float ResolveAngle(int angleOverride)
        {
            if (angleOverride == 0)
                return OpenAngleDegrees;

            float direction = Mathf.Approximately(OpenAngleDegrees, 0f) ? 1f : Mathf.Sign(OpenAngleDegrees);
            return Mathf.Abs(angleOverride) * direction;
        }

        private bool Matches(string targetKey, int groupId)
        {
            bool hasKey = !string.IsNullOrWhiteSpace(targetKey);
            bool hasGroup = groupId > 0;

            if (hasKey)
            {
                string effectiveKey = string.IsNullOrWhiteSpace(TargetKey) ? gameObject.name : TargetKey.Trim();
                if (string.Equals(effectiveKey, targetKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(gameObject.name, targetKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return hasGroup && GroupId == groupId;
        }
    }
}
