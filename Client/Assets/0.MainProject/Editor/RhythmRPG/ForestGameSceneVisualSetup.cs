#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ForestGameSceneVisualSetup
{
    private const string RootName = "Codex_ForestGame_Visuals";
    private const string VolumeName = "PP_ForestGame_Bloom_ACES";
    private const string VolumeProfilePath = "Assets/0.MainProject/Art/ForestLightingPipeline/PP_ForestGame_Bloom_ACES.asset";

    private static readonly string[] TargetScenes =
    {
        "Assets/0.MainProject/Scenes/Game/Game_Forest_Tutorial.unity",
        "Assets/0.MainProject/Scenes/Game/Game_Forest_First_Step.unity"
    };

    [MenuItem("RhythmRPG/Editors/World/Apply Forest Game Scene Visuals")]
    public static void Apply()
    {
        foreach (string scenePath in TargetScenes)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ForestGameSceneVisualSetup] Applied visual camera/post settings. Scenes={TargetScenes.Length}.");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Forest Game Scene Visuals")]
    public static void Validate()
    {
        foreach (string scenePath in TargetScenes)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var camera = ResolveMainCamera(scene);
            var cameraData = camera != null ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
            var volume = FindSceneObject(scene, VolumeName)?.GetComponent<Volume>();
            bool cameraOk = camera != null
                && camera.allowHDR
                && cameraData != null
                && cameraData.renderPostProcessing
                && cameraData.requiresDepthTexture;
            bool volumeOk = volume != null
                && volume.isGlobal
                && volume.sharedProfile != null
                && volume.sharedProfile.TryGet<Bloom>(out var bloom)
                && bloom.active
                && volume.sharedProfile.TryGet<Tonemapping>(out var tone)
                && tone.active;

            if (!cameraOk || !volumeOk)
            {
                Debug.LogError(
                    "[ForestGameSceneVisualSetup] Validation failed. " +
                    $"Scene={scene.name}, CameraOk={cameraOk}, VolumeOk={volumeOk}.");
                continue;
            }

            Debug.Log(
                "[ForestGameSceneVisualSetup] VALIDATION OK. " +
                $"Scene={scene.name}, CameraPost=True, Bloom=True, Tone=True.");
        }
    }

    private static void ApplyToScene(Scene scene)
    {
        var root = FindSceneObject(scene, RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
        }

        ConfigureMainCamera(scene);
        ConfigurePostProcessVolume(root.transform);
        ConfigureRenderSettings();
        EditorUtility.SetDirty(root);
    }

    private static void ConfigureMainCamera(Scene scene)
    {
        var camera = ResolveMainCamera(scene);
        if (camera == null)
        {
            Debug.LogWarning("[ForestGameSceneVisualSetup] Main Camera not found in scene: " + scene.name);
            return;
        }

        camera.clearFlags = CameraClearFlags.Skybox;
        camera.allowHDR = true;

        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.requiresDepthTexture = true;

        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(cameraData);
    }

    private static void ConfigurePostProcessVolume(Transform parent)
    {
        var profile = CreateOrUpdateVolumeProfile();

        var volumeObject = FindDirectChild(parent, VolumeName);
        if (volumeObject == null)
        {
            volumeObject = new GameObject(VolumeName);
            volumeObject.transform.SetParent(parent, false);
        }

        var volume = volumeObject.GetComponent<Volume>();
        if (volume == null)
            volume = volumeObject.AddComponent<Volume>();

        volume.isGlobal = true;
        volume.priority = 35f;
        volume.weight = 1f;
        volume.sharedProfile = profile;

        EditorUtility.SetDirty(volumeObject);
        EditorUtility.SetDirty(volume);
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
        profile.components.RemoveAll(component => component == null);

        if (!profile.TryGet<T>(out var component))
        {
            component = ScriptableObject.CreateInstance<T>();
            component.name = typeof(T).Name;
            component.active = true;
            profile.components.Add(component);
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        component.active = true;
        EditorUtility.SetDirty(component);
        return component;
    }

    private static void ConfigureRenderSettings()
    {
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
    }

    private static Camera ResolveMainCamera(Scene scene)
    {
        if (Camera.main != null && Camera.main.gameObject.scene == scene)
            return Camera.main;

        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (camera != null && camera.gameObject.scene == scene && camera.CompareTag("MainCamera"))
                return camera;
        }

        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (camera != null && camera.gameObject.scene == scene)
                return camera;
        }

        return null;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null
                && go.gameObject.scene == scene
                && go.name.Equals(objectName, StringComparison.Ordinal))
            {
                return go;
            }
        }

        return null;
    }

    private static GameObject FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child != null && child.name.Equals(childName, StringComparison.Ordinal))
                return child.gameObject;
        }

        return null;
    }
}
#endif
