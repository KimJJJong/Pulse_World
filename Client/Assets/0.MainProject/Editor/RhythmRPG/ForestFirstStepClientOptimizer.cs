#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ForestFirstStepClientOptimizer
{
    private const string TargetScenePath = "Assets/0.MainProject/Scenes/Game/Game_Forest_First_Step.unity";
    private const string BoardViewPrefabPath = "Assets/0.MainProject/Resources/GameInit/BoardView.prefab";
    private const string LegacyBoardViewPrefabPath = "Assets/0.MainProject/Resources/BoardView.prefab";
    private const float BoardTileVisibleDistance = 48f;
    private const float BoardTileCullingHysteresis = 10f;
    private const float BoardTileCameraForwardLookahead = 28f;

    [MenuItem("RhythmRPG/Editors/World/Optimize Forest First Step Client")]
    public static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        int optimizedRenderers = 0;
        optimizedRenderers += OptimizeRendererTree(scene, "PF_RhythmBattle_Functional_Set");
        optimizedRenderers += OptimizeRendererTree(scene, "Deco_Tiles");
        optimizedRenderers += OptimizeRendererTree(scene, "Forest_Decoration_Set");
        optimizedRenderers += OptimizeRendererTree(scene, "Runic_Circle_Platform");

        ConfigureBoardView(scene);
        ConfigureCuller(scene, "Forest_Decoration_Set", 70f, 12f, 0.12f, 2048);
        ConfigureCuller(scene, "Deco_Tiles", 70f, 12f, 0.12f, 1024);
        ConfigureCuller(scene, "RunTimeInteractionObject", 76f, 12f, 0.15f, 512);
        ConfigureCuller(scene, "Runic_Circle_Platform", 82f, 14f, 0.15f, 512);

        ConfigureBoardViewPrefab(BoardViewPrefabPath);
        ConfigureBoardViewPrefab(LegacyBoardViewPrefabPath);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[ForestFirstStepClientOptimizer] Applied client optimizations. " +
            $"Scene={scene.name}, OptimizedRenderers={optimizedRenderers}.");
    }

    private static int OptimizeRendererTree(Scene scene, string rootName)
    {
        GameObject root = FindSceneObject(scene, rootName);
        if (root == null)
            return 0;

        int count = 0;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererTarget = renderers[i];
            if (rendererTarget == null)
                continue;

            if (rendererTarget is ParticleSystemRenderer || rendererTarget is TrailRenderer || rendererTarget is LineRenderer)
                continue;

            rendererTarget.shadowCastingMode = ShadowCastingMode.Off;
            rendererTarget.receiveShadows = false;
            rendererTarget.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            rendererTarget.lightProbeUsage = LightProbeUsage.Off;
            rendererTarget.reflectionProbeUsage = ReflectionProbeUsage.Off;
            EditorUtility.SetDirty(rendererTarget);
            count++;
        }

        return count;
    }

    private static void ConfigureBoardView(Scene scene)
    {
        foreach (BoardView boardView in UnityEngine.Object.FindObjectsByType<BoardView>(FindObjectsSortMode.None))
        {
            if (boardView == null || boardView.gameObject.scene != scene)
                continue;

            ConfigureBoardViewSerialized(boardView);
            EditorUtility.SetDirty(boardView);
        }
    }

    private static void ConfigureBoardViewPrefab(string prefabPath)
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabRoot == null)
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            BoardView boardView = contents.GetComponentInChildren<BoardView>(true);
            if (boardView == null)
                return;

            ConfigureBoardViewSerialized(boardView);
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void ConfigureBoardViewSerialized(BoardView boardView)
    {
        var so = new SerializedObject(boardView);
        SetBool(so, "tileDistanceCullingEnabled", true);
        SetFloat(so, "tileVisibleDistance", BoardTileVisibleDistance);
        SetFloat(so, "tileCullingHysteresis", BoardTileCullingHysteresis);
        SetFloat(so, "tileCameraForwardLookahead", BoardTileCameraForwardLookahead);
        SetFloat(so, "tileCullingRefreshInterval", 0.1f);
        SetInt(so, "tileCullingChecksPerRefresh", 4096);
        SetBool(so, "tileCullingPreferLocalPlayer", true);
        SetFloat(so, "walkableGridEffectRefreshRate", 18f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureCuller(
        Scene scene,
        string rootName,
        float visibleDistance,
        float hysteresisDistance,
        float refreshInterval,
        int checksPerRefresh)
    {
        GameObject root = FindSceneObject(scene, rootName);
        if (root == null)
            return;

        StaticRendererDistanceCuller culler = root.GetComponent<StaticRendererDistanceCuller>();
        if (culler == null)
            culler = root.AddComponent<StaticRendererDistanceCuller>();

        var so = new SerializedObject(culler);
        SetFloat(so, "visibleDistance", visibleDistance);
        SetFloat(so, "hysteresisDistance", hysteresisDistance);
        SetFloat(so, "refreshInterval", refreshInterval);
        SetInt(so, "checksPerRefresh", checksPerRefresh);
        SetBool(so, "includeInactive", false);
        SetBool(so, "ignoreDynamicRenderers", true);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(culler);
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject found = FindInChildren(roots[i].transform, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject FindInChildren(Transform root, string objectName)
    {
        if (root == null)
            return null;

        if (root.name.Equals(objectName, StringComparison.Ordinal))
            return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject found = FindInChildren(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetFloat(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetInt(SerializedObject so, string propertyName, int value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }
}
#endif
