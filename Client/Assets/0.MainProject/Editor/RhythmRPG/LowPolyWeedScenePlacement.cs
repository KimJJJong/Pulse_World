#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LowPolyWeedScenePlacement
{
    private const string RootName = "Codex_LowPolyWeed_Placement";
    private const string AssetFolder = "Assets/0.MainProject/Art/LowPolyWeed";
    private const string ShortPrefabPath = AssetFolder + "/PF_LowPolyWeed_Short.prefab";
    private const string MediumPrefabPath = AssetFolder + "/PF_LowPolyWeed_Medium.prefab";
    private const string TallPrefabPath = AssetFolder + "/PF_LowPolyWeed_Tall.prefab";
    private const string PatchPrefabPath = AssetFolder + "/PF_LowPolyWeed_Patch.prefab";
    private const float StoneClearance = 0.08f;
    private const int StoneResolveIterations = 8;

    private static readonly AnchorPlacement[] Placements =
    {
        new("Gate_Crystal_01_Left_Base", "Crystal_Gate_01", PatchPrefabPath, new Vector2(1.35f, -1.30f), 24f, 0.56f),
        new("Gate_Crystal_01_Back_Crack", "Crystal_Gate_01", MediumPrefabPath, new Vector2(2.05f, 0.35f), -18f, 0.45f),
        new("Gate_Crystal_02_Right_Base", "Crystal_Gate_02", PatchPrefabPath, new Vector2(1.35f, 1.25f), -34f, 0.60f),
        new("Gate_Crystal_02_Stone_Gap", "Crystal_Gate_02", MediumPrefabPath, new Vector2(2.20f, -0.35f), 18f, 0.45f),

        new("Pine_01_Root_Shadow", "Origami_Pine_01", ShortPrefabPath, new Vector2(-0.75f, -0.60f), 55f, 0.36f),
        new("Pine_01_Rock_Side", "Origami_Pine_01", MediumPrefabPath, new Vector2(0.72f, 0.45f), -35f, 0.40f),
        new("Pine_02_Root_Shadow", "Origami_Pine_01 (1)", ShortPrefabPath, new Vector2(-0.65f, 0.55f), 20f, 0.34f),
        new("Left_Stack_05_Base", "Mossy_Stone_Stack (5)", PatchPrefabPath, new Vector2(1.20f, -0.65f), -18f, 0.60f),
        new("Left_Lantern_Grass_Base", "Lantern_Warm_01 (1)", PatchPrefabPath, new Vector2(-1.00f, 0.60f), 32f, 0.62f),
        new("Left_Lantern_Post_Tuft", "Lantern_Warm_01 (1)", ShortPrefabPath, new Vector2(0.75f, -0.80f), -64f, 0.40f),

        new("Lower_Stack_11_Gap", "Mossy_Stone_Stack (11)", MediumPrefabPath, new Vector2(-2.20f, 1.35f), 42f, 0.36f),
        new("Lower_Stack_12_Base", "Mossy_Stone_Stack (12)", MediumPrefabPath, new Vector2(1.00f, -0.70f), -20f, 0.45f),
        new("Lower_Stack_13_Tuft", "Mossy_Stone_Stack (13)", ShortPrefabPath, new Vector2(0.80f, 0.55f), 70f, 0.36f),
        new("Lower_Stack_11_Stone_Crevice", "Mossy_Stone_Stack (11)", ShortPrefabPath, new Vector2(0.42f, -0.76f), -24f, 0.32f),
        new("Lower_Stack_12_Stone_Crevice", "Mossy_Stone_Stack (12)", ShortPrefabPath, new Vector2(-0.58f, 0.78f), 48f, 0.34f),
        new("Lower_Glyph_02_03_Stone_Base", "Mossy_Azure_Glyph_Stone_02 (3)", MediumPrefabPath, new Vector2(0.72f, -0.58f), 18f, 0.38f),
        new("Lower_Faceted_06_Crevice", "Mossy_Faceted_Boulder (6)", PatchPrefabPath, new Vector2(-1.05f, 0.80f), 8f, 0.58f),
        new("Lower_Lantern_03_Base", "Lantern_Warm_03", PatchPrefabPath, new Vector2(-1.25f, 0.75f), -38f, 0.64f),
        new("Lower_Lantern_03_Back", "Lantern_Warm_03", MediumPrefabPath, new Vector2(0.90f, -0.60f), 24f, 0.44f),

        new("Mid_Boulder_00_Base", "Mossy_Boulder", PatchPrefabPath, new Vector2(-0.95f, -0.75f), 8f, 0.62f),
        new("Mid_Stack_00_Crack", "Mossy_Stone_Stack", MediumPrefabPath, new Vector2(0.85f, 0.65f), -28f, 0.45f),
        new("Mid_Stack_00_Stone_Front", "Mossy_Stone_Stack", ShortPrefabPath, new Vector2(-0.85f, -1.25f), 32f, 0.32f),
        new("Mid_Stack_01_Stone_Crevice", "Mossy_Stone_Stack (1)", MediumPrefabPath, new Vector2(0.64f, -0.46f), -58f, 0.38f),
        new("Mid_Faceted_00_Gap", "Mossy_Faceted_Boulder", PatchPrefabPath, new Vector2(-1.25f, 0.95f), 36f, 0.50f),
        new("Mid_Faceted_01_Tuft", "Mossy_Faceted_Boulder (1)", ShortPrefabPath, new Vector2(0.65f, -0.50f), -46f, 0.34f),
        new("Mid_Glyph_02_Stone_Base", "Mossy_Azure_Glyph_Stone_02", PatchPrefabPath, new Vector2(-0.86f, 0.48f), 16f, 0.48f),
        new("Mid_Glyph_03_Stone_Base", "Mossy_Azure_Glyph_Stone_03", MediumPrefabPath, new Vector2(0.72f, -0.56f), -32f, 0.38f),
        new("Mid_Log_00_Left_End", "Fallen_Log", PatchPrefabPath, new Vector2(-1.35f, -0.65f), -16f, 0.58f),
        new("Mid_Log_00_Right_End", "Fallen_Log", MediumPrefabPath, new Vector2(1.20f, 0.62f), 42f, 0.42f),
        new("Mid_Faceted_02_Tuft", "Mossy_Faceted_Boulder (2)", ShortPrefabPath, new Vector2(0.70f, -0.45f), 62f, 0.34f),
        new("Mid_Stack_02_Base", "Mossy_Stone_Stack (2)", PatchPrefabPath, new Vector2(-1.05f, 0.85f), -30f, 0.58f),
        new("Mid_Stack_03_Tuft", "Mossy_Stone_Stack (3)", ShortPrefabPath, new Vector2(0.75f, -0.50f), 18f, 0.32f),
        new("Mid_Stack_02_Stone_Crevice", "Mossy_Stone_Stack (2)", ShortPrefabPath, new Vector2(0.78f, -0.62f), 36f, 0.34f),
        new("Mid_Glyph_03_01_Stone_Gap", "Mossy_Azure_Glyph_Stone_03 (1)", MediumPrefabPath, new Vector2(-0.78f, 0.52f), 22f, 0.40f),
        new("Waypost_Base_Left", "Wooden_Arrow_Signpost", PatchPrefabPath, new Vector2(-0.85f, 0.55f), 10f, 0.56f),
        new("Waypost_Base_Right", "Wooden_Arrow_Signpost", MediumPrefabPath, new Vector2(0.75f, -0.70f), -48f, 0.40f),
        new("Center_Waypost_Path_Edge", "Wooden_Arrow_Signpost", ShortPrefabPath, new Vector2(-0.35f, -1.22f), 24f, 0.32f),
        new("Center_Waypost_Object_Gap", "Wooden_Arrow_Signpost", MediumPrefabPath, new Vector2(1.12f, 0.18f), -18f, 0.36f),
        new("Center_Log_00_Path_Edge", "Fallen_Log", ShortPrefabPath, new Vector2(0.08f, -1.08f), 14f, 0.31f),
        new("Center_Log_00_Back_Gap", "Fallen_Log", ShortPrefabPath, new Vector2(0.62f, 1.05f), -42f, 0.30f),
        new("Center_Faceted_02_Path_Side", "Mossy_Faceted_Boulder (2)", ShortPrefabPath, new Vector2(1.18f, -1.08f), 58f, 0.29f),
        new("Center_Stack_02_Path_Side", "Mossy_Stone_Stack (2)", ShortPrefabPath, new Vector2(1.35f, 1.18f), -28f, 0.30f),
        new("Center_Glyph_03_01_Path_Side", "Mossy_Azure_Glyph_Stone_03 (1)", ShortPrefabPath, new Vector2(1.22f, 0.92f), 34f, 0.29f),
        new("Center_Mid_Boulder_Path_Side", "Mossy_Boulder", ShortPrefabPath, new Vector2(1.18f, -1.08f), -12f, 0.30f),
        new("Rustic_Sign_Base_Left", "Rustic_Wooden_Arrow_Sign", PatchPrefabPath, new Vector2(-0.80f, 0.60f), 24f, 0.54f),
        new("Rustic_Sign_Post_Tuft", "Rustic_Wooden_Arrow_Sign", ShortPrefabPath, new Vector2(0.75f, -0.50f), -35f, 0.34f),
        new("Center_Rustic_Sign_Path_Edge", "Rustic_Wooden_Arrow_Sign", ShortPrefabPath, new Vector2(-0.20f, -1.12f), 18f, 0.30f),

        new("Upper_Log_01_Left_End", "Fallen_Log (1)", PatchPrefabPath, new Vector2(-1.20f, 0.80f), 32f, 0.60f),
        new("Upper_Stack_04_Base", "Mossy_Stone_Stack (4)", PatchPrefabPath, new Vector2(1.30f, -0.70f), -24f, 0.64f),
        new("Upper_Faceted_04_Tuft", "Mossy_Faceted_Boulder (4)", MediumPrefabPath, new Vector2(-0.85f, 0.45f), 28f, 0.42f),
        new("Upper_Boulder_01_Base", "Mossy_Boulder (1)", PatchPrefabPath, new Vector2(-1.10f, -0.80f), -18f, 0.62f),
        new("Upper_Boulder_02_Moss_Gap", "Mossy_Boulder (2)", MediumPrefabPath, new Vector2(1.35f, 0.75f), 44f, 0.44f),
        new("Upper_Faceted_03_Tuft", "Mossy_Faceted_Boulder (3)", ShortPrefabPath, new Vector2(-0.60f, -0.80f), -60f, 0.34f),
        new("Upper_Stack_06_Base", "Mossy_Stone_Stack (6)", PatchPrefabPath, new Vector2(1.55f, -1.05f), 18f, 0.54f),
        new("Upper_Stack_08_Tuft", "Mossy_Stone_Stack (8)", ShortPrefabPath, new Vector2(0.70f, 0.55f), 36f, 0.32f),
        new("Upper_Stack_09_Tuft", "Mossy_Stone_Stack (9)", MediumPrefabPath, new Vector2(-0.65f, -0.60f), -16f, 0.40f),
        new("Upper_Stack_10_Stone_Crevice", "Mossy_Stone_Stack (10)", ShortPrefabPath, new Vector2(-0.62f, 0.70f), 14f, 0.34f),
        new("Upper_Stack_14_Stone_Crevice", "Mossy_Stone_Stack (14)", MediumPrefabPath, new Vector2(0.70f, -0.58f), -38f, 0.38f),
        new("Upper_Glyph_02_01_Stone_Base", "Mossy_Azure_Glyph_Stone_02 (1)", PatchPrefabPath, new Vector2(0.95f, -0.64f), -20f, 0.50f),
        new("Upper_Glyph_03_02_Stone_Base", "Mossy_Azure_Glyph_Stone_03 (2)", MediumPrefabPath, new Vector2(-0.82f, 0.58f), 42f, 0.40f),
        new("Upper_Glyph_03_03_Stone_Gap", "Mossy_Azure_Glyph_Stone_03 (3)", ShortPrefabPath, new Vector2(0.96f, -1.04f), -52f, 0.30f),
        new("Upper_Faceted_05_Base", "Mossy_Faceted_Boulder (5)", PatchPrefabPath, new Vector2(-1.10f, -0.50f), 52f, 0.60f),

        new("Upper_Lantern_01_Base_Left", "Lantern_Warm_01", PatchPrefabPath, new Vector2(-0.95f, -0.55f), 12f, 0.62f),
        new("Upper_Lantern_01_Base_Right", "Lantern_Warm_01", ShortPrefabPath, new Vector2(0.85f, 0.65f), -54f, 0.36f),
        new("Right_Lantern_02_Base_Left", "Lantern_Warm_02", PatchPrefabPath, new Vector2(-1.20f, 0.70f), -34f, 0.64f),
        new("Right_Lantern_02_Base_Back", "Lantern_Warm_02", MediumPrefabPath, new Vector2(0.95f, 1.10f), 28f, 0.44f),
    };

    [MenuItem("RhythmRPG/Editors/World/Place Low Poly Weed In Current Scene")]
    public static void Place()
    {
        EnsureAssets();
        ClearPreviousPlacement();

        var root = new GameObject(RootName);
        var forestRoot = GameObject.Find("Forest_Decoration_Set");
        if (forestRoot != null)
        {
            root.transform.SetParent(forestRoot.transform, false);
            root.transform.localPosition = Vector3.zero;
        }

        var prefabCache = new Dictionary<string, GameObject>();
        var stoneBlockers = CollectStoneBlockers(null);
        var placedCount = 0;
        foreach (var placement in Placements)
        {
            var anchor = FindSceneTransform(placement.AnchorName);
            if (anchor == null)
            {
                Debug.LogWarning($"[LowPolyWeedScenePlacement] Anchor not found: {placement.AnchorName}");
                continue;
            }

            var prefab = GetPrefab(prefabCache, placement.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[LowPolyWeedScenePlacement] Missing prefab: {placement.PrefabPath}");
                continue;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, root.transform) as GameObject;
            if (instance == null)
            {
                continue;
            }

            instance.name = "Weed_" + placement.Name;
            var anchorYaw = Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);
            var worldOffset = anchorYaw * new Vector3(placement.Offset.x, 0f, placement.Offset.y);
            instance.transform.position = SnapToGround(anchor.position + worldOffset);
            instance.transform.rotation = Quaternion.Euler(0f, anchor.eulerAngles.y + placement.YawOffset, 0f);
            instance.transform.localScale = Vector3.one * placement.Scale;
            ConfigureRenderers(instance);
            ResolveStoneOverlap(instance, stoneBlockers);
            placedCount++;
        }

        Selection.activeGameObject = root;
        FrameSceneView(root);

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[LowPolyWeedScenePlacement] Placement complete. " +
            $"Scene={scene.name}, Root={GetPath(root.transform)}, AnchoredInstances={placedCount}/{Placements.Length}, " +
            $"Renderers={root.GetComponentsInChildren<MeshRenderer>(true).Length}.");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Low Poly Weed Scene Placement")]
    public static void Validate()
    {
        var root = GameObject.Find(RootName);
        if (root == null)
        {
            Debug.LogError("[LowPolyWeedScenePlacement] Validation failed: placement root not found.");
            return;
        }

        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        var material = AssetDatabase.LoadAssetAtPath<Material>(AssetFolder + "/M_LowPolyWeed.mat");
        var wrongMaterialCount = 0;
        foreach (var renderer in renderers)
        {
            if (renderer.sharedMaterial != material)
            {
                wrongMaterialCount++;
            }
        }

        var expectedCount = CountResolvablePlacements();
        if (root.transform.childCount < expectedCount || renderers.Length < expectedCount)
        {
            Debug.LogError("[LowPolyWeedScenePlacement] Validation failed: not enough weed instances/renderers were placed.");
            return;
        }

        if (wrongMaterialCount > 0)
        {
            Debug.LogError($"[LowPolyWeedScenePlacement] Validation failed: {wrongMaterialCount} renderers do not use M_LowPolyWeed.");
            return;
        }

        var overlapNames = new List<string>();
        var overlapCount = CountStoneOverlaps(root, overlapNames);
        if (overlapCount > 0)
        {
            Debug.LogError(
                $"[LowPolyWeedScenePlacement] Validation failed: {overlapCount} weed instances overlap Stone/Boulder/Glyph objects. " +
                $"Names={string.Join(", ", overlapNames)}");
            return;
        }

        Debug.Log(
            "[LowPolyWeedScenePlacement] VALIDATION OK. " +
            $"Root={GetPath(root.transform)}, AnchoredInstances={root.transform.childCount}/{Placements.Length}, Renderers={renderers.Length}.");
    }

    private static void EnsureAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PatchPrefabPath) == null)
        {
            LowPolyWeedSetup.Build();
        }
    }

    private static void ClearPreviousPlacement()
    {
        var objectsToDestroy = new List<GameObject>();
        var sceneObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var gameObject in sceneObjects)
        {
            if (gameObject == null)
            {
                continue;
            }

            if (!gameObject.scene.IsValid())
            {
                continue;
            }

            if (gameObject.name == RootName)
            {
                objectsToDestroy.Add(gameObject);
                continue;
            }

            if (gameObject.transform.parent == null && gameObject.name.StartsWith("PF_LowPolyWeed_", StringComparison.Ordinal))
            {
                objectsToDestroy.Add(gameObject);
            }
        }

        foreach (var gameObject in objectsToDestroy)
        {
            if (gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }

    private static GameObject GetPrefab(Dictionary<string, GameObject> cache, string path)
    {
        if (!cache.TryGetValue(path, out var prefab))
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            cache[path] = prefab;
        }

        return prefab;
    }

    private static Transform FindSceneTransform(string objectName)
    {
        foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (transform.name == objectName && transform.gameObject.scene.IsValid())
            {
                return transform;
            }
        }

        return null;
    }

    private static int CountResolvablePlacements()
    {
        var count = 0;
        foreach (var placement in Placements)
        {
            if (FindSceneTransform(placement.AnchorName) != null)
            {
                count++;
            }
        }

        return count;
    }

    private static List<Bounds> CollectStoneBlockers(Transform placementRoot)
    {
        var blockers = new List<Bounds>();
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (renderer == null || !renderer.gameObject.scene.IsValid())
            {
                continue;
            }

            if (placementRoot != null && renderer.transform.IsChildOf(placementRoot))
            {
                continue;
            }

            if (!IsStoneLike(renderer.transform))
            {
                continue;
            }

            blockers.Add(renderer.bounds);
        }

        return blockers;
    }

    private static bool IsStoneLike(Transform transform)
    {
        while (transform != null)
        {
            if (IsStoneLikeName(transform.name))
            {
                return true;
            }

            transform = transform.parent;
        }

        return false;
    }

    private static bool IsStoneLikeName(string objectName)
    {
        return objectName.IndexOf("Stone", StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Boulder", StringComparison.OrdinalIgnoreCase) >= 0
            || objectName.IndexOf("Glyph", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ResolveStoneOverlap(GameObject instance, List<Bounds> stoneBlockers)
    {
        for (var i = 0; i < StoneResolveIterations; i++)
        {
            if (!TryGetHorizontalOverlap(instance, stoneBlockers, out var push))
            {
                return;
            }

            var position = instance.transform.position + push;
            position = SnapToGround(position);
            instance.transform.position = position;
        }
    }

    private static bool TryGetHorizontalOverlap(GameObject instance, List<Bounds> stoneBlockers, out Vector3 push)
    {
        push = Vector3.zero;
        if (!TryGetRendererBounds(instance, out var weedBounds))
        {
            return false;
        }

        var largestPush = Vector3.zero;
        var largestMagnitude = 0f;
        foreach (var blocker in stoneBlockers)
        {
            if (!OverlapsXZ(weedBounds, blocker, StoneClearance, out var candidatePush))
            {
                continue;
            }

            var magnitude = candidatePush.sqrMagnitude;
            if (magnitude > largestMagnitude)
            {
                largestMagnitude = magnitude;
                largestPush = candidatePush;
            }
        }

        if (largestMagnitude <= 0f)
        {
            return false;
        }

        push = largestPush;
        return true;
    }

    private static bool TryGetRendererBounds(GameObject gameObject, out Bounds bounds)
    {
        bounds = default;
        var initialized = false;
        foreach (var renderer in gameObject.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return initialized;
    }

    private static bool OverlapsXZ(Bounds weedBounds, Bounds stoneBounds, float clearance, out Vector3 push)
    {
        push = Vector3.zero;

        var weedMinX = weedBounds.min.x - clearance;
        var weedMaxX = weedBounds.max.x + clearance;
        var weedMinZ = weedBounds.min.z - clearance;
        var weedMaxZ = weedBounds.max.z + clearance;

        if (weedMaxX <= stoneBounds.min.x || weedMinX >= stoneBounds.max.x
            || weedMaxZ <= stoneBounds.min.z || weedMinZ >= stoneBounds.max.z)
        {
            return false;
        }

        var pushRight = stoneBounds.max.x - weedMinX;
        var pushLeft = weedMaxX - stoneBounds.min.x;
        var pushForward = stoneBounds.max.z - weedMinZ;
        var pushBack = weedMaxZ - stoneBounds.min.z;

        var centerDelta = weedBounds.center - stoneBounds.center;
        var pushX = centerDelta.x >= 0f ? pushRight : -pushLeft;
        var pushZ = centerDelta.z >= 0f ? pushForward : -pushBack;

        if (Mathf.Abs(pushX) < Mathf.Abs(pushZ))
        {
            push = new Vector3(pushX + Mathf.Sign(pushX) * StoneClearance, 0f, 0f);
        }
        else
        {
            push = new Vector3(0f, 0f, pushZ + Mathf.Sign(pushZ) * StoneClearance);
        }

        return true;
    }

    private static int CountStoneOverlaps(GameObject placementRoot, List<string> overlapNames)
    {
        var stoneBlockers = CollectStoneBlockers(placementRoot.transform);
        var overlaps = 0;
        foreach (Transform child in placementRoot.transform)
        {
            if (TryGetHorizontalOverlap(child.gameObject, stoneBlockers, out _))
            {
                overlaps++;
                overlapNames.Add(child.name);
            }
        }

        return overlaps;
    }

    private static Vector3 SnapToGround(Vector3 fallbackPosition)
    {
        var origin = fallbackPosition + Vector3.up * 30f;
        var hits = Physics.RaycastAll(origin, Vector3.down, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.GetComponentInParent<ParticleSystem>() != null)
            {
                continue;
            }

            if (hit.normal.y < 0.35f)
            {
                continue;
            }

            return hit.point + Vector3.up * 0.025f;
        }

        return fallbackPosition;
    }

    private static void ConfigureRenderers(GameObject instance)
    {
        foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
        }
    }

    private static void FrameSceneView(GameObject root)
    {
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            return;
        }

        Selection.activeGameObject = root;
        sceneView.FrameSelected();
        sceneView.Repaint();
    }

    private static string GetPath(Transform transform)
    {
        var names = new Stack<string>();
        while (transform != null)
        {
            names.Push(transform.name);
            transform = transform.parent;
        }

        return string.Join("/", names);
    }

    private readonly struct AnchorPlacement
    {
        public AnchorPlacement(string name, string anchorName, string prefabPath, Vector2 offset, float yawOffset, float scale)
        {
            Name = name;
            AnchorName = anchorName;
            PrefabPath = prefabPath;
            Offset = offset;
            YawOffset = yawOffset;
            Scale = scale;
        }

        public string Name { get; }
        public string AnchorName { get; }
        public string PrefabPath { get; }
        public Vector2 Offset { get; }
        public float YawOffset { get; }
        public float Scale { get; }
    }
}
#endif
