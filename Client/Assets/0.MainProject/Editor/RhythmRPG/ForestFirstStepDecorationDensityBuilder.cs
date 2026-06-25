#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ForestFirstStepDecorationDensityBuilder
{
    private const string SourceScenePath = "Assets/0.MainProject/Scenes/Game/Game_Forest_Tutorial.unity";
    private const string TargetScenePath = "Assets/0.MainProject/Scenes/Game/Game_Forest_First_Step.unity";
    private const string SourceMapPath = "Assets/Resources/Data/Map/Game_Forest_Tutorial.asset";
    private const string TargetMapPath = "Assets/Resources/Data/Map/Game_Forest_First_Step.asset";
    private const string EnvironmentRootName = "Forest_Decoration_Set";
    private const int PlacementSeed = 20260608;
    private const float GroundY = 0f;

    private static readonly string[] PlacementGroupNames =
    {
        "Props_WallBackfill",
        "Props_WallEdge",
        "Props_WallAccents",
        "LowPolyWeed_WallEdge"
    };

    private static readonly string[] SharedLightingGroupNames =
    {
        "Samples_ForestVisual",
        "Showcase_LightingPipeline",
        "FX_FullscreenDepthFog"
    };

    [MenuItem("RhythmRPG/Editors/World/Build Forest First Step Decoration Density")]
    public static void Build()
    {
        var sourceMap = AssetDatabase.LoadAssetAtPath<MapAsset>(SourceMapPath);
        var targetMap = AssetDatabase.LoadAssetAtPath<MapAsset>(TargetMapPath);
        if (sourceMap == null || targetMap == null)
        {
            Debug.LogError(
                "[ForestFirstStepDecorationDensityBuilder] Missing map asset. " +
                $"Source={(sourceMap != null)}, Target={(targetMap != null)}.");
            return;
        }

        var targetScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        RemoveExistingRoot(targetScene);

        var sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
        var sourceRoot = FindRoot(sourceScene, EnvironmentRootName);
        if (sourceRoot == null)
        {
            Debug.LogError("[ForestFirstStepDecorationDensityBuilder] Missing source root: " + EnvironmentRootName);
            EditorSceneManager.CloseScene(sourceScene, true);
            return;
        }

        var library = BuildSourceLibrary(sourceRoot.transform);
        if (!library.HasRequiredSources)
        {
            Debug.LogError(
                "[ForestFirstStepDecorationDensityBuilder] Source decoration library is incomplete. " +
                $"Back={library.BackfillProps.Count}, Edge={library.EdgeProps.Count}, " +
                $"Accent={library.AccentProps.Count}, Weed={library.Weeds.Count}.");
            EditorSceneManager.CloseScene(sourceScene, true);
            return;
        }

        var tileMap = new TileMap(targetMap);
        var sourceWalkableCount = Mathf.Max(1, CountWalkable(sourceMap));
        var targetWalkableCount = Mathf.Max(1, CountWalkable(targetMap));
        var lightingOffset = GetWalkableBounds(targetMap).Center3 - GetWalkableBounds(sourceMap).Center3;
        var densityScale = Mathf.Clamp(targetWalkableCount / (float)sourceWalkableCount, 1f, 2.25f);
        var counts = new PlacementCounts(
            Mathf.RoundToInt(42f * densityScale),
            Mathf.RoundToInt(44f * densityScale),
            Mathf.RoundToInt(12f * densityScale),
            Mathf.RoundToInt(78f * densityScale));

        var targetRoot = CreateRoot(targetScene);
        var rng = new System.Random(PlacementSeed);
        var placed = new List<Vector3>();
        var sharedGroups = CopySharedLightingGroups(sourceRoot.transform, targetRoot.transform, targetScene, lightingOffset);

        var backfillGroup = CreateChildGroup(targetRoot.transform, "Props_WallBackfill");
        var edgeGroup = CreateChildGroup(targetRoot.transform, "Props_WallEdge");
        var accentGroup = CreateChildGroup(targetRoot.transform, "Props_WallAccents");
        var weedGroup = CreateChildGroup(targetRoot.transform, "LowPolyWeed_WallEdge");

        var accentPlaced = PlaceFromCandidates(
            targetScene,
            accentGroup,
            library.AccentProps,
            tileMap.GetWallCandidates(minDistanceToWalkable: 1, maxDistanceToWalkable: 3, requireFourWayEdge: true),
            counts.AccentProps,
            placed,
            rng,
            new PlacementStyle(2.25f, 0.14f, 0.36f, 0.18f, 0.92f, 1.15f, true));

        var backfillPlaced = PlaceFromCandidates(
            targetScene,
            backfillGroup,
            library.BackfillProps,
            tileMap.GetWallCandidates(minDistanceToWalkable: 2, maxDistanceToWalkable: 5, requireFourWayEdge: false),
            counts.BackfillProps,
            placed,
            rng,
            new PlacementStyle(1.75f, 0.36f, 0.08f, 0.10f, 0.92f, 1.22f, false));

        var edgePlaced = PlaceFromCandidates(
            targetScene,
            edgeGroup,
            library.EdgeProps,
            tileMap.GetWallCandidates(minDistanceToWalkable: 1, maxDistanceToWalkable: 2, requireFourWayEdge: true),
            counts.EdgeProps,
            placed,
            rng,
            new PlacementStyle(1.35f, 0.18f, 0.28f, 0.28f, 0.76f, 1.05f, true));

        var weedPlaced = PlaceFromCandidates(
            targetScene,
            weedGroup,
            library.Weeds,
            tileMap.GetWallCandidates(minDistanceToWalkable: 1, maxDistanceToWalkable: 1, requireFourWayEdge: true),
            counts.Weeds,
            placed,
            rng,
            new PlacementStyle(0.62f, 0.32f, 0.16f, 0.58f, 0.64f, 1.05f, true));

        SortEnvironmentChildren(targetRoot.transform);
        EditorSceneManager.CloseScene(sourceScene, true);

        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = targetRoot;
        Debug.Log(
            "[ForestFirstStepDecorationDensityBuilder] Built map-aware decoration density. " +
            $"Scene={targetScene.name}, SharedLightingGroups={sharedGroups}, Backfill={backfillPlaced}/{counts.BackfillProps}, " +
            $"Edge={edgePlaced}/{counts.EdgeProps}, Accent={accentPlaced}/{counts.AccentProps}, " +
            $"Weed={weedPlaced}/{counts.Weeds}, Renderers={targetRoot.GetComponentsInChildren<Renderer>(true).Length}, " +
            $"SourceWalkable={sourceWalkableCount}, TargetWalkable={targetWalkableCount}, DensityScale={densityScale:0.00}.");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Forest First Step Decoration Density")]
    public static void Validate()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != TargetScenePath)
        {
            scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        }

        var targetMap = AssetDatabase.LoadAssetAtPath<MapAsset>(TargetMapPath);
        var root = FindRoot(scene, EnvironmentRootName);
        if (root == null || targetMap == null)
        {
            Debug.LogError(
                "[ForestFirstStepDecorationDensityBuilder] Validation failed: " +
                $"Root={(root != null)}, TargetMap={(targetMap != null)}.");
            return;
        }

        var tileMap = new TileMap(targetMap);
        var missingGroups = PlacementGroupNames.Count(groupName => FindDirectChild(root.transform, groupName) == null);
        var invalidTilePlacements = CountInvalidTilePlacements(root.transform, tileMap);
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var placedRoots = CountDirectPlacedObjects(root.transform);

        if (missingGroups > 0 || invalidTilePlacements > 0 || placedRoots < 240 || renderers.Length == 0)
        {
            Debug.LogError(
                "[ForestFirstStepDecorationDensityBuilder] Validation failed. " +
                $"MissingGroups={missingGroups}, InvalidTilePlacements={invalidTilePlacements}, " +
                $"PlacedRoots={placedRoots}, Renderers={renderers.Length}.");
            return;
        }

        Debug.Log(
            "[ForestFirstStepDecorationDensityBuilder] VALIDATION OK. " +
            $"Groups={PlacementGroupNames.Length}, PlacedRoots={placedRoots}, Renderers={renderers.Length}, " +
            $"InvalidTilePlacements={invalidTilePlacements}.");
    }

    private static SourceLibrary BuildSourceLibrary(Transform sourceRoot)
    {
        var props = new List<GameObject>();
        var generated = new List<GameObject>();
        var weeds = new List<GameObject>();

        var propsRoot = FindDirectChild(sourceRoot, "Props_HandPlaced");
        if (propsRoot != null)
        {
            props.AddRange(GetRenderableDirectChildren(propsRoot));
        }

        var generatedRoot = FindDirectChild(sourceRoot, "Generated_Meshy_Props");
        if (generatedRoot != null)
        {
            foreach (Transform category in generatedRoot)
            {
                generated.AddRange(GetRenderableDirectChildren(category));
            }
        }

        foreach (var weedRoot in FindDirectChildren(sourceRoot, "LowPolyWeed_Placement"))
        {
            weeds.AddRange(GetRenderableDirectChildren(weedRoot));
        }

        var backfill = generated
            .Concat(props.Where(go => ContainsAny(go.name, "Pine", "Tree", "Boulder", "Monolith", "Faceted", "Stone_Stack")))
            .Distinct()
            .ToList();
        var edge = props
            .Where(go => ContainsAny(go.name, "Stone", "Boulder", "Glyph", "Log", "Sign", "Fence"))
            .Concat(generated.Where(go => ContainsAny(go.name, "Stone", "Rock", "Fence", "Monolith")))
            .Distinct()
            .ToList();
        var accent = props
            .Where(go => ContainsAny(go.name, "Log", "Sign", "Glyph", "Monolith", "Origami_Pine"))
            .Concat(generated.Where(go => ContainsAny(go.name, "Monolith", "Fence")))
            .Distinct()
            .ToList();

        if (backfill.Count == 0)
        {
            backfill.AddRange(props.Concat(generated));
        }

        if (edge.Count == 0)
        {
            edge.AddRange(props.Concat(generated));
        }

        if (accent.Count == 0)
        {
            accent.AddRange(edge);
        }

        return new SourceLibrary(backfill, edge, accent, weeds);
    }

    private static int PlaceFromCandidates(
        Scene targetScene,
        Transform parent,
        IReadOnlyList<GameObject> sources,
        IEnumerable<TileCandidate> candidates,
        int targetCount,
        List<Vector3> placed,
        System.Random rng,
        PlacementStyle style)
    {
        var shuffledCandidates = candidates.OrderBy(_ => rng.Next()).ToList();
        var placedCount = 0;
        var sourceIndex = rng.Next(sources.Count);

        foreach (var candidate in shuffledCandidates)
        {
            if (placedCount >= targetCount)
            {
                break;
            }

            var position = GetPlacementPosition(candidate, rng, style);
            if (!IsFarEnough(position, placed, style.MinSpacing))
            {
                continue;
            }

            var source = sources[sourceIndex % sources.Count];
            sourceIndex++;

            var clone = Object.Instantiate(source);
            StripCloneSuffix(clone.transform);
            SceneManager.MoveGameObjectToScene(clone, targetScene);
            clone.transform.SetParent(parent, true);
            clone.transform.position = position;
            clone.transform.rotation = GetPlacementRotation(candidate, rng, style, source.transform.rotation);
            clone.transform.localScale = ScaleVector(source.transform.localScale, Lerp(style.MinScale, style.MaxScale, (float)rng.NextDouble()));
            placed.Add(position);
            placedCount++;
        }

        return placedCount;
    }

    private static Vector3 GetPlacementPosition(TileCandidate candidate, System.Random rng, PlacementStyle style)
    {
        var normal = candidate.Normal;
        var tangent = new Vector2(-normal.y, normal.x);
        var normalOffset = style.AlignToWallNormal ? style.NormalOffset : 0f;
        var tangentJitter = Lerp(-style.TangentJitter, style.TangentJitter, (float)rng.NextDouble());
        var cellJitter = new Vector2(
            Lerp(-style.CellJitter, style.CellJitter, (float)rng.NextDouble()),
            Lerp(-style.CellJitter, style.CellJitter, (float)rng.NextDouble()));
        var position2 = new Vector2(candidate.X, candidate.Y) +
            normal * normalOffset +
            tangent * tangentJitter +
            cellJitter;
        position2.x = Mathf.Clamp(position2.x, candidate.X - 0.45f, candidate.X + 0.45f);
        position2.y = Mathf.Clamp(position2.y, candidate.Y - 0.45f, candidate.Y + 0.45f);

        return new Vector3(position2.x, GroundY, position2.y);
    }

    private static Quaternion GetPlacementRotation(TileCandidate candidate, System.Random rng, PlacementStyle style, Quaternion sourceRotation)
    {
        var yaw = style.AlignToWallNormal
            ? Mathf.Atan2(candidate.Normal.x, candidate.Normal.y) * Mathf.Rad2Deg
            : Lerp(0f, 360f, (float)rng.NextDouble());
        yaw += Lerp(-style.YawJitter, style.YawJitter, (float)rng.NextDouble());
        return Quaternion.AngleAxis(yaw, Vector3.up) * sourceRotation;
    }

    private static bool IsFarEnough(Vector3 position, IReadOnlyList<Vector3> placed, float minSpacing)
    {
        var minSqr = minSpacing * minSpacing;
        foreach (var previous in placed)
        {
            if ((previous - position).sqrMagnitude < minSqr)
            {
                return false;
            }
        }

        return true;
    }

    private static GameObject CreateRoot(Scene scene)
    {
        var root = new GameObject(EnvironmentRootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        return root;
    }

    private static Transform CreateChildGroup(Transform parent, string name)
    {
        var group = new GameObject(name);
        group.transform.SetParent(parent, false);
        group.transform.localPosition = Vector3.zero;
        group.transform.localRotation = Quaternion.identity;
        group.transform.localScale = Vector3.one;
        return group.transform;
    }

    private static int CopySharedLightingGroups(
        Transform sourceRoot,
        Transform targetRoot,
        Scene targetScene,
        Vector3 offset)
    {
        var copied = 0;
        foreach (var groupName in SharedLightingGroupNames)
        {
            var sourceGroup = FindDirectChild(sourceRoot, groupName);
            if (sourceGroup == null)
            {
                Debug.LogWarning("[ForestFirstStepDecorationDensityBuilder] Shared lighting group not found: " + groupName);
                continue;
            }

            var clone = Object.Instantiate(sourceGroup.gameObject);
            StripCloneSuffix(clone.transform);
            SceneManager.MoveGameObjectToScene(clone, targetScene);
            clone.transform.SetParent(targetRoot, true);
            clone.transform.position += offset;
            clone.name = groupName;
            copied++;
        }

        return copied;
    }

    private static IEnumerable<GameObject> GetRenderableDirectChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                yield return child.gameObject;
            }
        }
    }

    private static void StripCloneSuffix(Transform transform)
    {
        const string cloneSuffix = "(Clone)";
        if (transform.name.EndsWith(cloneSuffix, StringComparison.Ordinal))
        {
            transform.name = transform.name[..^cloneSuffix.Length].TrimEnd();
        }

        foreach (Transform child in transform)
        {
            StripCloneSuffix(child);
        }
    }

    private static void RemoveExistingRoot(Scene scene)
    {
        var root = FindRoot(scene, EnvironmentRootName);
        if (root != null)
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
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

    private static IEnumerable<Transform> FindDirectChildren(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                yield return child;
            }
        }
    }

    private static int CountWalkable(MapAsset map)
    {
        map.EnsureSize();
        var count = 0;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var kind = map.Get(x, y).Kind;
                if (kind == TileKind.Floor || kind == TileKind.Spawn)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static WalkableBounds GetWalkableBounds(MapAsset map)
    {
        map.EnsureSize();
        var minX = map.Width - 1;
        var minY = map.Height - 1;
        var maxX = 0;
        var maxY = 0;
        var found = false;

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var kind = map.Get(x, y).Kind;
                if (kind != TileKind.Floor && kind != TileKind.Spawn)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
                found = true;
            }
        }

        return found
            ? new WalkableBounds(minX, minY, maxX, maxY)
            : new WalkableBounds(0, 0, map.Width - 1, map.Height - 1);
    }

    private static int CountInvalidTilePlacements(Transform root, TileMap tileMap)
    {
        var invalid = 0;
        foreach (var groupName in PlacementGroupNames)
        {
            var group = FindDirectChild(root, groupName);
            if (group == null)
            {
                continue;
            }

            foreach (Transform placedRoot in group)
            {
                var x = Mathf.RoundToInt(placedRoot.position.x);
                var y = Mathf.RoundToInt(placedRoot.position.z);
                if (!tileMap.IsWall(x, y))
                {
                    invalid++;
                }
            }
        }

        return invalid;
    }

    private static int CountDirectPlacedObjects(Transform root)
    {
        var count = 0;
        foreach (var groupName in PlacementGroupNames)
        {
            var group = FindDirectChild(root, groupName);
            if (group != null)
            {
                count += group.childCount;
            }
        }

        return count;
    }

    private static void SortEnvironmentChildren(Transform environmentRoot)
    {
        for (var i = 0; i < SharedLightingGroupNames.Length; i++)
        {
            SetSibling(environmentRoot, SharedLightingGroupNames[i], i);
        }

        for (var i = 0; i < PlacementGroupNames.Length; i++)
        {
            SetSibling(environmentRoot, PlacementGroupNames[i], SharedLightingGroupNames.Length + i);
        }
    }

    private static void SetSibling(Transform parent, string childName, int index)
    {
        var child = FindDirectChild(parent, childName);
        child?.SetSiblingIndex(index);
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * Mathf.Clamp01(t);
    }

    private static Vector3 ScaleVector(Vector3 value, float scale)
    {
        return new Vector3(value.x * scale, value.y * scale, value.z * scale);
    }

    private readonly struct PlacementCounts
    {
        public readonly int BackfillProps;
        public readonly int EdgeProps;
        public readonly int AccentProps;
        public readonly int Weeds;

        public PlacementCounts(int backfillProps, int edgeProps, int accentProps, int weeds)
        {
            BackfillProps = backfillProps;
            EdgeProps = edgeProps;
            AccentProps = accentProps;
            Weeds = weeds;
        }
    }

    private readonly struct PlacementStyle
    {
        public readonly float MinSpacing;
        public readonly float CellJitter;
        public readonly float NormalOffset;
        public readonly float TangentJitter;
        public readonly float MinScale;
        public readonly float MaxScale;
        public readonly bool AlignToWallNormal;
        public readonly float YawJitter;

        public PlacementStyle(
            float minSpacing,
            float cellJitter,
            float normalOffset,
            float tangentJitter,
            float minScale,
            float maxScale,
            bool alignToWallNormal)
        {
            MinSpacing = minSpacing;
            CellJitter = cellJitter;
            NormalOffset = normalOffset;
            TangentJitter = tangentJitter;
            MinScale = minScale;
            MaxScale = maxScale;
            AlignToWallNormal = alignToWallNormal;
            YawJitter = alignToWallNormal ? 34f : 180f;
        }
    }

    private readonly struct SourceLibrary
    {
        public readonly List<GameObject> BackfillProps;
        public readonly List<GameObject> EdgeProps;
        public readonly List<GameObject> AccentProps;
        public readonly List<GameObject> Weeds;

        public SourceLibrary(
            List<GameObject> backfillProps,
            List<GameObject> edgeProps,
            List<GameObject> accentProps,
            List<GameObject> weeds)
        {
            BackfillProps = backfillProps;
            EdgeProps = edgeProps;
            AccentProps = accentProps;
            Weeds = weeds;
        }

        public bool HasRequiredSources =>
            BackfillProps.Count > 0 &&
            EdgeProps.Count > 0 &&
            AccentProps.Count > 0 &&
            Weeds.Count > 0;
    }

    private readonly struct WalkableBounds
    {
        private readonly int _minX;
        private readonly int _minY;
        private readonly int _maxX;
        private readonly int _maxY;

        public WalkableBounds(int minX, int minY, int maxX, int maxY)
        {
            _minX = minX;
            _minY = minY;
            _maxX = maxX;
            _maxY = maxY;
        }

        public Vector3 Center3 => new((_minX + _maxX) * 0.5f, 0f, (_minY + _maxY) * 0.5f);
    }

    private readonly struct TileCandidate
    {
        public readonly int X;
        public readonly int Y;
        public readonly Vector2 Normal;

        public TileCandidate(int x, int y, Vector2 normal)
        {
            X = x;
            Y = y;
            Normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.up;
        }
    }

    private sealed class TileMap
    {
        private readonly MapAsset _map;
        private readonly int[,] _distanceToWalkable;

        public TileMap(MapAsset map)
        {
            _map = map;
            _map.EnsureSize();
            _distanceToWalkable = BuildDistanceToWalkable();
        }

        public IEnumerable<TileCandidate> GetWallCandidates(
            int minDistanceToWalkable,
            int maxDistanceToWalkable,
            bool requireFourWayEdge)
        {
            for (var y = 0; y < _map.Height; y++)
            {
                for (var x = 0; x < _map.Width; x++)
                {
                    if (!IsWall(x, y))
                    {
                        continue;
                    }

                    var distance = _distanceToWalkable[x, y];
                    if (distance < minDistanceToWalkable || distance > maxDistanceToWalkable)
                    {
                        continue;
                    }

                    var normal = GetWallNormal(x, y, requireFourWayEdge);
                    if (requireFourWayEdge && normal == Vector2.zero)
                    {
                        continue;
                    }

                    yield return new TileCandidate(x, y, normal);
                }
            }
        }

        public bool IsWall(int x, int y)
        {
            return InBounds(x, y) && _map.Get(x, y).Kind == TileKind.Wall;
        }

        private int[,] BuildDistanceToWalkable()
        {
            var distances = new int[_map.Width, _map.Height];
            var queue = new Queue<Vector2Int>();
            for (var y = 0; y < _map.Height; y++)
            {
                for (var x = 0; x < _map.Width; x++)
                {
                    distances[x, y] = int.MaxValue;
                    if (IsWalkable(x, y))
                    {
                        distances[x, y] = 0;
                        queue.Enqueue(new Vector2Int(x, y));
                    }
                }
            }

            var directions = FourDirections;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var nextDistance = distances[current.x, current.y] + 1;
                foreach (var direction in directions)
                {
                    var next = current + direction;
                    if (!InBounds(next.x, next.y) || nextDistance >= distances[next.x, next.y])
                    {
                        continue;
                    }

                    distances[next.x, next.y] = nextDistance;
                    queue.Enqueue(next);
                }
            }

            return distances;
        }

        private Vector2 GetWallNormal(int x, int y, bool fourWayOnly)
        {
            var normal = Vector2.zero;
            var directions = fourWayOnly ? FourDirections : EightDirections;
            foreach (var direction in directions)
            {
                var nx = x + direction.x;
                var ny = y + direction.y;
                if (!IsWalkable(nx, ny))
                {
                    continue;
                }

                normal += new Vector2(x - nx, y - ny);
            }

            if (normal != Vector2.zero)
            {
                return normal.normalized;
            }

            var bestDistance = int.MaxValue;
            var bestNormal = Vector2.zero;
            foreach (var direction in EightDirections)
            {
                for (var step = 1; step <= 6; step++)
                {
                    var nx = x + direction.x * step;
                    var ny = y + direction.y * step;
                    if (!InBounds(nx, ny))
                    {
                        break;
                    }

                    if (!IsWalkable(nx, ny) || step >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = step;
                    bestNormal = new Vector2(x - nx, y - ny).normalized;
                    break;
                }
            }

            return bestNormal;
        }

        private bool IsWalkable(int x, int y)
        {
            if (!InBounds(x, y))
            {
                return false;
            }

            var kind = _map.Get(x, y).Kind;
            return kind == TileKind.Floor || kind == TileKind.Spawn;
        }

        private bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < _map.Width && y < _map.Height;
        }

        private static readonly Vector2Int[] FourDirections =
        {
            new(0, -1),
            new(1, 0),
            new(0, 1),
            new(-1, 0)
        };

        private static readonly Vector2Int[] EightDirections =
        {
            new(0, -1),
            new(1, -1),
            new(1, 0),
            new(1, 1),
            new(0, 1),
            new(-1, 1),
            new(-1, 0),
            new(-1, -1)
        };
    }
}
#endif
