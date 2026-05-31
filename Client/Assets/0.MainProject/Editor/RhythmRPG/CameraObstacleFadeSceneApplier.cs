#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class CameraObstacleFadeSceneApplier
{
    private const int WallLayer = 7;
    private const float MaxDistanceFromWalkableTile = 5.5f;
    private const float MinOccluderHeight = 0.65f;
    private const string WallLayerName = "Wall";

    private static readonly SceneConfig[] TargetScenes =
    {
        new(
            "Assets/0.MainProject/Scenes/Town/Town_Forest.unity",
            "Assets/Resources/Data/Map/Town_Forest.asset",
            new[] { "Town_Appearance" }),
        new(
            "Assets/0.MainProject/Scenes/Game/Game_Forest_Tutorial.unity",
            "Assets/Resources/Data/Map/Game_Forest_Tutorial.asset",
            new[] { "Forest_Decoration_Set" })
    };

    private static readonly string[] IncludeNameTokens =
    {
        "Pine",
        "Tree",
        "Evergreen",
        "Rock",
        "Stone",
        "Boulder",
        "Monolith",
        "Obelisk",
        "Fence",
        "Gate",
        "Cottage",
        "House",
        "Blacksmith",
        "Forge",
        "Furnace",
        "Anvil",
        "Workbench",
        "Barrel",
        "Crate",
        "Shield",
        "Bridge",
        "Footbridge",
        "Signpost",
        "Lantern",
        "Crystal",
        "Pillar",
        "Log",
        "Stump",
        "Wall"
    };

    private static readonly string[] ExcludePathTokens =
    {
        "Board_Tiles_Appearance",
        "Tile_",
        "__WalkableGridOutline",
        "Water",
        "River",
        "Ocean",
        "Pond",
        "Foam",
        "Fog",
        "Volume",
        "Particle",
        "Smoke",
        "Flame",
        "Glow",
        "Light_Receiver",
        "RoadClear"
    };

    [MenuItem("RhythmRPG/Editors/World/Apply Camera Obstacle Fade Targets")]
    public static void Apply()
    {
        var totalAddedColliders = 0;
        var totalUpdatedColliders = 0;
        var totalLayeredObjects = 0;

        foreach (var config in TargetScenes)
        {
            var result = ApplyToScene(config, saveScene: true);
            totalAddedColliders += result.AddedColliders;
            totalUpdatedColliders += result.UpdatedColliders;
            totalLayeredObjects += result.LayeredObjects;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[CameraObstacleFadeSceneApplier] Applied camera obstacle fade targets. " +
            $"Scenes={TargetScenes.Length}, AddedColliders={totalAddedColliders}, " +
            $"UpdatedColliders={totalUpdatedColliders}, LayeredObjects={totalLayeredObjects}.");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Camera Obstacle Fade Targets")]
    public static void Validate()
    {
        foreach (var config in TargetScenes)
        {
            var scene = OpenScene(config.ScenePath);
            var fade = EnsureCameraFadeComponent(scene, createIfMissing: false);
            var wallLayerObjects = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(collider => collider != null && collider.gameObject.scene == scene && collider.gameObject.layer == WallLayer)
                .ToArray();

            Debug.Log(
                "[CameraObstacleFadeSceneApplier] Validation " +
                $"Scene={scene.name}, HasFade={fade != null}, " +
                $"ObstacleLayer={(fade != null ? fade.obstacleLayer.value : 0)}, " +
                $"WallColliders={wallLayerObjects.Length}.");
        }
    }

    private static ApplyResult ApplyToScene(SceneConfig config, bool saveScene)
    {
        var scene = OpenScene(config.ScenePath);
        var mapAsset = AssetDatabase.LoadAssetAtPath<MapAsset>(config.MapAssetPath);
        if (mapAsset == null)
        {
            Debug.LogError("[CameraObstacleFadeSceneApplier] Missing map asset: " + config.MapAssetPath);
            return default;
        }

        mapAsset.EnsureSize();
        var walkableLookup = BuildWalkableLookup(mapAsset);
        var fade = EnsureCameraFadeComponent(scene, createIfMissing: true);

        var addedColliders = 0;
        var updatedColliders = 0;
        var layeredObjects = 0;
        var targets = CollectFadeTargets(scene, config, walkableLookup, mapAsset).ToArray();

        foreach (var renderer in targets)
        {
            var gameObject = renderer.gameObject;
            if (gameObject.layer != WallLayer)
            {
                gameObject.layer = WallLayer;
                layeredObjects++;
                EditorUtility.SetDirty(gameObject);
            }

            if (!gameObject.TryGetComponent<BoxCollider>(out var collider))
            {
                collider = gameObject.AddComponent<BoxCollider>();
                addedColliders++;
            }
            else
            {
                updatedColliders++;
            }

            ConfigureCollider(collider, renderer);
        }

        if (fade != null)
        {
            EditorUtility.SetDirty(fade);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (saveScene)
        {
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log(
            "[CameraObstacleFadeSceneApplier] Applied " +
            $"Scene={scene.name}, Targets={targets.Length}, AddedColliders={addedColliders}, " +
            $"UpdatedColliders={updatedColliders}, LayeredObjects={layeredObjects}.");

        return new ApplyResult(addedColliders, updatedColliders, layeredObjects);
    }

    private static Scene OpenScene(string scenePath)
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != scenePath)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        return scene;
    }

    private static CameraObstacleFade EnsureCameraFadeComponent(Scene scene, bool createIfMissing)
    {
        var camera = Camera.main;
        if (camera == null || camera.gameObject.scene != scene)
        {
            camera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && candidate.gameObject.scene == scene);
        }

        if (camera == null)
        {
            Debug.LogWarning("[CameraObstacleFadeSceneApplier] Main camera not found in scene: " + scene.name);
            return null;
        }

        var fade = camera.GetComponent<CameraObstacleFade>();
        if (fade == null && createIfMissing)
        {
            fade = camera.gameObject.AddComponent<CameraObstacleFade>();
        }

        if (fade == null)
        {
            return null;
        }

        fade.obstacleLayer = LayerMask.GetMask(WallLayerName);
        if (fade.obstacleLayer.value == 0)
        {
            fade.obstacleLayer = 1 << WallLayer;
        }

        fade.fadeSpeed = 5f;
        fade.targetAlpha = 0.1f;
        fade.targetHeightOffset = 1f;
        fade.rayRadius = 3f;
        fade.debugMode = false;

        return fade;
    }

    private static IEnumerable<MeshRenderer> CollectFadeTargets(
        Scene scene,
        SceneConfig config,
        bool[,] walkableLookup,
        MapAsset mapAsset)
    {
        var rootSet = new HashSet<string>(config.RootNames, StringComparer.Ordinal);
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (renderer == null ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy ||
                renderer.gameObject.scene != scene ||
                renderer.GetComponent<ParticleSystem>() != null)
            {
                continue;
            }

            var transform = renderer.transform;
            var path = GetPath(transform);
            if (!IsUnderConfiguredRoot(transform, rootSet) ||
                ContainsAny(path, ExcludePathTokens) ||
                !ContainsAny(path, IncludeNameTokens) ||
                !IsLargeEnoughOccluder(renderer.bounds) ||
                !IsNearWalkableTile(renderer.bounds, walkableLookup, mapAsset, MaxDistanceFromWalkableTile))
            {
                continue;
            }

            yield return renderer;
        }
    }

    private static bool[,] BuildWalkableLookup(MapAsset mapAsset)
    {
        var lookup = new bool[mapAsset.Width, mapAsset.Height];
        for (var y = 0; y < mapAsset.Height; y++)
        {
            for (var x = 0; x < mapAsset.Width; x++)
            {
                var kind = mapAsset.Get(x, y).Kind;
                lookup[x, y] = kind == TileKind.Floor || kind == TileKind.Spawn;
            }
        }

        return lookup;
    }

    private static bool IsNearWalkableTile(Bounds bounds, bool[,] walkableLookup, MapAsset mapAsset, float maxDistance)
    {
        var minX = Mathf.Clamp(Mathf.FloorToInt(bounds.min.x - maxDistance), 0, mapAsset.Width - 1);
        var maxX = Mathf.Clamp(Mathf.CeilToInt(bounds.max.x + maxDistance), 0, mapAsset.Width - 1);
        var minY = Mathf.Clamp(Mathf.FloorToInt(bounds.min.z - maxDistance), 0, mapAsset.Height - 1);
        var maxY = Mathf.Clamp(Mathf.CeilToInt(bounds.max.z + maxDistance), 0, mapAsset.Height - 1);

        var hasSampleRange = minX <= maxX && minY <= maxY;
        if (!hasSampleRange)
        {
            return false;
        }

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!walkableLookup[x, y])
                {
                    continue;
                }

                var tilePoint = new Vector2(x, y);
                var closest = new Vector2(
                    Mathf.Clamp(tilePoint.x, bounds.min.x, bounds.max.x),
                    Mathf.Clamp(tilePoint.y, bounds.min.z, bounds.max.z));

                if (Vector2.Distance(tilePoint, closest) <= maxDistance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsLargeEnoughOccluder(Bounds bounds)
    {
        var size = bounds.size;
        return size.y >= MinOccluderHeight && Mathf.Max(size.x, size.z) >= 0.25f;
    }

    private static void ConfigureCollider(BoxCollider collider, MeshRenderer renderer)
    {
        var meshFilter = renderer.GetComponent<MeshFilter>();
        var localBounds = meshFilter != null && meshFilter.sharedMesh != null
            ? meshFilter.sharedMesh.bounds
            : new Bounds(renderer.transform.InverseTransformPoint(renderer.bounds.center), Vector3.one);

        collider.isTrigger = false;
        collider.center = localBounds.center;
        collider.size = localBounds.size;
        EditorUtility.SetDirty(collider);
    }

    private static bool IsUnderConfiguredRoot(Transform transform, HashSet<string> rootNames)
    {
        var current = transform;
        while (current != null)
        {
            if (rootNames.Contains(current.name))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool ContainsAny(string value, IReadOnlyList<string> tokens)
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

    private readonly struct SceneConfig
    {
        public readonly string ScenePath;
        public readonly string MapAssetPath;
        public readonly string[] RootNames;

        public SceneConfig(string scenePath, string mapAssetPath, string[] rootNames)
        {
            ScenePath = scenePath;
            MapAssetPath = mapAssetPath;
            RootNames = rootNames;
        }
    }

    private readonly struct ApplyResult
    {
        public readonly int AddedColliders;
        public readonly int UpdatedColliders;
        public readonly int LayeredObjects;

        public ApplyResult(int addedColliders, int updatedColliders, int layeredObjects)
        {
            AddedColliders = addedColliders;
            UpdatedColliders = updatedColliders;
            LayeredObjects = layeredObjects;
        }
    }
}
#endif
