#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using RhythmRPG.Game.Visual.SceneEffects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BlacksmithFurnaceFireSetup
{
    private const string ScenePath = "Assets/0.MainProject/Scenes/Town/Town_Forest.unity";
    private const string AnchorPath = "Blacksmith_House/furnace";
    private const string RootName = "PF_Blacksmith_LowPoly_FurnaceFire";
    private const string ArtFolder = "Assets/0.MainProject/Art/BlacksmithFurnace";
    private const string MaterialFolder = ArtFolder + "/Materials";
    private const string MeshFolder = ArtFolder + "/Meshes";
    private const string PrefabPath = "Assets/0.MainProject/Prefabs/SceneSets/Forest/PF_Blacksmith_LowPoly_FurnaceFire.prefab";
    private const string BloomProfilePath = ArtFolder + "/PP_Blacksmith_FurnaceBloom.asset";
    private const string BloomVolumeName = "Codex_Blacksmith_Furnace_Bloom";

    private static readonly Color StoneColor = new(0.13f, 0.12f, 0.12f, 1f);
    private static readonly Color WarmStoneColor = new(0.24f, 0.17f, 0.13f, 1f);
    private static readonly Color CoalColor = new(0.035f, 0.035f, 0.04f, 1f);
    private static readonly Color EmberColor = new(1f, 0.25f, 0.025f, 1f);
    private static readonly Color FlameOrange = new(1f, 0.28f, 0.035f, 0.72f);
    private static readonly Color FlameYellow = new(1f, 0.86f, 0.18f, 0.62f);
    private static readonly Color GlowColor = new(1f, 0.33f, 0.04f, 0.34f);
    private static readonly Color SmokeColor = new(0.28f, 0.27f, 0.26f, 0.33f);

    [MenuItem("RhythmRPG/Editors/World/Apply Blacksmith Furnace Fire")]
    public static void Apply()
    {
        EnsureTownForestScene();
        EnsureFolders();

        var assets = CreateOrUpdateAssets();
        CreateOrUpdatePrefab(assets);
        var root = PlacePrefabInScene();
        ConfigureBloomVolume();
        ConfigureCameraPostProcessing();

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var stoneCount = root.transform.Find("StoneGroup")?.childCount ?? 0;
        var coalCount = root.transform.Find("CoalGroup")?.childCount ?? 0;

        Debug.Log(
            "[BlacksmithFurnaceFireSetup] Applied low-poly furnace fire. " +
            $"Root={GetPath(root)}, Anchor={AnchorPath}, Prefab={PrefabPath}, " +
            $"Stones={stoneCount}, " +
            $"Coals={coalCount}, " +
            $"Particles={root.GetComponentsInChildren<ParticleSystem>(true).Length}, " +
            $"BloomVolume={BloomVolumeName}.");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Blacksmith Furnace Fire")]
    public static void Validate()
    {
        var anchor = GameObject.Find(AnchorPath);
        var root = anchor != null ? anchor.transform.Find(RootName) : null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var effect = root != null ? root.GetComponent<BlacksmithFurnaceFireEffect>() : null;
        var light = root != null ? root.GetComponentInChildren<Light>(true) : null;
        var particles = root != null ? root.GetComponentsInChildren<ParticleSystem>(true) : Array.Empty<ParticleSystem>();
        var stoneGroup = root != null ? root.Find("StoneGroup") : null;
        var coalGroup = root != null ? root.Find("CoalGroup") : null;
        var flameGroup = root != null ? root.Find("FlameGroup") : null;
        var glowGroup = root != null ? root.Find("GlowGroup") : null;
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomProfilePath);
        var volume = GameObject.Find(BloomVolumeName);

        var ok = anchor != null
            && root != null
            && prefab != null
            && effect != null
            && light != null
            && particles.Length >= 2
            && stoneGroup != null && stoneGroup.childCount >= 12
            && coalGroup != null && coalGroup.childCount >= 30
            && flameGroup != null && flameGroup.childCount >= 6
            && glowGroup != null && glowGroup.childCount >= 2
            && profile != null
            && volume != null;

        if (!ok)
        {
            Debug.LogError(
                "[BlacksmithFurnaceFireSetup] Validation failed. " +
                $"Anchor={anchor != null}, Root={root != null}, Prefab={prefab != null}, " +
                $"Effect={effect != null}, Light={light != null}, Particles={particles.Length}, " +
                $"StoneGroup={stoneGroup?.childCount ?? -1}, CoalGroup={coalGroup?.childCount ?? -1}, " +
                $"FlameGroup={flameGroup?.childCount ?? -1}, GlowGroup={glowGroup?.childCount ?? -1}, " +
                $"BloomProfile={profile != null}, BloomVolume={volume != null}.");
            return;
        }

        Debug.Log(
            "[BlacksmithFurnaceFireSetup] VALIDATION OK. " +
            $"Root={GetPath(root.gameObject)}, LightIntensity={light.intensity:F2}, LightRange={light.range:F2}, " +
            $"Particles={particles.Length}, Prefab={PrefabPath}.");
    }

    private static FurnaceAssets CreateOrUpdateAssets()
    {
        var assets = new FurnaceAssets
        {
            Stone = CreateLitMaterial(MaterialFolder + "/M_BlacksmithFurnace_Stone_Dark.mat", StoneColor, Color.black, 0f, false),
            WarmStone = CreateLitMaterial(MaterialFolder + "/M_BlacksmithFurnace_Stone_WarmEdge.mat", WarmStoneColor, Color.black, 0f, false),
            Coal = CreateLitMaterial(MaterialFolder + "/M_BlacksmithFurnace_Coal_Charcoal.mat", CoalColor, new Color(0.09f, 0.025f, 0.01f, 1f), 0.4f, false),
            Ember = CreateLitMaterial(MaterialFolder + "/M_BlacksmithFurnace_Coal_Ember.mat", CoalColor, EmberColor, 2.2f, false),
            FlameOrange = CreateLitMaterial(MaterialFolder + "/M_BlacksmithFurnace_Flame_Orange.mat", FlameOrange, FlameOrange, 4.4f, true),
            FlameYellow = CreateLitMaterial(MaterialFolder + "/M_BlacksmithFurnace_Flame_Yellow.mat", FlameYellow, FlameYellow, 6.2f, true),
            Glow = CreateLitMaterial(MaterialFolder + "/M_BlacksmithFurnace_Glow_Plane.mat", GlowColor, GlowColor, 3.1f, true),
            Smoke = CreateLitMaterial(MaterialFolder + "/M_BlacksmithFurnace_Smoke.mat", SmokeColor, Color.black, 0f, true),
            EmberParticle = CreateParticleMaterial(MaterialFolder + "/M_BlacksmithFurnace_EmberParticle.mat", new Color(1f, 0.48f, 0.06f, 0.9f), EmberColor, 3.4f),
            SmokeParticle = CreateParticleMaterial(MaterialFolder + "/M_BlacksmithFurnace_SmokeParticle.mat", SmokeColor, Color.black, 0f)
        };

        assets.CoalMesh = CreateOrUpdateMesh(MeshFolder + "/SM_BlacksmithFurnace_LowPolyCoal.asset", "SM_BlacksmithFurnace_LowPolyCoal", CreateCoalVertices(), CreateOctahedronTriangles(), null);
        assets.FlameMesh = CreateOrUpdateMesh(MeshFolder + "/SM_BlacksmithFurnace_FlameShard.asset", "SM_BlacksmithFurnace_FlameShard", CreateFlameVertices(), CreateFlameTriangles(), CreateFlameUv());
        assets.GlowMesh = CreateOrUpdateMesh(MeshFolder + "/SM_BlacksmithFurnace_GlowDiamond.asset", "SM_BlacksmithFurnace_GlowDiamond", CreateGlowVertices(), CreateDoubleSidedQuadTriangles(), CreateGlowUv());
        assets.SmokePuffMesh = CreateOrUpdateMesh(MeshFolder + "/SM_BlacksmithFurnace_SmokePuff.asset", "SM_BlacksmithFurnace_SmokePuff", CreateSmokePuffVertices(), CreateOctahedronTriangles(), null);

        return assets;
    }

    private static void CreateOrUpdatePrefab(FurnaceAssets assets)
    {
        var source = BuildFurnaceRoot(assets);
        source.name = RootName;
        source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        source.transform.localScale = Vector3.one;

        PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
        Object.DestroyImmediate(source);
    }

    private static GameObject PlacePrefabInScene()
    {
        var anchor = GameObject.Find(AnchorPath);
        if (anchor == null)
        {
            throw new InvalidOperationException($"Anchor not found: {AnchorPath}");
        }

        var existing = anchor.transform.Find(RootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Prefab not found after build: {PrefabPath}");
        }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
        root.name = RootName;
        root.transform.SetPositionAndRotation(anchor.transform.position + Vector3.up * 0.045f, Quaternion.Euler(0f, 18f, 0f));
        root.transform.localScale = Vector3.one;
        root.transform.SetParent(anchor.transform, true);

        EditorUtility.SetDirty(root);
        return root;
    }

    private static GameObject BuildFurnaceRoot(FurnaceAssets assets)
    {
        var root = new GameObject(RootName);
        var stoneGroup = CreateGroup(root.transform, "StoneGroup");
        var coalGroup = CreateGroup(root.transform, "CoalGroup");
        var glowGroup = CreateGroup(root.transform, "GlowGroup");
        var flameGroup = CreateGroup(root.transform, "FlameGroup");
        var particleGroup = CreateGroup(root.transform, "ParticleGroup");
        var smokeGroup = CreateGroup(root.transform, "SmokeGroup");
        var lightGroup = CreateGroup(root.transform, "LightGroup");

        var flameRenderers = new List<Renderer>();
        BuildStoneRing(stoneGroup, assets);
        BuildCoalBed(coalGroup, assets);
        BuildGlowPlanes(glowGroup, assets, flameRenderers);
        BuildFlames(flameGroup, assets, flameRenderers);
        BuildSmokePuffs(smokeGroup, assets);

        var particles = new[]
        {
            CreateEmberParticles(particleGroup, assets),
            CreateSmokeParticles(particleGroup, assets)
        };

        var fireLight = CreateFireLight(lightGroup);

        var furnaceEffect = root.AddComponent<BlacksmithFurnaceFireEffect>();
        furnaceEffect.Configure(fireLight, flameRenderers.ToArray(), particles, EmberColor, 2.35f, 4.2f);

        var beatPulse = root.AddComponent<ForestBeatLightPulse>();
        beatPulse.Configure(
            new[] { fireLight },
            flameRenderers.ToArray(),
            particles,
            EmberColor,
            lightPeak: 1.18f,
            emissionPeak: 1.24f,
            alphaPeak: 1.08f,
            particlePeak: 1.12f,
            durationBeats: 0.38f,
            falloff: 2.4f,
            flicker: 0.035f,
            offsetBeats: 0f,
            lightBase: 1f,
            rendererBase: 1f,
            particleBase: 1f);

        return root;
    }

    private static void BuildStoneRing(Transform parent, FurnaceAssets assets)
    {
        var random = new System.Random(1147);
        const int count = 16;
        for (var i = 0; i < count; i++)
        {
            var angle = (Mathf.PI * 2f / count) * i;
            var radiusX = 0.66f + RandomRange(random, -0.025f, 0.03f);
            var radiusZ = 0.49f + RandomRange(random, -0.02f, 0.035f);
            var position = new Vector3(Mathf.Cos(angle) * radiusX, 0.055f, Mathf.Sin(angle) * radiusZ);
            var scale = new Vector3(
                RandomRange(random, 0.18f, 0.29f),
                RandomRange(random, 0.09f, 0.16f),
                RandomRange(random, 0.14f, 0.24f));
            var rotation = new Vector3(
                RandomRange(random, -3f, 3f),
                -angle * Mathf.Rad2Deg + 90f + RandomRange(random, -9f, 9f),
                RandomRange(random, -4f, 4f));

            CreateBlock(
                parent,
                $"Stone_Ring_{i:00}",
                position,
                rotation,
                scale,
                i % 4 == 0 ? assets.WarmStone : assets.Stone);
        }

        CreateBlock(parent, "HeatShield_Back_Left", new Vector3(-0.42f, 0.42f, 0.58f), new Vector3(0f, -8f, 2f), new Vector3(0.22f, 0.78f, 0.1f), assets.WarmStone);
        CreateBlock(parent, "HeatShield_Back_Center", new Vector3(0f, 0.45f, 0.61f), new Vector3(0f, 0f, -2f), new Vector3(0.26f, 0.88f, 0.11f), assets.WarmStone);
        CreateBlock(parent, "HeatShield_Back_Right", new Vector3(0.42f, 0.42f, 0.58f), new Vector3(0f, 8f, -1.5f), new Vector3(0.22f, 0.78f, 0.1f), assets.WarmStone);
    }

    private static void BuildCoalBed(Transform parent, FurnaceAssets assets)
    {
        var random = new System.Random(4082);
        for (var i = 0; i < 42; i++)
        {
            var radius = Mathf.Sqrt((float)random.NextDouble()) * 0.43f;
            var angle = (float)random.NextDouble() * Mathf.PI * 2f;
            var position = new Vector3(Mathf.Cos(angle) * radius, RandomRange(random, 0.1f, 0.18f), Mathf.Sin(angle) * radius * 0.82f);
            var scaleValue = RandomRange(random, 0.075f, 0.16f);
            var scale = new Vector3(scaleValue * RandomRange(random, 0.85f, 1.35f), scaleValue * RandomRange(random, 0.45f, 0.82f), scaleValue * RandomRange(random, 0.85f, 1.35f));
            var material = i % 3 == 0 || radius < 0.2f ? assets.Ember : assets.Coal;

            CreateMeshObject(
                parent,
                $"Coal_Chunk_{i:00}",
                assets.CoalMesh,
                material,
                position,
                new Vector3(RandomRange(random, 0f, 360f), RandomRange(random, 0f, 360f), RandomRange(random, 0f, 360f)),
                scale);
        }
    }

    private static void BuildGlowPlanes(Transform parent, FurnaceAssets assets, List<Renderer> glowRenderers)
    {
        var scales = new[]
        {
            new Vector3(1.05f, 1f, 0.72f),
            new Vector3(0.72f, 1f, 0.5f),
            new Vector3(0.42f, 1f, 0.3f)
        };

        for (var i = 0; i < scales.Length; i++)
        {
            var renderer = CreateMeshObject(
                parent,
                $"Glow_EmberPlane_{i:00}",
                assets.GlowMesh,
                assets.Glow,
                new Vector3(0f, 0.072f + i * 0.004f, 0f),
                new Vector3(0f, i * 37f, 0f),
                scales[i]);
            glowRenderers.Add(renderer);
        }
    }

    private static void BuildFlames(Transform parent, FurnaceAssets assets, List<Renderer> flameRenderers)
    {
        var random = new System.Random(839);
        var positions = new[]
        {
            new Vector3(0f, 0.14f, -0.02f),
            new Vector3(-0.18f, 0.13f, 0.03f),
            new Vector3(0.19f, 0.13f, 0.02f),
            new Vector3(-0.06f, 0.15f, -0.18f),
            new Vector3(0.09f, 0.14f, 0.17f),
            new Vector3(0.28f, 0.13f, -0.11f),
            new Vector3(-0.3f, 0.13f, -0.08f)
        };

        for (var i = 0; i < positions.Length; i++)
        {
            var cluster = CreateGroup(parent, $"Flame_Cluster_{i:00}");
            cluster.localPosition = positions[i];
            cluster.localRotation = Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f);

            var height = RandomRange(random, 0.45f, 0.8f);
            var width = height * RandomRange(random, 0.32f, 0.48f);
            var orange = CreateMeshObject(
                cluster,
                "Flame_Orange",
                assets.FlameMesh,
                assets.FlameOrange,
                Vector3.zero,
                Vector3.zero,
                new Vector3(width, height, 1f));
            flameRenderers.Add(orange);

            var cross = CreateMeshObject(
                cluster,
                "Flame_Orange_Cross",
                assets.FlameMesh,
                assets.FlameOrange,
                Vector3.zero,
                new Vector3(0f, 90f, 0f),
                new Vector3(width * 0.78f, height * 0.86f, 1f));
            flameRenderers.Add(cross);

            var core = CreateMeshObject(
                cluster,
                "Flame_Yellow_Core",
                assets.FlameMesh,
                assets.FlameYellow,
                new Vector3(0f, 0.02f, 0f),
                new Vector3(0f, 45f, 0f),
                new Vector3(width * 0.48f, height * 0.72f, 1f));
            flameRenderers.Add(core);
        }
    }

    private static void BuildSmokePuffs(Transform parent, FurnaceAssets assets)
    {
        var random = new System.Random(245);
        for (var i = 0; i < 5; i++)
        {
            CreateMeshObject(
                parent,
                $"LowPoly_SmokePuff_{i:00}",
                assets.SmokePuffMesh,
                assets.Smoke,
                new Vector3(RandomRange(random, -0.13f, 0.13f), 0.74f + i * 0.16f, 0.08f + i * 0.035f),
                new Vector3(RandomRange(random, -12f, 12f), RandomRange(random, 0f, 360f), RandomRange(random, -12f, 12f)),
                Vector3.one * RandomRange(random, 0.13f, 0.25f));
        }
    }

    private static ParticleSystem CreateEmberParticles(Transform parent, FurnaceAssets assets)
    {
        var go = new GameObject("FX_Embers_Rising");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.24f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.duration = 4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.82f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.045f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.38f, 0.04f, 0.95f), new Color(1f, 0.72f, 0.14f, 0.75f));
        main.gravityModifier = -0.02f;
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = ps.emission;
        emission.rateOverTime = 13f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0.08f, 2, 5, 4, 0.24f)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.38f;
        shape.radiusThickness = 0.8f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.6f, 1.15f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.85f, 0.24f), 0f),
                new GradientColorKey(new Color(1f, 0.26f, 0.03f), 0.65f),
                new GradientColorKey(new Color(0.28f, 0.08f, 0.02f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(0.62f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = assets.EmberParticle;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingFudge = 0.15f;

        return ps;
    }

    private static ParticleSystem CreateSmokeParticles(Transform parent, FurnaceAssets assets)
    {
        var go = new GameObject("FX_Smoke_SoftPuffs");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0.08f, 0.48f, 0.08f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.7f, 3.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.28f, 0.25f, 0.23f, 0.22f), new Color(0.42f, 0.4f, 0.37f, 0.32f));
        main.gravityModifier = -0.015f;
        main.maxParticles = 55;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = ps.emission;
        emission.rateOverTime = 5.5f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.24f;
        shape.radiusThickness = 0.65f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.12f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.26f, 0.58f);
        velocity.z = new ParticleSystem.MinMaxCurve(0.02f, 0.18f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.7f));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.25f, 0.22f, 0.2f), 0f),
                new GradientColorKey(new Color(0.44f, 0.41f, 0.37f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.28f, 0.16f),
                new GradientAlphaKey(0.18f, 0.58f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = assets.SmokeParticle;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingFudge = -0.05f;

        return ps;
    }

    private static Light CreateFireLight(Transform parent)
    {
        var go = new GameObject("PointLight_Furnace_Flicker");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.55f, 0.02f);

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.43f, 0.12f, 1f);
        light.intensity = 2.35f;
        light.range = 4.2f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.5f;
        return light;
    }

    private static Transform CreateGroup(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Renderer CreateBlock(Transform parent, string name, Vector3 position, Vector3 rotation, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localEulerAngles = rotation;
        go.transform.localScale = scale;

        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    private static Renderer CreateMeshObject(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 rotation, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localEulerAngles = rotation;
        go.transform.localScale = scale;

        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static Material CreateLitMaterial(string path, Color baseColor, Color emissionColor, float emissionIntensity, bool transparent)
    {
        var shader = FindShader(
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Standard");

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        SetMaterialColor(material, baseColor);
        ConfigureTransparency(material, transparent);
        ConfigureEmission(material, emissionColor, emissionIntensity);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateParticleMaterial(string path, Color baseColor, Color emissionColor, float emissionIntensity)
    {
        var shader = FindShader(
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default",
            "Universal Render Pipeline/Unlit");

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        SetMaterialColor(material, baseColor);
        ConfigureTransparency(material, true);
        ConfigureEmission(material, emissionColor, emissionIntensity);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void ConfigureTransparency(Material material, bool transparent)
    {
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", transparent ? 1f : 0f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)CullMode.Off);
        }

        if (transparent)
        {
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = -1;
        }
    }

    private static void ConfigureEmission(Material material, Color color, float intensity)
    {
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", color * Mathf.Max(0f, intensity));
        }

        if (intensity > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
    }

    private static Mesh CreateOrUpdateMesh(string path, string meshName, Vector3[] vertices, int[] triangles, Vector2[] uv)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var created = false;
        if (mesh == null)
        {
            mesh = new Mesh();
            created = true;
        }

        mesh.name = meshName;
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        if (uv != null)
        {
            mesh.uv = uv;
        }

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (created)
        {
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            EditorUtility.SetDirty(mesh);
        }

        return mesh;
    }

    private static Vector3[] CreateCoalVertices()
    {
        return new[]
        {
            new Vector3(0f, 0.65f, 0f),
            new Vector3(0f, -0.35f, 0f),
            new Vector3(-0.56f, 0f, -0.1f),
            new Vector3(0.48f, 0.02f, 0.12f),
            new Vector3(0.08f, -0.04f, 0.58f),
            new Vector3(-0.1f, 0.05f, -0.5f)
        };
    }

    private static Vector3[] CreateSmokePuffVertices()
    {
        return new[]
        {
            new Vector3(0f, 0.52f, 0f),
            new Vector3(0f, -0.42f, 0f),
            new Vector3(-0.58f, 0f, -0.12f),
            new Vector3(0.52f, 0.02f, 0.1f),
            new Vector3(0.08f, -0.04f, 0.5f),
            new Vector3(-0.12f, 0.04f, -0.56f)
        };
    }

    private static int[] CreateOctahedronTriangles()
    {
        return new[]
        {
            0, 4, 3,
            0, 3, 5,
            0, 5, 2,
            0, 2, 4,
            1, 3, 4,
            1, 5, 3,
            1, 2, 5,
            1, 4, 2
        };
    }

    private static Vector3[] CreateFlameVertices()
    {
        return new[]
        {
            new Vector3(-0.5f, 0f, 0f),
            new Vector3(0.5f, 0f, 0f),
            new Vector3(0.24f, 0.38f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(-0.28f, 0.44f, 0f)
        };
    }

    private static int[] CreateFlameTriangles()
    {
        return new[]
        {
            0, 1, 2,
            0, 2, 4,
            4, 2, 3,
            2, 1, 0,
            4, 2, 0,
            3, 2, 4
        };
    }

    private static Vector2[] CreateFlameUv()
    {
        return new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.72f, 0.42f),
            new Vector2(0.5f, 1f),
            new Vector2(0.22f, 0.45f)
        };
    }

    private static Vector3[] CreateGlowVertices()
    {
        return new[]
        {
            new Vector3(0f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0f),
            new Vector3(0f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0f)
        };
    }

    private static int[] CreateDoubleSidedQuadTriangles()
    {
        return new[]
        {
            0, 1, 2,
            0, 2, 3,
            2, 1, 0,
            3, 2, 0
        };
    }

    private static Vector2[] CreateGlowUv()
    {
        return new[]
        {
            new Vector2(0.5f, 1f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 0.5f)
        };
    }

    private static void ConfigureBloomVolume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, BloomProfilePath);
        }

        if (!profile.TryGet<Bloom>(out var bloom))
        {
            bloom = profile.Add<Bloom>(true);
        }

        bloom.active = true;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.82f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.9f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.68f;
        bloom.tint.overrideState = true;
        bloom.tint.value = new Color(1f, 0.58f, 0.22f, 1f);

        var volumeObject = GameObject.Find(BloomVolumeName);
        if (volumeObject == null)
        {
            volumeObject = new GameObject(BloomVolumeName);
        }

        var volume = volumeObject.GetComponent<Volume>();
        if (volume == null)
        {
            volume = volumeObject.AddComponent<Volume>();
        }

        volume.isGlobal = true;
        volume.priority = 75f;
        volume.weight = 0.55f;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(volumeObject);
    }

    private static void ConfigureCameraPostProcessing()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
        {
            cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        cameraData.renderPostProcessing = true;
        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(cameraData);
    }

    private static Shader FindShader(params string[] shaderNames)
    {
        for (var i = 0; i < shaderNames.Length; i++)
        {
            var shader = Shader.Find(shaderNames[i]);
            if (shader != null)
            {
                return shader;
            }
        }

        return Shader.Find("Standard");
    }

    private static void EnsureTownForestScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == "Town_Forest")
        {
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            throw new InvalidOperationException("Scene switch cancelled before opening Town_Forest.");
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/0.MainProject/Art");
        EnsureFolder(ArtFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(MeshFolder);
        EnsureFolder("Assets/0.MainProject/Prefabs");
        EnsureFolder("Assets/0.MainProject/Prefabs/SceneSets");
        EnsureFolder("Assets/0.MainProject/Prefabs/SceneSets/Forest");
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var slash = folderPath.LastIndexOf('/');
        if (slash < 0)
        {
            return;
        }

        var parent = folderPath.Substring(0, slash);
        var name = folderPath.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private static string GetPath(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return "None";
        }

        var path = gameObject.name;
        var current = gameObject.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private sealed class FurnaceAssets
    {
        public Material Stone;
        public Material WarmStone;
        public Material Coal;
        public Material Ember;
        public Material FlameOrange;
        public Material FlameYellow;
        public Material Glow;
        public Material Smoke;
        public Material EmberParticle;
        public Material SmokeParticle;
        public Mesh CoalMesh;
        public Mesh FlameMesh;
        public Mesh GlowMesh;
        public Mesh SmokePuffMesh;
    }
}
#endif
