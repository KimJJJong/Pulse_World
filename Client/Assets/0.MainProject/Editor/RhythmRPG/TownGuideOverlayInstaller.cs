#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TownGuideOverlayInstaller
{
    private const string MenuRoot = "RhythmRPG/Editors/UI";
    private const string TownMapScenePath = "Assets/0.MainProject/Scenes/Town/TownMap.unity";
    private const string TownForestScenePath = "Assets/0.MainProject/Scenes/Town/Town_Forest.unity";
    private const string GuideCanvasName = "Canvas_TownGuideOverlay";
    private const string GuideOverlayPrefabName = "PF_Canvas_TownGuideOverlay";
    private const string GuideSpritePath = "Assets/0.MainProject/Resources/UI/TownGuide/TownControlsGuide.png";
    private const string GuideResourcePath = "UI/TownGuide/TownControlsGuide";
    private const string GuideOverlayPrefabPath = "Assets/0.MainProject/Prefabs/UI/Town/" + GuideOverlayPrefabName + ".prefab";
    private const int GuideCanvasSortingOrder = 29000;

    private static readonly string[] TownScenePaths =
    {
        TownMapScenePath,
        TownForestScenePath
    };

    [MenuItem(MenuRoot + "/Ensure Town Guide Overlay")]
    public static void EnsureTownGuideOverlay()
    {
        ConfigureGuideTexture();
        EnsureGuidePrefab();

        ForEachTownScene(scene =>
        {
            EnsureScene(scene);
            Save(scene);
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TownGuideOverlayInstaller] Ensured Town guide overlay in TownMap and Town_Forest.");
    }

    [MenuItem(MenuRoot + "/Ensure Town Guide Overlay Prefab")]
    public static void EnsureTownGuideOverlayPrefab()
    {
        ConfigureGuideTexture();
        EnsureGuidePrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TownGuideOverlayInstaller] Ensured Town guide overlay prefab: {GuideOverlayPrefabPath}");
    }

    [MenuItem(MenuRoot + "/Ensure Town Forest Guide Overlay")]
    public static void EnsureTownForestGuideOverlay()
    {
        ConfigureGuideTexture();
        EnsureGuidePrefab();

        WithScene(TownForestScenePath, scene =>
        {
            EnsureScene(scene);
            Save(scene);
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TownGuideOverlayInstaller] Ensured Town guide overlay in Town_Forest.");
    }

    [MenuItem(MenuRoot + "/Verify Town Guide Overlay")]
    public static void VerifyTownGuideOverlay()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GuideSpritePath);
        Require(sprite != null, $"Guide sprite is missing or not imported as Sprite: {GuideSpritePath}");

        VerifyGuidePrefab(sprite);
        ForEachTownScene(scene => VerifyScene(scene, sprite));

        Debug.Log("[TownGuideOverlayInstaller] Town guide overlay verification passed.");
    }

    [MenuItem("GameObject/RhythmRPG/UI/Town Guide Overlay", false, 10)]
    public static void CreateTownGuideOverlayInActiveScene(MenuCommand menuCommand)
    {
        ConfigureGuideTexture();
        var prefab = EnsureGuidePrefab();
        var scene = SceneManager.GetActiveScene();
        Require(scene.IsValid() && scene.isLoaded, "Active scene is not valid.");
        var instanceName = GetUniqueRootName(scene, GuideCanvasName);

        var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        Require(instance != null, $"Failed to instantiate guide overlay prefab: {GuideOverlayPrefabPath}");

        instance.name = instanceName;
        ConfigureRect(instance.transform as RectTransform);
        ConfigureCanvas(instance);
        ConfigureGuideComponent(instance);

        Undo.RegisterCreatedObjectUndo(instance, "Create Town Guide Overlay");
        Selection.activeGameObject = instance;
        EditorUtility.SetDirty(instance);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigureGuideTexture()
    {
        AssetDatabase.ImportAsset(GuideSpritePath, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(GuideSpritePath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"Guide texture importer not found: {GuideSpritePath}");

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        var platform = importer.GetDefaultPlatformTextureSettings();
        if (platform.format != TextureImporterFormat.Automatic || platform.maxTextureSize < 1024)
        {
            platform.format = TextureImporterFormat.Automatic;
            platform.maxTextureSize = 2048;
            importer.SetPlatformTextureSettings(platform);
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
    }

    private static void ForEachTownScene(Action<Scene> action)
    {
        foreach (string scenePath in TownScenePaths)
            WithScene(scenePath, action);
    }

    private static void WithScene(string scenePath, Action<Scene> action)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
            throw new InvalidOperationException("Scene path is empty.");

        var scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForCheck = false;

        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            openedForCheck = true;
        }

        try
        {
            action(scene);
        }
        finally
        {
            if (openedForCheck && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void EnsureScene(Scene scene)
    {
        if (!scene.IsValid())
            throw new InvalidOperationException("Town scene is invalid.");

        var root = FindGuideRoot(scene);
        if (root == null)
        {
            var prefab = EnsureGuidePrefab();
            root = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (root == null)
            {
                root = new GameObject(GuideCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TownGuideOverlay));
                SceneManager.MoveGameObjectToScene(root, scene);
            }
        }
        else if (root.transform.parent != null)
        {
            root.transform.SetParent(null, true);
        }

        root.name = GuideCanvasName;
        root.SetActive(true);
        ConfigureRect(root.transform as RectTransform);
        ConfigureCanvas(root);
        ConfigureGuideComponent(root);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigureRect(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void ConfigureCanvas(GameObject root)
    {
        var canvas = root.GetComponent<Canvas>();
        if (canvas == null)
            canvas = root.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = GuideCanvasSortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = root.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (root.GetComponent<GraphicRaycaster>() == null)
            root.AddComponent<GraphicRaycaster>();
    }

    private static GameObject EnsureGuidePrefab()
    {
        EnsureFolderPath("Assets/0.MainProject/Prefabs/UI/Town");

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GuideOverlayPrefabPath);
        GameObject root = null;
        bool loadedPrefabContents = existing != null;

        try
        {
            root = loadedPrefabContents
                ? PrefabUtility.LoadPrefabContents(GuideOverlayPrefabPath)
                : new GameObject(GuideCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TownGuideOverlay));

            root.name = GuideCanvasName;
            root.SetActive(true);
            ConfigureRect(root.transform as RectTransform);
            ConfigureCanvas(root);
            ConfigureGuideComponent(root);

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, GuideOverlayPrefabPath);
            Require(savedPrefab != null, $"Failed to save guide overlay prefab: {GuideOverlayPrefabPath}");
        }
        finally
        {
            if (root != null)
            {
                if (loadedPrefabContents)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuideOverlayPrefabPath);
        Require(prefab != null, $"Guide overlay prefab was not created: {GuideOverlayPrefabPath}");
        return prefab;
    }

    private static void ConfigureGuideComponent(GameObject root)
    {
        var guide = root.GetComponent<TownGuideOverlay>();
        if (guide == null)
            guide = root.AddComponent<TownGuideOverlay>();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GuideSpritePath);
        var so = new SerializedObject(guide);
        so.FindProperty("_guideSprite").objectReferenceValue = sprite;
        so.FindProperty("_guideResourcePath").stringValue = GuideResourcePath;
        so.FindProperty("_showOnStart").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        guide.Configure(sprite, GuideResourcePath, true);
        EditorUtility.SetDirty(guide);
    }

    private static void VerifyGuidePrefab(Sprite expectedSprite)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuideOverlayPrefabPath);
        Require(prefab != null, $"Guide overlay prefab is missing: {GuideOverlayPrefabPath}");

        var root = PrefabUtility.LoadPrefabContents(GuideOverlayPrefabPath);
        try
        {
            VerifyRoot(root, expectedSprite, GuideOverlayPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void VerifyScene(Scene scene, Sprite expectedSprite)
    {
        var root = FindGuideRoot(scene);
        Require(root != null, $"{GuideCanvasName} is missing in {scene.path}.");
        VerifyRoot(root, expectedSprite, scene.path);
    }

    private static GameObject FindGuideRoot(Scene scene)
    {
        return FindSceneObject(scene, GuideCanvasName) ?? FindSceneObject(scene, GuideOverlayPrefabName);
    }

    private static void VerifyRoot(GameObject root, Sprite expectedSprite, string context)
    {
        Require(root.activeSelf, $"{GuideCanvasName} must be active in {context}.");

        var canvas = root.GetComponent<Canvas>();
        Require(canvas != null, $"{GuideCanvasName} is missing Canvas in {context}.");
        Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay, $"{GuideCanvasName} must be Screen Space Overlay in {context}.");
        Require(canvas.sortingOrder == GuideCanvasSortingOrder, $"{GuideCanvasName} sorting order must be {GuideCanvasSortingOrder} in {context}.");
        Require(root.GetComponent<CanvasScaler>() != null, $"{GuideCanvasName} is missing CanvasScaler in {context}.");
        Require(root.GetComponent<GraphicRaycaster>() != null, $"{GuideCanvasName} is missing GraphicRaycaster in {context}.");

        var guide = root.GetComponent<TownGuideOverlay>();
        Require(guide != null, $"{GuideCanvasName} is missing TownGuideOverlay in {context}.");

        var so = new SerializedObject(guide);
        Require(so.FindProperty("_guideSprite").objectReferenceValue == expectedSprite, $"{GuideCanvasName} guide sprite is not linked in {context}.");
        Require(so.FindProperty("_guideResourcePath").stringValue == GuideResourcePath, $"{GuideCanvasName} resource path is incorrect in {context}.");
        Require(so.FindProperty("_showOnStart").boolValue, $"{GuideCanvasName} must show on start in {context}.");

        var guideButton = FindChild(root.transform, "Button_Guide");
        var window = FindChild(root.transform, "TownGuideWindow");
        var guideImage = window != null ? FindChild(window, "GuideImage") : null;
        var backButton = window != null ? FindChild(window, "Button_BackGuide") : null;
        Require(guideButton != null, $"{GuideCanvasName} is missing Button_Guide in {context}.");
        Require(guideButton.GetComponent<Button>() != null, $"Button_Guide is missing Button in {context}.");
        Require(window != null, $"{GuideCanvasName} is missing TownGuideWindow in {context}.");
        Require(window.gameObject.activeSelf, $"TownGuideWindow must be visible by default in {context}.");
        Require(guideImage != null, $"TownGuideWindow is missing GuideImage in {context}.");
        Require(guideImage.GetComponent<Image>() != null, $"GuideImage is missing Image in {context}.");
        Require(backButton != null, $"TownGuideWindow is missing Button_BackGuide in {context}.");
        Require(backButton.GetComponent<Button>() != null, $"Button_BackGuide is missing Button in {context}.");
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var found = FindChild(roots[i].transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChild(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static string GetUniqueRootName(Scene scene, string baseName)
    {
        if (FindSceneObject(scene, baseName) == null)
            return baseName;

        int index = 1;
        string candidate;
        do
        {
            candidate = $"{baseName} ({index})";
            index++;
        }
        while (FindSceneObject(scene, candidate) != null);

        return candidate;
    }

    private static void EnsureFolderPath(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parts = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"Failed to save scene: {scene.path}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

}
#endif
