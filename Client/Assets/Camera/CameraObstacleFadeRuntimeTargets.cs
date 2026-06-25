using System;
using UnityEngine;

public static class CameraObstacleFadeRuntimeTargets
{
    private const int CrystalAppearanceId = 500;
    private const int RunicTowerAppearanceId = 502;
    private const string FadeTriggerName = "__CameraFadeTrigger";
    private const string WallLayerName = "Wall";
    private const int WallLayerFallback = 7;
    private const float MinOccluderHeight = 0.35f;
    private const float MinOccluderWidth = 0.08f;

    public static void EnsureForEntity(ClientEntityInfo info, Transform root)
    {
        if (root == null || info.EntityType != (int)EntityType.Object)
            return;

        if (info.AppearanceId != CrystalAppearanceId && info.AppearanceId != RunicTowerAppearanceId)
            return;

        EnsureForHierarchy(root);
    }

    public static void EnsureForHierarchy(Transform root)
    {
        if (root == null)
            return;

        int wallLayer = LayerMask.NameToLayer(WallLayerName);
        if (wallLayer < 0)
            wallLayer = WallLayerFallback;

        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (!ShouldCreateTarget(renderer))
                continue;

            EnsureTargetForRenderer(renderer, wallLayer);
        }
    }

    private static bool ShouldCreateTarget(MeshRenderer renderer)
    {
        if (renderer == null)
            return false;

        if (renderer.GetComponent<CameraObstacleFadeTarget>() != null)
            return false;

        string objectName = renderer.gameObject.name;
        if (objectName.StartsWith(FadeTriggerName, StringComparison.Ordinal))
            return false;

        Bounds bounds = renderer.bounds;
        Vector3 size = bounds.size;
        if (size.y < MinOccluderHeight || Mathf.Max(size.x, size.z) < MinOccluderWidth)
            return false;

        return true;
    }

    private static void EnsureTargetForRenderer(MeshRenderer renderer, int wallLayer)
    {
        if (renderer == null)
            return;

        if (TryUseExistingTriggerCollider(renderer, wallLayer))
            return;

        Transform trigger = renderer.transform.Find(FadeTriggerName);
        if (trigger == null)
        {
            var triggerObject = new GameObject(FadeTriggerName);
            trigger = triggerObject.transform;
            trigger.SetParent(renderer.transform, false);
        }

        trigger.localPosition = Vector3.zero;
        trigger.localRotation = Quaternion.identity;
        trigger.localScale = Vector3.one;
        trigger.gameObject.layer = wallLayer;

        var target = trigger.GetComponent<CameraObstacleFadeTarget>();
        if (target == null)
            target = trigger.gameObject.AddComponent<CameraObstacleFadeTarget>();
        target.TargetRenderer = renderer;

        var collider = trigger.GetComponent<BoxCollider>();
        if (collider == null)
            collider = trigger.gameObject.AddComponent<BoxCollider>();

        collider.isTrigger = true;
        ConfigureCollider(collider, renderer);
    }

    private static bool TryUseExistingTriggerCollider(MeshRenderer renderer, int wallLayer)
    {
        if (renderer.gameObject.layer != wallLayer)
            return false;

        var collider = renderer.GetComponent<Collider>();
        if (collider == null || !collider.isTrigger)
            return false;

        var target = renderer.GetComponent<CameraObstacleFadeTarget>();
        if (target == null)
            target = renderer.gameObject.AddComponent<CameraObstacleFadeTarget>();

        target.TargetRenderer = renderer;
        return true;
    }

    private static void ConfigureCollider(BoxCollider collider, MeshRenderer renderer)
    {
        if (collider == null || renderer == null)
            return;

        var meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            collider.center = meshFilter.sharedMesh.bounds.center;
            collider.size = meshFilter.sharedMesh.bounds.size;
            return;
        }

        Bounds bounds = renderer.bounds;
        Transform transform = renderer.transform;
        collider.center = transform.InverseTransformPoint(bounds.center);
        collider.size = new Vector3(
            Mathf.Max(0.01f, bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x))),
            Mathf.Max(0.01f, bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y))),
            Mathf.Max(0.01f, bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.z))));
    }
}
