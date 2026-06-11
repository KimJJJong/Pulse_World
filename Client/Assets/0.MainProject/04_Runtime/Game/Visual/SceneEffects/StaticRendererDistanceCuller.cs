using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StaticRendererDistanceCuller : MonoBehaviour
{
    [SerializeField] private Transform reference;
    [SerializeField, Min(1f)] private float visibleDistance = 55f;
    [SerializeField, Min(0f)] private float hysteresisDistance = 8f;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;
    [SerializeField, Min(1)] private int checksPerRefresh = 512;
    [SerializeField] private bool includeInactive;
    [SerializeField] private bool ignoreDynamicRenderers = true;

    private readonly List<Renderer> _renderers = new List<Renderer>(512);
    private readonly List<Vector3> _centers = new List<Vector3>(512);
    private readonly List<float> _radii = new List<float>(512);
    private readonly List<bool> _initialEnabled = new List<bool>(512);
    private readonly List<bool> _disabledByCuller = new List<bool>(512);
    private float _nextRefreshTime;
    private int _cursor;
    private bool _cacheReady;

    private void OnEnable()
    {
        RebuildCache();
        _nextRefreshTime = 0f;
    }

    private void OnDisable()
    {
        RestoreDisabledRenderers();
    }

    private void OnTransformChildrenChanged()
    {
        _cacheReady = false;
    }

    private void Update()
    {
        if (!_cacheReady)
            RebuildCache();

        if (_renderers.Count == 0 || Time.unscaledTime < _nextRefreshTime)
            return;

        Transform target = ResolveReference();
        if (target == null)
            return;

        _nextRefreshTime = Time.unscaledTime + refreshInterval;
        Vector3 referencePosition = target.position;
        int checks = Mathf.Min(Mathf.Max(1, checksPerRefresh), _renderers.Count);

        for (int i = 0; i < checks; i++)
        {
            if (_cursor >= _renderers.Count)
                _cursor = 0;

            UpdateRendererVisibility(_cursor, referencePosition);
            _cursor++;
        }
    }

    public void RebuildCache()
    {
        RestoreDisabledRenderers();
        _renderers.Clear();
        _centers.Clear();
        _radii.Clear();
        _initialEnabled.Clear();
        _disabledByCuller.Clear();

        var foundRenderers = GetComponentsInChildren<Renderer>(includeInactive);
        for (int i = 0; i < foundRenderers.Length; i++)
        {
            Renderer rendererTarget = foundRenderers[i];
            if (!ShouldManage(rendererTarget))
                continue;

            Bounds bounds = rendererTarget.bounds;
            _renderers.Add(rendererTarget);
            _centers.Add(bounds.center);
            _radii.Add(bounds.extents.magnitude);
            _initialEnabled.Add(rendererTarget.enabled);
            _disabledByCuller.Add(false);
        }

        _cursor = 0;
        _cacheReady = true;
    }

    private Transform ResolveReference()
    {
        if (reference != null)
            return reference;

        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
    }

    private void UpdateRendererVisibility(int index, Vector3 referencePosition)
    {
        Renderer rendererTarget = _renderers[index];
        if (rendererTarget == null)
            return;

        float radius = _radii[index];
        float showDistance = visibleDistance + radius;
        float hideDistance = visibleDistance + hysteresisDistance + radius;
        float distanceSqr = (_centers[index] - referencePosition).sqrMagnitude;

        if (rendererTarget.enabled)
        {
            if (distanceSqr > hideDistance * hideDistance)
            {
                rendererTarget.enabled = false;
                _disabledByCuller[index] = true;
            }

            return;
        }

        if (_disabledByCuller[index] && _initialEnabled[index] && distanceSqr <= showDistance * showDistance)
        {
            rendererTarget.enabled = true;
            _disabledByCuller[index] = false;
        }
    }

    private bool ShouldManage(Renderer rendererTarget)
    {
        if (rendererTarget == null)
            return false;

        if (rendererTarget is SkinnedMeshRenderer
            || rendererTarget is ParticleSystemRenderer
            || rendererTarget is TrailRenderer
            || rendererTarget is LineRenderer)
        {
            return false;
        }

        if (!ignoreDynamicRenderers)
            return true;

        return rendererTarget.GetComponentInParent<Animator>() == null;
    }

    private void RestoreDisabledRenderers()
    {
        for (int i = 0; i < _renderers.Count; i++)
        {
            Renderer rendererTarget = _renderers[i];
            if (rendererTarget != null && _disabledByCuller[i] && _initialEnabled[i])
                rendererTarget.enabled = true;
        }
    }
}
