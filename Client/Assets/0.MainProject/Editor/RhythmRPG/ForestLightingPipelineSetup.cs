#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class ForestLightingPipelineSetup
{
    private const string RootName = "Codex_LightingPipeline_Showcase";
    private const string AssetFolder = "Assets/0.MainProject/Art/ForestLightingPipeline";
    private const string CrystalPrefabPath = AssetFolder + "/PF_Crystal_Pillar_Lighting.prefab";
    private const string LanternPrefabPath = AssetFolder + "/PF_Lantern_Lighting.prefab";
    private const string VolumeProfilePath = AssetFolder + "/PP_ForestLightingPipeline_Bloom_ACES.asset";
    private const string LightingSettingsPath = AssetFolder + "/LS_ForestLightingPipeline_Shadowmask.asset";
    private const string CrystalMeshPath = AssetFolder + "/SM_Crystal_Body_HexBipyramid.asset";
    private const string HexCylinderMeshPath = AssetFolder + "/SM_HexCylinder.asset";
    private const string DiskMeshPath = AssetFolder + "/SM_LightReceiver_Disk.asset";
    private const string RuneMaskPath = AssetFolder + "/T_Crystal_RuneEmissionMask.png";
    private const string ParticleSoftTexturePath = AssetFolder + "/T_Particle_SoftCircle.png";
    private const string SkyboxPanoramicTexturePath = AssetFolder + "/T_Forest_SkyBox_Panoramic.png";
    private const string SkyboxMaterialPath = AssetFolder + "/M_Forest_Night_Skybox.mat";

    private static readonly Color LanternBase = new(1f, 150f / 255f, 50f / 255f, 1f);
    private static readonly Color CrystalBase = new(50f / 255f, 200f / 255f, 1f, 1f);

    [MenuItem("RhythmRPG/Editors/World/Build Forest Lighting Pipeline Showcase")]
    public static void Build()
    {
        EnsureTargetScene();
        EnsureAssetFolder();

        var assets = CreateOrUpdateAssets();
        RemoveExistingGeneratedRoots();

        var root = new GameObject(RootName);
        root.transform.position = Vector3.zero;

        CreatePostProcessVolume(root.transform, assets.VolumeProfile);
        CreateEnvironmentFillLights(root.transform);
        CreateLightingNote(root.transform);
        CreateLocalFogExamples(root.transform, assets);

        var crystalPrefab = CreateCrystalPrefab(assets);
        var lanternPrefab = CreateLanternPrefab(assets);

        InstantiatePrefab(crystalPrefab, "PF_Crystal_Pillar_Cyan_Left", root.transform, new Vector3(5.9f, 0f, 12.85f), 18f);
        InstantiatePrefab(crystalPrefab, "PF_Crystal_Pillar_Cyan_Right", root.transform, new Vector3(9.65f, 0f, 12.15f), -22f);
        InstantiatePrefab(lanternPrefab, "PF_Lantern_Warm_Path_Left", root.transform, new Vector3(6.35f, 0f, 9.35f), 0f);
        InstantiatePrefab(lanternPrefab, "PF_Lantern_Warm_Path_Right", root.transform, new Vector3(10.75f, 0f, 10.35f), -16f);

        SetStaticFlagsRecursively(root);
        ConfigureForestNightRenderSettings(assets.Skybox);
        ConfigureLightingSettings();
        TryStartBake();
        FrameSceneView(root);

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var focus = GameObject.Find("FX_LocalFog_Gate_Cyan_Box") ?? root;
        FrameSceneView(focus);

        Debug.Log(
            "[ForestLightingPipelineSetup] Setup complete. " +
            $"Root={root.name}, Scene={scene.name}, Materials=10, Prefabs=2, BloomThreshold=1.25, " +
            $"BloomIntensity=1.45, BloomScatter=0.68, LanternEmissionEV=2.5, CrystalEmissionEV=2.0, " +
            $"BakeRunning={Lightmapping.isRunning}.");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Forest Lighting Pipeline Showcase")]
    public static void Validate()
    {
        var root = FindLatestRoot();
        if (root == null)
        {
            Debug.LogError("[ForestLightingPipelineSetup] Validation failed: generated root object was not found.");
            return;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var lights = root.GetComponentsInChildren<Light>(true);
        var mixedLights = 0;
        foreach (var light in lights)
        {
            if (light.lightmapBakeType == LightmapBakeType.Mixed)
            {
                mixedLights++;
            }
        }
        var pointLights = 0;
        var directionalLights = 0;
        foreach (var light in lights)
        {
            if (light.type == LightType.Point)
            {
                pointLights++;
            }
            else if (light.type == LightType.Directional)
            {
                directionalLights++;
            }
        }

        var volume = root.GetComponentInChildren<Volume>(true);
        var bloomOk = false;
        var toneOk = false;
        var skyboxOk = RenderSettings.skybox != null
            && RenderSettings.skybox.name == "M_Forest_Night_Skybox"
            && RenderSettings.skybox.shader != null
            && RenderSettings.skybox.shader.name == "Skybox/Panoramic"
            && RenderSettings.skybox.HasProperty("_MainTex")
            && RenderSettings.skybox.GetTexture("_MainTex") != null;
        if (volume != null && volume.sharedProfile != null)
        {
            bloomOk = volume.sharedProfile.TryGet<Bloom>(out var bloom)
                && bloom.active
                && Mathf.Abs(bloom.threshold.value - 1.25f) < 0.02f
                && bloom.intensity.value >= 1.2f
                && bloom.scatter.value >= 0.6f;
            toneOk = volume.sharedProfile.TryGet<Tonemapping>(out var tone)
                && tone.active
                && tone.mode.value == TonemappingMode.ACES;
        }

        var mainCamera = Camera.main;
        var cameraData = mainCamera != null ? mainCamera.GetComponent<UniversalAdditionalCameraData>() : null;
        var postProcessingReady = cameraData != null && cameraData.renderPostProcessing;
        var localFogVolumes = 0;
        foreach (var renderer in renderers)
        {
            var material = renderer.sharedMaterial;
            if (renderer.name.StartsWith("FX_LocalFog", StringComparison.Ordinal)
                && material != null
                && material.shader != null
                && material.shader.name == "RhythmRPG/Effects/LocalFogVolume")
            {
                localFogVolumes++;
            }
        }

        if (localFogVolumes < 2)
        {
            Debug.LogError("[ForestLightingPipelineSetup] Validation failed: local fog volume examples were not found.");
            return;
        }

        if (!skyboxOk)
        {
            Debug.LogError("[ForestLightingPipelineSetup] Validation failed: forest skybox was not assigned.");
            return;
        }

        Debug.Log(
            "[ForestLightingPipelineSetup] VALIDATION OK. " +
            $"Root={root.name}, Renderers={renderers.Length}, Lights={lights.Length}, PointLights={pointLights}, " +
            $"DirectionalLights={directionalLights}, MixedLights={mixedLights}, " +
            $"GlobalVolume={(volume != null)}, Bloom={bloomOk}, ACES={toneOk}, " +
            $"Skybox={skyboxOk}, LocalFogVolumes={localFogVolumes}, " +
            $"MainCameraPostProcessing={postProcessingReady}, BakeRunning={Lightmapping.isRunning}.");
    }

    private static void EnsureTargetScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "Game_Forest_01")
        {
            Debug.LogWarning($"[ForestLightingPipelineSetup] Active scene is {scene.name}, expected Game_Forest_01.");
        }
    }

    private static void RemoveExistingGeneratedRoots()
    {
        var roots = new List<GameObject>();
        foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.scene.IsValid() && go.transform.parent == null && go.name.StartsWith(RootName, StringComparison.Ordinal))
            {
                roots.Add(go);
            }
        }

        foreach (var root in roots)
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    private static void EnsureAssetFolder()
    {
        var parts = AssetFolder.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static GeneratedAssets CreateOrUpdateAssets()
    {
        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null)
        {
            throw new InvalidOperationException("Universal Render Pipeline/Lit shader was not found.");
        }

        var runeMask = CreateRuneMaskTexture();
        var particleSoftTexture = CreateSoftParticleTexture();

        return new GeneratedAssets
        {
            CrystalMesh = LoadOrCreateMesh(CrystalMeshPath, CreateCrystalMesh()),
            HexCylinderMesh = LoadOrCreateMesh(HexCylinderMeshPath, CreateHexCylinderMesh(6)),
            DiskMesh = LoadOrCreateMesh(DiskMeshPath, CreateDiskMesh(24)),
            LanternFrame = CreateOrUpdateMaterial("M_Lantern_Frame", litShader, new Color(0.18f, 0.13f, 0.09f, 1f), Color.black, false, 0f),
            LanternGlass = CreateOrUpdateMaterial("M_Lantern_Glass", litShader, new Color(1f, 0.68f, 0.25f, 0.45f), Hdr(LanternBase, 1.2f), true, 0.45f),
            LanternGlow = CreateOrUpdateMaterial("M_Lantern_Glow", litShader, LanternBase, Hdr(LanternBase, 2.5f), false, 0f),
            CrystalPedestal = CreateOrUpdateMaterial("M_Crystal_Pedestal", litShader, new Color(0.24f, 0.27f, 0.28f, 1f), Color.black, false, 0f),
            CrystalGlow = CreateOrUpdateMaterial("M_Crystal_Glow", litShader, new Color(0.18f, 0.86f, 1f, 0.66f), Hdr(CrystalBase, 2.0f), true, 0.44f),
            CrystalRuneGlow = CreateOrUpdateMaterial("M_Crystal_Rune_Glow", litShader, CrystalBase, Hdr(CrystalBase, 2.6f), false, 0f, runeMask),
            ReceiverStone = CreateOrUpdateMaterial("M_Light_Receiver_Stone", litShader, new Color(0.31f, 0.32f, 0.29f, 1f), Color.black, false, 0f),
            WarmParticle = CreateOrUpdateParticleMaterial("M_Particle_Warm_Ember", LanternBase, particleSoftTexture),
            CyanParticle = CreateOrUpdateParticleMaterial("M_Particle_Cyan_Dust", CrystalBase, particleSoftTexture),
            GateLocalFog = CreateOrUpdateLocalFogMaterial(
                "M_LocalFog_Gate_Cyan",
                new Color(0.42f, 0.86f, 1f, 0.42f),
                new Vector3(7.75f, 0.86f, 12.42f),
                4.0f,
                0.52f,
                1.9f,
                1.1f,
                0.30f),
            LanternLocalFog = CreateOrUpdateLocalFogMaterial(
                "M_LocalFog_Lantern_Warm",
                new Color(1f, 0.62f, 0.28f, 0.24f),
                new Vector3(6.75f, 0.38f, 9.72f),
                2.9f,
                0.34f,
                1.6f,
                1.05f,
                0.18f),
            VolumeProfile = CreateOrUpdateVolumeProfile(),
            Skybox = CreateOrUpdateForestSkyboxMaterial()
        };
    }

    private static Texture2D CreateRuneMaskTexture()
    {
        if (!File.Exists(RuneMaskPath))
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            var black = new Color32(0, 0, 0, 255);
            var white = new Color32(255, 255, 255, 255);

            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, black);
                }
            }

            DrawLine(texture, 31, 8, 31, 55, white);
            DrawLine(texture, 32, 8, 32, 55, white);
            DrawLine(texture, 20, 18, 32, 30, white);
            DrawLine(texture, 44, 18, 32, 30, white);
            DrawLine(texture, 22, 46, 32, 34, white);
            DrawLine(texture, 42, 46, 32, 34, white);
            DrawLine(texture, 25, 32, 39, 32, white);

            texture.Apply();
            File.WriteAllBytes(RuneMaskPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(RuneMaskPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(RuneMaskPath) as TextureImporter;
            if (importer != null)
            {
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(RuneMaskPath);
    }

    private static Texture2D CreateSoftParticleTexture()
    {
        if (!File.Exists(ParticleSoftTexturePath))
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var radius = size * 0.48f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    var alpha = Mathf.Clamp01(1f - distance);
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    alpha *= alpha;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            File.WriteAllBytes(ParticleSoftTexturePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(ParticleSoftTexturePath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(ParticleSoftTexturePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleSoftTexturePath);
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
    {
        var dx = Mathf.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Mathf.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            texture.SetPixel(Mathf.Clamp(x0, 0, texture.width - 1), Mathf.Clamp(y0, 0, texture.height - 1), color);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static Mesh LoadOrCreateMesh(string path, Mesh mesh)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Material CreateOrUpdateMaterial(
        string materialName,
        Shader shader,
        Color baseColor,
        Color emissionColor,
        bool transparent,
        float alpha,
        Texture2D emissionMap = null)
    {
        var path = $"{AssetFolder}/{materialName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        if (material.HasProperty("_BaseColor"))
        {
            var color = baseColor;
            if (transparent)
            {
                color.a = alpha;
            }

            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", transparent ? 0.72f : 0.38f);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", materialName.Contains("Frame") ? 0.55f : 0f);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", emissionColor);
        }

        if (emissionMap != null && material.HasProperty("_EmissionMap"))
        {
            material.SetTexture("_EmissionMap", emissionMap);
        }

        if (emissionColor.maxColorComponent > 0.01f)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        SetSurface(material, transparent, alpha);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateLocalFogMaterial(
        string materialName,
        Color fogColor,
        Vector3 center,
        float radius,
        float density,
        float edgeFade,
        float heightFade,
        float noiseStrength)
    {
        var shader = Shader.Find("RhythmRPG/Effects/LocalFogVolume");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        var path = $"{AssetFolder}/{materialName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.renderQueue = (int)RenderQueue.Transparent + 20;
        material.SetOverrideTag("RenderType", "Transparent");

        if (material.HasProperty("_FogColor"))
        {
            material.SetColor("_FogColor", fogColor);
            material.SetVector("_Center", center);
            material.SetFloat("_Radius", radius);
            material.SetFloat("_Density", density);
            material.SetFloat("_EdgeFade", edgeFade);
            material.SetFloat("_HeightFade", heightFade);
            material.SetFloat("_NoiseScale", 1.55f);
            material.SetFloat("_NoiseStrength", noiseStrength);
        }
        else if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", fogColor);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateForestSkyboxMaterial()
    {
        var skyboxTexture = PrepareForestPanoramicSkyboxTexture();

        var shader = Shader.Find("Skybox/Panoramic");
        if (shader == null)
        {
            throw new InvalidOperationException("Skybox/Panoramic shader was not found.");
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, SkyboxMaterialPath);
        }

        material.name = "M_Forest_Night_Skybox";
        material.shader = shader;
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", skyboxTexture);
        }

        if (material.HasProperty("_Mapping"))
        {
            material.SetFloat("_Mapping", 1f);
        }

        if (material.HasProperty("_ImageType"))
        {
            material.SetFloat("_ImageType", 0f);
        }

        if (material.HasProperty("_MirrorOnBack"))
        {
            material.SetFloat("_MirrorOnBack", 0f);
        }

        if (material.HasProperty("_Layout"))
        {
            material.SetFloat("_Layout", 0f);
        }

        if (material.HasProperty("_Tint"))
        {
            material.SetColor("_Tint", new Color(0.82f, 0.92f, 1f, 1f));
        }

        if (material.HasProperty("_Exposure"))
        {
            material.SetFloat("_Exposure", 0.78f);
        }

        if (material.HasProperty("_Rotation"))
        {
            material.SetFloat("_Rotation", 0f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D PrepareForestPanoramicSkyboxTexture()
    {
        var sourceFile = ResolveForestPanoramicSkyboxSource();

        if (File.Exists(sourceFile))
        {
            File.Copy(sourceFile, SkyboxPanoramicTexturePath, true);
        }
        else if (!File.Exists(SkyboxPanoramicTexturePath))
        {
            throw new FileNotFoundException("Forest skybox source image was not found.", sourceFile);
        }

        var validationTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!validationTexture.LoadImage(File.ReadAllBytes(SkyboxPanoramicTexturePath)))
        {
            UnityEngine.Object.DestroyImmediate(validationTexture);
            throw new InvalidOperationException("Forest panoramic skybox image could not be loaded.");
        }

        var ratio = validationTexture.width / (float)validationTexture.height;
        if (Mathf.Abs(ratio - 2f) > 0.03f)
        {
            var size = $"{validationTexture.width}x{validationTexture.height}";
            UnityEngine.Object.DestroyImmediate(validationTexture);
            throw new InvalidOperationException($"Forest panoramic skybox image must be close to a 2:1 ratio. Current size: {size}.");
        }

        UnityEngine.Object.DestroyImmediate(validationTexture);
        ConfigureSkyboxTextureImporter(SkyboxPanoramicTexturePath);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SkyboxPanoramicTexturePath);
        if (texture == null)
        {
            throw new InvalidOperationException("Forest panoramic skybox texture was not imported.");
        }

        return texture;
    }

    private static string ResolveForestPanoramicSkyboxSource()
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var preferredMatches = Directory.GetFiles(downloads, "ChatGPT Image 2026*03_15_45.png");
        if (preferredMatches.Length > 0)
        {
            Array.Sort(preferredMatches, StringComparer.OrdinalIgnoreCase);
            return preferredMatches[0];
        }

        return Path.Combine(downloads, "Forest_SkyBox_02.png");
    }

    private static void ConfigureSkyboxTextureImporter(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static Material CreateOrUpdateParticleMaterial(string materialName, Color tint, Texture2D softTexture)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        var path = $"{AssetFolder}/{materialName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        var color = tint;
        color.a = 0.72f;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (softTexture != null)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", softTexture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", softTexture);
            }
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", Hdr(tint, 1.4f));
            material.EnableKeyword("_EMISSION");
        }

        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 2f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetSurface(Material material, bool transparent, float alpha)
    {
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", transparent ? 1f : 0f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        if (transparent)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            material.SetOverrideTag("RenderType", "Opaque");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
        }

        if (material.HasProperty("_BaseColor"))
        {
            var color = material.GetColor("_BaseColor");
            color.a = transparent ? alpha : 1f;
            material.SetColor("_BaseColor", color);
        }
    }

    private static VolumeProfile CreateOrUpdateVolumeProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }

        var bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(1.25f);
        bloom.intensity.Override(1.45f);
        bloom.scatter.Override(0.68f);
        bloom.tint.Override(Color.white);
        bloom.highQualityFiltering.Override(true);

        var tonemapping = GetOrAdd<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);

        var colorAdjustments = GetOrAdd<ColorAdjustments>(profile);
        colorAdjustments.postExposure.Override(0.05f);
        colorAdjustments.contrast.Override(-5f);
        colorAdjustments.saturation.Override(-1f);

        var shadowsMidtonesHighlights = GetOrAdd<ShadowsMidtonesHighlights>(profile);
        shadowsMidtonesHighlights.shadows.Override(new Vector4(0.82f, 0.98f, 1.08f, 0.18f));
        shadowsMidtonesHighlights.midtones.Override(new Vector4(0.98f, 1.04f, 1.03f, 0.03f));
        shadowsMidtonesHighlights.highlights.Override(new Vector4(1f, 0.98f, 0.94f, 0f));
        shadowsMidtonesHighlights.shadowsStart.Override(0f);
        shadowsMidtonesHighlights.shadowsEnd.Override(0.36f);
        shadowsMidtonesHighlights.highlightsStart.Override(0.62f);
        shadowsMidtonesHighlights.highlightsEnd.Override(1f);

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var component))
        {
            component = profile.Add<T>(true);
        }

        component.active = true;
        return component;
    }

    private static void CreateEnvironmentFillLights(Transform parent)
    {
        var moonFillObject = new GameObject("Directional Light_Moon_Cool_Fill");
        moonFillObject.transform.SetParent(parent, false);
        moonFillObject.transform.rotation = Quaternion.Euler(58f, -34f, 0f);

        var moonFill = moonFillObject.AddComponent<Light>();
        moonFill.type = LightType.Directional;
        moonFill.color = new Color(0.36f, 0.56f, 0.7f, 1f);
        moonFill.intensity = 0.28f;
        moonFill.shadows = LightShadows.None;
        moonFill.lightmapBakeType = LightmapBakeType.Mixed;
    }

    private static GameObject CreateCrystalPrefab(GeneratedAssets assets)
    {
        var root = new GameObject("PF_Crystal_Pillar");

        CreateReceiverStones("SM_Crystal_Light_Receiver", root.transform, assets.ReceiverStone, Vector3.zero, 0.72f, 0.48f);
        CreateMeshChild("SM_Crystal_Pedestal_Base", root.transform, assets.HexCylinderMesh, assets.CrystalPedestal, new Vector3(0f, 0.16f, 0f), Quaternion.identity, new Vector3(0.72f, 0.32f, 0.72f));
        CreateMeshChild("SM_Crystal_Pedestal_Top", root.transform, assets.HexCylinderMesh, assets.CrystalPedestal, new Vector3(0f, 0.43f, 0f), Quaternion.Euler(0f, 30f, 0f), new Vector3(0.46f, 0.18f, 0.46f));
        CreateMeshChild("SM_Crystal_Body", root.transform, assets.CrystalMesh, assets.CrystalGlow, new Vector3(0f, 1.16f, 0f), Quaternion.identity, new Vector3(0.55f, 1.05f, 0.55f));

        CreateRune("SM_Crystal_Rune_Front", root.transform, assets.CrystalRuneGlow, new Vector3(0f, 1.22f, -0.33f), Quaternion.identity);
        CreateRune("SM_Crystal_Rune_Back", root.transform, assets.CrystalRuneGlow, new Vector3(0f, 1.22f, 0.33f), Quaternion.Euler(0f, 180f, 0f));
        CreateGroundRune("SM_Crystal_Ground_Rune_North", root.transform, assets.CrystalRuneGlow, new Vector3(0f, 0.04f, -0.8f), Quaternion.identity);
        CreateGroundRune("SM_Crystal_Ground_Rune_South", root.transform, assets.CrystalRuneGlow, new Vector3(0f, 0.04f, 0.8f), Quaternion.identity);

        var light = CreatePointLight("Point Light_Crystal", root.transform, new Vector3(0f, 1.1f, 0f), CrystalBase, 0.68f, 7.6f);
        light.lightmapBakeType = LightmapBakeType.Mixed;

        CreateParticleSystem("FX_Crystal_Cyan_Dust", root.transform, new Vector3(0f, 1.18f, 0f), CrystalBase, 0.55f, 0.08f, 9f, ParticleSystemShapeType.Sphere, assets.CyanParticle);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, CrystalPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateLanternPrefab(GeneratedAssets assets)
    {
        var root = new GameObject("PF_Lantern_Rig");

        CreateReceiverStones("SM_Lantern_Light_Receiver", root.transform, assets.ReceiverStone, new Vector3(0.33f, 0f, 0f), 0.48f, 0.34f);
        CreateCube("SM_Lantern_Frame_Post", root.transform, assets.LanternFrame, new Vector3(0f, 0.78f, 0f), Quaternion.identity, new Vector3(0.08f, 1.56f, 0.08f));
        CreateCube("SM_Lantern_Frame_Arm", root.transform, assets.LanternFrame, new Vector3(0.22f, 1.48f, 0f), Quaternion.identity, new Vector3(0.52f, 0.08f, 0.08f));
        CreateCube("SM_Lantern_Frame_Hanger", root.transform, assets.LanternFrame, new Vector3(0.47f, 1.24f, 0f), Quaternion.identity, new Vector3(0.045f, 0.48f, 0.045f));
        CreateCube("SM_Lantern_Frame_Cage_Top", root.transform, assets.LanternFrame, new Vector3(0.47f, 1.1f, 0f), Quaternion.identity, new Vector3(0.34f, 0.045f, 0.34f));
        CreateCube("SM_Lantern_Frame_Cage_Bottom", root.transform, assets.LanternFrame, new Vector3(0.47f, 0.74f, 0f), Quaternion.identity, new Vector3(0.34f, 0.045f, 0.34f));

        for (var i = 0; i < 4; i++)
        {
            var angle = i * 90f;
            var radians = angle * Mathf.Deg2Rad;
            var position = new Vector3(0.47f + Mathf.Cos(radians) * 0.17f, 0.92f, Mathf.Sin(radians) * 0.17f);
            CreateCube($"SM_Lantern_Frame_Cage_Bar_{i + 1:00}", root.transform, assets.LanternFrame, position, Quaternion.identity, new Vector3(0.035f, 0.38f, 0.035f));
        }

        CreateCube("SM_Lantern_Glass", root.transform, assets.LanternGlass, new Vector3(0.47f, 0.92f, 0f), Quaternion.identity, new Vector3(0.27f, 0.3f, 0.27f));
        CreateSphere("SM_Lantern_Flame", root.transform, assets.LanternGlow, new Vector3(0.47f, 0.91f, 0f), Quaternion.identity, new Vector3(0.13f, 0.2f, 0.13f));

        var light = CreatePointLight("Point Light_Lantern", root.transform, new Vector3(0.47f, 0.92f, 0f), LanternBase, 0.78f, 6.8f);
        light.lightmapBakeType = LightmapBakeType.Mixed;

        CreateParticleSystem("FX_Lantern_Warm_Embers", root.transform, new Vector3(0.47f, 1.0f, 0f), LanternBase, 0.35f, 0.04f, 7f, ParticleSystemShapeType.Cone, assets.WarmParticle);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, LanternPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CreateLocalFogExamples(Transform parent, GeneratedAssets assets)
    {
        CreateLocalFogVolume(
            "FX_LocalFog_Gate_Cyan_Box",
            parent,
            assets.GateLocalFog,
            new Vector3(7.75f, 0.86f, 12.42f),
            new Vector3(6.8f, 2.35f, 4.7f));
        CreateLocalFogMistCore(
            "FX_LocalFog_Gate_Cyan_MistCore",
            parent,
            assets.DiskMesh,
            assets.GateLocalFog,
            new Vector3(7.75f, 0.43f, 12.42f),
            new Vector3(8.6f, 1f, 8.6f));

        CreateLocalFogVolume(
            "FX_LocalFog_Lantern_Warm_Low",
            parent,
            assets.LanternLocalFog,
            new Vector3(6.75f, 0.38f, 9.72f),
            new Vector3(3.6f, 0.96f, 3.0f));
        CreateLocalFogMistCore(
            "FX_LocalFog_Lantern_Warm_MistCore",
            parent,
            assets.DiskMesh,
            assets.LanternLocalFog,
            new Vector3(6.75f, 0.18f, 9.72f),
            new Vector3(6.3f, 1f, 6.3f));
    }

    private static GameObject CreateLocalFogVolume(string name, Transform parent, Material material, Vector3 position, Vector3 scale)
    {
        var volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
        volume.name = name;
        UnityEngine.Object.DestroyImmediate(volume.GetComponent<Collider>());
        volume.transform.SetParent(parent, false);
        volume.transform.position = position;
        volume.transform.localScale = scale;

        var renderer = volume.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.enabled = false;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;

        return volume;
    }

    private static GameObject CreateLocalFogMistCore(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var mist = new GameObject(name);
        mist.transform.SetParent(parent, false);
        mist.transform.position = position;
        mist.transform.localScale = scale;

        var meshFilter = mist.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        var renderer = mist.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;

        return mist;
    }

    private static void CreateRune(string name, Transform parent, Material material, Vector3 localPosition, Quaternion localRotation)
    {
        CreateCube(name + "_Stem", parent, material, localPosition, localRotation, new Vector3(0.035f, 0.52f, 0.018f));
        CreateCube(name + "_Cross", parent, material, localPosition + localRotation * new Vector3(0f, 0.04f, -0.002f), localRotation * Quaternion.Euler(0f, 0f, 90f), new Vector3(0.025f, 0.3f, 0.018f));
        CreateCube(name + "_SlashA", parent, material, localPosition + localRotation * new Vector3(0f, 0.18f, -0.004f), localRotation * Quaternion.Euler(0f, 0f, 38f), new Vector3(0.025f, 0.24f, 0.018f));
        CreateCube(name + "_SlashB", parent, material, localPosition + localRotation * new Vector3(0f, -0.18f, -0.004f), localRotation * Quaternion.Euler(0f, 0f, -38f), new Vector3(0.025f, 0.24f, 0.018f));
    }

    private static void CreateGroundRune(string name, Transform parent, Material material, Vector3 localPosition, Quaternion localRotation)
    {
        CreateCube(name + "_Long", parent, material, localPosition, localRotation, new Vector3(0.08f, 0.025f, 0.52f));
        CreateCube(name + "_Short", parent, material, localPosition, localRotation * Quaternion.Euler(0f, 45f, 0f), new Vector3(0.06f, 0.025f, 0.28f));
    }

    private static void CreateReceiverStones(string prefix, Transform parent, Material material, Vector3 center, float radius, float baseSize)
    {
        var offsets = new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(radius, 0f, radius * 0.15f),
            new Vector3(-radius * 0.65f, 0f, radius * 0.35f),
            new Vector3(radius * 0.25f, 0f, -radius * 0.75f),
            new Vector3(-radius * 0.35f, 0f, -radius * 0.55f)
        };

        var scales = new[]
        {
            new Vector3(baseSize * 1.2f, 0.026f, baseSize * 0.95f),
            new Vector3(baseSize * 0.85f, 0.022f, baseSize * 0.52f),
            new Vector3(baseSize * 0.72f, 0.02f, baseSize * 0.58f),
            new Vector3(baseSize * 0.64f, 0.02f, baseSize * 0.78f),
            new Vector3(baseSize * 0.58f, 0.02f, baseSize * 0.5f)
        };

        for (var i = 0; i < offsets.Length; i++)
        {
            CreateCube(
                $"{prefix}_{i + 1:00}",
                parent,
                material,
                center + offsets[i] + new Vector3(0f, 0.012f, 0f),
                Quaternion.Euler(0f, i * 31f, 0f),
                scales[i]);
        }
    }

    private static GameObject CreateMeshChild(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = localRotation;
        child.transform.localScale = localScale;

        var meshFilter = child.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.receiveShadows = true;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return child;
    }

    private static GameObject CreateCube(string name, Transform parent, Material material, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
        child.name = name;
        UnityEngine.Object.DestroyImmediate(child.GetComponent<Collider>());
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = localRotation;
        child.transform.localScale = localScale;
        child.GetComponent<MeshRenderer>().sharedMaterial = material;
        return child;
    }

    private static GameObject CreateSphere(string name, Transform parent, Material material, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        var child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        child.name = name;
        UnityEngine.Object.DestroyImmediate(child.GetComponent<Collider>());
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = localRotation;
        child.transform.localScale = localScale;
        child.GetComponent<MeshRenderer>().sharedMaterial = material;
        return child;
    }

    private static Light CreatePointLight(string name, Transform parent, Vector3 localPosition, Color color, float intensity, float range)
    {
        var lightObject = new GameObject(name);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = localPosition;

        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Mixed;
        return light;
    }

    private static void CreateParticleSystem(
        string name,
        Transform parent,
        Vector3 localPosition,
        Color color,
        float radius,
        float size,
        float rate,
        ParticleSystemShapeType shapeType,
        Material material)
    {
        var particleObject = new GameObject(name);
        particleObject.transform.SetParent(parent, false);
        particleObject.transform.localPosition = localPosition;

        var particleSystem = particleObject.AddComponent<ParticleSystem>();
        var main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.22f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.maxParticles = 42;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = particleSystem.emission;
        emission.rateOverTime = rate;

        var shape = particleSystem.shape;
        shape.shapeType = shapeType;
        shape.radius = radius;
        if (shapeType == ParticleSystemShapeType.Cone)
        {
            shape.angle = 14f;
        }

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color * 0.45f, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
    }

    private static void CreatePostProcessVolume(Transform parent, VolumeProfile profile)
    {
        var volumeObject = new GameObject("PP_GlobalVolume_Bloom_ACES");
        volumeObject.transform.SetParent(parent, false);
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 25f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
    }

    private static void CreateLightingNote(Transform parent)
    {
        var note = new GameObject("Pipeline_Settings_Note");
        note.transform.SetParent(parent, false);
        note.hideFlags = HideFlags.NotEditable;
    }

    private static void FrameSceneView(GameObject root)
    {
        Selection.activeGameObject = root;
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            return;
        }

        sceneView.FrameSelected();
        sceneView.Repaint();
    }

    private static GameObject InstantiatePrefab(GameObject prefab, string name, Transform parent, Vector3 position, float yaw)
    {
        var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null)
        {
            throw new InvalidOperationException($"Failed to instantiate prefab: {prefab.name}");
        }

        instance.name = name;
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        return instance;
    }

    private static void SetStaticFlagsRecursively(GameObject root)
    {
        var defaultFlags = StaticEditorFlags.BatchingStatic
                           | StaticEditorFlags.OccluderStatic
                           | StaticEditorFlags.OccludeeStatic
                           | StaticEditorFlags.ReflectionProbeStatic;

        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            var go = transform.gameObject;
            if (go.GetComponent<Light>() == null && go.GetComponent<ParticleSystem>() == null && go.GetComponent<Volume>() == null)
            {
                if (go.name.StartsWith("FX_LocalFog", StringComparison.Ordinal))
                {
                    GameObjectUtility.SetStaticEditorFlags(go, 0);
                    continue;
                }

                var flags = defaultFlags;
                if (CanContributeToBakedGI(go))
                {
                    flags |= StaticEditorFlags.ContributeGI;
                }

                GameObjectUtility.SetStaticEditorFlags(go, flags);
            }
        }
    }

    private static bool CanContributeToBakedGI(GameObject go)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null)
        {
            return false;
        }

        var meshFilter = go.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return false;
        }

        var meshPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
        return string.IsNullOrEmpty(meshPath) || !meshPath.StartsWith(AssetFolder, StringComparison.Ordinal);
    }

    private static void ConfigureForestNightRenderSettings(Material skyboxMaterial)
    {
        if (skyboxMaterial != null)
        {
            RenderSettings.skybox = skyboxMaterial;
            DynamicGI.UpdateEnvironment();
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.15f, 0.24f, 0.29f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.075f, 0.16f, 0.14f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.035f, 0.05f, 0.048f, 1f);
        RenderSettings.ambientIntensity = 0.95f;
        RenderSettings.reflectionIntensity = 0.22f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.045f, 0.095f, 0.11f, 1f);
        RenderSettings.fogDensity = 0.008f;

        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.Skybox;
            EditorUtility.SetDirty(mainCamera);
        }
    }

    private static void ConfigureLightingSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings();
            AssetDatabase.CreateAsset(settings, LightingSettingsPath);
        }

        SetProperty(settings, "bakedGI", true);
        SetProperty(settings, "realtimeGI", false);
        SetEnumProperty(settings, "lightmapper", "ProgressiveGPU");
        SetEnumProperty(settings, "mixedBakeMode", "Shadowmask");
        SetProperty(settings, "lightmapResolution", 48f);
        SetProperty(settings, "lightmapMaxSize", 2048);

        Lightmapping.lightingSettings = settings;
        Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
        EditorUtility.SetDirty(settings);
    }

    private static void SetProperty(UnityEngine.Object target, string propertyName, object value)
    {
        var property = target.GetType().GetProperty(propertyName);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value);
        }
    }

    private static void SetEnumProperty(UnityEngine.Object target, string propertyName, string enumName)
    {
        var property = target.GetType().GetProperty(propertyName);
        if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
        {
            return;
        }

        var value = Enum.Parse(property.PropertyType, enumName);
        property.SetValue(target, value);
    }

    private static void TryStartBake()
    {
        if (Lightmapping.isRunning)
        {
            Debug.Log("[ForestLightingPipelineSetup] Light bake is already running.");
            return;
        }

        var started = Lightmapping.BakeAsync();
        Debug.Log(started
            ? "[ForestLightingPipelineSetup] Generate Lighting started with BakeAsync."
            : "[ForestLightingPipelineSetup] Generate Lighting was not started by Unity.");
    }

    private static Mesh CreateCrystalMesh()
    {
        var mesh = new Mesh { name = "SM_Crystal_Body_HexBipyramid" };
        var vertices = new Vector3[14];
        vertices[0] = new Vector3(0f, -0.72f, 0f);
        vertices[13] = new Vector3(0f, 0.92f, 0f);

        for (var i = 0; i < 6; i++)
        {
            var angle = (i / 6f) * Mathf.PI * 2f + Mathf.PI / 6f;
            var lowerRadius = 0.34f;
            var upperRadius = 0.44f;
            vertices[1 + i] = new Vector3(Mathf.Cos(angle) * lowerRadius, -0.2f, Mathf.Sin(angle) * lowerRadius);
            vertices[7 + i] = new Vector3(Mathf.Cos(angle) * upperRadius, 0.42f, Mathf.Sin(angle) * upperRadius);
        }

        var triangles = new int[6 * 3 + 6 * 6 + 6 * 3];
        var index = 0;
        for (var i = 0; i < 6; i++)
        {
            var next = (i + 1) % 6;
            triangles[index++] = 0;
            triangles[index++] = 1 + next;
            triangles[index++] = 1 + i;

            triangles[index++] = 1 + i;
            triangles[index++] = 1 + next;
            triangles[index++] = 7 + next;
            triangles[index++] = 1 + i;
            triangles[index++] = 7 + next;
            triangles[index++] = 7 + i;

            triangles[index++] = 13;
            triangles[index++] = 7 + i;
            triangles[index++] = 7 + next;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        PrepareMesh(mesh);
        return mesh;
    }

    private static Mesh CreateHexCylinderMesh(int sides)
    {
        var mesh = new Mesh { name = "SM_HexCylinder" };
        var vertices = new Vector3[(sides * 2) + 2];
        vertices[0] = new Vector3(0f, 0.5f, 0f);
        vertices[1] = new Vector3(0f, -0.5f, 0f);

        for (var i = 0; i < sides; i++)
        {
            var angle = (i / (float)sides) * Mathf.PI * 2f + Mathf.PI / sides;
            var x = Mathf.Cos(angle) * 0.5f;
            var z = Mathf.Sin(angle) * 0.5f;
            vertices[2 + i] = new Vector3(x, 0.5f, z);
            vertices[2 + sides + i] = new Vector3(x, -0.5f, z);
        }

        var triangles = new int[sides * 12];
        var index = 0;
        for (var i = 0; i < sides; i++)
        {
            var next = (i + 1) % sides;
            var top = 2 + i;
            var topNext = 2 + next;
            var bottom = 2 + sides + i;
            var bottomNext = 2 + sides + next;

            triangles[index++] = 0;
            triangles[index++] = top;
            triangles[index++] = topNext;

            triangles[index++] = 1;
            triangles[index++] = bottomNext;
            triangles[index++] = bottom;

            triangles[index++] = top;
            triangles[index++] = bottom;
            triangles[index++] = bottomNext;
            triangles[index++] = top;
            triangles[index++] = bottomNext;
            triangles[index++] = topNext;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        PrepareMesh(mesh);
        return mesh;
    }

    private static Mesh CreateDiskMesh(int sides)
    {
        var mesh = new Mesh { name = "SM_LightReceiver_Disk" };
        var vertices = new Vector3[sides + 1];
        vertices[0] = Vector3.zero;
        for (var i = 0; i < sides; i++)
        {
            var angle = (i / (float)sides) * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
        }

        var triangles = new int[sides * 3];
        var index = 0;
        for (var i = 0; i < sides; i++)
        {
            var next = (i + 1) % sides;
            triangles[index++] = 0;
            triangles[index++] = next + 1;
            triangles[index++] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        PrepareMesh(mesh);
        return mesh;
    }

    private static void PrepareMesh(Mesh mesh)
    {
        var vertices = mesh.vertices;
        var uvs = new Vector2[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x + 0.5f, vertices[i].z + 0.5f);
        }

        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        Unwrapping.GenerateSecondaryUVSet(mesh);
    }

    private static Color Hdr(Color color, float exposureValue)
    {
        var hdr = color * Mathf.Pow(2f, exposureValue);
        hdr.a = 1f;
        return hdr;
    }

    private static GameObject FindLatestRoot()
    {
        GameObject latest = null;
        foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.scene.IsValid() && go.name.StartsWith(RootName, StringComparison.Ordinal))
            {
                latest = go;
            }
        }

        return latest;
    }

    private sealed class GeneratedAssets
    {
        public Mesh CrystalMesh;
        public Mesh HexCylinderMesh;
        public Mesh DiskMesh;
        public Material LanternFrame;
        public Material LanternGlass;
        public Material LanternGlow;
        public Material CrystalPedestal;
        public Material CrystalGlow;
        public Material CrystalRuneGlow;
        public Material ReceiverStone;
        public Material WarmParticle;
        public Material CyanParticle;
        public Material GateLocalFog;
        public Material LanternLocalFog;
        public VolumeProfile VolumeProfile;
        public Material Skybox;
    }
}
#endif
