#if UNITY_EDITOR
using System;
using System.Linq;
using RhythmRPG.Game.Visual.SceneEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class RunicInteractionCrystalLightingSetup
{
    private const string AssetFolder = "Assets/0.MainProject/Art/ForestLightingPipeline";
    private const string AdditiveGlowShaderName = "RhythmRPG/Effects/AdditiveGlow";
    private const string TransparentLitShaderName = "Universal Render Pipeline/Lit";
    private const string ParticleMaterialPath = AssetFolder + "/M_Particle_Cyan_Dust.mat";

    private static readonly Color CrystalColor = new(0.08f, 0.78f, 0.48f, 1f);
    private static readonly Color CrystalGlassTint = new(0.12f, 0.72f, 0.45f, 0.58f);

    private static readonly PrefabSpec[] Prefabs =
    {
        new(
            "Assets/Resources/Prefabs/Interaction/Runic_Circle_Platform.prefab",
            "Emerald_Crystal_Big",
            AssetFolder + "/M_Runic_Circle_Platform_Crystal_GlassGlow.mat",
            AssetFolder + "/M_Runic_Circle_Platform_Crystal_EmissionShell.mat",
            AssetFolder + "/M_Runic_Circle_Platform_Crystal_VolumeGlow.mat",
            2.5f,
            6.2f),
        new(
            "Assets/Resources/Prefabs/Interaction/Runic_Obelisk.prefab",
            "EmeraldCrystal",
            AssetFolder + "/M_Runic_Obelisk_Crystal_GlassGlow.mat",
            AssetFolder + "/M_Runic_Obelisk_Crystal_EmissionShell.mat",
            AssetFolder + "/M_Runic_Obelisk_Crystal_VolumeGlow.mat",
            2.35f,
            5.8f)
    };

    [MenuItem("RhythmRPG/Editors/Interaction/Apply Runic Crystal Lighting")]
    public static void Apply()
    {
        EnsureAssetFolder();

        foreach (var spec in Prefabs)
        {
            ApplyToPrefab(spec);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RunicInteractionCrystalLightingSetup] Applied transparent crystal materials, point lights, and ForestBeatLightPulse targets.");
    }

    [MenuItem("RhythmRPG/Editors/Interaction/Validate Runic Crystal Lighting")]
    public static void Validate()
    {
        foreach (var spec in Prefabs)
        {
            ValidatePrefab(spec);
        }

        Debug.Log("[RunicInteractionCrystalLightingSetup] VALIDATION OK.");
    }

    private static void ApplyToPrefab(PrefabSpec spec)
    {
        var prefabRoot = PrefabUtility.LoadPrefabContents(spec.PrefabPath);
        try
        {
            var crystalRoot = FindRequiredChild(prefabRoot, spec.CrystalRootName);
            var crystalRenderers = crystalRoot.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer is MeshRenderer or SkinnedMeshRenderer)
                .ToArray();

            if (crystalRenderers.Length == 0)
            {
                throw new InvalidOperationException($"No crystal renderers found under {spec.CrystalRootName} in {spec.PrefabPath}.");
            }

            var crystalMaterial = CreateOrUpdateCrystalMaterial(spec.MaterialPath, crystalRenderers[0].sharedMaterial);
            foreach (var renderer in crystalRenderers)
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = crystalMaterial;
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                EditorUtility.SetDirty(renderer);
            }

            var particles = CreateOrUpdateGlowLightAndParticles(prefabRoot, crystalRoot, crystalRenderers, spec, out var light);
            var shellRenderer = CreateOrUpdateEmissionMesh(
                prefabRoot,
                crystalRenderers[0],
                "FX_Runic_Crystal_EmissionShell",
                spec.ShellMaterialPath,
                scaleMultiplier: 1.025f,
                intensity: 1.35f,
                alpha: 0.20f,
                baseStrength: 0.09f,
                fresnelStrength: 0.68f,
                fresnelPower: 1.7f);
            var volumeRenderer = CreateOrUpdateEmissionMesh(
                prefabRoot,
                crystalRenderers[0],
                "FX_Runic_Crystal_VolumeGlow",
                spec.VolumeMaterialPath,
                scaleMultiplier: 1.065f,
                intensity: 0.44f,
                alpha: 0.055f,
                baseStrength: 0.05f,
                fresnelStrength: 0.26f,
                fresnelPower: 2.3f);
            var pulseRenderers = crystalRenderers.Concat(new[] { shellRenderer, volumeRenderer }).ToArray();
            var pulse = prefabRoot.GetComponent<ForestBeatLightPulse>();
            if (pulse == null)
            {
                pulse = prefabRoot.AddComponent<ForestBeatLightPulse>();
            }

            pulse.Configure(
                new[] { light },
                pulseRenderers,
                new[] { particles },
                CrystalColor,
                lightPeak: 1.65f,
                emissionPeak: 1.95f,
                alphaPeak: 1.35f,
                particlePeak: 1.6f,
                durationBeats: 0.42f,
                falloff: 2.1f,
                flicker: 0.018f,
                lightBase: 0.86f,
                rendererBase: 0.88f,
                particleBase: 0.82f,
                tintBaseColor: true);
            pulse.ConfigureTiming(true, 120f);
            EditorUtility.SetDirty(pulse);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, spec.PrefabPath);
            Debug.Log($"[RunicInteractionCrystalLightingSetup] Updated {spec.PrefabPath}. Crystal={spec.CrystalRootName}, Light={light.name}, Material={crystalMaterial.name}, Shell={shellRenderer.name}, Volume={volumeRenderer.name}, Particles={particles.name}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static Material CreateOrUpdateCrystalMaterial(string path, Material sourceMaterial)
    {
        var shader = Shader.Find(TransparentLitShaderName);
        if (shader == null)
        {
            throw new InvalidOperationException($"Shader not found: {TransparentLitShaderName}");
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", "Transparent");

        var baseTexture = GetTexture(sourceMaterial, "_BaseMap") ?? GetTexture(sourceMaterial, "_MainTex");
        if (baseTexture != null && material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", baseTexture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", CrystalGlassTint);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", CrystalGlassTint);
        }

        SetFloat(material, "_Surface", 1f);
        SetFloat(material, "_Blend", 0f);
        SetFloat(material, "_AlphaClip", 0f);
        SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
        SetFloat(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        SetFloat(material, "_ZWrite", 0f);
        SetFloat(material, "_Cull", (float)CullMode.Off);
        SetFloat(material, "_ReceiveShadows", 0f);
        SetFloat(material, "_Metallic", 0f);
        SetFloat(material, "_Smoothness", 0.76f);

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", Hdr(CrystalColor, 0.75f));
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_EMISSION");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        material.SetShaderPassEnabled("DepthOnly", false);
        material.SetShaderPassEnabled("SHADOWCASTER", false);
        material.SetShaderPassEnabled("MOTIONVECTORS", false);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateGlowMaterial(
        string path,
        float intensity,
        float alpha,
        float baseStrength,
        float fresnelStrength,
        float fresnelPower)
    {
        var shader = Shader.Find(AdditiveGlowShaderName);
        if (shader == null)
        {
            throw new InvalidOperationException($"Shader not found: {AdditiveGlowShaderName}");
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.renderQueue = (int)RenderQueue.Transparent + 20;
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetColor("_GlowColor", CrystalColor);
        material.SetFloat("_Intensity", intensity);
        material.SetFloat("_Alpha", alpha);
        material.SetFloat("_BaseStrength", baseStrength);
        material.SetFloat("_FresnelStrength", fresnelStrength);
        material.SetFloat("_FresnelPower", fresnelPower);
        material.SetFloat("_UseTextureMask", 0f);
        material.SetFloat("_UseFresnel", 1f);
        material.SetFloat("_UseTextureAlpha", 0f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Renderer CreateOrUpdateEmissionMesh(
        GameObject prefabRoot,
        Renderer sourceRenderer,
        string objectName,
        string materialPath,
        float scaleMultiplier,
        float intensity,
        float alpha,
        float baseStrength,
        float fresnelStrength,
        float fresnelPower)
    {
        var meshFilter = sourceRenderer.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            throw new InvalidOperationException($"Crystal renderer has no MeshFilter/sharedMesh: {sourceRenderer.name}.");
        }

        var shellParent = sourceRenderer.transform.parent != null
            ? sourceRenderer.transform.parent
            : prefabRoot.transform;

        var shellObject = FindDirectChild(shellParent, objectName);
        if (shellObject == null)
        {
            shellObject = new GameObject(objectName);
            shellObject.transform.SetParent(shellParent, false);
        }

        shellObject.transform.localPosition = sourceRenderer.transform.localPosition;
        shellObject.transform.localRotation = sourceRenderer.transform.localRotation;
        shellObject.transform.localScale = sourceRenderer.transform.localScale * scaleMultiplier;

        var shellMeshFilter = shellObject.GetComponent<MeshFilter>();
        if (shellMeshFilter == null)
        {
            shellMeshFilter = shellObject.AddComponent<MeshFilter>();
        }

        var shellRenderer = shellObject.GetComponent<MeshRenderer>();
        if (shellRenderer == null)
        {
            shellRenderer = shellObject.AddComponent<MeshRenderer>();
        }

        shellMeshFilter.sharedMesh = meshFilter.sharedMesh;
        shellRenderer.sharedMaterial = CreateOrUpdateGlowMaterial(
            materialPath,
            intensity,
            alpha,
            baseStrength,
            fresnelStrength,
            fresnelPower);
        shellRenderer.shadowCastingMode = ShadowCastingMode.Off;
        shellRenderer.receiveShadows = false;
        shellRenderer.lightProbeUsage = LightProbeUsage.Off;
        shellRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        EditorUtility.SetDirty(shellObject);
        EditorUtility.SetDirty(shellRenderer);
        return shellRenderer;
    }

    private static ParticleSystem CreateOrUpdateGlowLightAndParticles(
        GameObject prefabRoot,
        GameObject crystalRoot,
        Renderer[] crystalRenderers,
        PrefabSpec spec,
        out Light light)
    {
        const string lightName = "FX_Runic_Crystal_Glow";
        var lightObject = FindDirectChild(prefabRoot.transform, lightName);
        if (lightObject == null)
        {
            lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(prefabRoot.transform, false);
        }

        var bounds = crystalRenderers[0].bounds;
        for (var i = 1; i < crystalRenderers.Length; i++)
        {
            bounds.Encapsulate(crystalRenderers[i].bounds);
        }

        lightObject.transform.localPosition = prefabRoot.transform.InverseTransformPoint(bounds.center);
        lightObject.transform.localRotation = Quaternion.identity;
        lightObject.transform.localScale = Vector3.one;

        light = lightObject.GetComponent<Light>();
        if (light == null)
        {
            light = lightObject.AddComponent<Light>();
        }

        light.type = LightType.Point;
        light.color = CrystalColor;
        light.intensity = spec.LightIntensity;
        light.range = spec.LightRange;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        light.renderMode = LightRenderMode.ForcePixel;

        var additionalLight = lightObject.GetComponent<UniversalAdditionalLightData>();
        if (additionalLight == null)
        {
            additionalLight = lightObject.AddComponent<UniversalAdditionalLightData>();
        }

        additionalLight.usePipelineSettings = true;
        var particles = ConfigureParticles(lightObject, crystalRenderers);
        EditorUtility.SetDirty(lightObject);
        return particles;
    }

    private static ParticleSystem ConfigureParticles(GameObject rig, Renderer[] crystalRenderers)
    {
        var particles = rig.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = rig.AddComponent<ParticleSystem>();
        }

        var maxExtent = 0.5f;
        foreach (var renderer in crystalRenderers)
        {
            maxExtent = Mathf.Max(maxExtent, renderer.bounds.extents.magnitude);
        }

        var main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.025f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.014f, 0.04f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.30f, 0.92f, 0.56f, 0.28f),
            new Color(0.06f, 0.44f, 0.28f, 0.04f));
        main.maxParticles = 28;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 2.1f;

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Clamp(maxExtent * 0.38f, 0.18f, 0.8f);

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.62f, 1f, 0.68f, 1f), 0f),
                new GradientColorKey(new Color(0.06f, 0.86f, 0.34f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.34f, 0.18f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
        particles.Play();
        return particles;
    }

    private static void ValidatePrefab(PrefabSpec spec)
    {
        var prefabRoot = PrefabUtility.LoadPrefabContents(spec.PrefabPath);
        try
        {
            var crystalRoot = FindRequiredChild(prefabRoot, spec.CrystalRootName);
            var renderers = crystalRoot.GetComponentsInChildren<Renderer>(true);
            var hasTransparentCrystalMaterial = renderers.Any(renderer => renderer.sharedMaterials.Any(material =>
                material != null
                && material.shader != null
                && material.shader.name == TransparentLitShaderName
                && material.HasProperty("_Surface")
                && Mathf.Approximately(material.GetFloat("_Surface"), 1f)
                && material.HasProperty("_BaseColor")
                && material.GetColor("_BaseColor").a < 0.75f
                && material.HasProperty("_EmissionColor")
                && material.GetColor("_EmissionColor").maxColorComponent > 0.65f));

            var lightObject = FindDirectChild(prefabRoot.transform, "FX_Runic_Crystal_Glow");
            var light = lightObject != null ? lightObject.GetComponent<Light>() : null;
            var shell = FindRequiredChild(prefabRoot, "FX_Runic_Crystal_EmissionShell");
            var shellRenderer = shell.GetComponent<Renderer>();
            var volume = FindRequiredChild(prefabRoot, "FX_Runic_Crystal_VolumeGlow");
            var volumeRenderer = volume.GetComponent<Renderer>();
            var particles = lightObject != null ? lightObject.GetComponent<ParticleSystem>() : null;
            var pulse = prefabRoot.GetComponent<ForestBeatLightPulse>();
            var hasShellGlowMaterial = shellRenderer != null
                && shellRenderer.sharedMaterial != null
                && shellRenderer.sharedMaterial.shader != null
                && shellRenderer.sharedMaterial.shader.name == AdditiveGlowShaderName
                && shellRenderer.sharedMaterial.HasProperty("_Intensity")
                && shellRenderer.sharedMaterial.HasProperty("_Alpha");
            var hasVolumeGlowMaterial = volumeRenderer != null
                && volumeRenderer.sharedMaterial != null
                && volumeRenderer.sharedMaterial.shader != null
                && volumeRenderer.sharedMaterial.shader.name == AdditiveGlowShaderName
                && volumeRenderer.sharedMaterial.HasProperty("_Intensity")
                && volumeRenderer.sharedMaterial.HasProperty("_Alpha");

            if (!hasTransparentCrystalMaterial
                || !hasShellGlowMaterial
                || !hasVolumeGlowMaterial
                || light == null
                || particles == null
                || pulse == null
                || pulse.LightTargetCount == 0
                || pulse.RendererTargetCount < 3
                || pulse.ParticleTargetCount == 0)
            {
                throw new InvalidOperationException(
                    $"Validation failed for {spec.PrefabPath}. " +
                    $"TransparentCrystal={hasTransparentCrystalMaterial}, ShellGlow={hasShellGlowMaterial}, VolumeGlow={hasVolumeGlowMaterial}, " +
                    $"Light={light != null}, Particles={particles != null}, Pulse={pulse != null}, " +
                    $"LightTargets={pulse?.LightTargetCount ?? 0}, RendererTargets={pulse?.RendererTargetCount ?? 0}, ParticleTargets={pulse?.ParticleTargetCount ?? 0}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static GameObject FindRequiredChild(GameObject root, string childName)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name.Equals(childName, StringComparison.Ordinal))
            {
                return transform.gameObject;
            }
        }

        throw new InvalidOperationException($"Child not found: {childName} under {root.name}.");
    }

    private static GameObject FindDirectChild(Transform parent, string childName)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name.Equals(childName, StringComparison.Ordinal))
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static Texture GetTexture(Material material, string propertyName)
    {
        return material != null && material.HasProperty(propertyName)
            ? material.GetTexture(propertyName)
            : null;
    }

    private static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static Color Hdr(Color color, float exposureValue)
    {
        var hdr = color * Mathf.Pow(2f, exposureValue);
        hdr.a = 1f;
        return hdr;
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

    private readonly struct PrefabSpec
    {
        public PrefabSpec(
            string prefabPath,
            string crystalRootName,
            string materialPath,
            string shellMaterialPath,
            string volumeMaterialPath,
            float lightIntensity,
            float lightRange)
        {
            PrefabPath = prefabPath;
            CrystalRootName = crystalRootName;
            MaterialPath = materialPath;
            ShellMaterialPath = shellMaterialPath;
            VolumeMaterialPath = volumeMaterialPath;
            LightIntensity = lightIntensity;
            LightRange = lightRange;
        }

        public string PrefabPath { get; }
        public string CrystalRootName { get; }
        public string MaterialPath { get; }
        public string ShellMaterialPath { get; }
        public string VolumeMaterialPath { get; }
        public float LightIntensity { get; }
        public float LightRange { get; }
    }
}
#endif
