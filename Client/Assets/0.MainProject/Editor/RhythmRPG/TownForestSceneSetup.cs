#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using RhythmRPG.Game.Visual.SceneEffects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class TownForestSceneSetup
{
    private const string ScenePath = "Assets/0.MainProject/Scenes/Town/Town_Forest.unity";
    private const string MapAssetPath = "Assets/Resources/Data/Map/Town_Forest.asset";
    private const string TilePrefabPath = "Assets/HaniJahanDesign/FreePack/Prefabs/HJD_FP_Ground_Block_02.prefab";

    private const string GameplayRootName = "Gameplay_Functions";
    private const string AppearanceRootName = "Town_Appearance";
    private const string LightingRootName = "Town_Lighting";
    private const string BoardTilesRootName = "Board_Tiles_Appearance";
    private const string LightingPropsRootName = "Lighting_Props";
    private const string RuntimeEntitiesRootName = "Runtime_Entities";
    private const string RuntimeSkillRunnersRootName = "Runtime_SkillRunners";
    private const string DepthFogRootName = "FX_FullscreenDepthFog";
    private const string PostProcessName = "PP_TownForest_GlobalVolume_Bloom_ACES";
    private const string MoonLightName = "DirectionalLight_Moon_Sample";
    private const string CoolFillLightName = "Directional Light_Moon_Cool_Fill";

    private const string ForestAssetFolder = "Assets/0.MainProject/Art/ForestLightingPipeline";
    private const string SkyboxPath = ForestAssetFolder + "/M_Forest_Night_Skybox.mat";
    private const string VolumeProfilePath = ForestAssetFolder + "/PP_ForestLightingPipeline_Bloom_ACES.asset";
    private const string FogMaterialPath = ForestAssetFolder + "/M_Tutorial_ForestDepthBoundaryFog.mat";
    private const string FogShaderName = "RhythmRPG/Effects/ForestDepthBoundaryFog";
    private const string RendererFeatureName = "Forest Depth Boundary Fog";
    private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";
    private const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";
    private const string LanternPrefabPath = ForestAssetFolder + "/PF_Lantern_Lighting.prefab";
    private const string CrystalPrefabPath = ForestAssetFolder + "/PF_Crystal_Pillar_Lighting.prefab";

    private static readonly Color LanternColor = new(1f, 150f / 255f, 50f / 255f, 1f);
    private static readonly Color CrystalColor = new(50f / 255f, 200f / 255f, 1f, 1f);
    private static readonly Color MoonColor = new(99f / 255f, 105f / 255f, 171f / 255f, 1f);
    private static readonly Color CoolFillColor = new(92f / 255f, 143f / 255f, 178f / 255f, 1f);

    private static readonly string[] RuntimePrefabPaths =
    {
        "Assets/0.MainProject/Resources/GameInit/Main Camera.prefab",
        "Assets/0.MainProject/Resources/GameInit/Directional Light.prefab",
        "Assets/0.MainProject/Resources/GameInit/BeatDebugUI_TMP.prefab",
        "Assets/0.MainProject/Resources/GameInit/BgmDirector.prefab",
        "Assets/0.MainProject/Resources/GameInit/BoardView.prefab",
        "Assets/0.MainProject/Resources/GameInit/Binder.prefab",
        "Assets/0.MainProject/Resources/GameInit/Canvas_RhythmHUD.prefab",
        "Assets/0.MainProject/Resources/GameInit/ClientGameState.prefab",
        "Assets/0.MainProject/Resources/GameInit/ClientHandlers.prefab",
        "Assets/0.MainProject/Resources/GameInit/MapData.prefab",
        "Assets/0.MainProject/Resources/GameInit/RhythmClient.prefab",
        "Assets/0.MainProject/Resources/GameInit/RhythmInputController.prefab",
        "Assets/0.MainProject/Resources/GameInit/EventSystem.prefab",
        "Assets/0.MainProject/Resources/ApiClientProvider.prefab"
    };

    private static readonly string[] ResetRootNames =
    {
        GameplayRootName,
        AppearanceRootName,
        LightingRootName,
        "Main Camera",
        "Directional Light",
        MoonLightName,
        CoolFillLightName,
        "BeatDebugUI_TMP",
        "BgmDirector",
        "BoardView",
        "Binder",
        "Canvas_RhythmHUD",
        "ClientGameState",
        "ClientHandlers",
        "MapData",
        "RhythmClient",
        "RhythmInputController",
        "TownSceneContext",
        "ApiClientProvider",
        "EventSystem",
        "InventoryManager",
        "IneventoryManager"
    };

    [MenuItem("RhythmRPG/Editors/Town/Setup Town Forest Runtime Objects")]
    [MenuItem("RhythmRPG/Editors/Town/Build Town Forest Scene")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        RemoveGeneratedAndRuntimeRoots();

        var gameplayRoot = CreateRoot(GameplayRootName);
        var appearanceRoot = CreateRoot(AppearanceRootName);
        var lightingRoot = CreateRoot(LightingRootName);
        var tileRoot = FindOrCreateChild(appearanceRoot.transform, BoardTilesRootName);
        var lightingPropsRoot = FindOrCreateChild(appearanceRoot.transform, LightingPropsRootName);
        var entityRoot = FindOrCreateChild(gameplayRoot.transform, RuntimeEntitiesRootName);
        var skillRoot = FindOrCreateChild(gameplayRoot.transform, RuntimeSkillRunnersRootName);

        InstantiateRuntimePrefabs(gameplayRoot.transform);

        var boardView = Object.FindFirstObjectByType<BoardView>();
        var mapAsset = AssetDatabase.LoadAssetAtPath<MapAsset>(MapAssetPath);
        var tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TilePrefabPath);
        if (boardView == null || mapAsset == null || tilePrefab == null)
        {
            Debug.LogError($"[TownForestSceneSetup] Missing refs. BoardView={boardView != null}, MapAsset={mapAsset != null}, TilePrefab={tilePrefab != null}");
            return;
        }

        boardView.ConfigureSceneRoots(tileRoot, entityRoot, skillRoot);
        ConfigureMapRegistry(mapAsset);
        CreateTownSceneContext(gameplayRoot.transform);
        CreateInventoryManager(gameplayRoot.transform);
        BakeTiles(boardView, mapAsset, tilePrefab, tileRoot);
        BuildLightingAndFog(lightingRoot.transform, lightingPropsRoot, mapAsset);
        SortHierarchy();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = appearanceRoot;
        Debug.Log("[TownForestSceneSetup] Town_Forest lighting, fog, shader pipeline, and separated hierarchy are ready.");
    }

    [MenuItem("RhythmRPG/Editors/Town/Validate Town Forest Scene")]
    public static void Validate()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var gameplayRoot = GameObject.Find(GameplayRootName);
        var appearanceRoot = GameObject.Find(AppearanceRootName);
        var lightingRoot = GameObject.Find(LightingRootName);
        var tileRoot = appearanceRoot != null ? FindDirectChild(appearanceRoot.transform, BoardTilesRootName) : null;
        var fogRoot = lightingRoot != null ? FindDirectChild(lightingRoot.transform, DepthFogRootName) : null;
        var boardView = Object.FindFirstObjectByType<BoardView>();
        var mapAsset = AssetDatabase.LoadAssetAtPath<MapAsset>(MapAssetPath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
        var volume = lightingRoot != null ? lightingRoot.GetComponentInChildren<Volume>(true) : null;
        var cameraData = Camera.main != null ? Camera.main.GetUniversalAdditionalCameraData() : null;
        var expectedTiles = mapAsset != null ? mapAsset.Width * mapAsset.Height : 0;
        var actualTiles = tileRoot != null
            ? tileRoot.Cast<Transform>().Count(child => child.name.StartsWith("Tile_", StringComparison.Ordinal))
            : 0;

        var hasError = false;
        Report(gameplayRoot != null, "Gameplay root exists.", "Missing " + GameplayRootName, ref hasError);
        Report(appearanceRoot != null, "Appearance root exists.", "Missing " + AppearanceRootName, ref hasError);
        Report(lightingRoot != null, "Lighting root exists.", "Missing " + LightingRootName, ref hasError);
        Report(boardView != null, "BoardView exists.", "Missing BoardView.", ref hasError);
        Report(tileRoot != null && actualTiles == expectedTiles, $"Appearance tiles baked: {actualTiles}.", $"Tile count mismatch. Expected={expectedTiles}, Actual={actualTiles}.", ref hasError);
        Report(RenderSettings.skybox != null && RenderSettings.skybox.name == "M_Forest_Night_Skybox", "Forest tutorial skybox applied.", "Forest night skybox is not assigned.", ref hasError);
        Report(volume != null && volume.sharedProfile != null, "Global post-process volume exists.", "Missing global post-process volume.", ref hasError);
        Report(fogRoot != null && fogRoot.GetComponent<ForestDepthFogZoneController>() != null, "Depth fog controller exists.", "Missing depth fog controller.", ref hasError);
        Report(material != null && material.shader != null && material.shader.name == FogShaderName, "Depth fog shader material exists.", "Depth fog material/shader is invalid.", ref hasError);
        Report(HasRendererFeature(PcRendererPath), "PC renderer has depth fog feature.", "PC renderer is missing depth fog feature.", ref hasError);
        Report(cameraData != null && cameraData.renderPostProcessing && cameraData.requiresDepthTexture, "Main camera post/depth settings enabled.", "Main camera post/depth settings are not ready.", ref hasError);

        if (!hasError)
        {
            Debug.Log($"[TownForestSceneSetup] VALIDATION OK. Scene={scene.name}, Tiles={actualTiles}, Lights={Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length}.");
        }
    }

    private static void RemoveGeneratedAndRuntimeRoots()
    {
        var reset = new HashSet<string>(ResetRootNames, StringComparer.Ordinal);
        var roots = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(go => go.scene.IsValid() && go.transform.parent == null && reset.Contains(go.name))
            .ToArray();

        foreach (var root in roots)
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateRoot(string name)
    {
        var root = new GameObject(name);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        return root;
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        var existing = FindDirectChild(parent, name);
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
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

    private static void InstantiateRuntimePrefabs(Transform parent)
    {
        foreach (var path in RuntimePrefabPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[TownForestSceneSetup] Prefab missing: {path}");
                continue;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                Debug.LogWarning($"[TownForestSceneSetup] Prefab instantiate failed: {path}");
                continue;
            }

            instance.name = prefab.name;
        }
    }

    private static void ConfigureMapRegistry(MapAsset mapAsset)
    {
        var registry = Object.FindFirstObjectByType<MapRegistry>();
        if (registry == null)
        {
            return;
        }

        var maps = registry.Maps != null ? registry.Maps.ToList() : new List<MapAsset>();
        if (!maps.Contains(mapAsset))
        {
            maps.Add(mapAsset);
        }

        registry.Maps = maps.Where(m => m != null).Distinct().ToArray();
        EditorUtility.SetDirty(registry);
    }

    private static void CreateTownSceneContext(Transform parent)
    {
        var go = new GameObject("TownSceneContext");
        go.transform.SetParent(parent, false);
        var context = go.AddComponent<TownSceneContext>();
        var serialized = new SerializedObject(context);
        serialized.FindProperty("_mapId").stringValue = "Town_Forest";
        serialized.FindProperty("_wantSnapshot").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(context);
    }

    private static void CreateInventoryManager(Transform parent)
    {
        var go = new GameObject("InventoryManager");
        go.transform.SetParent(parent, false);
        go.AddComponent<InventoryManager>();
    }

    private static void BakeTiles(BoardView boardView, MapAsset mapAsset, GameObject tilePrefab, Transform tileRoot)
    {
        mapAsset.EnsureSize();
        mapAsset.RebuildAppearanceAutoTiles();
        boardView.tilePrefab = tilePrefab;
        boardView.appearancePalette = mapAsset.AppearancePalette;

        DeleteExistingTiles(boardView.transform);
        DeleteExistingTiles(tileRoot);

        var baseMaterial = GetBaseMaterial(tilePrefab);
        var palette = mapAsset.AppearancePalette;

        for (int y = 0; y < mapAsset.Height; y++)
        {
            for (int x = 0; x < mapAsset.Width; x++)
            {
                var appearance = mapAsset.GetAppearance(x, y);
                var prefab = ResolveTilePrefab(tilePrefab, palette, appearance);
                var tile = PrefabUtility.InstantiatePrefab(prefab, tileRoot) as GameObject;
                if (tile == null)
                {
                    continue;
                }

                tile.name = $"Tile_{x}_{y}";
                tile.transform.position = new Vector3(x * boardView.cellSize, -2f, y * boardView.cellSize);

                ApplyTileVisual(boardView, mapAsset, tile, x, y, baseMaterial, palette, appearance);
                SetTileStaticFlags(tile);
            }
        }

        EditorUtility.SetDirty(boardView.gameObject);
        EditorUtility.SetDirty(boardView);
        EditorUtility.SetDirty(mapAsset);
    }

    private static void DeleteExistingTiles(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (child.name.StartsWith("Tile_", StringComparison.Ordinal))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static GameObject ResolveTilePrefab(GameObject fallback, AppearanceAutoTilePalette palette, AppearanceTileCell appearance)
    {
        if (appearance.Kind != AppearanceTileKind.None &&
            palette != null &&
            palette.TryGetPrefab(appearance.Kind, appearance.Variant, out var prefab) &&
            prefab != null)
        {
            return prefab;
        }

        return fallback;
    }

    private static void ApplyTileVisual(
        BoardView boardView,
        MapAsset mapAsset,
        GameObject tile,
        int x,
        int y,
        Material baseMaterial,
        AppearanceAutoTilePalette palette,
        AppearanceTileCell appearance)
    {
        if (!tile.TryGetComponent<BoardTileVisual>(out var visual))
        {
            visual = tile.AddComponent<BoardTileVisual>();
        }

        if (baseMaterial != null)
        {
            visual.SetBaseMaterial(baseMaterial);
        }

        var logicColor = boardView.GetTileColor((int)mapAsset.Get(x, y).Kind);
        if (appearance.Kind != AppearanceTileKind.None &&
            palette != null &&
            palette.TryGetMaterial(appearance.Kind, appearance.Variant, out var material) &&
            material != null)
        {
            visual.SetBaseColor(logicColor);
            visual.SetTopMaterial(material);
        }
        else
        {
            visual.HideTopSurface();
            visual.SetBaseColor(logicColor);
        }

        EditorUtility.SetDirty(visual);
    }

    private static Material GetBaseMaterial(GameObject prefab)
    {
        var renderer = BoardTileVisual.FindBaseRenderer(prefab);
        return renderer != null ? renderer.sharedMaterial : null;
    }

    private static void SetTileStaticFlags(GameObject tile)
    {
        foreach (var transform in tile.GetComponentsInChildren<Transform>(true))
        {
            var go = transform.gameObject;
            if (go.GetComponent<Light>() != null || go.GetComponent<ParticleSystem>() != null)
            {
                continue;
            }

            GameObjectUtility.SetStaticEditorFlags(
                go,
                StaticEditorFlags.BatchingStatic
                | StaticEditorFlags.OccludeeStatic
                | StaticEditorFlags.ReflectionProbeStatic);
        }
    }

    private static void BuildLightingAndFog(Transform lightingRoot, Transform lightingPropsRoot, MapAsset mapAsset)
    {
        var skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
        ConfigureRenderSettings(skybox);
        ConfigureMainCamera();
        CreatePostProcessVolume(lightingRoot);
        ConfigureMoonLight(lightingRoot);
        CreateCoolFillLight(lightingRoot);
        CreateTownLightingProps(lightingPropsRoot, mapAsset);
        ConfigureDepthFog(lightingRoot, mapAsset);
    }

    private static void ConfigureRenderSettings(Material skybox)
    {
        if (skybox != null)
        {
            RenderSettings.skybox = skybox;
            DynamicGI.UpdateEnvironment();
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.043f, 0.102f, 0.094f, 1f);
        RenderSettings.ambientIntensity = 0.32f;
        RenderSettings.reflectionIntensity = 0.35f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.fog = false;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.043f, 0.094f, 0.110f, 1f);
        RenderSettings.fogDensity = 0f;
    }

    private static void ConfigureMainCamera()
    {
        var mainCamera = Camera.main ?? Object.FindFirstObjectByType<Camera>();
        if (mainCamera == null)
        {
            Debug.LogWarning("[TownForestSceneSetup] Main Camera not found.");
            return;
        }

        mainCamera.clearFlags = CameraClearFlags.Skybox;
        mainCamera.allowHDR = true;

        var cameraData = mainCamera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.requiresDepthTexture = true;
        EditorUtility.SetDirty(mainCamera);
        EditorUtility.SetDirty(cameraData);
    }

    private static void CreatePostProcessVolume(Transform parent)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            Debug.LogWarning("[TownForestSceneSetup] Volume profile missing: " + VolumeProfilePath);
            return;
        }

        var volumeObject = new GameObject(PostProcessName);
        volumeObject.transform.SetParent(parent, false);

        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 35f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
    }

    private static void ConfigureMoonLight(Transform parent)
    {
        var lightObject = GameObject.Find("Directional Light") ?? GameObject.Find(MoonLightName);
        if (lightObject == null)
        {
            lightObject = new GameObject(MoonLightName);
            lightObject.AddComponent<Light>();
        }

        lightObject.name = MoonLightName;
        lightObject.transform.SetParent(parent, true);
        lightObject.transform.position = new Vector3(25f, 4.5f, 25f);
        lightObject.transform.rotation = Quaternion.Euler(55f, 345f, 0f);

        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = MoonColor;
        light.intensity = 1.98f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 1f;
        light.lightmapBakeType = LightmapBakeType.Mixed;
        RenderSettings.sun = light;
        EditorUtility.SetDirty(light);
    }

    private static void CreateCoolFillLight(Transform parent)
    {
        var lightObject = new GameObject(CoolFillLightName);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.position = new Vector3(25f, 0f, 16f);
        lightObject.transform.rotation = Quaternion.Euler(58f, 326f, 0f);

        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = CoolFillColor;
        light.intensity = 0.71f;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;
    }

    private static void CreateTownLightingProps(Transform parent, MapAsset mapAsset)
    {
        var lanternPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LanternPrefabPath);
        var crystalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalPrefabPath);
        var groundY = -1.28f;

        var lanterns = new[]
        {
            new PropPreset("Lantern_Warm_Path_01", new Vector3(7f, groundY, 8f), 18f),
            new PropPreset("Lantern_Warm_Path_02", new Vector3(15f, groundY, 16f), -24f),
            new PropPreset("Lantern_Warm_Plaza_01", new Vector3(25f, groundY, 23f), 0f),
            new PropPreset("Lantern_Warm_Plaza_02", new Vector3(35f, groundY, 17f), 36f),
            new PropPreset("Lantern_Warm_Gate_01", new Vector3(22f, groundY, 38f), -18f),
            new PropPreset("Lantern_Warm_Gate_02", new Vector3(29f, groundY, 38f), 18f)
        };

        var crystals = new[]
        {
            new PropPreset("Crystal_Cyan_Gate_01", new Vector3(20f, groundY, 42f), 0f),
            new PropPreset("Crystal_Cyan_Gate_02", new Vector3(31f, groundY, 42f), 0f),
            new PropPreset("Crystal_Cyan_Road_01", new Vector3(10f, groundY, 29f), -12f),
            new PropPreset("Crystal_Cyan_Road_02", new Vector3(41f, groundY, 31f), 18f),
            new PropPreset("Crystal_Cyan_South_01", new Vector3(25f, groundY, 6f), 0f)
        };

        foreach (var preset in lanterns)
        {
            var instance = InstantiatePropOrLight(lanternPrefab, parent, preset, LanternColor, 5f, 7.2f);
            ConfigureBeatPulse(instance, LanternColor, 1.78f, 1.08f, 1.03f, 1.22f, 0.5f, 2.05f, 0.03f, 0.72f, 0.48f, 0.85f);
        }

        foreach (var preset in crystals)
        {
            var instance = InstantiatePropOrLight(crystalPrefab, parent, preset, CrystalColor, 2.35f, 6.8f);
            ConfigureBeatPulse(instance, CrystalColor, 1.16f, 1.32f, 2.75f, 1.12f, 0.56f, 1.75f, 0.012f, 1f, 1f, 1f);
        }

        if (mapAsset != null)
        {
            parent.position = Vector3.zero;
        }
    }

    private static GameObject InstantiatePropOrLight(GameObject prefab, Transform parent, PropPreset preset, Color color, float intensity, float range)
    {
        GameObject instance;
        if (prefab != null)
        {
            instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Failed to instantiate lighting prefab: " + prefab.name);
            }
        }
        else
        {
            instance = new GameObject(preset.Name);
            instance.transform.SetParent(parent, false);
            var light = instance.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
        }

        instance.name = preset.Name;
        instance.transform.position = preset.Position;
        instance.transform.rotation = Quaternion.Euler(0f, preset.Yaw, 0f);
        ConfigurePointLights(instance, color, intensity, range);
        return instance;
    }

    private static void ConfigurePointLights(GameObject root, Color color, float intensity, float range)
    {
        foreach (var light in root.GetComponentsInChildren<Light>(true))
        {
            if (light.type != LightType.Point)
            {
                continue;
            }

            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = color == LanternColor ? LightShadows.Soft : LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            EditorUtility.SetDirty(light);
        }
    }

    private static void ConfigureBeatPulse(
        GameObject root,
        Color color,
        float lightPeak,
        float emissionPeak,
        float alphaPeak,
        float particlePeak,
        float durationBeats,
        float falloff,
        float flicker,
        float lightBase,
        float rendererBase,
        float particleBase)
    {
        if (root == null)
        {
            return;
        }

        var pulse = root.GetComponent<ForestBeatLightPulse>();
        if (pulse == null)
        {
            pulse = root.AddComponent<ForestBeatLightPulse>();
        }

        pulse.Configure(
            root.GetComponentsInChildren<Light>(true),
            root.GetComponentsInChildren<Renderer>(true),
            root.GetComponentsInChildren<ParticleSystem>(true),
            color,
            lightPeak,
            emissionPeak,
            alphaPeak,
            particlePeak,
            durationBeats,
            falloff,
            flicker,
            lightBase: lightBase,
            rendererBase: rendererBase,
            particleBase: particleBase);
        EditorUtility.SetDirty(pulse);
    }

    private static void ConfigureDepthFog(Transform parent, MapAsset mapAsset)
    {
        var material = CreateOrUpdateFogMaterial();
        var root = new GameObject(DepthFogRootName);
        root.transform.SetParent(parent, false);

        var zones = CreateFogZones(root.transform, mapAsset);
        var controller = root.AddComponent<ForestDepthFogZoneController>();
        controller.Configure(material, zones, true, applyImmediately: false);

        ConfigureRendererFeature(PcRendererPath, material);
        ConfigureRendererFeature(MobileRendererPath, material);
        SaveMaterialDisabled(material);
        controller.ApplyNow();
    }

    private static Material CreateOrUpdateFogMaterial()
    {
        var shader = Shader.Find(FogShaderName);
        if (shader == null)
        {
            throw new InvalidOperationException("Could not find shader: " + FogShaderName);
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "M_Tutorial_ForestDepthBoundaryFog"
            };
            AssetDatabase.CreateAsset(material, FogMaterialPath);
        }

        material.shader = shader;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static ForestDepthFogZone[] CreateFogZones(Transform parent, MapAsset mapAsset)
    {
        var width = mapAsset != null ? mapAsset.Width : 50;
        var height = mapAsset != null ? mapAsset.Height : 50;
        var midX = (width - 1) * 0.5f;
        var midZ = (height - 1) * 0.5f;
        var maxSize = Mathf.Max(width, height);

        var presets = new[]
        {
            new FogZonePreset("FogZone_01_NorthForest", new Vector3(midX, 0.05f, height - 3f), Vector3.back, width + 10f, 16f, 7f, 0.040f, 1.0f, 0.040f, new Color(0.07f, 0.14f, 0.15f, 1f)),
            new FogZonePreset("FogZone_02_WestForest", new Vector3(3f, 0.05f, midZ), Vector3.right, height + 8f, 13f, 7f, 0.035f, 0.9f, 0.045f, new Color(0.06f, 0.13f, 0.14f, 1f)),
            new FogZonePreset("FogZone_03_EastForest", new Vector3(width - 3f, 0.05f, midZ), Vector3.left, height + 8f, 13f, 7f, 0.035f, 0.9f, 0.045f, new Color(0.06f, 0.13f, 0.14f, 1f)),
            new FogZonePreset("FogZone_04_SouthRiver", new Vector3(midX, 0.05f, 3f), Vector3.forward, width + 6f, 12f, 6f, 0.030f, 0.8f, 0.050f, new Color(0.05f, 0.12f, 0.15f, 1f)),
            new FogZonePreset("FogZone_05_GateDepth", new Vector3(midX, 0.05f, height * 0.72f), Vector3.back, maxSize * 0.55f, 18f, 8f, 0.032f, 0.8f, 0.045f, new Color(0.08f, 0.15f, 0.16f, 1f))
        };

        var zones = new ForestDepthFogZone[presets.Length];
        for (var i = 0; i < presets.Length; i++)
        {
            var preset = presets[i];
            var zoneObject = new GameObject(preset.Name);
            zoneObject.transform.SetParent(parent, false);
            zoneObject.transform.position = preset.Position;
            zoneObject.transform.rotation = Quaternion.LookRotation(preset.Forward.normalized, Vector3.up);

            var zone = zoneObject.AddComponent<ForestDepthFogZone>();
            zone.Configure(
                preset.Width,
                preset.Length,
                preset.EdgeBlendDistance,
                preset.Density,
                preset.NoiseStrength,
                preset.NoiseScale,
                preset.Color);
            zones[i] = zone;
        }

        return zones;
    }

    private static void ConfigureRendererFeature(string rendererPath, Material material)
    {
        var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
        if (rendererData == null)
        {
            Debug.LogWarning("[TownForestSceneSetup] Renderer asset not found: " + rendererPath);
            return;
        }

        var feature = FindOrCreateRendererFeature(rendererData);
        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
        feature.fetchColorBuffer = true;
        feature.requirements = ScriptableRenderPassInput.Depth;
        feature.passMaterial = material;
        feature.passIndex = 0;
        feature.bindDepthStencilAttachment = false;

        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(rendererData);
    }

    private static FullScreenPassRendererFeature FindOrCreateRendererFeature(ScriptableRendererData rendererData)
    {
        var obsoleteFeatures = new List<Object>();
        var serializedRenderer = new SerializedObject(rendererData);
        var features = serializedRenderer.FindProperty("m_RendererFeatures");
        FullScreenPassRendererFeature feature = null;

        for (var i = features.arraySize - 1; i >= 0; i--)
        {
            var element = features.GetArrayElementAtIndex(i);
            var rendererFeature = element.objectReferenceValue as ScriptableRendererFeature;
            if (rendererFeature == null)
            {
                continue;
            }

            var isDepthFogFeature = rendererFeature.name == RendererFeatureName;
            if (rendererFeature is FullScreenPassRendererFeature fullscreenFeature && isDepthFogFeature && feature == null)
            {
                feature = fullscreenFeature;
                continue;
            }

            if (isDepthFogFeature || rendererFeature.GetType().Name == "ForestDepthFogRendererFeature")
            {
                obsoleteFeatures.Add(rendererFeature);
                element.objectReferenceValue = null;
                features.DeleteArrayElementAtIndex(i);
            }
        }

        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();

        foreach (var obsoleteFeature in obsoleteFeatures)
        {
            Object.DestroyImmediate(obsoleteFeature, true);
        }

        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = RendererFeatureName;
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            serializedRenderer = new SerializedObject(rendererData);
            var updatedFeatures = serializedRenderer.FindProperty("m_RendererFeatures");
            updatedFeatures.InsertArrayElementAtIndex(updatedFeatures.arraySize);
            updatedFeatures.GetArrayElementAtIndex(updatedFeatures.arraySize - 1).objectReferenceValue = feature;
            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        }

        return feature;
    }

    private static FullScreenPassRendererFeature FindRendererFeature(ScriptableRendererData rendererData)
    {
        var serializedRenderer = new SerializedObject(rendererData);
        var features = serializedRenderer.FindProperty("m_RendererFeatures");
        for (var i = 0; i < features.arraySize; i++)
        {
            if (features.GetArrayElementAtIndex(i).objectReferenceValue is FullScreenPassRendererFeature feature &&
                feature.name == RendererFeatureName)
            {
                return feature;
            }
        }

        return null;
    }

    private static bool HasRendererFeature(string rendererPath)
    {
        var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
        return rendererData != null && FindRendererFeature(rendererData) != null;
    }

    private static void SaveMaterialDisabled(Material material)
    {
        material.SetFloat("_FogEnabled", 0f);
        material.SetInt("_FogZoneCount", 0);
        EditorUtility.SetDirty(material);
    }

    private static void SortHierarchy()
    {
        SetRootSibling(GameplayRootName, 0);
        SetRootSibling(AppearanceRootName, 1);
        SetRootSibling(LightingRootName, 2);

        var gameplayRoot = GameObject.Find(GameplayRootName);
        if (gameplayRoot != null)
        {
            SetChildSibling(gameplayRoot.transform, "Main Camera", 0);
            SetChildSibling(gameplayRoot.transform, "BoardView", 1);
            SetChildSibling(gameplayRoot.transform, RuntimeEntitiesRootName, 2);
            SetChildSibling(gameplayRoot.transform, RuntimeSkillRunnersRootName, 3);
            SetChildSibling(gameplayRoot.transform, "TownSceneContext", 4);
        }

        var appearanceRoot = GameObject.Find(AppearanceRootName);
        if (appearanceRoot != null)
        {
            SetChildSibling(appearanceRoot.transform, BoardTilesRootName, 0);
            SetChildSibling(appearanceRoot.transform, LightingPropsRootName, 1);
        }

        var lightingRoot = GameObject.Find(LightingRootName);
        if (lightingRoot != null)
        {
            SetChildSibling(lightingRoot.transform, MoonLightName, 0);
            SetChildSibling(lightingRoot.transform, CoolFillLightName, 1);
            SetChildSibling(lightingRoot.transform, DepthFogRootName, 2);
            SetChildSibling(lightingRoot.transform, PostProcessName, 3);
        }
    }

    private static void SetRootSibling(string rootName, int index)
    {
        var root = GameObject.Find(rootName);
        root?.transform.SetSiblingIndex(index);
    }

    private static void SetChildSibling(Transform parent, string childName, int index)
    {
        var child = FindDirectChild(parent, childName);
        child?.SetSiblingIndex(index);
    }

    private static void Report(bool condition, string success, string failure, ref bool hasError)
    {
        if (condition)
        {
            Debug.Log("[TownForestSceneSetup] OK - " + success);
            return;
        }

        hasError = true;
        Debug.LogError("[TownForestSceneSetup] " + failure);
    }

    private readonly struct PropPreset
    {
        public readonly string Name;
        public readonly Vector3 Position;
        public readonly float Yaw;

        public PropPreset(string name, Vector3 position, float yaw)
        {
            Name = name;
            Position = position;
            Yaw = yaw;
        }
    }

    private readonly struct FogZonePreset
    {
        public readonly string Name;
        public readonly Vector3 Position;
        public readonly Vector3 Forward;
        public readonly float Width;
        public readonly float Length;
        public readonly float EdgeBlendDistance;
        public readonly float Density;
        public readonly float NoiseStrength;
        public readonly float NoiseScale;
        public readonly Color Color;

        public FogZonePreset(
            string name,
            Vector3 position,
            Vector3 forward,
            float width,
            float length,
            float edgeBlendDistance,
            float density,
            float noiseStrength,
            float noiseScale,
            Color color)
        {
            Name = name;
            Position = position;
            Forward = forward;
            Width = width;
            Length = length;
            EdgeBlendDistance = edgeBlendDistance;
            Density = density;
            NoiseStrength = noiseStrength;
            NoiseScale = noiseScale;
            Color = color;
        }
    }
}
#endif
