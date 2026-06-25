#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class TownForestWeedEnvironmentScatter
{
    private const string ScenePath = "Assets/0.MainProject/Scenes/Town/Town_Forest.unity";
    private const string WeedSourceRootName = "Weed";
    private const string PlacementRootName = "Codex_Environment_Weed_Scatter";
    private const int TargetWeedClusterCount = 420;
    private const int RandomSeed = 20260530;
    private const float MinClusterSpacing = 0.62f;
    private const float DefaultWeedY = -1.0f;

    [MenuItem("RhythmRPG/Editors/World/Scatter Weed Across Town Forest")]
    public static void Scatter()
    {
        var scene = EnsureTownForestScene();
        var weedRoot = GameObject.Find(WeedSourceRootName);
        if (weedRoot == null)
        {
            Debug.LogError("[TownForestWeedEnvironmentScatter] Missing Weed root.");
            return;
        }

        ClearPreviousPlacement(weedRoot.transform);

        var sourceWeeds = CollectSourceWeeds(weedRoot.transform);
        var environmentPoints = CollectEnvironmentPoints(scene, weedRoot.transform);
        var anchors = CollectNaturalAnchors(scene, weedRoot.transform);
        var blockers = CollectBlockers(scene, weedRoot.transform);
        if (sourceWeeds.Count == 0 || environmentPoints.Count < 12 || anchors.Count < 8)
        {
            Debug.LogError(
                "[TownForestWeedEnvironmentScatter] Not enough data to scatter weeds. " +
                $"Sources={sourceWeeds.Count}, EnvironmentPoints={environmentPoints.Count}, Anchors={anchors.Count}");
            return;
        }

        var bounds = CalculateBounds(environmentPoints);
        var root = new GameObject(PlacementRootName);
        root.transform.SetParent(weedRoot.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        var rng = new System.Random(RandomSeed);
        var candidates = BuildAnchorCandidates(anchors, rng);
        candidates.AddRange(BuildAmbientCandidates(bounds, environmentPoints, anchors, rng));
        Shuffle(candidates, rng);

        var placedPositions = new List<Vector2>();
        var usageCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var placedCount = 0;

        foreach (var candidate in candidates)
        {
            if (placedCount >= TargetWeedClusterCount)
            {
                break;
            }

            if (!CanPlaceAt(candidate, bounds, environmentPoints, anchors, blockers, placedPositions))
            {
                continue;
            }

            PlaceWeedInstance(sourceWeeds, root.transform, candidate, rng, placedCount, usageCounts, weedRoot.transform);
            placedPositions.Add(candidate.Position);
            placedCount++;
        }

        FillRemaining(sourceWeeds, root.transform, bounds, environmentPoints, anchors, blockers, placedPositions, rng, usageCounts, weedRoot.transform, ref placedCount);

        Selection.activeGameObject = root;
        FrameSceneView(root);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[TownForestWeedEnvironmentScatter] Weed scatter complete. " +
            $"Scene={scene.name}, Root={GetPath(root.transform)}, WeedClusters={placedCount}, " +
            $"Renderers={root.GetComponentsInChildren<MeshRenderer>(true).Length}, " +
            $"Sources={sourceWeeds.Count}, Anchors={anchors.Count}, Bounds={bounds}, " +
            $"Usage={string.Join(", ", usageCounts.Select(kvp => kvp.Key + ":" + kvp.Value))}");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Weed Environment Scatter")]
    public static void Validate()
    {
        var root = GameObject.Find(PlacementRootName);
        if (root == null)
        {
            Debug.LogError("[TownForestWeedEnvironmentScatter] Validation failed: scatter root not found.");
            return;
        }

        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        if (root.transform.childCount < TargetWeedClusterCount || renderers.Length < TargetWeedClusterCount)
        {
            Debug.LogError(
                "[TownForestWeedEnvironmentScatter] Validation failed: not enough weed clusters/renderers. " +
                $"Children={root.transform.childCount}, Renderers={renderers.Length}, Expected={TargetWeedClusterCount}");
            return;
        }

        var riverOverlapCount = 0;
        foreach (Transform child in root.transform)
        {
            var point = new Vector2(child.position.x, child.position.z);
            if (IsInsideRiver(point, 3.18f))
            {
                riverOverlapCount++;
            }
        }

        if (riverOverlapCount > 0)
        {
            Debug.LogError($"[TownForestWeedEnvironmentScatter] Validation failed: {riverOverlapCount} clusters are inside river water.");
            return;
        }

        Debug.Log(
            "[TownForestWeedEnvironmentScatter] VALIDATION OK. " +
            $"Root={GetPath(root.transform)}, Children={root.transform.childCount}, Renderers={renderers.Length}.");
    }

    private static Scene EnsureTownForestScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.path == ScenePath)
        {
            return scene;
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void ClearPreviousPlacement(Transform weedRoot)
    {
        var oldRoot = FindDirectChild(weedRoot, PlacementRootName);
        if (oldRoot != null)
        {
            Object.DestroyImmediate(oldRoot.gameObject);
        }
    }

    private static List<GameObject> CollectSourceWeeds(Transform weedRoot)
    {
        var sources = new List<GameObject>();
        foreach (Transform child in weedRoot)
        {
            if (child.name == PlacementRootName)
            {
                continue;
            }

            if (child.GetComponentInChildren<MeshRenderer>(true) != null)
            {
                sources.Add(child.gameObject);
            }
        }

        return sources;
    }

    private static List<Vector2> CollectEnvironmentPoints(Scene scene, Transform weedRoot)
    {
        var points = new List<Vector2>();
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!IsUsableSceneRenderer(renderer, scene, weedRoot))
            {
                continue;
            }

            var position = renderer.transform.position;
            if (Mathf.Abs(position.x) > 180f || Mathf.Abs(position.z) > 180f)
            {
                continue;
            }

            points.Add(new Vector2(position.x, position.z));
        }

        return points;
    }

    private static List<AnchorPoint> CollectNaturalAnchors(Scene scene, Transform weedRoot)
    {
        var anchors = new List<AnchorPoint>();
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!IsUsableSceneRenderer(renderer, scene, weedRoot))
            {
                continue;
            }

            if (!TryClassifyAnchor(renderer.transform, out var category, out var minRadius, out var maxRadius, out var blockerRadius))
            {
                continue;
            }

            var position = renderer.transform.position;
            anchors.Add(new AnchorPoint(
                renderer.name,
                category,
                new Vector2(position.x, position.z),
                minRadius,
                maxRadius,
                blockerRadius));
        }

        return DeduplicateAnchors(anchors);
    }

    private static List<BlockerDisc> CollectBlockers(Scene scene, Transform weedRoot)
    {
        var blockers = new List<BlockerDisc>();
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!IsUsableSceneRenderer(renderer, scene, weedRoot))
            {
                continue;
            }

            if (!TryClassifyAnchor(renderer.transform, out _, out _, out _, out var blockerRadius))
            {
                continue;
            }

            if (blockerRadius <= 0f)
            {
                continue;
            }

            var position = renderer.transform.position;
            blockers.Add(new BlockerDisc(new Vector2(position.x, position.z), blockerRadius));
        }

        return blockers;
    }

    private static bool IsUsableSceneRenderer(MeshRenderer renderer, Scene scene, Transform weedRoot)
    {
        if (renderer == null || !renderer.gameObject.activeInHierarchy || renderer.gameObject.scene != scene)
        {
            return false;
        }

        if (renderer.transform.IsChildOf(weedRoot))
        {
            return false;
        }

        if (renderer.GetComponentInParent<ParticleSystem>() != null)
        {
            return false;
        }

        var name = GetHierarchyName(renderer.transform);
        return !ContainsAny(name, "Canvas", "UI", "Camera", "Light", "Fog", "Sky", "Waterfall", "Mist", "FlowLine", "River_Water");
    }

    private static bool TryClassifyAnchor(Transform transform, out AnchorCategory category, out float minRadius, out float maxRadius, out float blockerRadius)
    {
        var name = GetHierarchyName(transform);

        if (ContainsAny(name, "Pine", "Tree"))
        {
            category = AnchorCategory.Tree;
            minRadius = 0.75f;
            maxRadius = 2.75f;
            blockerRadius = 0.55f;
            return true;
        }

        if (ContainsAny(name, "Stone", "Boulder", "Glyph", "Rock", "Monolith"))
        {
            category = AnchorCategory.Stone;
            minRadius = 0.48f;
            maxRadius = 1.9f;
            blockerRadius = 0.68f;
            return true;
        }

        if (ContainsAny(name, "Log", "Fence", "Sign", "Lantern", "Bridge", "Barrel", "Cottage", "Forge", "Anvil", "Workbench"))
        {
            category = AnchorCategory.Prop;
            minRadius = 0.45f;
            maxRadius = 1.65f;
            blockerRadius = 0.52f;
            return true;
        }

        category = AnchorCategory.Open;
        minRadius = 0f;
        maxRadius = 0f;
        blockerRadius = 0f;
        return false;
    }

    private static List<AnchorPoint> DeduplicateAnchors(List<AnchorPoint> anchors)
    {
        var result = new List<AnchorPoint>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var anchor in anchors.OrderBy(anchor => anchor.Name, StringComparer.Ordinal))
        {
            var key = $"{Mathf.RoundToInt(anchor.Position.x * 3f)}:{Mathf.RoundToInt(anchor.Position.y * 3f)}:{anchor.Category}";
            if (keys.Add(key))
            {
                result.Add(anchor);
            }
        }

        return result;
    }

    private static PlacementBounds CalculateBounds(IReadOnlyList<Vector2> points)
    {
        var minX = points.Min(point => point.x) - 2.0f;
        var maxX = points.Max(point => point.x) + 2.0f;
        var minZ = points.Min(point => point.y) - 2.0f;
        var maxZ = points.Max(point => point.y) + 2.0f;
        return new PlacementBounds(minX, maxX, minZ, maxZ);
    }

    private static List<WeedCandidate> BuildAnchorCandidates(IReadOnlyList<AnchorPoint> anchors, System.Random rng)
    {
        var candidates = new List<WeedCandidate>();
        foreach (var anchor in anchors)
        {
            var count = anchor.Category switch
            {
                AnchorCategory.Tree => rng.Next(3, 6),
                AnchorCategory.Stone => rng.Next(2, 5),
                AnchorCategory.Prop => rng.Next(1, 4),
                _ => 1
            };

            for (var i = 0; i < count; i++)
            {
                var offset = RandomUnitVector(rng) * RandomRange(rng, anchor.MinRadius, anchor.MaxRadius);
                candidates.Add(new WeedCandidate(anchor.Position + offset, anchor.Category, 1.0f));
            }
        }

        return candidates;
    }

    private static List<WeedCandidate> BuildAmbientCandidates(
        PlacementBounds bounds,
        IReadOnlyList<Vector2> environmentPoints,
        IReadOnlyList<AnchorPoint> anchors,
        System.Random rng)
    {
        var candidates = new List<WeedCandidate>();
        const float step = 2.15f;
        for (var x = bounds.MinX; x <= bounds.MaxX; x += step)
        {
            for (var z = bounds.MinZ; z <= bounds.MaxZ; z += step)
            {
                if (rng.NextDouble() > 0.34)
                {
                    continue;
                }

                var point = new Vector2(
                    x + RandomRange(rng, -0.88f, 0.88f),
                    z + RandomRange(rng, -0.88f, 0.88f));

                if (DistanceToNearest(point, environmentPoints) > 6.0f && DistanceToNearestAnchor(point, anchors) > 7.5f)
                {
                    continue;
                }

                candidates.Add(new WeedCandidate(point, AnchorCategory.Open, 0.82f));
            }
        }

        return candidates;
    }

    private static void FillRemaining(
        IReadOnlyList<GameObject> sourceWeeds,
        Transform root,
        PlacementBounds bounds,
        IReadOnlyList<Vector2> environmentPoints,
        IReadOnlyList<AnchorPoint> anchors,
        IReadOnlyList<BlockerDisc> blockers,
        List<Vector2> placedPositions,
        System.Random rng,
        Dictionary<string, int> usageCounts,
        Transform weedRoot,
        ref int placedCount)
    {
        var attempts = 0;
        while (placedCount < TargetWeedClusterCount && attempts < TargetWeedClusterCount * 80)
        {
            attempts++;
            var anchor = anchors[rng.Next(anchors.Count)];
            var point = anchor.Position + RandomUnitVector(rng) * RandomRange(rng, anchor.MinRadius, anchor.MaxRadius + 0.9f);
            var candidate = new WeedCandidate(point, anchor.Category, 0.9f);
            if (!CanPlaceAt(candidate, bounds, environmentPoints, anchors, blockers, placedPositions))
            {
                continue;
            }

            PlaceWeedInstance(sourceWeeds, root, candidate, rng, placedCount, usageCounts, weedRoot);
            placedPositions.Add(candidate.Position);
            placedCount++;
        }
    }

    private static bool CanPlaceAt(
        WeedCandidate candidate,
        PlacementBounds bounds,
        IReadOnlyList<Vector2> environmentPoints,
        IReadOnlyList<AnchorPoint> anchors,
        IReadOnlyList<BlockerDisc> blockers,
        IReadOnlyList<Vector2> placedPositions)
    {
        var position = candidate.Position;
        if (!bounds.Contains(position))
        {
            return false;
        }

        if (IsInsideRiver(position, 3.22f))
        {
            return false;
        }

        if (DistanceToNearest(position, environmentPoints) > 7.5f && DistanceToNearestAnchor(position, anchors) > 7.5f)
        {
            return false;
        }

        foreach (var blocker in blockers)
        {
            if (Vector2.Distance(position, blocker.Position) < blocker.Radius)
            {
                return false;
            }
        }

        var spacing = MinClusterSpacing * candidate.SpacingMultiplier;
        foreach (var placedPosition in placedPositions)
        {
            if (Vector2.Distance(position, placedPosition) < spacing)
            {
                return false;
            }
        }

        return true;
    }

    private static void PlaceWeedInstance(
        IReadOnlyList<GameObject> sourceWeeds,
        Transform root,
        WeedCandidate candidate,
        System.Random rng,
        int index,
        Dictionary<string, int> usageCounts,
        Transform weedRoot)
    {
        var source = PickSourceWeed(sourceWeeds, candidate, rng);
        var instance = Object.Instantiate(source, root);
        var label = GetSourceLabel(source.name);
        instance.name = $"ForestWeed_{index + 1:000}_{label}";

        var worldPosition = new Vector3(candidate.Position.x, GetFallbackY(source), candidate.Position.y);
        worldPosition = SnapToGround(worldPosition, weedRoot);
        worldPosition.y += RandomRange(rng, -0.012f, 0.028f);

        instance.transform.position = worldPosition;
        instance.transform.rotation = Quaternion.Euler(0f, RandomRange(rng, 0f, 360f), 0f);
        instance.transform.localScale = source.transform.localScale * GetScaleMultiplier(source.name, candidate.Category, rng);
        ConfigureRenderers(instance);
        MarkStatic(instance);

        if (!usageCounts.ContainsKey(label))
        {
            usageCounts[label] = 0;
        }

        usageCounts[label]++;
    }

    private static GameObject PickSourceWeed(IReadOnlyList<GameObject> sourceWeeds, WeedCandidate candidate, System.Random rng)
    {
        var roll = rng.NextDouble();
        if (candidate.Category == AnchorCategory.Tree || candidate.Category == AnchorCategory.Stone)
        {
            if (roll < 0.42)
            {
                return PickByName(sourceWeeds, "Patch", rng);
            }

            if (roll < 0.66)
            {
                return PickByName(sourceWeeds, "Medium", rng);
            }

            if (roll < 0.86)
            {
                return PickByName(sourceWeeds, "Short", rng);
            }

            return PickByName(sourceWeeds, "Tall", rng);
        }

        if (roll < 0.22)
        {
            return PickByName(sourceWeeds, "Patch", rng);
        }

        if (roll < 0.60)
        {
            return PickByName(sourceWeeds, "Short", rng);
        }

        if (roll < 0.86)
        {
            return PickByName(sourceWeeds, "Medium", rng);
        }

        return PickByName(sourceWeeds, "Tall", rng);
    }

    private static GameObject PickByName(IReadOnlyList<GameObject> sourceWeeds, string keyword, System.Random rng)
    {
        var matches = sourceWeeds
            .Where(source => source.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToArray();
        return matches.Length > 0 ? matches[rng.Next(matches.Length)] : sourceWeeds[rng.Next(sourceWeeds.Count)];
    }

    private static float GetScaleMultiplier(string sourceName, AnchorCategory category, System.Random rng)
    {
        var categoryBoost = category == AnchorCategory.Open ? 0.94f : 1.0f;
        if (sourceName.IndexOf("Patch", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return categoryBoost * RandomRange(rng, 0.72f, 1.08f);
        }

        if (sourceName.IndexOf("Tall", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return categoryBoost * RandomRange(rng, 0.64f, 0.95f);
        }

        if (sourceName.IndexOf("Medium", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return categoryBoost * RandomRange(rng, 0.74f, 1.12f);
        }

        return categoryBoost * RandomRange(rng, 0.82f, 1.24f);
    }

    private static Vector3 SnapToGround(Vector3 fallbackPosition, Transform weedRoot)
    {
        var origin = fallbackPosition + Vector3.up * 30f;
        var hits = Physics.RaycastAll(origin, Vector3.down, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            var hitTransform = hit.collider.transform;
            if (hitTransform.IsChildOf(weedRoot) || hit.collider.GetComponentInParent<ParticleSystem>() != null)
            {
                continue;
            }

            if (ContainsAny(GetHierarchyName(hitTransform), "River_Water", "Waterfall", "Mist"))
            {
                continue;
            }

            if (hit.normal.y < 0.35f)
            {
                continue;
            }

            return hit.point + Vector3.up * 0.018f;
        }

        return fallbackPosition;
    }

    private static bool IsInsideRiver(Vector2 point, float halfWidth)
    {
        var controlPoints = new[]
        {
            new Vector2(-7.5f, 3.0f),
            new Vector2(15.0f, 3.0f),
            new Vector2(33.23f, 0.0f),
            new Vector2(42.0f, -3.0f),
            new Vector2(50.0f, 0.0f),
            new Vector2(60.5f, 0.0f)
        };

        for (var i = 1; i < controlPoints.Length; i++)
        {
            if (DistanceToSegment(point, controlPoints[i - 1], controlPoints[i]) <= halfWidth)
            {
                return true;
            }
        }

        return false;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var segment = b - a;
        var segmentLength = segment.sqrMagnitude;
        if (segmentLength < 0.0001f)
        {
            return Vector2.Distance(point, a);
        }

        var t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / segmentLength);
        return Vector2.Distance(point, a + segment * t);
    }

    private static float DistanceToNearest(Vector2 point, IReadOnlyList<Vector2> points)
    {
        var nearest = float.MaxValue;
        foreach (var other in points)
        {
            nearest = Mathf.Min(nearest, Vector2.Distance(point, other));
        }

        return nearest;
    }

    private static float DistanceToNearestAnchor(Vector2 point, IReadOnlyList<AnchorPoint> anchors)
    {
        var nearest = float.MaxValue;
        foreach (var anchor in anchors)
        {
            nearest = Mathf.Min(nearest, Vector2.Distance(point, anchor.Position));
        }

        return nearest;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static float GetFallbackY(GameObject source)
    {
        var y = source.transform.position.y;
        return Mathf.Abs(y) < 0.001f ? DefaultWeedY : y;
    }

    private static string GetSourceLabel(string sourceName)
    {
        if (sourceName.IndexOf("Patch", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Patch";
        }

        if (sourceName.IndexOf("Tall", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Tall";
        }

        if (sourceName.IndexOf("Medium", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Medium";
        }

        return "Short";
    }

    private static void ConfigureRenderers(GameObject instance)
    {
        foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
        }
    }

    private static void MarkStatic(GameObject instance)
    {
        foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(transform.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetHierarchyName(Transform transform)
    {
        var names = new Stack<string>();
        var current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static Vector2 RandomUnitVector(System.Random rng)
    {
        var angle = RandomRange(rng, 0f, Mathf.PI * 2f);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static float RandomRange(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    private static void Shuffle<T>(IList<T> items, System.Random rng)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
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
        sceneView.Frame(new Bounds(root.transform.position, new Vector3(62f, 9f, 62f)), false);
        sceneView.Repaint();
    }

    private static string GetPath(Transform transform)
    {
        var names = new Stack<string>();
        var current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private readonly struct AnchorPoint
    {
        public readonly string Name;
        public readonly AnchorCategory Category;
        public readonly Vector2 Position;
        public readonly float MinRadius;
        public readonly float MaxRadius;
        public readonly float BlockerRadius;

        public AnchorPoint(string name, AnchorCategory category, Vector2 position, float minRadius, float maxRadius, float blockerRadius)
        {
            Name = name;
            Category = category;
            Position = position;
            MinRadius = minRadius;
            MaxRadius = maxRadius;
            BlockerRadius = blockerRadius;
        }
    }

    private readonly struct WeedCandidate
    {
        public readonly Vector2 Position;
        public readonly AnchorCategory Category;
        public readonly float SpacingMultiplier;

        public WeedCandidate(Vector2 position, AnchorCategory category, float spacingMultiplier)
        {
            Position = position;
            Category = category;
            SpacingMultiplier = spacingMultiplier;
        }
    }

    private readonly struct BlockerDisc
    {
        public readonly Vector2 Position;
        public readonly float Radius;

        public BlockerDisc(Vector2 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    private readonly struct PlacementBounds
    {
        public readonly float MinX;
        public readonly float MaxX;
        public readonly float MinZ;
        public readonly float MaxZ;

        public PlacementBounds(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public bool Contains(Vector2 point)
        {
            return point.x >= MinX && point.x <= MaxX && point.y >= MinZ && point.y <= MaxZ;
        }

        public override string ToString()
        {
            return $"x[{MinX:F1},{MaxX:F1}] z[{MinZ:F1},{MaxZ:F1}]";
        }
    }

    private enum AnchorCategory
    {
        Open,
        Tree,
        Stone,
        Prop
    }
}
#endif
