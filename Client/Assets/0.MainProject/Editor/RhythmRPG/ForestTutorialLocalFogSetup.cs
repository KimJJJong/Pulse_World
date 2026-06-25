#if UNITY_EDITOR
using RhythmRPG.Game.Visual.SceneEffects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class ForestTutorialLocalFogSetup
{
    private const string ScenePath = "Assets/0.MainProject/Scenes/Game/Game_Forest_Tutorial.unity";
    private const string RootName = "FX_FullscreenDepthFog";
    private const string EnvironmentParentName = "Forest_Decoration_Set";
    private const string AssetFolder = "Assets/0.MainProject/Art/ForestLightingPipeline";
    private const string FogShaderName = "RhythmRPG/Effects/ForestDepthBoundaryFog";
    private const string FogMaterialPath = AssetFolder + "/M_Tutorial_ForestDepthBoundaryFog.mat";
    private const string LegacyFogMaterialPath = AssetFolder + "/M_Tutorial_AreaFog_LocalVolume.mat";
    private const string RendererFeatureName = "Forest Depth Boundary Fog";
    private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";
    private const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";

    private static readonly string[] LegacyRootNames =
    {
        "FX_LocalAreaFog",
        "AreaFog_RoadClear_Zones"
    };

    private static readonly FogZonePreset[] ZonePresets =
    {
        new("FogZone_01_GateApproach", new Vector3(18f, 0.05f, 15.5f), Vector3.left, 16f, 18f, 7f, 0.032f, 0.8f, 0.045f, new Color(0.08f, 0.15f, 0.16f, 1f)),
        new("FogZone_02_NorthForest", new Vector3(17f, 0.05f, 25.5f), Vector3.left, 18f, 24f, 8f, 0.038f, 1.0f, 0.042f, new Color(0.07f, 0.14f, 0.15f, 1f)),
        new("FogZone_03_SouthForest", new Vector3(17f, 0.05f, 5.5f), Vector3.left, 18f, 24f, 8f, 0.038f, 1.0f, 0.042f, new Color(0.07f, 0.14f, 0.15f, 1f)),
        new("FogZone_04_DistantBackline", new Vector3(7f, 0.05f, 15.5f), Vector3.left, 34f, 20f, 10f, 0.05f, 1.2f, 0.035f, new Color(0.09f, 0.14f, 0.18f, 1f))
    };

    [MenuItem("RhythmRPG/Editors/World/Build Forest Tutorial Area Fog Zones")]
    [MenuItem("RhythmRPG/Editors/World/Build Forest Tutorial Depth Boundary Fog")]
    public static void Build()
    {
        var scene = OpenTargetScene();
        EnsureAssetFolder();

        var material = CreateOrUpdateFogMaterial();
        RemoveFogRoots();
        DeleteLegacyAssets();

        var root = CreateFogRoot();
        var zones = CreateZones(root.transform);
        var controller = root.AddComponent<ForestDepthFogZoneController>();
        controller.Configure(material, zones, true, applyImmediately: false);

        ConfigureRendererFeature(PcRendererPath, material);
        ConfigureRendererFeature(MobileRendererPath, material);
        ConfigureMainCameraDepthTexture();
        DisableGlobalDistanceFog();
        SaveMaterialDisabled(material);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        controller.ApplyNow();

        Selection.activeGameObject = root;
        Debug.Log("[ForestTutorialLocalFogSetup] Built scene-scoped fullscreen depth fog zones and removed legacy fog planes/boxes.");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Forest Tutorial Area Fog Zones")]
    [MenuItem("RhythmRPG/Editors/World/Validate Forest Tutorial Depth Boundary Fog")]
    public static void Validate()
    {
        OpenTargetScene();

        var hasError = false;
        var root = GameObject.Find(RootName);
        var controller = root != null ? root.GetComponent<ForestDepthFogZoneController>() : null;
        var zones = root != null ? root.GetComponentsInChildren<ForestDepthFogZone>(true) : System.Array.Empty<ForestDepthFogZone>();
        var material = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);

        Report(root != null, "Depth fog root exists.", "Missing " + RootName, ref hasError);
        Report(controller != null, "Zone controller exists.", "Missing ForestDepthFogZoneController.", ref hasError);
        Report(zones.Length > 0, "Depth fog zones exist.", "Missing ForestDepthFogZone children.", ref hasError);
        Report(material != null && material.shader != null && material.shader.name == FogShaderName,
            "Depth fog material uses the fullscreen shader.",
            "Fog material is missing or uses the wrong shader.",
            ref hasError);
        Report(root == null || root.GetComponentsInChildren<Renderer>(true).Length == 0,
            "Depth fog root has no fog mesh renderers.",
            "Depth fog root still contains mesh/plane fog renderers.",
            ref hasError);
        Report(HasRendererFeature(PcRendererPath),
            "PC renderer has the fullscreen depth fog feature.",
            "PC renderer is missing the fullscreen depth fog feature.",
            ref hasError);

        foreach (var legacyRootName in LegacyRootNames)
        {
            Report(GameObject.Find(legacyRootName) == null,
                legacyRootName + " removed.",
                "Legacy fog root still exists: " + legacyRootName,
                ref hasError);
        }

        if (!hasError)
        {
            Debug.Log("[ForestTutorialLocalFogSetup] Validation passed.");
        }
    }

    private static Scene OpenTargetScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.path != ScenePath)
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        return activeScene;
    }

    private static void EnsureAssetFolder()
    {
        if (AssetDatabase.IsValidFolder(AssetFolder))
        {
            return;
        }

        AssetDatabase.CreateFolder("Assets/0.MainProject/Art", "ForestLightingPipeline");
    }

    private static Material CreateOrUpdateFogMaterial()
    {
        var shader = Shader.Find(FogShaderName);
        if (shader == null)
        {
            throw new System.InvalidOperationException("Could not find shader: " + FogShaderName);
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
        SaveMaterialDisabled(material);
        EditorUtility.SetDirty(material);

        return material;
    }

    private static void RemoveFogRoots()
    {
        RemoveRoot(RootName);

        foreach (var legacyRootName in LegacyRootNames)
        {
            RemoveRoot(legacyRootName);
        }
    }

    private static void RemoveRoot(string rootName)
    {
        var root = GameObject.Find(rootName);
        if (root == null)
        {
            return;
        }

        Object.DestroyImmediate(root);
    }

    private static void DeleteLegacyAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(LegacyFogMaterialPath) != null)
        {
            AssetDatabase.DeleteAsset(LegacyFogMaterialPath);
        }
    }

    private static GameObject CreateFogRoot()
    {
        var root = new GameObject(RootName);
        var parent = GameObject.Find(EnvironmentParentName);
        if (parent != null)
        {
            root.transform.SetParent(parent.transform, true);
        }

        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        return root;
    }

    private static ForestDepthFogZone[] CreateZones(Transform parent)
    {
        var zones = new ForestDepthFogZone[ZonePresets.Length];
        for (var i = 0; i < ZonePresets.Length; i++)
        {
            var preset = ZonePresets[i];
            var zoneObject = new GameObject(preset.Name);
            zoneObject.transform.SetParent(parent, false);
            zoneObject.transform.position = preset.Position;
            zoneObject.transform.rotation = Quaternion.LookRotation(preset.Forward.normalized, Vector3.up);
            zoneObject.transform.localScale = Vector3.one;

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
            Debug.LogWarning("[ForestTutorialLocalFogSetup] Renderer asset not found: " + rendererPath);
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
        var obsoleteFeatures = new System.Collections.Generic.List<Object>();
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
    }

    private static void ConfigureMainCameraDepthTexture()
    {
        var mainCamera = Camera.main ?? Object.FindFirstObjectByType<Camera>();
        if (mainCamera == null)
        {
            Debug.LogWarning("[ForestTutorialLocalFogSetup] Main Camera not found. Depth texture was not configured.");
            return;
        }

        var cameraData = mainCamera.GetUniversalAdditionalCameraData();
        if (cameraData == null)
        {
            Debug.LogWarning("[ForestTutorialLocalFogSetup] Main Camera has no UniversalAdditionalCameraData.");
            return;
        }

        cameraData.requiresDepthTexture = true;
        EditorUtility.SetDirty(cameraData);
    }

    private static void DisableGlobalDistanceFog()
    {
        RenderSettings.fog = false;
        RenderSettings.fogDensity = 0f;

        var visualSettings = Object.FindFirstObjectByType<ForestVisualSceneSettings>();
        if (visualSettings == null)
        {
            return;
        }

        var serializedSettings = new SerializedObject(visualSettings);
        serializedSettings.FindProperty("fogEnabled").boolValue = false;
        serializedSettings.FindProperty("fogDensity").floatValue = 0f;
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(visualSettings);
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static void Report(bool condition, string success, string failure, ref bool hasError)
    {
        if (condition)
        {
            Debug.Log("[ForestTutorialLocalFogSetup] OK - " + success);
            return;
        }

        hasError = true;
        Debug.LogError("[ForestTutorialLocalFogSetup] " + failure);
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
