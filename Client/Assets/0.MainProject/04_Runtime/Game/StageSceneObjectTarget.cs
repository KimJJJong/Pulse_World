using System;
using System.Collections;
using System.Collections.Generic;
using GameServer.InGame.Director.Data;
using RhythmRPG.Game.Visual.SceneEffects;
using UnityEngine;

namespace RhythmRPG.Game.Stage
{
    [DisallowMultipleComponent]
    public sealed class StageSceneObjectTarget : MonoBehaviour
    {
        public string TargetKey = string.Empty;
        public int GroupId;

        [Header("Visibility")]
        public int DefaultDurationMs = 650;
        public float HiddenScale = 0.88f;
        public float HiddenYOffset = -0.35f;
        public bool DisableCollidersWhenHidden = true;
        public Transform[] MotionRoots = Array.Empty<Transform>();

        [Header("Rise")]
        public bool EnableRiseFromGround;
        public bool ReplayShowAnimationWhenAlreadyVisible;
        public float RiseHiddenYOffset = -1.45f;
        public float RiseOvershootHeight = 0.14f;

        [Header("Idle Float")]
        public bool EnableIdleFloat;
        public float FloatAmplitude = 0.16f;
        public float FloatPeriodSeconds = 2.4f;
        public float FloatBlendInSeconds = 0.35f;
        public bool UseParabolicFloat = true;

        private Transform[] _motionTargets = Array.Empty<Transform>();
        private Vector3[] _shownLocalPositions = Array.Empty<Vector3>();
        private Vector3[] _shownLocalScales = Array.Empty<Vector3>();
        private bool _hasShownPose;
        private float _visibleAmount = 1f;
        private Coroutine _visibilityRoutine;
        private Collider[] _colliders = Array.Empty<Collider>();
        private ForestDepthFogZone[] _fogZones = Array.Empty<ForestDepthFogZone>();
        private float[] _shownFogDensities = Array.Empty<float>();
        private ForestDepthFogZoneController _fogController;
        private bool _isFloating;
        private float _floatStartedAt;

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

                bool currentlyVisible = target.gameObject.activeSelf && target._visibleAmount > 0.99f;
                if (currentlyVisible != data.Visible)
                    changed++;

                target.SetVisible(data.Visible, data.DurationMs);
            }

            Debug.Log($"[StageSceneObjectTarget] SetActive visible={data.Visible} key='{targetKey}' group={groupId} changed={changed}");
            return changed;
        }

        private void Awake()
        {
            CacheShownPose();
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(TargetKey))
                TargetKey = gameObject.name;

            if (DefaultDurationMs < 0)
                DefaultDurationMs = 0;

            if (HiddenScale < 0f)
                HiddenScale = 0f;

            if (FloatAmplitude < 0f)
                FloatAmplitude = 0f;

            if (FloatPeriodSeconds < 0.05f)
                FloatPeriodSeconds = 0.05f;

            if (FloatBlendInSeconds < 0f)
                FloatBlendInSeconds = 0f;

            if (RiseOvershootHeight < 0f)
                RiseOvershootHeight = 0f;
        }

        public void BindRuntimeTarget(int groupId, string targetKey = null)
        {
            if (groupId > 0)
                GroupId = groupId;

            if (!string.IsNullOrWhiteSpace(targetKey))
                TargetKey = targetKey.Trim();

            RecaptureShownPose();
        }

        public void RecaptureShownPose()
        {
            bool wasFloating = _isFloating;
            _isFloating = false;
            _hasShownPose = false;
            CacheShownPose();

            if (wasFloating)
                StartIdleFloat();
        }

        private void SetVisible(bool visible, int durationMs)
        {
            CacheShownPose();
            StopIdleFloat();

            int resolvedDurationMs = durationMs > 0 ? durationMs : DefaultDurationMs;
            if (_visibilityRoutine != null && isActiveAndEnabled)
            {
                StopCoroutine(_visibilityRoutine);
                _visibilityRoutine = null;
            }

            if (visible && !gameObject.activeSelf)
                gameObject.SetActive(true);

            if (DisableCollidersWhenHidden && !visible)
                SetCollidersEnabled(false);

            bool replayShow = visible
                              && ReplayShowAnimationWhenAlreadyVisible
                              && gameObject.activeSelf
                              && _visibleAmount > 0.99f
                              && !HasFogZones();

            if (!isActiveAndEnabled || resolvedDurationMs <= 0)
            {
                if (replayShow)
                    ApplyVisibility(0f);

                ApplyVisibility(visible ? 1f : 0f);
                if (DisableCollidersWhenHidden && visible)
                    SetCollidersEnabled(true);

                if (visible)
                    StartIdleFloat();
                else
                    gameObject.SetActive(false);

                return;
            }

            _visibilityRoutine = StartCoroutine(AnimateVisibility(visible, resolvedDurationMs / 1000f, replayShow));
        }

        private IEnumerator AnimateVisibility(bool visible, float durationSeconds, bool replayShow)
        {
            float from = replayShow ? 0f : _visibleAmount;
            float to = visible ? 1f : 0f;
            float elapsed = 0f;

            if (replayShow)
                ApplyVisibility(from);

            while (elapsed < durationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / durationSeconds);
                float eased = t * t * (3f - 2f * t);
                ApplyVisibility(Mathf.Lerp(from, to, eased), visible);
                yield return null;
            }

            ApplyVisibility(to);
            if (DisableCollidersWhenHidden && visible)
                SetCollidersEnabled(true);

            if (visible)
                StartIdleFloat();
            else
                gameObject.SetActive(false);

            _visibilityRoutine = null;
        }

        private void LateUpdate()
        {
            if (!_isFloating || _visibleAmount < 0.999f || HasFogZones())
                return;

            ApplyIdleFloat();
        }

        private void CacheShownPose()
        {
            if (_hasShownPose)
                return;

            _motionTargets = ResolveMotionTargets();
            _shownLocalPositions = new Vector3[_motionTargets.Length];
            _shownLocalScales = new Vector3[_motionTargets.Length];
            for (int i = 0; i < _motionTargets.Length; i++)
            {
                Transform motionTarget = _motionTargets[i];
                _shownLocalPositions[i] = motionTarget != null ? motionTarget.localPosition : Vector3.zero;
                _shownLocalScales[i] = motionTarget != null ? motionTarget.localScale : Vector3.one;
            }

            _colliders = GetComponentsInChildren<Collider>(true);
            _fogZones = GetComponentsInChildren<ForestDepthFogZone>(true);
            _shownFogDensities = new float[_fogZones.Length];
            for (int i = 0; i < _fogZones.Length; i++)
            {
                _shownFogDensities[i] = _fogZones[i] != null ? _fogZones[i].Density : 0f;
            }
            _fogController = GetComponentInParent<ForestDepthFogZoneController>(true);
            if (_fogController == null && _fogZones.Length > 0)
                _fogController = _fogZones[0] != null ? _fogZones[0].GetComponentInParent<ForestDepthFogZoneController>(true) : null;
            _hasShownPose = true;
        }

        private Transform[] ResolveMotionTargets()
        {
            if (MotionRoots != null && MotionRoots.Length > 0)
            {
                var roots = new List<Transform>(MotionRoots.Length);
                foreach (var root in MotionRoots)
                {
                    if (root == null || roots.Contains(root))
                        continue;

                    roots.Add(root);
                }

                if (roots.Count > 0)
                    return roots.ToArray();
            }

            return new[] { transform };
        }

        private void ApplyVisibility(float amount, bool showRiseArc = false)
        {
            CacheShownPose();

            _visibleAmount = Mathf.Clamp01(amount);
            if (HasFogZones())
            {
                ApplyFogDensity(_visibleAmount);
                return;
            }

            ApplyMotionVisibility(_visibleAmount, showRiseArc);
        }

        private void ApplyMotionVisibility(float amount, bool showRiseArc)
        {
            float hiddenYOffset = EnableRiseFromGround ? RiseHiddenYOffset : HiddenYOffset;
            float riseArc = showRiseArc && EnableRiseFromGround
                ? Mathf.Sin(amount * Mathf.PI) * RiseOvershootHeight
                : 0f;

            for (int i = 0; i < _motionTargets.Length; i++)
            {
                Transform motionTarget = _motionTargets[i];
                if (motionTarget == null)
                    continue;

                Vector3 shownPosition = i < _shownLocalPositions.Length ? _shownLocalPositions[i] : motionTarget.localPosition;
                Vector3 shownScale = i < _shownLocalScales.Length ? _shownLocalScales[i] : motionTarget.localScale;

                motionTarget.localPosition = Vector3.Lerp(shownPosition + Vector3.up * hiddenYOffset, shownPosition, amount)
                                             + Vector3.up * riseArc;
                motionTarget.localScale = Vector3.Lerp(shownScale * HiddenScale, shownScale, amount);
            }
        }

        private void StartIdleFloat()
        {
            if (!EnableIdleFloat || HasFogZones())
                return;

            CacheShownPose();
            _isFloating = true;
            _floatStartedAt = Time.time;
            ApplyIdleFloat();
        }

        private void StopIdleFloat()
        {
            if (!_isFloating)
                return;

            _isFloating = false;
            if (_hasShownPose && !HasFogZones())
                ApplyMotionVisibility(_visibleAmount, showRiseArc: false);
        }

        private void ApplyIdleFloat()
        {
            float offset = EvaluateFloatOffset();
            for (int i = 0; i < _motionTargets.Length; i++)
            {
                Transform motionTarget = _motionTargets[i];
                if (motionTarget == null)
                    continue;

                Vector3 shownPosition = i < _shownLocalPositions.Length ? _shownLocalPositions[i] : motionTarget.localPosition;
                Vector3 shownScale = i < _shownLocalScales.Length ? _shownLocalScales[i] : motionTarget.localScale;
                motionTarget.localPosition = shownPosition + Vector3.up * offset;
                motionTarget.localScale = shownScale;
            }
        }

        private float EvaluateFloatOffset()
        {
            float elapsed = Mathf.Max(0f, Time.time - _floatStartedAt);
            float period = Mathf.Max(0.05f, FloatPeriodSeconds);
            float wave = Mathf.Sin(elapsed / period * Mathf.PI * 2f);
            if (UseParabolicFloat)
                wave = Mathf.Sign(wave) * (1f - Mathf.Pow(1f - Mathf.Abs(wave), 2f));

            float blend = FloatBlendInSeconds > 0f ? Mathf.Clamp01(elapsed / FloatBlendInSeconds) : 1f;
            return wave * FloatAmplitude * blend;
        }

        private bool HasFogZones()
            => _fogZones != null && _fogZones.Length > 0;

        private void ApplyFogDensity(float amount)
        {
            for (int i = 0; i < _fogZones.Length; i++)
            {
                var fogZone = _fogZones[i];
                if (fogZone == null)
                    continue;

                float shownDensity = i < _shownFogDensities.Length ? _shownFogDensities[i] : fogZone.Density;
                fogZone.SetDensity(Mathf.Lerp(0f, shownDensity, amount), applyImmediately: false);
            }

            _fogController?.ApplyNow();
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (_colliders == null)
                return;

            foreach (var targetCollider in _colliders)
            {
                if (targetCollider != null)
                    targetCollider.enabled = enabled;
            }
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
