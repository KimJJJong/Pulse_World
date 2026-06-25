#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class AzureCrystalGlowSetup
{
    private const string TargetName = "Azure_Crystal_Prism";
    private const string GlowRigName = "FX_Azure_Crystal_Prism_Glow";
    private const string ShellName = "FX_Azure_Crystal_Prism_EmissionShell";
    private const string VolumeGlowName = "FX_Azure_Crystal_Prism_VolumeGlow";
    private const string LegacyAuraFrontName = "FX_Azure_Crystal_Prism_Aura_Front";
    private const string LegacyAuraSideName = "FX_Azure_Crystal_Prism_Aura_Side";

    private const string AssetFolder = "Assets/0.MainProject/Art/ForestLightingPipeline";
    private const string OriginalMaterialPath = "Assets/Resources/Prefabs/Decoration/Azure_Crystal_Prism/Meshy_AI_Azure_Crystal_Prism_0518075423_texture_fbx/Materials/Meshy_AI_Azure_Crystal_Prism_0518075423_texture.mat";
    private const string EmissionTexturePath = "Assets/Resources/Prefabs/Decoration/Azure_Crystal_Prism/Meshy_AI_Azure_Crystal_Prism_0518075423_texture_fbx/Meshy_AI_Azure_Crystal_Prism_0518075423_texture_emission.png";
    private const string ShellMaterialPath = AssetFolder + "/M_Azure_Crystal_Prism_EmissionShell.mat";
    private const string VolumeMaterialPath = AssetFolder + "/M_Azure_Crystal_Prism_VolumeGlow.mat";
    private const string CyanParticleMaterialPath = AssetFolder + "/M_Particle_Cyan_Dust.mat";

    [MenuItem("RhythmRPG/Editors/World/Apply Azure Crystal Prism Glow")]
    public static void Apply()
    {
        var target = FindTarget();
        if (target == null)
        {
            Debug.LogError($"[AzureCrystalGlowSetup] Target not found: {TargetName}");
            return;
        }

        var snapshot = TransformSnapshot.Capture(target.transform);
        var targetMeshFilter = target.GetComponent<MeshFilter>();
        var targetRenderer = target.GetComponent<MeshRenderer>();
        if (targetMeshFilter == null || targetRenderer == null || targetMeshFilter.sharedMesh == null)
        {
            Debug.LogError($"[AzureCrystalGlowSetup] {TargetName} needs MeshFilter, MeshRenderer, and a valid mesh.");
            return;
        }

        var originalMaterial = AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterialPath);
        if (originalMaterial == null)
        {
            Debug.LogError($"[AzureCrystalGlowSetup] Original material not found: {OriginalMaterialPath}");
            return;
        }

        RestoreOriginalMaterial(targetRenderer, originalMaterial);
        ConfigureOriginalMaterialEmission(originalMaterial);

        var shellMaterial = CreateOrUpdateGlowMaterial(
            ShellMaterialPath,
            texture: null,
            useTextureMask: false,
            useTextureAlpha: false,
            useFresnel: true,
            intensity: 4.2f,
            alpha: 0.46f,
            baseStrength: 0.18f,
            fresnelStrength: 1.15f,
            fresnelPower: 1.35f);

        var volumeMaterial = CreateOrUpdateGlowMaterial(
            VolumeMaterialPath,
            texture: null,
            useTextureMask: false,
            useTextureAlpha: false,
            useFresnel: true,
            intensity: 2.15f,
            alpha: 0.28f,
            baseStrength: 0.24f,
            fresnelStrength: 0.62f,
            fresnelPower: 2.1f);

        CreateOrUpdateMeshGlowObject(ShellName, target, targetMeshFilter.sharedMesh, shellMaterial, 1.035f);
        CreateOrUpdateMeshGlowObject(VolumeGlowName, target, targetMeshFilter.sharedMesh, volumeMaterial, 1.18f);
        CreateOrUpdateGlowRig(target, targetRenderer);
        DeleteLegacySpriteAuraObjects();

        if (!snapshot.Matches(target.transform))
        {
            Debug.LogError("[AzureCrystalGlowSetup] Aborted: target Transform changed during glow setup.");
            return;
        }

        EditorUtility.SetDirty(targetRenderer);
        EditorSceneManager.MarkSceneDirty(target.scene);
        EditorSceneManager.SaveScene(target.scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[AzureCrystalGlowSetup] Applied layered crystal glow while preserving the original material. " +
            $"Target={GetPath(target)}, OriginalMaterial={originalMaterial.name}, " +
            $"OriginalEmission=True, TransformUnchanged=True, MeshGlowOnly=True, Shell={ShellName}, Volume={VolumeGlowName}, Rig={GlowRigName}.");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Azure Crystal Prism Glow")]
    public static void Validate()
    {
        var target = FindTarget();
        if (target == null)
        {
            Debug.LogError($"[AzureCrystalGlowSetup] Validation failed: {TargetName} was not found.");
            return;
        }

        var targetMeshFilter = target.GetComponent<MeshFilter>();
        var targetRenderer = target.GetComponent<MeshRenderer>();
        var originalMaterial = AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterialPath);
        var shellMaterial = AssetDatabase.LoadAssetAtPath<Material>(ShellMaterialPath);
        var volumeMaterial = AssetDatabase.LoadAssetAtPath<Material>(VolumeMaterialPath);

        var materialOk = targetRenderer != null
            && originalMaterial != null
            && targetRenderer.sharedMaterial == originalMaterial
            && OriginalMaterialHasEmission(originalMaterial);

        var shell = FindSceneObject(ShellName);
        var shellRenderer = shell != null ? shell.GetComponent<MeshRenderer>() : null;
        var shellMeshFilter = shell != null ? shell.GetComponent<MeshFilter>() : null;
        var shellOk = shell != null
            && shellRenderer != null
            && shellMeshFilter != null
            && targetMeshFilter != null
            && shellMeshFilter.sharedMesh == targetMeshFilter.sharedMesh
            && shellRenderer.sharedMaterial == shellMaterial;

        var volume = FindSceneObject(VolumeGlowName);
        var volumeRenderer = volume != null ? volume.GetComponent<MeshRenderer>() : null;
        var volumeMeshFilter = volume != null ? volume.GetComponent<MeshFilter>() : null;
        var volumeOk = volume != null
            && volumeRenderer != null
            && volumeMeshFilter != null
            && targetMeshFilter != null
            && volumeMeshFilter.sharedMesh == targetMeshFilter.sharedMesh
            && volumeRenderer.sharedMaterial == volumeMaterial;

        var rig = FindSceneObject(GlowRigName);
        var light = rig != null ? rig.GetComponent<Light>() : null;
        var particleSystem = rig != null ? rig.GetComponent<ParticleSystem>() : null;
        var legacyAuraRemoved = FindSceneObject(LegacyAuraFrontName) == null
            && FindSceneObject(LegacyAuraSideName) == null;

        var helperOk = rig != null && light != null && particleSystem != null && legacyAuraRemoved;
        var transformOk = TransformMatchesRecordedScene(target.transform);

        if (!materialOk)
        {
            Debug.LogError("[AzureCrystalGlowSetup] Validation failed: original crystal material is not restored or emission is not configured.");
            return;
        }

        if (!shellOk)
        {
            Debug.LogError("[AzureCrystalGlowSetup] Validation failed: additive emission shell is missing or misconfigured.");
            return;
        }

        if (!helperOk)
        {
            Debug.LogError("[AzureCrystalGlowSetup] Validation failed: glow rig is missing or legacy sprite aura objects still exist.");
            return;
        }

        if (!volumeOk)
        {
            Debug.LogError("[AzureCrystalGlowSetup] Validation failed: mesh-based volume glow is missing or misconfigured.");
            return;
        }

        if (!transformOk)
        {
            Debug.LogError("[AzureCrystalGlowSetup] Validation failed: target Transform differs from the recorded scene values.");
            return;
        }

        Debug.Log(
            "[AzureCrystalGlowSetup] VALIDATION OK. " +
            $"OriginalMaterialPreserved=True, OriginalEmission=True, TargetTransformUnchanged=True, " +
            $"SpriteAuraRemoved=True, ShellMaterial={shellMaterial.name}, VolumeMaterial={volumeMaterial.name}, " +
            $"LightIntensity={light.intensity}, LightRange={light.range}.");
    }

    private static void RestoreOriginalMaterial(Renderer renderer, Material originalMaterial)
    {
        var materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            materials = new[] { originalMaterial };
        }
        else
        {
            materials[0] = originalMaterial;
        }

        renderer.sharedMaterials = materials;
    }

    private static void ConfigureOriginalMaterialEmission(Material material)
    {
        var emissionTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionTexturePath);
        if (emissionTexture != null && material.HasProperty("_EmissionMap"))
        {
            material.SetTexture("_EmissionMap", emissionTexture);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", Hdr(new Color(0.16f, 0.88f, 1f, 1f), 2.05f));
        }

        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
    }

    private static bool OriginalMaterialHasEmission(Material material)
    {
        return material != null
            && material.IsKeywordEnabled("_EMISSION")
            && material.HasProperty("_EmissionColor")
            && material.GetColor("_EmissionColor").maxColorComponent > 1f;
    }

    private static Material CreateOrUpdateGlowMaterial(
        string path,
        Texture texture,
        bool useTextureMask,
        bool useTextureAlpha,
        bool useFresnel,
        float intensity,
        float alpha,
        float baseStrength,
        float fresnelStrength,
        float fresnelPower)
    {
        EnsureAssetFolder();

        var shader = Shader.Find("RhythmRPG/Effects/AdditiveGlow");
        if (shader == null)
        {
            Debug.LogError("[AzureCrystalGlowSetup] Shader not found: RhythmRPG/Effects/AdditiveGlow");
            return null;
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
        material.SetTexture("_MainTex", texture);
        material.SetColor("_GlowColor", new Color(0.18f, 0.9f, 1f, 1f));
        material.SetFloat("_Intensity", intensity);
        material.SetFloat("_Alpha", alpha);
        material.SetFloat("_BaseStrength", baseStrength);
        material.SetFloat("_FresnelStrength", fresnelStrength);
        material.SetFloat("_FresnelPower", fresnelPower);
        material.SetFloat("_UseTextureMask", useTextureMask ? 1f : 0f);
        material.SetFloat("_UseFresnel", useFresnel ? 1f : 0f);
        material.SetFloat("_UseTextureAlpha", useTextureAlpha ? 1f : 0f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateOrUpdateMeshGlowObject(
        string name,
        GameObject target,
        Mesh sourceMesh,
        Material glowMaterial,
        float scaleMultiplier)
    {
        if (glowMaterial == null)
        {
            return;
        }

        var glowObject = FindSceneObject(name);
        if (glowObject == null)
        {
            glowObject = new GameObject(name);
        }

        glowObject.transform.SetParent(target.transform.parent, false);
        glowObject.transform.localPosition = target.transform.localPosition;
        glowObject.transform.localRotation = target.transform.localRotation;
        glowObject.transform.localScale = target.transform.localScale * scaleMultiplier;

        var meshFilter = glowObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = glowObject.AddComponent<MeshFilter>();
        }

        var renderer = glowObject.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = glowObject.AddComponent<MeshRenderer>();
        }

        meshFilter.sharedMesh = sourceMesh;
        renderer.sharedMaterial = glowMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        EditorUtility.SetDirty(glowObject);
    }

    private static void CreateOrUpdateGlowRig(GameObject target, Renderer targetRenderer)
    {
        var rig = FindSceneObject(GlowRigName);
        if (rig == null)
        {
            rig = new GameObject(GlowRigName);
        }

        var parent = target.transform.parent;
        rig.transform.SetParent(parent, false);
        rig.transform.localPosition = parent != null
            ? parent.InverseTransformPoint(targetRenderer.bounds.center + Vector3.up * 0.35f)
            : targetRenderer.bounds.center + Vector3.up * 0.35f;
        rig.transform.localRotation = Quaternion.identity;
        rig.transform.localScale = Vector3.one;

        var light = rig.GetComponent<Light>();
        if (light == null)
        {
            light = rig.AddComponent<Light>();
        }

        light.type = LightType.Point;
        light.color = new Color(0.2f, 0.88f, 1f, 1f);
        light.intensity = 2.05f;
        light.range = 6.2f;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        light.renderMode = LightRenderMode.ForcePixel;

        ConfigureParticles(rig, targetRenderer);

        EditorUtility.SetDirty(rig);
    }

    private static void ConfigureParticles(GameObject rig, Renderer targetRenderer)
    {
        var particles = rig.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = rig.AddComponent<ParticleSystem>();
        }

        var main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.09f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.45f, 0.95f, 1f, 0.62f),
            new Color(0.06f, 0.62f, 1f, 0.0f));
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 8f;

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Clamp(Mathf.Max(targetRenderer.bounds.extents.x, targetRenderer.bounds.extents.z) * 0.48f, 0.35f, 1.3f);

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.42f, 0.95f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.08f, 0.6f, 1f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.45f, 0.18f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(CyanParticleMaterialPath);
        particles.Play();
    }

    private static void DeleteLegacySpriteAuraObjects()
    {
        DeleteSceneObject(LegacyAuraFrontName);
        DeleteSceneObject(LegacyAuraSideName);
    }

    private static void DeleteSceneObject(string name)
    {
        var sceneObject = FindSceneObject(name);
        if (sceneObject != null)
        {
            Object.DestroyImmediate(sceneObject);
        }
    }

    private static bool TransformMatchesRecordedScene(Transform transform)
    {
        return Mathf.Abs(transform.position.x - 7.08f) < 0.001f
            && Mathf.Abs(transform.position.y - 0.11f) < 0.001f
            && Mathf.Abs(transform.position.z - 12.91f) < 0.001f
            && Mathf.Abs(transform.eulerAngles.x - 270f) < 0.001f
            && Mathf.Abs(transform.eulerAngles.y) < 0.001f
            && Mathf.Abs(transform.eulerAngles.z) < 0.001f
            && Mathf.Abs(transform.lossyScale.x - 60f) < 0.001f
            && Mathf.Abs(transform.lossyScale.y - 60f) < 0.001f
            && Mathf.Abs(transform.lossyScale.z - 60f) < 0.001f;
    }

    private static Color Hdr(Color color, float exposureValue)
    {
        var hdr = color * Mathf.Pow(2f, exposureValue);
        hdr.a = 1f;
        return hdr;
    }

    private static GameObject FindTarget()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == TargetName && go.scene.IsValid())
            {
                return go;
            }
        }

        return null;
    }

    private static GameObject FindSceneObject(string name)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == name && go.scene.IsValid())
            {
                return go;
            }
        }

        return null;
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

    private static string GetPath(GameObject go)
    {
        var path = go.name;
        var current = go.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private readonly struct TransformSnapshot
    {
        private readonly Transform _parent;
        private readonly Vector3 _position;
        private readonly Vector3 _localPosition;
        private readonly Quaternion _rotation;
        private readonly Quaternion _localRotation;
        private readonly Vector3 _localScale;

        private TransformSnapshot(Transform transform)
        {
            _parent = transform.parent;
            _position = transform.position;
            _localPosition = transform.localPosition;
            _rotation = transform.rotation;
            _localRotation = transform.localRotation;
            _localScale = transform.localScale;
        }

        public static TransformSnapshot Capture(Transform transform)
        {
            return new TransformSnapshot(transform);
        }

        public bool Matches(Transform transform)
        {
            return transform.parent == _parent
                && Approximately(transform.position, _position)
                && Approximately(transform.localPosition, _localPosition)
                && Approximately(transform.rotation, _rotation)
                && Approximately(transform.localRotation, _localRotation)
                && Approximately(transform.localScale, _localScale);
        }

        private static bool Approximately(Vector3 lhs, Vector3 rhs)
        {
            return Vector3.SqrMagnitude(lhs - rhs) < 0.000001f;
        }

        private static bool Approximately(Quaternion lhs, Quaternion rhs)
        {
            return Quaternion.Angle(lhs, rhs) < 0.001f;
        }
    }
}
#endif
