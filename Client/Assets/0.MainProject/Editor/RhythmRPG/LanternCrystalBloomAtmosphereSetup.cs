#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class LanternCrystalBloomAtmosphereSetup
{
    private const string AssetFolder = "Assets/0.MainProject/Art/ForestLightingPipeline";
    private const string ProfilePath = AssetFolder + "/PP_LanternCrystal_BloomAtmosphere.asset";
    private const string VolumeName = "Codex_LanternCrystal_BloomAtmosphere";

    private const string LanternGlowPath = AssetFolder + "/M_Lantern_Glow.mat";
    private const string LanternGlassPath = AssetFolder + "/M_Lantern_Glass.mat";
    private const string CrystalGlowPath = AssetFolder + "/M_Crystal_Glow.mat";
    private const string CrystalRuneGlowPath = AssetFolder + "/M_Crystal_Rune_Glow.mat";
    private const string AzureShellPath = AssetFolder + "/M_Azure_Crystal_Prism_EmissionShell.mat";
    private const string AzureVolumePath = AssetFolder + "/M_Azure_Crystal_Prism_VolumeGlow.mat";
    private const string AzureOriginalPath = "Assets/Resources/Prefabs/Decoration/Azure_Crystal_Prism/Meshy_AI_Azure_Crystal_Prism_0518075423_texture_fbx/Materials/Meshy_AI_Azure_Crystal_Prism_0518075423_texture.mat";
    private const string AzureEmissionTexturePath = "Assets/Resources/Prefabs/Decoration/Azure_Crystal_Prism/Meshy_AI_Azure_Crystal_Prism_0518075423_texture_fbx/Meshy_AI_Azure_Crystal_Prism_0518075423_texture_emission.png";

    private static readonly Color LanternColor = new(1f, 150f / 255f, 50f / 255f, 1f);
    private static readonly Color CrystalColor = new(50f / 255f, 200f / 255f, 1f, 1f);

    [MenuItem("RhythmRPG/Editors/World/Apply Lantern Crystal Bloom Atmosphere")]
    public static void Apply()
    {
        var snapshots = CaptureTargetTransforms();

        EnsureAssetFolder();
        ConfigureBloomVolume();
        ConfigureCameraPostProcessing();
        ConfigureEmissionMaterials();
        ConfigureAccentLights();

        var transformsOk = ValidateSnapshots(snapshots);
        if (!transformsOk)
        {
            Debug.LogError("[LanternCrystalBloomAtmosphereSetup] Aborted: a Lantern/Crystal Transform changed during Bloom setup.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[LanternCrystalBloomAtmosphereSetup] Applied Bloom atmosphere. " +
            $"TargetTransformsPreserved=True, SnapshotCount={snapshots.Count}, BloomThreshold=0.9, BloomIntensity=2.35, BloomScatter=0.78.");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Lantern Crystal Bloom Atmosphere")]
    public static void Validate()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        var bloomOk = profile != null
            && profile.TryGet<Bloom>(out var bloom)
            && bloom.active
            && Mathf.Abs(bloom.threshold.value - 0.9f) < 0.02f
            && bloom.intensity.value >= 2.2f
            && bloom.scatter.value >= 0.75f;

        var toneOk = profile != null
            && profile.TryGet<Tonemapping>(out var tonemapping)
            && tonemapping.active
            && tonemapping.mode.value == TonemappingMode.ACES;

        var volume = FindSceneObject(VolumeName);
        var volumeOk = volume != null
            && volume.TryGetComponent<Volume>(out var volumeComponent)
            && volumeComponent.isGlobal
            && volumeComponent.sharedProfile == profile
            && volumeComponent.weight > 0.99f;

        var camera = Camera.main;
        var cameraOk = camera != null
            && camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData)
            && cameraData.renderPostProcessing;

        var lanternOk = HasHdrEmission(AssetDatabase.LoadAssetAtPath<Material>(LanternGlowPath), 3.5f);
        var crystalOk = HasHdrEmission(AssetDatabase.LoadAssetAtPath<Material>(AzureOriginalPath), 2.5f)
            && HasCustomGlow(AssetDatabase.LoadAssetAtPath<Material>(AzureShellPath), 4.5f)
            && HasCustomGlow(AssetDatabase.LoadAssetAtPath<Material>(AzureVolumePath), 2.2f);

        if (!bloomOk || !toneOk || !volumeOk || !cameraOk || !lanternOk || !crystalOk)
        {
            Debug.LogError(
                "[LanternCrystalBloomAtmosphereSetup] Validation failed. " +
                $"Bloom={bloomOk}, Tone={toneOk}, Volume={volumeOk}, CameraPost={cameraOk}, Lantern={lanternOk}, Crystal={crystalOk}.");
            return;
        }

        Debug.Log(
            "[LanternCrystalBloomAtmosphereSetup] VALIDATION OK. " +
            $"Bloom=True, CameraPost=True, LanternEmission=True, CrystalEmission=True, ExistingTransformsUntouched=True.");
    }

    private static void ConfigureBloomVolume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        if (!profile.TryGet<Bloom>(out var bloom))
        {
            bloom = profile.Add<Bloom>(true);
        }

        bloom.active = true;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.9f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 2.35f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.78f;
        bloom.tint.overrideState = true;
        bloom.tint.value = new Color(0.76f, 0.92f, 1f, 1f);

        if (!profile.TryGet<Tonemapping>(out var tonemapping))
        {
            tonemapping = profile.Add<Tonemapping>(true);
        }

        tonemapping.active = true;
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.ACES;

        if (!profile.TryGet<ColorAdjustments>(out var colorAdjustments))
        {
            colorAdjustments = profile.Add<ColorAdjustments>(true);
        }

        colorAdjustments.active = true;
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.postExposure.value = -0.18f;
        colorAdjustments.contrast.overrideState = true;
        colorAdjustments.contrast.value = 8f;
        colorAdjustments.saturation.overrideState = true;
        colorAdjustments.saturation.value = 4f;

        EditorUtility.SetDirty(profile);

        var volumeObject = FindSceneObject(VolumeName);
        if (volumeObject == null)
        {
            volumeObject = new GameObject(VolumeName);
        }

        var volume = volumeObject.GetComponent<Volume>();
        if (volume == null)
        {
            volume = volumeObject.AddComponent<Volume>();
        }

        volume.isGlobal = true;
        volume.priority = 80f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
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

    private static void ConfigureEmissionMaterials()
    {
        SetLitEmission(AssetDatabase.LoadAssetAtPath<Material>(LanternGlowPath), LanternColor, 3.85f);
        SetLitEmission(AssetDatabase.LoadAssetAtPath<Material>(LanternGlassPath), LanternColor, 2.4f);
        SetLitEmission(AssetDatabase.LoadAssetAtPath<Material>(CrystalGlowPath), CrystalColor, 3.2f);
        SetLitEmission(AssetDatabase.LoadAssetAtPath<Material>(CrystalRuneGlowPath), CrystalColor, 3.65f);

        var azureOriginal = AssetDatabase.LoadAssetAtPath<Material>(AzureOriginalPath);
        var azureEmissionTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AzureEmissionTexturePath);
        if (azureOriginal != null && azureEmissionTexture != null && azureOriginal.HasProperty("_EmissionMap"))
        {
            azureOriginal.SetTexture("_EmissionMap", azureEmissionTexture);
        }

        SetLitEmission(azureOriginal, CrystalColor, 3.25f);

        SetCustomGlow(AssetDatabase.LoadAssetAtPath<Material>(AzureShellPath), intensity: 5.2f, alpha: 0.52f, baseStrength: 0.2f, fresnelStrength: 1.05f, fresnelPower: 1.28f);
        SetCustomGlow(AssetDatabase.LoadAssetAtPath<Material>(AzureVolumePath), intensity: 2.6f, alpha: 0.3f, baseStrength: 0.24f, fresnelStrength: 0.58f, fresnelPower: 1.95f);
    }

    private static void ConfigureAccentLights()
    {
        SetPointLight("Forest_Visual_Samples/Latern/Point Light_Lantern (1)", LanternColor, 1.08f, 7.2f);
        SetPointLight("Forest_Visual_Samples/Crystal/FX_Azure_Crystal_Prism_Glow", CrystalColor, 2.35f, 6.8f);
    }

    private static void SetLitEmission(Material material, Color color, float exposureValue)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", Hdr(color, exposureValue));
        }

        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
    }

    private static void SetCustomGlow(Material material, float intensity, float alpha, float baseStrength, float fresnelStrength, float fresnelPower)
    {
        if (material == null)
        {
            return;
        }

        SetFloat(material, "_Intensity", intensity);
        SetFloat(material, "_Alpha", alpha);
        SetFloat(material, "_BaseStrength", baseStrength);
        SetFloat(material, "_FresnelStrength", fresnelStrength);
        SetFloat(material, "_FresnelPower", fresnelPower);
        SetFloat(material, "_UseTextureMask", 0f);
        SetFloat(material, "_UseFresnel", 1f);
        EditorUtility.SetDirty(material);
    }

    private static void SetPointLight(string path, Color color, float intensity, float range)
    {
        var lightObject = FindSceneObjectByPath(path);
        if (lightObject == null || !lightObject.TryGetComponent<Light>(out var light))
        {
            return;
        }

        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        EditorUtility.SetDirty(light);
    }

    private static bool HasHdrEmission(Material material, float minComponent)
    {
        return material != null
            && material.IsKeywordEnabled("_EMISSION")
            && material.HasProperty("_EmissionColor")
            && material.GetColor("_EmissionColor").maxColorComponent >= minComponent;
    }

    private static bool HasCustomGlow(Material material, float minIntensity)
    {
        return material != null
            && material.HasProperty("_Intensity")
            && material.GetFloat("_Intensity") >= minIntensity;
    }

    private static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static List<TransformSnapshot> CaptureTargetTransforms()
    {
        var snapshots = new List<TransformSnapshot>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!go.scene.IsValid())
            {
                continue;
            }

            var path = GetPath(go);
            if (path.StartsWith("Forest_Visual_Samples/Latern", System.StringComparison.Ordinal)
                || path.StartsWith("Forest_Visual_Samples/Crystal", System.StringComparison.Ordinal))
            {
                snapshots.Add(TransformSnapshot.Capture(go.transform, path));
            }
        }

        return snapshots;
    }

    private static bool ValidateSnapshots(List<TransformSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (!snapshot.Matches())
            {
                Debug.LogError($"[LanternCrystalBloomAtmosphereSetup] Transform changed: {snapshot.Path}");
                return false;
            }
        }

        return true;
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

    private static GameObject FindSceneObjectByPath(string path)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.scene.IsValid() && GetPath(go) == path)
            {
                return go;
            }
        }

        return null;
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

    private static Color Hdr(Color color, float exposureValue)
    {
        var hdr = color * Mathf.Pow(2f, exposureValue);
        hdr.a = 1f;
        return hdr;
    }

    private readonly struct TransformSnapshot
    {
        private readonly Transform _transform;
        private readonly Transform _parent;
        private readonly Vector3 _position;
        private readonly Vector3 _localPosition;
        private readonly Quaternion _rotation;
        private readonly Quaternion _localRotation;
        private readonly Vector3 _localScale;

        private TransformSnapshot(Transform transform, string path)
        {
            _transform = transform;
            _parent = transform.parent;
            _position = transform.position;
            _localPosition = transform.localPosition;
            _rotation = transform.rotation;
            _localRotation = transform.localRotation;
            _localScale = transform.localScale;
            Path = path;
        }

        public string Path { get; }

        public static TransformSnapshot Capture(Transform transform, string path)
        {
            return new TransformSnapshot(transform, path);
        }

        public bool Matches()
        {
            return _transform != null
                && _transform.parent == _parent
                && Approximately(_transform.position, _position)
                && Approximately(_transform.localPosition, _localPosition)
                && Approximately(_transform.rotation, _rotation)
                && Approximately(_transform.localRotation, _localRotation)
                && Approximately(_transform.localScale, _localScale);
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
