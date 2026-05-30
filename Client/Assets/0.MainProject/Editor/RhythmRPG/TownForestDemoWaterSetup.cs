#if UNITY_EDITOR
using Assets.Scripts.Water;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class TownForestDemoWaterSetup
{
    private const string ScenePath = "Assets/0.MainProject/Scenes/Town/Town_Forest.unity";
    private const string WaterPlanePrefabPath = "Assets/MobileDepthWater/Scenes/WaterPlane.prefab";
    private const string WaterMaterialPath = "Assets/MobileDepthWater/Materials/Water/Water.mat";
    private const string ToonWaterTexturePath = "Assets/MobileDepthWater/Textures/ToonWater.psd";
    private const string DistortionTexturePath = "Assets/MobileDepthWater/Textures/WaterDistortion.psd";
    private const string AppearanceRootName = "Town_Appearance";
    private const string WaterPlaneName = "WaterPlane";
    private const string PropertySetterName = "OceanWaterPropertySetter";
    private const string WaterAreaName = "OceanWaterArea";

    private static readonly Color DemoOceanWaterColor = new(0.18431373f, 0.666f, 0.9921569f, 1f);
    private static readonly Color DemoOceanBorderColor = new(1f, 1f, 1f, 0.622f);
    private static readonly Vector2 DemoOceanWaterTile = new(0.15f, 0.15f);
    private static readonly Vector2 DemoOceanDistortionTile = new(0.06f, 0.06f);
    private static readonly Vector2 DemoOceanMoveDirection = new(-0.7f, 0.3f);

    [MenuItem("Tools/Town Forest/Apply DemoScene Water Settings")]
    [MenuItem("RhythmRPG/Editors/Town/Apply DemoScene Water Settings")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var waterPlane = FindOrCreateWaterPlane();
        var waterRenderer = waterPlane.GetComponent<MeshRenderer>();
        if (waterRenderer == null)
        {
            Debug.LogError("[TownForestDemoWaterSetup] WaterPlane is missing MeshRenderer.");
            return;
        }

        var waterMaterial = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
        if (waterMaterial != null)
        {
            waterRenderer.sharedMaterial = waterMaterial;
        }

        waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
        waterRenderer.receiveShadows = false;

        var propertySetter = FindOrCreateComponent<WaterPropertyBlockSetter>(PropertySetterName);
        ConfigurePropertySetter(propertySetter, waterRenderer, waterPlane.transform.position.y);

        var waterArea = FindOrCreateComponent<WaterArea>(WaterAreaName);
        ConfigureWaterArea(waterArea, propertySetter, waterRenderer);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = waterPlane;
        Debug.Log("[TownForestDemoWaterSetup] DemoScene ocean water settings applied to Town_Forest.");
    }

    private static GameObject FindOrCreateWaterPlane()
    {
        var existing = GameObject.Find(WaterPlaneName);
        if (existing != null)
        {
            return existing;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPlanePrefabPath);
        var waterPlane = prefab != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
            : GameObject.CreatePrimitive(PrimitiveType.Plane);

        waterPlane.name = WaterPlaneName;
        waterPlane.transform.position = new Vector3(49.5f, -1.66208375f, 15.9946289f);
        waterPlane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        waterPlane.transform.localScale = new Vector3(100f, 100f, 1f);
        return waterPlane;
    }

    private static T FindOrCreateComponent<T>(string objectName) where T : Component
    {
        var go = GameObject.Find(objectName);
        if (go == null)
        {
            go = new GameObject(objectName);
            var appearanceRoot = GameObject.Find(AppearanceRootName);
            if (appearanceRoot != null)
            {
                go.transform.SetParent(appearanceRoot.transform, false);
            }
        }

        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static void ConfigurePropertySetter(WaterPropertyBlockSetter setter, Renderer waterRenderer, float waterHeight)
    {
        var serialized = new SerializedObject(setter);
        SetArrayElement(serialized, "waterRenderers", 0, waterRenderer);
        serialized.FindProperty("waterColor").colorValue = DemoOceanWaterColor;
        serialized.FindProperty("waterTex").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture>(ToonWaterTexturePath);
        serialized.FindProperty("waterTile").vector2Value = DemoOceanWaterTile;
        serialized.FindProperty("textureVisibility").floatValue = 0.276f;
        serialized.FindProperty("distortionTex").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture>(DistortionTexturePath);
        serialized.FindProperty("distortionTile").vector2Value = DemoOceanDistortionTile;
        serialized.FindProperty("waterHeight").floatValue = waterHeight;
        serialized.FindProperty("waterDeep").floatValue = 22.9f;
        serialized.FindProperty("waterDepthParam").floatValue = 0.0406f;
        serialized.FindProperty("waterMinAlpha").floatValue = 0.552f;
        serialized.FindProperty("borderColor").colorValue = DemoOceanBorderColor;
        serialized.FindProperty("borderWidth").floatValue = 0.184f;
        serialized.FindProperty("moveDirection").vector2Value = DemoOceanMoveDirection;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        setter.Awake();
        EditorUtility.SetDirty(setter);
    }

    private static void ConfigureWaterArea(WaterArea waterArea, WaterPropertyBlockSetter propertySetter, Renderer waterRenderer)
    {
        waterArea.gameObject.tag = "Water";

        var serialized = new SerializedObject(waterArea);
        serialized.FindProperty("waterProperties").objectReferenceValue = propertySetter;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var collider = waterArea.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = waterArea.gameObject.AddComponent<BoxCollider>();
        }

        var bounds = waterRenderer.bounds;
        waterArea.transform.position = bounds.center;
        collider.isTrigger = true;
        collider.center = Vector3.zero;
        collider.size = new Vector3(Mathf.Max(bounds.size.x, 100f), 32f, Mathf.Max(bounds.size.z, 100f));

        EditorUtility.SetDirty(waterArea);
        EditorUtility.SetDirty(collider);
    }

    private static void SetArrayElement(SerializedObject serialized, string propertyName, int index, Object value)
    {
        var property = serialized.FindProperty(propertyName);
        property.arraySize = Mathf.Max(property.arraySize, index + 1);
        property.GetArrayElementAtIndex(index).objectReferenceValue = value;
    }
}
#endif
