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
    private const int DefaultLayer = 0;
    private const int WallLayer = 7;
    private const float MaxDistanceFromWalkableTile = 5.5f;
    private const float MinOccluderHeight = 0.65f;
    private const float FadeTargetAlpha = 0.28f;
    private const float FadeRayRadius = 0.8f;
    private const string WallLayerName = "Wall";
    private const string FadeTriggerName = "__CameraFadeTrigger";

    private static readonly SceneConfig[] TargetScenes =
    {
        new(
            "Assets/0.MainProject/Scenes/Town/Town_Forest.unity",
            "Assets/Resources/Data/Map/Town_Forest.asset",
            new[] { "Town_Appearance" }),
        new(
            "Assets/0.MainProject/Scenes/Game/Game_Forest_Tutorial.unity",
            "Assets/Resources/Data/Map/Game_Forest_Tutorial.asset",
            new[] { "Forest_Decoration_Set" }),
        new(
            "Assets/0.MainProject/Scenes/Game/Game_Forest_First_Step.unity",
            "Assets/Resources/Data/Map/Game_Forest_First_Step.asset",
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
        var totalRestoredVisualLayers = 0;
        var totalRemovedLegacyColliders = 0;

        foreach (var config in TargetScenes)
        {
            var result = ApplyToScene(config, saveScene: true);
            totalAddedColliders += result.AddedColliders;
            totalUpdatedColliders += result.UpdatedColliders;
            totalRestoredVisualLayers += result.RestoredVisualLayers;
            totalRemovedLegacyColliders += result.RemovedLegacyColliders;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[CameraObstacleFadeSceneApplier] Applied camera obstacle fade targets. " +
            $"Scenes={TargetScenes.Length}, AddedColliders={totalAddedColliders}, " +
            $"UpdatedColliders={totalUpdatedColliders}, RestoredVisualLayers={totalRestoredVisualLayers}, " +
            $"RemovedLegacyColliders={totalRemovedLegacyColliders}.");
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
            var fadeTriggerColliders = wallLayerObjects.Count(collider => collider.GetComponent<CameraObstacleFadeTarget>() != null);
            var triggerColliders = wallLayerObjects.Count(collider => collider.isTrigger);
            var blockingColliders = wallLayerObjects.Length - triggerColliders;
            var wallLayerRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Count(renderer => renderer != null
                                   && renderer.gameObject.scene == scene
                                   && renderer.gameObject.layer == WallLayer
                                   && renderer.GetComponent<CameraObstacleFadeTarget>() == null);
            var fadeSettingsOk = fade != null
                                 && Mathf.Approximately(fade.targetAlpha, FadeTargetAlpha)
                                 && Mathf.Approximately(fade.rayRadius, FadeRayRadius);

            Debug.Log(
                "[CameraObstacleFadeSceneApplier] Validation " +
                $"Scene={scene.name}, HasFade={fade != null}, " +
                $"ObstacleLayer={(fade != null ? fade.obstacleLayer.value : 0)}, " +
                $"TargetAlpha={(fade != null ? fade.targetAlpha : 0f)}, " +
                $"RayRadius={(fade != null ? fade.rayRadius : 0f)}, " +
                $"FadeSettingsOk={fadeSettingsOk}, " +
                $"WallColliders={wallLayerObjects.Length}, " +
                $"FadeTriggerColliders={fadeTriggerColliders}, " +
                $"FadeTriggers={triggerColliders}, BlockingWallColliders={blockingColliders}.");
            if (!fadeSettingsOk)
            {
                Debug.LogError(
                    "[CameraObstacleFadeSceneApplier] Fade settings mismatch. " +
                    $"Scene={scene.name}, ExpectedTargetAlpha={FadeTargetAlpha}, ExpectedRayRadius={FadeRayRadius}.");
            }

            if (wallLayerRenderers > 0)
            {
                Debug.LogWarning(
                    "[CameraObstacleFadeSceneApplier] Wall-layer visual renderers remain. " +
                    $"Scene={scene.name}, WallLayerRenderers={wallLayerRenderers}.");
            }
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
        var restoredVisualLayers = 0;
        var removedLegacyColliders = 0;
        var targets = CollectFadeTargets(scene, config, walkableLookup, mapAsset).ToArray();

        foreach (var renderer in targets)
        {
            var gameObject = renderer.gameObject;
            if (gameObject.layer != WallLayer)
            {
                // Existing visual layer is already safe; only the trigger child needs Wall.
            }
            else
            {
                gameObject.layer = DefaultLayer;
                restoredVisualLayers++;
                EditorUtility.SetDirty(gameObject);
            }

            removedLegacyColliders += RemoveLegacyFadeColliders(renderer);

            var collider = EnsureFadeTriggerCollider(renderer, out var createdCollider);
            if (createdCollider)
                addedColliders++;
            else
                updatedColliders++;
            ConfigureCollider(collider, renderer);
        }

        var cleanup = CleanupLegacyWallLayerVisuals(scene);
        restoredVisualLayers += cleanup.RestoredVisualLayers;
        removedLegacyColliders += cleanup.RemovedLegacyColliders;

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
            $"UpdatedColliders={updatedColliders}, RestoredVisualLayers={restoredVisualLayers}, " +
            $"RemovedLegacyColliders={removedLegacyColliders}.");

        return new ApplyResult(addedColliders, updatedColliders, restoredVisualLayers, removedLegacyColliders);
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
        fade.targetAlpha = FadeTargetAlpha;
        fade.targetHeightOffset = 1f;
        fade.rayRadius = FadeRayRadius;
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
        var localBounds = GetRendererLocalBounds(renderer);

        collider.isTrigger = true;
        collider.center = localBounds.center;
        collider.size = localBounds.size;
        EditorUtility.SetDirty(collider);
    }

    private static BoxCollider EnsureFadeTriggerCollider(MeshRenderer renderer, out bool createdCollider)
    {
        var trigger = FindDirectChild(renderer.transform, FadeTriggerName);
        if (trigger == null)
        {
            var triggerObject = new GameObject(FadeTriggerName);
            trigger = triggerObject.transform;
            trigger.SetParent(renderer.transform, false);
        }

        trigger.localPosition = Vector3.zero;
        trigger.localRotation = Quaternion.identity;
        trigger.localScale = Vector3.one;
        trigger.gameObject.layer = WallLayer;

        var target = trigger.GetComponent<CameraObstacleFadeTarget>();
        if (target == null)
            target = trigger.gameObject.AddComponent<CameraObstacleFadeTarget>();

        target.TargetRenderer = renderer;
        EditorUtility.SetDirty(target);

        var collider = trigger.GetComponent<BoxCollider>();
        createdCollider = collider == null;
        if (collider == null)
            collider = trigger.gameObject.AddComponent<BoxCollider>();

        EditorUtility.SetDirty(trigger.gameObject);
        return collider;
    }

    private static int RemoveLegacyFadeColliders(MeshRenderer renderer)
    {
        var collider = renderer != null ? renderer.GetComponent<BoxCollider>() : null;
        if (collider == null || !collider.isTrigger)
            return 0;

        var localBounds = GetRendererLocalBounds(renderer);
        if (!Approximately(collider.center, localBounds.center) || !Approximately(collider.size, localBounds.size))
            return 0;

        Object.DestroyImmediate(collider);
        EditorUtility.SetDirty(renderer.gameObject);
        return 1;
    }

    private static ApplyResult CleanupLegacyWallLayerVisuals(Scene scene)
    {
        var restoredVisualLayers = 0;
        var removedLegacyColliders = 0;
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (renderer == null
                || renderer.gameObject.scene != scene
                || renderer.gameObject.layer != WallLayer
                || renderer.GetComponent<CameraObstacleFadeTarget>() != null
                || renderer.name.Equals(FadeTriggerName, StringComparison.Ordinal))
            {
                continue;
            }

            int removed = RemoveLegacyFadeColliders(renderer);
            bool hasRemainingCollider = renderer.GetComponent<Collider>() != null;
            if (removed <= 0 && hasRemainingCollider)
                continue;

            renderer.gameObject.layer = DefaultLayer;
            restoredVisualLayers++;
            removedLegacyColliders += removed;
            EditorUtility.SetDirty(renderer.gameObject);
        }

        return new ApplyResult(0, 0, restoredVisualLayers, removedLegacyColliders);
    }

    private static Bounds GetRendererLocalBounds(MeshRenderer renderer)
    {
        var meshFilter = renderer.GetComponent<MeshFilter>();
        return meshFilter != null && meshFilter.sharedMesh != null
            ? meshFilter.sharedMesh.bounds
            : new Bounds(renderer.transform.InverseTransformPoint(renderer.bounds.center), Vector3.one);
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child != null && child.name.Equals(childName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    private static bool Approximately(Vector3 lhs, Vector3 rhs)
        => Vector3.SqrMagnitude(lhs - rhs) <= 0.000001f;

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
        public readonly int RestoredVisualLayers;
        public readonly int RemovedLegacyColliders;

        public ApplyResult(
            int addedColliders,
            int updatedColliders,
            int restoredVisualLayers,
            int removedLegacyColliders)
        {
            AddedColliders = addedColliders;
            UpdatedColliders = updatedColliders;
            RestoredVisualLayers = restoredVisualLayers;
            RemovedLegacyColliders = removedLegacyColliders;
        }
    }
}
#endif
