using System;
using System.Collections.Generic;
using System.IO;
using Client.Content.Item;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class Home1SceneSpaceUiBuilder
{
    private const string ScenePath = "Assets/0.MainProject/Scenes/Home 1.unity";
    private const string TownMapScenePath = "Assets/0.MainProject/Scenes/Town/TownMap.unity";
    private const string TownForestScenePath = "Assets/0.MainProject/Scenes/Town/Town_Forest.unity";
    private const string TownHomeControllerName = "TownHomeUiController";
    private const string TownHomeOverlayCanvasName = "Canvas_TownHomeOverlay";
    private const int TownHomeOverlaySortingOrder = 30000;
    private const string UiResourceSource = "../Resource/UI";
    private const string UiResourceTarget = "Assets/Resources/UI";
    private const string NanumGothicFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/NanumGothic SDF.asset";
    private const string NanumGothicFontFallbackPath = "Assets/TextMesh Pro/Resources/NanumGothic SDF.asset";
    private const string TownPassTicket = "Town Pass";
    private const string MissingMapTicket = "없음";
    private const string DetailIconFrameResourcePath = "UI/UI_Home_Equipment_Detail/UI_equipment_icon_frame_detail";
    private const string SlotIconFrameResourcePath = "UI/UI_Home_Equipment_Detail/UI_equipment_icon_frame_slot";
    private const string TownInventoryPrefabPath = "Assets/0.MainProject/Prefabs/UI/Town/PF_TownInventory_UI.prefab";

    private static readonly Vector2 LayoutSize = new(1280f, 720f);
    private static readonly Color ParchmentText = new(0.10f, 0.22f, 0.20f, 1f);
    private static readonly Color ParchmentMutedText = new(0.32f, 0.26f, 0.18f, 1f);
    private static readonly Color GoldText = new(0.94f, 0.72f, 0.20f, 1f);
    private static readonly Color ButtonLightText = new(0.96f, 0.92f, 0.82f, 1f);
    private static readonly Color EquipmentFrameBackground = new(0.70f, 0.53f, 0.32f, 0.76f);
    private static Sprite _defaultUiSprite;
    private static TMP_FontAsset _nanumGothicFont;

    [MenuItem("RhythmRPG/Editors/UI/Rebuild Home1 Scene Space UI")]
    public static void Build()
    {
        EnsureUiResources();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        RemoveExistingUi();
        var camera = EnsureMainCamera();
        EnsureEventSystem();

        var canvas = CreateOverlayCanvas();
        var cameraDirector = canvas.gameObject.AddComponent<HomeSceneCameraDirector>();
        ConfigureCameraDirector(cameraDirector, camera);

        var built = BuildHomeOverlay(canvas.transform, cameraDirector);
        if (built.InventoryUi != null)
            EditorUtility.SetDirty(built.InventoryUi);

        EnsureAppearancePreviewController();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Home1SceneSpaceUiBuilder] Rebuilt Home 1 with Screen Space Overlay resource UI.");
    }

    [MenuItem("RhythmRPG/Editors/UI/Ensure Town Home Overlay In Active Scene")]
    public static void EnsureTownHomeOverlayInActiveScene()
    {
        EnsureUiResources();
        var scene = EditorSceneManager.GetActiveScene();
        EnsureTownHomeOverlay(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Home1SceneSpaceUiBuilder] Ensured Town Home overlay in {scene.name}.");
    }

    [MenuItem("RhythmRPG/Editors/UI/Ensure Town Home Overlay In Town Scenes")]
    public static void EnsureTownHomeOverlayInTownScenes()
    {
        EnsureUiResources();
        var originalScenePath = EditorSceneManager.GetActiveScene().path;
        var scenePaths = new[] { TownMapScenePath, TownForestScenePath };

        foreach (var scenePath in scenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EnsureTownHomeOverlay(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrWhiteSpace(originalScenePath) && Array.IndexOf(scenePaths, originalScenePath) < 0)
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        Debug.Log("[Home1SceneSpaceUiBuilder] Ensured Town Home overlays in TownMap and Town_Forest.");
    }

    [MenuItem("RhythmRPG/Editors/UI/Verify Town Home Overlay In Town Scenes")]
    public static void VerifyTownHomeOverlayInTownScenes()
    {
        var originalScenePath = EditorSceneManager.GetActiveScene().path;
        var scenePaths = new[] { TownMapScenePath, TownForestScenePath };

        foreach (var scenePath in scenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            VerifyTownHomeOverlay(scene);
        }

        if (!string.IsNullOrWhiteSpace(originalScenePath) && Array.IndexOf(scenePaths, originalScenePath) < 0)
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);

        Debug.Log("[Home1SceneSpaceUiBuilder] Town Home overlay verification passed in TownMap and Town_Forest.");
    }

    [MenuItem("RhythmRPG/Editors/UI/Verify Home1 Scene Space Flow")]
    public static void VerifyFlow()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Require(SceneNames.Home == "Home 1", $"SceneNames.Home must be 'Home 1' but was '{SceneNames.Home}'.");
        RequireBuildScene("Assets/0.MainProject/Scenes/Home 1.unity", true);
        RequireBuildScene("Assets/0.MainProject/Scenes/Home.unity", false);

        var canvas = RequireComponent<Canvas>("Canvas_Home1_SceneSpace");
        Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay, "Canvas_Home1_SceneSpace must be Screen Space Overlay.");

        var navigator = canvas.GetComponent<HomeUiPageNavigator>();
        Require(navigator != null, "Canvas_Home1_SceneSpace is missing HomeUiPageNavigator.");
        Require(canvas.GetComponent<HomeSceneCameraDirector>() != null, "Canvas_Home1_SceneSpace is missing HomeSceneCameraDirector.");

        RequireSceneObject("UI_Home_Interface");
        RequireSceneObject("UI_Home_Equipment");
        RequireSceneObject("UI_Home_Inventory");
        RequireSceneObject("UI_Home_Appearance");
        RequireSceneObject("UI_Home_Map");
        RequireSceneObject("UI_Home_Equipment_Detail");
        RequireSceneObject("TownEntryChoicePanel");

        RequireComponent<HomeInventoryUI>("UI_Home_Equipment");
        var popup = RequireComponent<HomeEquipPopupUI>("UI_Home_Equipment_Detail");
        RequireComponent<HomeAppearancePageUI>("UI_Home_Appearance");
        var mapUi = RequireComponent<HomeMapRealmUI>("UI_Home_Map");
        VerifyHomeMapTownEntry(mapUi);
        popup.Show(EquipmentSlot.Weapon);
        RequireComponent<RectMask2D>("ItemListViewport");
        popup.Hide();
        RequireAllButtonsHaveFeedback();
        RequireNoLoadingDependencies();

        navigator.ShowHome();
        Require(RequireSceneObject("UI_Home_Interface").activeSelf, "Home root should be active after ShowHome.");
        navigator.ShowEquipment();
        Require(RequireSceneObject("UI_Home_Equipment").activeSelf, "Equipment root should be active after ShowEquipment.");
        navigator.ShowAppearance();
        Require(RequireSceneObject("UI_Home_Appearance").activeSelf, "Appearance root should be active after ShowAppearance.");
        navigator.ShowMap();
        Require(RequireSceneObject("UI_Home_Map").activeSelf, "Map root should be active after ShowMap.");

        Debug.Log("[Home1SceneSpaceUiBuilder] Home1 Overlay flow verification passed.");
    }

    [MenuItem("RhythmRPG/Editors/UI/Capture Home1 Map Town Entry Preview Screenshots")]
    public static void CaptureHomeMapTownEntryPreviewScreenshots()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvas = RequireComponent<Canvas>("Canvas_Home1_SceneSpace");
        var mapRoot = RequireSceneObject("UI_Home_Map");
        ActivateOnlyHomeRoot(mapRoot);
        PrepareForestMapPreview(mapRoot.transform);

        var panel = RequireSceneObject("TownEntryChoicePanel");
        panel.SetActive(true);
        var panelTransform = panel.transform;

        SetTownPreviewView(panelTransform, "EntryChoiceRoot", "Whispering Forest Town", "Choose how you want to enter a town.");
        CaptureCanvasPreview(canvas, "HomeMap_TownEntry_Choice_Preview.png");

        PrepareExistingTownPreview(panelTransform);
        SetTownPreviewView(panelTransform, "ExistingTownsRoot", "Existing Towns", "Browse available towns and join one.");
        CaptureCanvasPreview(canvas, "HomeMap_TownEntry_Existing_Preview.png");

        PrepareCreateTownPreview(panelTransform);
        SetTownPreviewView(panelTransform, "CreateTownRoot", "Create My Town", "Create a new town and choose who can join.");
        CaptureCanvasPreview(canvas, "HomeMap_TownEntry_Create_Preview.png");

        PrepareKeyTownPreview(panelTransform);
        SetTownPreviewView(panelTransform, "JoinWithKeyRoot", "Join with Key", "Enter the town key shared by the host.");
        CaptureCanvasPreview(canvas, "HomeMap_TownEntry_Key_Preview.png");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("[Home1SceneSpaceUiBuilder] Captured Home1 Map Town entry preview screenshots.");
    }

    [MenuItem("RhythmRPG/Editors/UI/Configure Home1 Map Realm Routes")]
    public static void ConfigureMapRealmRoutes()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var mapUi = FindMapRealmUiInScene(scene.path);
        if (mapUi == null)
            throw new InvalidOperationException("UI_Home_Map is missing HomeMapRealmUI.");

        ApplyMapRealmRoutes(mapUi);
        EditorUtility.SetDirty(mapUi);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Home1SceneSpaceUiBuilder] Configured Home 1 map routes: plains=TownMap, forest=Town_Forest, others=missing.");
    }

    [MenuItem("RhythmRPG/Editors/UI/Configure Home1 Map Town Entry UI")]
    public static void ConfigureHome1MapTownEntryUi()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var mapUi = FindMapRealmUiInScene(scene.path);
        if (mapUi == null)
            throw new InvalidOperationException("UI_Home_Map is missing HomeMapRealmUI.");

        ApplyMapRealmRoutes(mapUi);
        BuildTownEntryChoicePanel(mapUi.transform as RectTransform, mapUi);
        ApplyMapPieceHighlights(mapUi);
        ApplyNanumGothicFontToChildren(mapUi.transform as RectTransform);
        EditorUtility.SetDirty(mapUi);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Home1SceneSpaceUiBuilder] Configured Home 1 map Town entry UI object references.");
    }

    [MenuItem("RhythmRPG/Editors/UI/Configure Home1 Map Piece Highlights")]
    public static void ConfigureHome1MapPieceHighlights()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var mapUi = FindMapRealmUiInScene(scene.path);
        if (mapUi == null)
            throw new InvalidOperationException("UI_Home_Map is missing HomeMapRealmUI.");

        ApplyMapPieceHighlights(mapUi);
        EditorUtility.SetDirty(mapUi);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Home1SceneSpaceUiBuilder] Configured Home 1 map piece-shaped highlights.");
    }

    private static void EnsureUiResources()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        var sourceRoot = Path.GetFullPath(Path.Combine(projectRoot, UiResourceSource));
        var targetRoot = Path.Combine(Application.dataPath, "Resources", "UI");

        if (!Directory.Exists(sourceRoot))
        {
            Debug.LogError($"[Home1SceneSpaceUiBuilder] Missing UI source root: {sourceRoot}");
            return;
        }

        Directory.CreateDirectory(targetRoot);
        foreach (var generatedExample in Directory.EnumerateFiles(targetRoot, "*example*.png", SearchOption.AllDirectories))
        {
            var relativeGenerated = Path.GetRelativePath(targetRoot, generatedExample).Replace("\\", "/");
            if (relativeGenerated.StartsWith("UI_Lodaing/", StringComparison.OrdinalIgnoreCase))
                continue;

            File.Delete(generatedExample);
            var meta = $"{generatedExample}.meta";
            if (File.Exists(meta))
                File.Delete(meta);
        }

        foreach (var source in Directory.EnumerateFiles(sourceRoot, "*.png", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, source).Replace("\\", "/");
            if (relative.StartsWith("UI_Lodaing/", StringComparison.OrdinalIgnoreCase))
                continue;
            if (relative.IndexOf("_example", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            var target = Path.Combine(targetRoot, relative.Replace("/", Path.DirectorySeparatorChar.ToString()));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!File.Exists(target) || new FileInfo(source).Length != new FileInfo(target).Length)
                File.Copy(source, target, true);

            var assetPath = $"{UiResourceTarget}/{relative}";
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                if (IsMapPieceTexturePath(relative))
                    importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }
        }
    }

    private static void RemoveExistingUi()
    {
        foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
            UnityEngine.Object.DestroyImmediate(canvas.gameObject);

        foreach (var eventSystem in UnityEngine.Object.FindObjectsOfType<EventSystem>(true))
            UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
    }

    private static Camera EnsureMainCamera()
    {
        var camera = Camera.main ?? UnityEngine.Object.FindObjectOfType<Camera>(true);
        if (camera != null)
        {
            camera.tag = "MainCamera";
            return camera;
        }

        var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        cameraGo.transform.SetPositionAndRotation(new Vector3(13.9f, 24.3f, -29.4f), Quaternion.Euler(6.2f, 331.4f, 0f));
        camera = cameraGo.GetComponent<Camera>();
        camera.fieldOfView = 60f;
        camera.clearFlags = CameraClearFlags.Skybox;
        return camera;
    }

    private static void EnsureEventSystem()
    {
        var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemGo.transform.SetAsLastSibling();
    }

    private static void EnsureEventSystemIfMissing()
    {
        if (UnityEngine.Object.FindObjectOfType<EventSystem>(true) != null)
            return;

        EnsureEventSystem();
    }

    private static Canvas CreateOverlayCanvas()
    {
        return CreateOverlayCanvas("Canvas_Home1_SceneSpace", 100);
    }

    private static Canvas CreateOverlayCanvas(string name, int sortingOrder)
    {
        var canvasGo = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.localPosition = Vector3.zero;
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = Vector3.one;
        canvasRect.sizeDelta = LayoutSize;
        canvasRect.pivot = new Vector2(0.5f, 0.5f);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = LayoutSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 12f;
        scaler.referencePixelsPerUnit = 100f;

        return canvas;
    }

    private static void ConfigureCameraDirector(HomeSceneCameraDirector director, Camera camera)
    {
        var model = GameObject.Find("Barbarian");
        var so = new SerializedObject(director);
        so.FindProperty("_camera").objectReferenceValue = camera;
        so.FindProperty("_modelRoot").objectReferenceValue = model != null ? model.transform : null;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static BuiltHomeOverlay BuildHomeOverlay(Transform parent, HomeSceneCameraDirector cameraDirector, bool isTownOverlay = false)
    {
        var homeRoot = CreatePageRoot(parent, "UI_Home_Interface", true);
        var equipmentRoot = CreatePageRoot(parent, "UI_Home_Equipment", false);
        var inventoryRoot = CreatePageRoot(parent, "UI_Home_Inventory", false);
        var appearanceRoot = CreatePageRoot(parent, "UI_Home_Appearance", false);
        var mapRoot = CreatePageRoot(parent, "UI_Home_Map", false);
        var detailRoot = CreatePageRoot(parent, "UI_Home_Equipment_Detail", false);

        var homeButtons = BuildHomeInterface(homeRoot, isTownOverlay);
        var equipmentBack = BuildEquipmentScreen(equipmentRoot, detailRoot, out var inventoryUi, isTownOverlay);
        var inventoryBack = BuildInventoryScreen(inventoryRoot, isTownOverlay);
        var appearanceBack = BuildAppearanceScreen(appearanceRoot, isTownOverlay);
        var mapBack = BuildMapScreen(mapRoot, isTownOverlay);
        BuildDetailScreen(detailRoot, isTownOverlay);

        var navigator = parent.gameObject.AddComponent<HomeUiPageNavigator>();
        var navigatorSo = new SerializedObject(navigator);
        navigatorSo.FindProperty("_homeRoot").objectReferenceValue = homeRoot.gameObject;
        navigatorSo.FindProperty("_equipmentRoot").objectReferenceValue = equipmentRoot.gameObject;
        navigatorSo.FindProperty("_inventoryRoot").objectReferenceValue = inventoryRoot.gameObject;
        navigatorSo.FindProperty("_appearanceRoot").objectReferenceValue = appearanceRoot.gameObject;
        navigatorSo.FindProperty("_mapRoot").objectReferenceValue = mapRoot.gameObject;
        navigatorSo.FindProperty("_detailRoot").objectReferenceValue = detailRoot.gameObject;
        navigatorSo.FindProperty("_equipmentButton").objectReferenceValue = homeButtons.Equipment;
        navigatorSo.FindProperty("_inventoryButton").objectReferenceValue = homeButtons.Inventory;
        navigatorSo.FindProperty("_appearanceButton").objectReferenceValue = homeButtons.Appearance;
        navigatorSo.FindProperty("_mapButton").objectReferenceValue = homeButtons.Map;
        navigatorSo.FindProperty("_equipmentBackButton").objectReferenceValue = equipmentBack;
        navigatorSo.FindProperty("_cameraDirector").objectReferenceValue = cameraDirector;
        navigatorSo.FindProperty("_forcedPresentationScreenLeftOffset").floatValue = isTownOverlay ? 0f : 1.65f;
        navigatorSo.FindProperty("_appearancePresentationScreenLeftOffset").floatValue = isTownOverlay ? 2.35f : 1.65f;

        var homeButtonsProperty = navigatorSo.FindProperty("_homeButtons");
        homeButtonsProperty.arraySize = 3;
        homeButtonsProperty.GetArrayElementAtIndex(0).objectReferenceValue = inventoryBack;
        homeButtonsProperty.GetArrayElementAtIndex(1).objectReferenceValue = appearanceBack;
        homeButtonsProperty.GetArrayElementAtIndex(2).objectReferenceValue = mapBack;
        navigatorSo.ApplyModifiedPropertiesWithoutUndo();

        return new BuiltHomeOverlay
        {
            Navigator = navigator,
            InventoryUi = inventoryUi
        };
    }

    private static void EnsureTownHomeOverlay(Scene scene)
    {
        if (!scene.IsValid())
            throw new InvalidOperationException("Active scene is invalid.");

        DestroySceneObject(scene, TownHomeControllerName);
        DestroySceneObject(scene, TownHomeOverlayCanvasName);
        EnsureEventSystemIfMissing();

        var camera = EnsureMainCamera();
        var controllerGo = new GameObject(TownHomeControllerName);
        var cameraDirector = controllerGo.AddComponent<HomeSceneCameraDirector>();
        ConfigureCameraDirector(cameraDirector, camera);
        cameraDirector.enabled = false;

        var directorSo = new SerializedObject(cameraDirector);
        directorSo.FindProperty("_modelRoot").objectReferenceValue = null;
        directorSo.FindProperty("_blendSpeed").floatValue = 2.7f;
        directorSo.FindProperty("_modelScreenLeftOffset").floatValue = 0f;
        directorSo.FindProperty("_appearanceDistance").floatValue = 5.2f;
        directorSo.FindProperty("_appearanceHeightOffset").floatValue = 0.48f;
        directorSo.FindProperty("_presentationCameraHeightOffset").floatValue = 0.65f;
        directorSo.FindProperty("_useModelFacingForPresentation").boolValue = true;
        directorSo.FindProperty("_invertModelFacingForPresentation").boolValue = true;
        directorSo.FindProperty("_useCurrentCameraOppositeForPresentation").boolValue = true;
        directorSo.FindProperty("_useStagedPresentationEnter").boolValue = true;
        directorSo.FindProperty("_entryApproachDuration").floatValue = 0.32f;
        directorSo.FindProperty("_entryRotateDuration").floatValue = 0.78f;
        directorSo.FindProperty("_entryApproachDistanceMultiplier").floatValue = 0.78f;
        directorSo.ApplyModifiedPropertiesWithoutUndo();

        var canvas = CreateOverlayCanvas(TownHomeOverlayCanvasName, TownHomeOverlaySortingOrder);
        var uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            canvas.gameObject.layer = uiLayer;

        var built = BuildHomeOverlay(canvas.transform, cameraDirector, true);
        var controller = controllerGo.AddComponent<TownHomeUiController>();
        var controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("_root").objectReferenceValue = canvas.gameObject;
        controllerSo.FindProperty("_navigator").objectReferenceValue = built.Navigator;
        controllerSo.FindProperty("_cameraDirector").objectReferenceValue = cameraDirector;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        canvas.gameObject.SetActive(false);

        EditorUtility.SetDirty(controllerGo);
        EditorUtility.SetDirty(canvas.gameObject);
        if (built.InventoryUi != null)
            EditorUtility.SetDirty(built.InventoryUi);
    }

    private static void VerifyTownHomeOverlay(Scene scene)
    {
        Require(scene.IsValid(), "Town scene is invalid.");

        var controllerGo = RequireSceneObject(TownHomeControllerName);
        Require(controllerGo.scene == scene, $"{TownHomeControllerName} must live in {scene.name}.");
        Require(controllerGo.activeSelf, $"{TownHomeControllerName} must stay active so the I hotkey can open the overlay.");
        Require(controllerGo.GetComponent<TownHomeUiController>() != null, $"{TownHomeControllerName} is missing TownHomeUiController.");

        var cameraDirector = controllerGo.GetComponent<HomeSceneCameraDirector>();
        Require(cameraDirector != null, $"{TownHomeControllerName} is missing HomeSceneCameraDirector.");
        Require(!cameraDirector.enabled, "Town Home camera director must be disabled by default so Town entry camera stays unchanged.");
        var directorSo = new SerializedObject(cameraDirector);
        Require(Mathf.Abs(directorSo.FindProperty("_blendSpeed").floatValue - 2.7f) <= 0.01f, "Town Home camera blend speed must stay smooth.");
        Require(Mathf.Abs(directorSo.FindProperty("_modelScreenLeftOffset").floatValue) <= 0.01f, "Town Home camera must keep the character near center.");
        Require(Mathf.Abs(directorSo.FindProperty("_appearanceHeightOffset").floatValue - 0.48f) <= 0.01f, "Town Home camera must keep the character vertically centered.");
        Require(directorSo.FindProperty("_invertModelFacingForPresentation").boolValue, "Town Home camera fallback must face the character front.");
        Require(directorSo.FindProperty("_useCurrentCameraOppositeForPresentation").boolValue, "Town Home camera must move to the front from the current Town camera pose.");
        Require(directorSo.FindProperty("_useStagedPresentationEnter").boolValue, "Town Home camera must approach the character before rotating.");
        Require(Mathf.Abs(directorSo.FindProperty("_entryApproachDuration").floatValue - 0.32f) <= 0.01f, "Town Home camera approach duration must stay readable.");
        Require(Mathf.Abs(directorSo.FindProperty("_entryRotateDuration").floatValue - 0.78f) <= 0.01f, "Town Home camera rotation duration must stay comfortable.");
        Require(Mathf.Abs(directorSo.FindProperty("_entryApproachDistanceMultiplier").floatValue - 0.78f) <= 0.01f, "Town Home camera approach distance must keep the character close before rotation.");

        var overlayGo = RequireSceneObject(TownHomeOverlayCanvasName);
        Require(overlayGo.scene == scene, $"{TownHomeOverlayCanvasName} must live in {scene.name}.");
        Require(overlayGo.transform.parent == null, $"{TownHomeOverlayCanvasName} must be a scene-root canvas to keep its UI layout stable.");
        Require(!overlayGo.activeSelf, $"{TownHomeOverlayCanvasName} must be inactive until I opens the Home overlay.");

        var canvas = overlayGo.GetComponent<Canvas>();
        Require(canvas != null, $"{TownHomeOverlayCanvasName} is missing Canvas.");
        Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay, $"{TownHomeOverlayCanvasName} must be Screen Space Overlay.");
        Require(canvas.sortingOrder == TownHomeOverlaySortingOrder, $"{TownHomeOverlayCanvasName} sorting order must be {TownHomeOverlaySortingOrder}.");
        Require(overlayGo.GetComponent<GraphicRaycaster>() != null, $"{TownHomeOverlayCanvasName} is missing GraphicRaycaster.");
        var navigator = overlayGo.GetComponent<HomeUiPageNavigator>();
        Require(navigator != null, $"{TownHomeOverlayCanvasName} is missing HomeUiPageNavigator.");
        var navigatorSo = new SerializedObject(navigator);
        Require(Mathf.Abs(navigatorSo.FindProperty("_forcedPresentationScreenLeftOffset").floatValue) <= 0.01f, "Town Home page camera offset must keep the character near center.");
        Require(Mathf.Abs(navigatorSo.FindProperty("_appearancePresentationScreenLeftOffset").floatValue - 2.35f) <= 0.01f, "Town Appearance page camera offset must place the character on the left.");

        var homeRoot = RequireSceneObject("UI_Home_Interface");
        var equipmentRoot = RequireSceneObject("UI_Home_Equipment");
        var appearanceRoot = RequireSceneObject("UI_Home_Appearance");
        var mapRoot = RequireSceneObject("UI_Home_Map");
        var detailRoot = RequireSceneObject("UI_Home_Equipment_Detail");
        Require(homeRoot.transform.Find("OverlayDim") == null, "Town Home root must not show the Home dim panel.");
        Require(homeRoot.transform.Find("HomeReference/NamePanel") == null, "Town Home root must not show the Arden profile panel.");
        RequireComponent<HomeInventoryUI>("UI_Home_Equipment");
        var equipPopup = RequireComponent<HomeEquipPopupUI>("UI_Home_Equipment_Detail");
        RequireComponent<HomeAppearancePageUI>("UI_Home_Appearance");
        RequireComponent<HomeMapRealmUI>("UI_Home_Map");

        var detailDim = detailRoot.transform.Find("DimOverlay")?.GetComponent<Image>();
        Require(detailDim != null, "Town equipment detail screen must include DimOverlay for Home parity.");
        Require(detailDim.color.a <= 0.01f, "Town equipment detail DimOverlay must be invisible.");
        Require(!detailDim.raycastTarget, "Town invisible DimOverlay must not block equipment item clicks.");

        var itemPrefab = detailRoot.transform.Find("99_Prefabs/Prefab_PopupItem");
        var itemButton = itemPrefab != null ? itemPrefab.GetComponent<Button>() : null;
        Require(itemButton != null, "Town equipment popup item prefab must have a Button.");
        Require(itemButton.enabled && itemButton.interactable, "Town equipment popup item Button must be clickable.");
        Require(itemButton.targetGraphic != null && itemButton.targetGraphic.raycastTarget, "Town equipment popup item Button must have a raycast target.");

        var equipSlots = equipmentRoot.GetComponentsInChildren<HomeEquipSlotUI>(true);
        Require(equipSlots.Length >= 6, "Town Equipment page must expose all Home equipment slot buttons.");
        foreach (var slot in equipSlots)
        {
            var button = slot != null ? slot.GetComponent<Button>() : null;
            Require(button != null, $"{slot?.name ?? "Equipment slot"} is missing Button.");
            Require(button.enabled && button.interactable, $"{slot.name} Button must be clickable.");
            Require(button.targetGraphic != null && button.targetGraphic.raycastTarget, $"{slot.name} Button must have a raycast target.");
        }

        var wasOverlayActive = overlayGo.activeSelf;
        try
        {
            overlayGo.SetActive(true);
            navigator.ShowHome();
            Require(homeRoot.activeSelf, "Town Home overlay should show Home root.");
            navigator.ShowEquipment();
            Require(equipmentRoot.activeSelf, "Town Home overlay should show Equipment root.");
            equipPopup.Show(EquipmentSlot.Weapon);
            Require(detailRoot.activeSelf, "Town Home overlay should open equipment popup.");
            equipPopup.Hide();
            navigator.ShowAppearance();
            Require(appearanceRoot.activeSelf, "Town Home overlay should show Appearance root.");
            navigator.ShowMap();
            Require(mapRoot.activeSelf, "Town Home overlay should show Map root.");
            navigator.ShowHome();
        }
        finally
        {
            if (equipPopup != null)
                equipPopup.Hide();
            overlayGo.SetActive(wasOverlayActive);
        }

        var mainCamera = Camera.main;
        Require(mainCamera != null, $"{scene.name} is missing a Main Camera.");
        var cameraFollow = mainCamera.GetComponent<CameraFollow>();
        Require(cameraFollow != null, $"{scene.name} Main Camera is missing CameraFollow.");
        Require(cameraFollow.enabled, $"{scene.name} CameraFollow must be enabled by default.");
    }

    private static void DestroySceneObject(Scene scene, string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in objects)
        {
            if (go == null || go.scene != scene || go.name != objectName)
                continue;

            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static RectTransform CreatePageRoot(Transform parent, string name, bool active)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        Stretch(rect);
        go.SetActive(active);
        return rect;
    }

    private static HomeMenuButtons BuildHomeInterface(RectTransform root, bool isTownOverlay = false)
    {
        if (!isTownOverlay)
            CreateSolid(root, "OverlayDim", new Color(0f, 0f, 0f, 0.78f));

        var referenceSize = new Vector2(1093f, 820f);
        var reference = CreateDesignRoot(root, "HomeReference", referenceSize);

        if (!isTownOverlay)
        {
            CreateTexture(reference, "NamePanel", "UI_Home_Interface/UI_Panel_NameSpace.png", new Rect(18f, 122f, 310f, 86f), referenceSize);
            CreateTexture(reference, "NameDecoration", "UI_Home_Interface/UI_Decoration_NameSpace.png", new Rect(32f, 130f, 76f, 68f), referenceSize);
            CreateText(reference, "ProfileName", "Arden", new Rect(120f, 138f, 170f, 24f), 23f, TextAlignmentOptions.MidlineLeft, ParchmentText, referenceSize);
            CreateText(reference, "ProfileLevel", "Lv. 24", new Rect(120f, 170f, 86f, 17f), 14f, TextAlignmentOptions.MidlineLeft, ParchmentMutedText, referenceSize);
            CreateText(reference, "ProfileExp", "2,480 / 4,500 XP", new Rect(214f, 170f, 100f, 17f), 12f, TextAlignmentOptions.MidlineRight, ParchmentMutedText, referenceSize);
            var expFill = CreateSolid(reference, "ProfileExpFill", new Color(0f, 0.58f, 0.60f, 0.82f));
            SetRectFromTopLeft(expFill.rectTransform, new Rect(120f, 190f, 120f, 5f), referenceSize);
        }

        var equipment = CreateHomeMenuCard(reference, "Button_Equipment", "UI_Home_Interface/UI_Decoration_Equipment.png", "EQUIPMENT", "Equip weapons, armor, and\naccessories.", "Manage Equipment", new Rect(88f, 242f, 300f, 176f), referenceSize);
        var inventory = CreateHomeMenuCard(reference, "Button_Inventory", "UI_Home_Interface/UI_Decoration_Inventory.png", "INVENTORY", "View items, materials,\nand useful goods.", "Open Inventory", new Rect(88f, 464f, 300f, 176f), referenceSize);
        var appearance = CreateHomeMenuCard(reference, "Button_Appearance", "UI_Home_Interface/UI_Decoration_Appear.png", "APPEARANCE", "Customize your look\nand outfit.", "Change Appearance", new Rect(750f, 242f, 300f, 176f), referenceSize);
        var map = CreateHomeMenuCard(reference, "Button_Map", "UI_Home_Interface/UI_Decoration_Map.png", "MAP", "View the world map and\nplan your route to Town.", "Open World Map", new Rect(750f, 464f, 300f, 176f), referenceSize);

        return new HomeMenuButtons
        {
            Equipment = equipment,
            Inventory = inventory,
            Appearance = appearance,
            Map = map
        };
    }

    private static Button BuildEquipmentScreen(RectTransform root, RectTransform detailRoot, out HomeInventoryUI inventoryUi, bool isTownOverlay = false)
    {
        var referenceSize = new Vector2(1672f, 941f);
        if (!isTownOverlay)
            CreateSolid(root, "OverlayDim", new Color(0f, 0f, 0f, 0.58f));

        var reference = CreateDesignRoot(root, "EquipmentReference", referenceSize);
        var back = CreateButtonTexture(reference, "Button_Back", "UI_Home_Equipment/UI_Button_BackfromEquipment.png", new Rect(0f, 18f, 500f, 78f), referenceSize);
        CreateText(reference, "Title", "EQUIPMENT", new Rect(162f, 36f, 280f, 42f), 38f, TextAlignmentOptions.MidlineLeft, new Color(0.98f, 0.88f, 0.62f, 1f), referenceSize);

        var slots = new List<HomeEquipSlotUI>
        {
            CreateEquipSlot(reference, "Slot_Weapon", EquipmentSlot.Weapon, "WEAPON", "UI_Home_Equipment/UI_Decoration_Weapon.png", new Rect(76f, 126f, 380f, 176f), referenceSize),
            CreateEquipSlot(reference, "Slot_Accessory", EquipmentSlot.Accessory, "ACCESSORY", "UI_Home_Equipment/UI_Decoration_Line.png", new Rect(76f, 320f, 380f, 172f), referenceSize),
            CreateEquipSlot(reference, "Slot_Pants", EquipmentSlot.Pants, "SKILL GEAR", "UI_Home_Equipment/UI_Decoration_Shose.png", new Rect(76f, 511f, 380f, 172f), referenceSize),
            CreateEquipSlot(reference, "Slot_Head", EquipmentSlot.Head, "HELMET", "UI_Home_Equipment/UI_Decoration_Head.png", new Rect(1220f, 122f, 360f, 172f), referenceSize),
            CreateEquipSlot(reference, "Slot_Armor", EquipmentSlot.Armor, "ARMOR", "UI_Home_Equipment/UI_Decoration_Armor.png", new Rect(1220f, 314f, 360f, 172f), referenceSize),
            CreateEquipSlot(reference, "Slot_Shoes", EquipmentSlot.Shoes, "BOOTS", "UI_Home_Equipment/UI_Decoration_Shose.png", new Rect(1220f, 506f, 360f, 172f), referenceSize)
        };

        CreateTexture(reference, "SummaryPanel", "UI_Home_Equipment/UI_Panel_StateDeatil.png", new Rect(52f, 720f, 475f, 120f), referenceSize);
        CreateText(reference, "SummaryTitle", "EQUIPMENT SUMMARY", new Rect(185f, 740f, 230f, 28f), 20f, TextAlignmentOptions.MidlineLeft, ParchmentText, referenceSize);
        CreateText(reference, "SummaryValues", "Gear Score  2,485     ATK 368   DEF 284", new Rect(185f, 782f, 280f, 24f), 16f, TextAlignmentOptions.MidlineLeft, ParchmentMutedText, referenceSize);

        CreateTexture(reference, "SelectedItemPanel", "UI_Home_Equipment/UI_Panle_CurrentSelected_Equipment.png", new Rect(590f, 770f, 500f, 118f), referenceSize);
        CreateText(reference, "SelectedItemText", "SELECTED: TAP A SLOT", new Rect(730f, 806f, 260f, 28f), 20f, TextAlignmentOptions.Center, ParchmentText, referenceSize);
        CreateTexture(reference, "Button_AutoEquip", "UI_Home_Equipment/UI_Button.png", new Rect(1240f, 708f, 320f, 70f), referenceSize);
        CreateText(reference, "AutoEquipText", "AUTO EQUIP", new Rect(1280f, 726f, 230f, 28f), 21f, TextAlignmentOptions.Center, ButtonLightText, referenceSize);
        CreateTexture(reference, "Button_ManageLoadout", "UI_Home_Equipment/UI_Button.png", new Rect(1240f, 800f, 320f, 70f), referenceSize);
        CreateText(reference, "ManageLoadoutText", "MANAGE LOADOUT", new Rect(1262f, 818f, 270f, 28f), 21f, TextAlignmentOptions.Center, ButtonLightText, referenceSize);

        inventoryUi = root.gameObject.AddComponent<HomeInventoryUI>();
        var popup = detailRoot.GetComponent<HomeEquipPopupUI>() ?? detailRoot.gameObject.AddComponent<HomeEquipPopupUI>();
        var so = new SerializedObject(inventoryUi);
        var slotsProperty = so.FindProperty("_slots");
        slotsProperty.arraySize = slots.Count;
        for (var i = 0; i < slots.Count; i++)
            slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        so.FindProperty("_popup").objectReferenceValue = popup;
        so.FindProperty("_enableAppearanceSelector").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        return back;
    }

    private static void BuildDetailScreen(RectTransform root, bool isTownOverlay = false)
    {
        var dim = CreateSolid(root, "DimOverlay", isTownOverlay ? new Color(0f, 0f, 0f, 0f) : new Color(0f, 0f, 0f, 0.58f));
        dim.raycastTarget = !isTownOverlay;

        var content = CreateRect(root, "Content");
        Stretch(content);

        var chrome = CreateRect(content, "ResourceChrome");
        Stretch(chrome);
        CreateTexture(chrome, "OwnedItemsFrame", "UI_Home_Equipment_Detail/UI_Panle_equipment_list.png", new Rect(12f, 42f, 386f, 640f));
        CreateTexture(chrome, "DetailInfoFrame", "UI_Home_Equipment_Detail/UI_Panel_equipment_detail.png", new Rect(895f, 78f, 356f, 568f));
        CreateTexture(chrome, "CategoryButtonA", "UI_Home_Equipment_Detail/UI_Button_Category.png", new Rect(38f, 626f, 150f, 40f));
        CreateTexture(chrome, "CategoryButtonB", "UI_Home_Equipment_Detail/UI_Button_Category.png", new Rect(202f, 626f, 150f, 40f));
        CreateTexture(chrome, "SwitchButtonFrame", "UI_Home_Equipment_Detail/UI_Button_equipment_switch.png", new Rect(900f, 646f, 168f, 56f));
        CreateTexture(chrome, "EnhanceButtonFrame", "UI_Home_Equipment_Detail/UI_Button_Equipment_enhance.png", new Rect(1084f, 646f, 168f, 56f));
        var close = CreateTransparentButton(root, "CloseBtn", new Rect(1120f, 14f, 148f, 48f));
        CreateText(root, "Title", "MELEE WEAPONS", new Rect(120f, 36f, 260f, 34f), 24f, TextAlignmentOptions.MidlineLeft, ParchmentText);

        var prefabGroup = CreateRect(root, "99_Prefabs");
        Stretch(prefabGroup);
        var itemPrefab = CreatePopupItemPrefab(prefabGroup);

        var popup = root.GetComponent<HomeEquipPopupUI>() ?? root.gameObject.AddComponent<HomeEquipPopupUI>();
        var so = new SerializedObject(popup);
        so.FindProperty("_content").objectReferenceValue = content.transform;
        so.FindProperty("_itemPrefab").objectReferenceValue = itemPrefab;
        so.FindProperty("_title").objectReferenceValue = RequireTmp(root, "Title");
        so.FindProperty("_closeBtn").objectReferenceValue = close;
        so.FindProperty("_useHomeDetailResourceLayout").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        popup.Show(EquipmentSlot.Weapon);
        popup.Hide();
    }

    private static Button BuildInventoryScreen(RectTransform root, bool isTownOverlay = false)
    {
        if (!isTownOverlay)
            CreateSolid(root, "SceneDim", new Color(0f, 0f, 0f, 0.24f));

        var townInventory = CreateHomeTownInventory(root);
        if (townInventory == null)
            BuildInventoryFallback(root);

        var back = CreateBackButton(root, "Button_Back_Inventory");
        CreateText(root, "Title", "INVENTORY", new Rect(148f, 36f, 260f, 34f), 28f, TextAlignmentOptions.MidlineLeft, GoldText);
        back.transform.SetAsLastSibling();

        return back;
    }

    private static GameObject CreateHomeTownInventory(RectTransform root)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TownInventoryPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[Home1SceneSpaceUiBuilder] Town inventory prefab not found: {TownInventoryPrefabPath}");
            return null;
        }

        var instance = PrefabUtility.InstantiatePrefab(prefab, root) as GameObject;
        if (instance == null)
            instance = UnityEngine.Object.Instantiate(prefab, root);

        instance.name = "TownInventory_UI";

        var rect = instance.GetComponent<RectTransform>();
        if (rect == null)
            rect = instance.AddComponent<RectTransform>();
        Stretch(rect);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        ConfigureHomeTownInventoryInstance(instance);
        ApplyNanumGothicFontToChildren(rect);
        return instance;
    }

    private static void ConfigureHomeTownInventoryInstance(GameObject instance)
    {
        if (instance == null)
            return;

        var raycaster = instance.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            UnityEngine.Object.DestroyImmediate(raycaster);

        var scaler = instance.GetComponent<CanvasScaler>();
        if (scaler != null)
            UnityEngine.Object.DestroyImmediate(scaler);

        var canvas = instance.GetComponent<Canvas>();
        if (canvas != null)
            UnityEngine.Object.DestroyImmediate(canvas);

        var panel = instance.transform.Find("Panel") as RectTransform;
        if (panel != null)
        {
            var fitScale = GetTownInventoryPanelFitScale();
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(1550f, 945f);
            panel.localScale = new Vector3(fitScale, fitScale, 1f);
            panel.gameObject.SetActive(true);
        }

        var inventory = instance.GetComponent<TownInventoryUI>();
        if (inventory != null)
        {
            var so = new SerializedObject(inventory);
            var hotkey = so.FindProperty("_handleHotkey");
            if (hotkey != null)
                hotkey.boolValue = false;

            var openOnEnable = so.FindProperty("_openOnEnable");
            if (openOnEnable != null)
                openOnEnable.boolValue = true;

            var logRefresh = so.FindProperty("_logRefresh");
            if (logRefresh != null)
                logRefresh.boolValue = false;

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static float GetTownInventoryPanelFitScale()
    {
        const float panelWidth = 1550f;
        const float panelHeight = 945f;
        const float horizontalMargin = 80f;
        const float verticalMargin = 64f;

        var availableWidth = Mathf.Max(1f, LayoutSize.x - horizontalMargin);
        var availableHeight = Mathf.Max(1f, LayoutSize.y - verticalMargin);
        return Mathf.Min(1f, Mathf.Min(availableWidth / panelWidth, availableHeight / panelHeight));
    }

    private static void BuildInventoryFallback(RectTransform root)
    {
        CreateTexture(root, "InventoryPanel", "UI_Home_Interface/UI_Panel.png", new Rect(116f, 116f, 1048f, 516f));
        var scroll = CreateScrollView(root, "InventoryScroll", new Rect(170f, 170f, 940f, 390f), new Vector2(900f, 820f));
        for (var i = 0; i < 24; i++)
        {
            var row = CreateSolid(scroll.Content, $"InventoryRow_{i:00}", i % 2 == 0 ? new Color(0.78f, 0.64f, 0.44f, 0.46f) : new Color(0.64f, 0.48f, 0.30f, 0.42f));
            var rowRect = row.rectTransform;
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, 54f);
            rowRect.anchoredPosition = new Vector2(0f, -i * 60f);
            CreateText(rowRect, "Name", $"ITEM SLOT {i + 1:00}", new Rect(18f, 12f, 220f, 22f), 15f, TextAlignmentOptions.MidlineLeft, ParchmentText);
            CreateText(rowRect, "State", "EMPTY", new Rect(760f, 12f, 110f, 22f), 13f, TextAlignmentOptions.MidlineRight, ParchmentMutedText);
        }
    }

    private static Button BuildAppearanceScreen(RectTransform root, bool isTownOverlay = false)
    {
        if (!isTownOverlay)
            CreateSolid(root, "OverlayDim", new Color(0f, 0f, 0f, 0.18f));

        CreateTexture(root, "AppearancePanel", "UI_Appear/UI_Panel.png", new Rect(622f, 30f, 548f, 670f));
        CreateTexture(root, "AppearanceTitleFrame", "UI_Appear/UI_Title_Text.png", new Rect(708f, 42f, 368f, 86f));
        CreateText(root, "Title", "외형 선택", new Rect(790f, 64f, 212f, 34f), 30f, TextAlignmentOptions.Center, GoldText);
        var back = CreateButtonTexture(root, "Button_Back_Appearance", "UI_Appear/UI_Button_Close.png", new Rect(1088f, 44f, 64f, 64f));

        CreateText(root, "CurrentText", "선택 외형: -", new Rect(692f, 588f, 236f, 24f), 16f, TextAlignmentOptions.MidlineLeft, ParchmentText);
        CreateText(root, "AppliedText", "적용 외형: -", new Rect(692f, 616f, 236f, 22f), 14f, TextAlignmentOptions.MidlineLeft, ParchmentMutedText);
        CreateText(root, "StatusText", "준비 중", new Rect(692f, 642f, 236f, 28f), 12f, TextAlignmentOptions.TopLeft, ParchmentMutedText);

        var scroll = CreateScrollView(root, "AppearanceScroll", new Rect(668f, 124f, 442f, 448f), new Vector2(442f, 610f));
        var optionBindings = new List<HomeAppearancePageUI.OptionBinding>();
        var options = AppearanceCatalog.Options;
        var count = Mathf.Min(options.Count, 8);
        for (var i = 0; i < count; i++)
        {
            var option = options[i];
            var col = i % 2;
            var row = i / 2;
            var rect = new Rect(4f + col * 220f, 2f + row * 154f, 214f, 144f);
            var button = CreateButtonTexture(scroll.Content, $"AppearanceOption_{option.Id}", "UI_Appear/UI_Panel_Character_Appear.png", rect, scroll.Content.sizeDelta);
            var highlight = CreateSolid(button.transform as RectTransform, "SelectedHighlight", new Color(1f, 1f, 1f, 0f));
            Stretch(highlight.rectTransform);
            var label = CreateText(button.transform as RectTransform, "Label", option.DisplayName, new Rect(20f, 96f, 150f, 24f), 16f, TextAlignmentOptions.Center, ParchmentText, new Vector2(214f, 144f));
            CreateTexture(button.transform, "HoldBadge", "UI_Appear/UI_Decoration_Appear_State_Hold.png", new Rect(150f, 8f, 54f, 26f), new Vector2(214f, 144f));
            var equipped = CreateTexture(button.transform, "EquippedMark", "UI_Appear/UI_Decoration_Appear_State_Equip.png", new Rect(150f, 8f, 54f, 26f), new Vector2(214f, 144f));
            equipped.gameObject.SetActive(false);

            optionBindings.Add(new HomeAppearancePageUI.OptionBinding
            {
                AppearanceId = option.Id,
                Button = button,
                Label = label,
                Highlight = highlight,
                EquippedMark = equipped.gameObject
            });
        }

        var apply = CreateButtonTexture(root, "Button_ApplyAppearance", "UI_Appear/UI_Button_Applay.png", new Rect(926f, 588f, 176f, 62f));
        CreateText(apply.transform as RectTransform, "Label", "적용", new Rect(32f, 15f, 112f, 28f), 24f, TextAlignmentOptions.Center, ButtonLightText, new Vector2(176f, 62f));

        var ui = root.gameObject.AddComponent<HomeAppearancePageUI>();
        var so = new SerializedObject(ui);
        var optionsProperty = so.FindProperty("_options");
        optionsProperty.arraySize = optionBindings.Count;
        for (var i = 0; i < optionBindings.Count; i++)
        {
            var element = optionsProperty.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("AppearanceId").intValue = optionBindings[i].AppearanceId;
            element.FindPropertyRelative("Button").objectReferenceValue = optionBindings[i].Button;
            element.FindPropertyRelative("Label").objectReferenceValue = optionBindings[i].Label;
            element.FindPropertyRelative("Highlight").objectReferenceValue = optionBindings[i].Highlight;
            element.FindPropertyRelative("EquippedMark").objectReferenceValue = optionBindings[i].EquippedMark;
        }
        so.FindProperty("_currentText").objectReferenceValue = RequireTmp(root, "CurrentText");
        so.FindProperty("_appliedText").objectReferenceValue = RequireTmp(root, "AppliedText");
        so.FindProperty("_statusText").objectReferenceValue = RequireTmp(root, "StatusText");
        so.FindProperty("_applyButton").objectReferenceValue = apply;
        so.ApplyModifiedPropertiesWithoutUndo();

        return back;
    }

    private static Button BuildMapScreen(RectTransform root, bool isTownOverlay = false)
    {
        if (!isTownOverlay)
            CreateSolid(root, "OverlayDim", new Color(0f, 0f, 0f, 0.22f));

        CreateTexture(root, "MapPaper", "UI_Map/UI_Panel_MapPaper.png", new Rect(0f, 0f, 1280f, 720f));
        CreateTexture(root, "MapFrame", "UI_Map/UI_Panel_MapFrame.png", new Rect(0f, 0f, 1280f, 720f));
        var back = CreateButtonTexture(root, "Button_Back_Map", "UI_Map/UI_Button_Back.png", new Rect(42f, 48f, 140f, 66f));
        CreateTexture(root, "MapTitleFrame", "UI_Map/UI_Title_Main.png", new Rect(424f, 18f, 430f, 104f));
        CreateText(root, "MapTitleText", "WORLD MAP", new Rect(504f, 38f, 270f, 42f), 36f, TextAlignmentOptions.Center, ParchmentText);
        var realms = new[]
        {
            CreateRealmButton(root, "Realm_Plains", "UI_Map/UI_Map_Location_Farm.png", new Rect(354f, 336f, 276f, 158f), "plains", "Golden Plains", "마을로 이동 가능한 중심 평야입니다. 현재 Town 입장 티켓 검증 흐름과 동일하게 동작합니다.", TownPassTicket, SceneNames.TownMap),
            CreateRealmButton(root, "Realm_Forest", "UI_Map/UI_Map_Location_Forest.png", new Rect(142f, 150f, 320f, 236f), "forest", "Whispering Forest", "울창한 숲과 작은 마을이 있는 초반 영역입니다. 리듬 왜곡이 가장 약해 입장 준비에 적합합니다.", TownPassTicket, SceneNames.Town_Forest),
            CreateRealmButton(root, "Realm_Snow", "UI_Map/UI_Map_Location_SnowMountain.png", new Rect(424f, 150f, 222f, 160f), "snow", "Frostpeak Mountains", "얼어붙은 박자와 지연 입력이 섞이는 설산 영역입니다. 방어 장비 확인을 권장합니다.", MissingMapTicket, string.Empty),
            CreateRealmButton(root, "Realm_Ruins", "UI_Map/UI_Map_Location_Ruins.png", new Rect(626f, 176f, 220f, 164f), "ruins", "Ancient Ruins", "무너진 유적과 잔향 패턴이 겹치는 고대 영역입니다. 복합 리듬 전투가 등장합니다.", MissingMapTicket, string.Empty),
            CreateRealmButton(root, "Realm_Lake", "UI_Map/UI_Map_Location_Seashore.png", new Rect(96f, 326f, 340f, 248f), "lake", "Sapphire Lake", "파도 리듬과 반향 전투가 만나는 수상 영역입니다. 긴 패턴을 안정적으로 처리해야 합니다.", MissingMapTicket, string.Empty),
            CreateRealmButton(root, "Realm_Desert", "UI_Map/UI_Map_Location_Descert.png", new Rect(314f, 506f, 258f, 160f), "desert", "Sandworn Wastes", "모래 위로 느린 박동이 흐르는 고열 영역입니다. 회피 타이밍이 느리게 흔들립니다.", MissingMapTicket, string.Empty),
            CreateRealmButton(root, "Realm_Volcano", "UI_Map/UI_Map_Location_Volcano.png", new Rect(642f, 400f, 246f, 182f), "volcano", "Ember Volcano", "과열된 박동이 빠르게 증폭되는 화산 영역입니다. 짧은 입력 판단이 중요합니다.", MissingMapTicket, string.Empty)
        };
        CreateTexture(root, "DetailPanel", "UI_Map/UI_Panel_MapLocation_Detail.png", new Rect(900f, 120f, 314f, 488f));
        CreateText(root, "RealmTitle", "GOLDEN PLAINS", new Rect(932f, 192f, 252f, 36f), 27f, TextAlignmentOptions.Center, ParchmentText);
        CreateText(root, "RealmTicket", "Ticket: -", new Rect(934f, 238f, 248f, 22f), 14f, TextAlignmentOptions.Center, ParchmentMutedText);

        var detailScroll = CreateScrollView(root, "RealmDetailScroll", new Rect(932f, 282f, 250f, 108f), new Vector2(230f, 150f));
        var detail = CreateText(detailScroll.Content, "RealmDescription", "숲의 균열과 리듬 왜곡이 시작된 첫 영역입니다.", new Rect(0f, 0f, 230f, 132f), 14f, TextAlignmentOptions.TopLeft, ParchmentText, detailScroll.Content.sizeDelta);
        detail.textWrappingMode = TextWrappingModes.Normal;
        detail.lineSpacing = -4f;

        var select = CreateButtonTexture(root, "Button_SelectRealm", "UI_Map/UI_Button_SelectLocation.png", new Rect(958f, 518f, 214f, 68f));
        CreateText(select.transform as RectTransform, "Label", "TRAVEL HERE", new Rect(30f, 18f, 152f, 26f), 20f, TextAlignmentOptions.Center, ButtonLightText, new Vector2(214f, 68f));
        CreateText(root, "MapStatus", "지역을 선택하세요.", new Rect(916f, 432f, 282f, 40f), 13f, TextAlignmentOptions.Center, ParchmentMutedText);

        var ui = root.gameObject.AddComponent<HomeMapRealmUI>();
        var so = new SerializedObject(ui);
        var realmsProperty = so.FindProperty("_realms");
        realmsProperty.arraySize = realms.Length;
        for (var i = 0; i < realms.Length; i++)
        {
            var element = realmsProperty.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("RealmId").stringValue = realms[i].RealmId;
            element.FindPropertyRelative("DisplayName").stringValue = realms[i].DisplayName;
            element.FindPropertyRelative("Description").stringValue = realms[i].Description;
            element.FindPropertyRelative("RequiredTicket").stringValue = realms[i].RequiredTicket;
            element.FindPropertyRelative("SceneName").stringValue = realms[i].SceneName;
            element.FindPropertyRelative("Button").objectReferenceValue = realms[i].Button;
            element.FindPropertyRelative("Highlight").objectReferenceValue = realms[i].Highlight;
        }
        so.FindProperty("_title").objectReferenceValue = RequireTmp(root, "RealmTitle");
        so.FindProperty("_description").objectReferenceValue = detail;
        so.FindProperty("_ticketInfo").objectReferenceValue = RequireTmp(root, "RealmTicket");
        so.FindProperty("_selectButton").objectReferenceValue = select;
        so.FindProperty("_status").objectReferenceValue = RequireTmp(root, "MapStatus");
        so.ApplyModifiedPropertiesWithoutUndo();

        BuildTownEntryChoicePanel(root, ui);
        ApplyNanumGothicFontToChildren(root);

        return back;
    }

    private static void ApplyMapRealmRoutes(HomeMapRealmUI mapUi)
    {
        var so = new SerializedObject(mapUi);
        var realmsProperty = so.FindProperty("_realms");
        if (realmsProperty == null || !realmsProperty.isArray)
            throw new InvalidOperationException("HomeMapRealmUI._realms is missing or not an array.");

        for (var i = 0; i < realmsProperty.arraySize; i++)
        {
            var element = realmsProperty.GetArrayElementAtIndex(i);
            var realmId = element.FindPropertyRelative("RealmId").stringValue;
            var sceneName = ResolveHomeMapSceneName(realmId);

            element.FindPropertyRelative("SceneName").stringValue = sceneName;
            element.FindPropertyRelative("RequiredTicket").stringValue =
                string.IsNullOrEmpty(sceneName) ? MissingMapTicket : TownPassTicket;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyMapPieceHighlights(HomeMapRealmUI mapUi)
    {
        var so = new SerializedObject(mapUi);
        var realmsProperty = so.FindProperty("_realms");
        if (realmsProperty == null || !realmsProperty.isArray)
            throw new InvalidOperationException("HomeMapRealmUI._realms is missing or not an array.");

        for (var i = 0; i < realmsProperty.arraySize; i++)
        {
            var element = realmsProperty.GetArrayElementAtIndex(i);
            var button = element.FindPropertyRelative("Button").objectReferenceValue as Button;
            if (button == null)
                continue;

            var buttonTexture = ResolveMapButtonTexture(button);
            EnsureMapPieceTextureReadable(buttonTexture);

            var sprite = ResolveMapButtonSprite(button);
            if (sprite == null)
            {
                Debug.LogWarning($"[Home1SceneSpaceUiBuilder] Map button '{button.name}' has no sprite-backed texture for shaped highlight.");
                continue;
            }

            ConfigureMapPieceHitArea(button);

            var highlightProperty = element.FindPropertyRelative("Highlight");
            var highlight = highlightProperty.objectReferenceValue as Image;
            if (highlight == null)
                highlight = FindOrCreateMapPieceHighlight(button.transform as RectTransform);

            ConfigureMapPieceHighlight(highlight, sprite);
            highlightProperty.objectReferenceValue = highlight;
            EditorUtility.SetDirty(highlight);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool IsMapPieceTexturePath(string relativePath)
    {
        return relativePath != null
               && relativePath.StartsWith("UI_Map/UI_Map_Location_", StringComparison.OrdinalIgnoreCase)
               && relativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureMapPieceTextureReadable(Texture2D texture)
    {
        if (texture == null)
            return;

        var path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path))
            return;

        var relativePath = path.StartsWith($"{UiResourceTarget}/", StringComparison.OrdinalIgnoreCase)
            ? path.Substring(UiResourceTarget.Length + 1)
            : path;
        if (!IsMapPieceTexturePath(relativePath))
            return;

        if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.isReadable)
            return;

        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    private static void ConfigureMapPieceHitArea(Button button)
    {
        if (button == null)
            return;

        var hitArea = button.GetComponent<HomeMapPieceHitArea>() ?? button.gameObject.AddComponent<HomeMapPieceHitArea>();
        hitArea.Configure();
        EditorUtility.SetDirty(hitArea);
    }

    private static Image FindOrCreateMapPieceHighlight(RectTransform buttonRect)
    {
        if (buttonRect == null)
            throw new InvalidOperationException("Map realm button RectTransform is missing.");

        var existing = buttonRect.Find("SelectedHighlight");
        if (existing != null && existing.TryGetComponent<Image>(out var image))
            return image;

        var go = new GameObject("SelectedHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HomeMapPieceHighlight));
        go.transform.SetParent(buttonRect, false);
        Stretch(go.GetComponent<RectTransform>());
        return go.GetComponent<Image>();
    }

    private static Sprite ResolveMapButtonSprite(Button button)
    {
        if (button == null)
            return null;

        if (button.targetGraphic is Image targetImage && targetImage.sprite != null)
            return targetImage.sprite;

        var texture = ResolveMapButtonTexture(button);
        if (texture == null)
            return null;

        var path = AssetDatabase.GetAssetPath(texture);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Texture2D ResolveMapButtonTexture(Button button)
    {
        if (button == null)
            return null;

        if (button.targetGraphic is RawImage targetRawImage && targetRawImage.texture is Texture2D targetTexture)
            return targetTexture;

        var rawImage = button.GetComponent<RawImage>();
        return rawImage != null ? rawImage.texture as Texture2D : null;
    }

    private static void ConfigureMapPieceHighlight(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
            return;

        Stretch(image.rectTransform);
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.useSpriteMesh = false;
        image.raycastTarget = false;
        image.color = new Color(1f, 0.78f, 0.18f, 0f);
        image.enabled = false;

        var highlight = image.GetComponent<HomeMapPieceHighlight>() ?? image.gameObject.AddComponent<HomeMapPieceHighlight>();
        highlight.Configure(sprite);
        highlight.SetSelected(false);
        EditorUtility.SetDirty(highlight);
    }

    private static HomeMapRealmUI FindMapRealmUiInScene(string scenePath)
    {
        foreach (var candidate in Resources.FindObjectsOfTypeAll<HomeMapRealmUI>())
        {
            if (candidate == null || candidate.gameObject == null)
                continue;

            if (candidate.gameObject.scene.path == scenePath)
                return candidate;
        }

        return null;
    }

    private static string ResolveHomeMapSceneName(string realmId)
    {
        if (string.Equals(realmId, "plains", StringComparison.OrdinalIgnoreCase))
            return SceneNames.TownMap;

        if (string.Equals(realmId, "forest", StringComparison.OrdinalIgnoreCase))
            return SceneNames.Town_Forest;

        return string.Empty;
    }

    private static void BuildTownEntryChoicePanel(RectTransform mapRoot, HomeMapRealmUI mapUi)
    {
        if (mapRoot == null)
            throw new InvalidOperationException("UI_Home_Map RectTransform is missing.");
        if (mapUi == null)
            throw new InvalidOperationException("HomeMapRealmUI is missing.");

        var existing = mapRoot.Find("TownEntryChoicePanel");
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        var panelSize = new Vector2(620f, 548f);
        var panelImage = CreateTownTexture(mapRoot, "TownEntryChoicePanel", "Panel_ModalTitle.png", new Rect(272f, 98f, panelSize.x, panelSize.y), LayoutSize);
        panelImage.raycastTarget = true;

        var panelRect = panelImage.rectTransform;
        var title = CreateText(panelRect, "TownEntryTitle", "Whispering Forest Town", new Rect(72f, 19f, 466f, 34f), 27f, TextAlignmentOptions.Center, new Color(1f, 0.91f, 0.66f, 1f), panelSize);
        var closeButton = CreateTownChoiceButton(panelRect, "Button_CloseTownChoice", "", new Rect(568f, 17f, 34f, 34f), panelSize, "Button_Close.png");
        var status = CreateText(panelRect, "TownEntryStatus", "Choose how you want to enter a town.", new Rect(38f, 78f, 544f, 28f), 15f, TextAlignmentOptions.Center, new Color(0.20f, 0.18f, 0.13f, 1f), panelSize);
        CreateTownSeparator(panelRect, "TownEntryStatusDivider", new Rect(116f, 112f, 388f, 18f), panelSize);

        var choiceRoot = CreateRect(panelRect, "EntryChoiceRoot");
        SetRectFromTopLeft(choiceRoot, new Rect(26f, 126f, 568f, 388f), panelSize);
        var choiceSize = new Vector2(568f, 388f);
        var choiceExisting = CreateTownOptionButton(choiceRoot, "Button_ChoiceExistingTown", "Find Existing Town", "Browse towns created by other players", "FIND", new Rect(24f, 0f, 520f, 74f), choiceSize);
        var choiceCreate = CreateTownOptionButton(choiceRoot, "Button_ChoiceCreateTown", "Create My Town", "Set name, visibility, and player limit", "NEW", new Rect(24f, 88f, 520f, 74f), choiceSize);
        var choiceKey = CreateTownOptionButton(choiceRoot, "Button_ChoiceJoinKey", "Join with Key", "Enter an invite key for direct access", "KEY", new Rect(24f, 176f, 520f, 74f), choiceSize);
        CreateTownSeparator(choiceRoot, "QuickKeyDivider", new Rect(28f, 260f, 512f, 16f), choiceSize, "Quick Key Join");
        var quickKeyInput = CreateTownChoiceInput(choiceRoot, "Input_QuickTownKey", "Enter town key...", new Rect(26f, 288f, 390f, 42f), choiceSize);
        var quickJoinButton = CreateTownChoiceButton(choiceRoot, "Button_QuickKeyJoin", "Join", new Rect(436f, 288f, 104f, 42f), choiceSize);
        var choiceBack = CreateTownChoiceButton(choiceRoot, "Button_ChoiceBack", "Back", new Rect(22f, 346f, 220f, 42f), choiceSize);
        var choiceOpen = CreateTownChoiceButton(choiceRoot, "Button_ChoiceOpenSelected", "Open Selected", new Rect(318f, 346f, 226f, 42f), choiceSize);

        var existingRoot = CreateRect(panelRect, "ExistingTownsRoot");
        SetRectFromTopLeft(existingRoot, new Rect(24f, 126f, 572f, 388f), panelSize);
        var existingSize = new Vector2(572f, 388f);
        var existingStatus = CreateText(existingRoot, "ExistingStatus", "Browse available towns and join one.", new Rect(0f, 0f, 572f, 22f), 13f, TextAlignmentOptions.Center, ParchmentMutedText, existingSize);
        existingStatus.gameObject.SetActive(false);
        var searchInput = CreateTownChoiceInput(existingRoot, "Input_SearchTown", "Search Town", new Rect(28f, 26f, 318f, 34f), existingSize, "Search_Input.png");
        var listFrame = CreateTownTexture(existingRoot, "TownRoomListFrame", "Detail_Card.png", new Rect(20f, 62f, 334f, 254f), existingSize);
        listFrame.raycastTarget = false;
        var roomList = CreateRect(existingRoot, "TownRoomList");
        SetRectFromTopLeft(roomList, new Rect(28f, 70f, 318f, 238f), existingSize);
        var listSize = new Vector2(318f, 238f);
        var rows = new List<TownEntryRowBuildBinding>();
        for (var i = 0; i < 5; i++)
            rows.Add(CreateTownRoomRow(roomList, $"TownRoomRow_{i:00}", new Rect(6f, 6f + i * 44f, 306f, 40f), listSize));
        var emptyText = CreateText(roomList, "EmptyRoomText", "열려 있는 Town이 없습니다.", new Rect(0f, 88f, 318f, 44f), 16f, TextAlignmentOptions.Center, new Color(0.22f, 0.19f, 0.13f, 1f), listSize);
        emptyText.gameObject.SetActive(false);

        var detailBox = CreateTownTexture(existingRoot, "SelectedTownDetailPanel", "Detail_Card_Clean.png", new Rect(352f, 28f, 210f, 294f), existingSize);
        detailBox.raycastTarget = false;
        var selectedTownTitle = CreateText(existingRoot, "SelectedTownTitle", "Select a Town", new Rect(376f, 46f, 160f, 40f), 15.5f, TextAlignmentOptions.Center, ParchmentText, existingSize);
        var selectedTownHost = CreateText(existingRoot, "SelectedTownHost", "Host: -", new Rect(378f, 90f, 158f, 18f), 10.5f, TextAlignmentOptions.MidlineLeft, ParchmentMutedText, existingSize);
        var selectedTownPlayers = CreateText(existingRoot, "SelectedTownPlayers", "Players: -", new Rect(378f, 114f, 158f, 18f), 10.5f, TextAlignmentOptions.MidlineLeft, ParchmentMutedText, existingSize);
        CreateTownSeparator(existingRoot, "SelectedTownDivider", new Rect(386f, 174f, 148f, 12f), existingSize);
        var selectedTownDescription = CreateText(existingRoot, "SelectedTownDescription", "Browse available towns and choose one.", new Rect(376f, 194f, 160f, 54f), 9.5f, TextAlignmentOptions.TopLeft, ParchmentText, existingSize);
        var selectedTownKey = CreateText(existingRoot, "SelectedTownKey", "", new Rect(378f, 254f, 158f, 16f), 9f, TextAlignmentOptions.Center, ParchmentMutedText, existingSize);
        var joinSelectedButton = CreateTownChoiceButton(existingRoot, "Button_JoinSelectedTown", "Join Selected Town", new Rect(384f, 284f, 150f, 34f), existingSize);
        var existingBack = CreateTownChoiceButton(existingRoot, "Button_ExistingBack", "Back", new Rect(146f, 346f, 170f, 42f), existingSize);
        var refreshButton = CreateTownChoiceButton(existingRoot, "Button_FindTown", "Refresh", new Rect(336f, 346f, 170f, 42f), existingSize);
        existingRoot.gameObject.SetActive(false);

        var createRoot = CreateRect(panelRect, "CreateTownRoot");
        SetRectFromTopLeft(createRoot, new Rect(44f, 126f, 532f, 398f), panelSize);
        var createSize = new Vector2(532f, 398f);
        CreateTownSeparator(createRoot, "CreateHeaderDivider", new Rect(146f, 0f, 240f, 12f), createSize);
        CreateText(createRoot, "TownNameLabel", "Town Name", new Rect(26f, 24f, 160f, 24f), 15f, TextAlignmentOptions.MidlineLeft, ParchmentText, createSize);
        var townNameInput = CreateTownChoiceInput(createRoot, "Input_TownName", "Whispering Nest", new Rect(26f, 54f, 480f, 42f), createSize);
        CreateText(createRoot, "VisibilityLabel", "Visibility", new Rect(26f, 116f, 160f, 24f), 15f, TextAlignmentOptions.MidlineLeft, ParchmentText, createSize);
        var privateButton = CreateTownChoiceButton(createRoot, "Button_CreatePrivate", "Private", new Rect(26f, 148f, 234f, 42f), createSize, "Button_Parchment_Wide.png");
        var publicButton = CreateTownChoiceButton(createRoot, "Button_CreatePublic", "Public", new Rect(272f, 148f, 234f, 42f), createSize, "Button_Parchment_Wide.png");
        CreateText(createRoot, "MaxPlayersLabel", "Max Players", new Rect(26f, 210f, 160f, 24f), 15f, TextAlignmentOptions.MidlineLeft, ParchmentText, createSize);
        var maxButtons = new List<MaxPlayerBuildBinding>
        {
            CreateTownMaxPlayerButton(createRoot, "Button_MaxPlayers_2", 2, new Rect(26f, 242f, 108f, 38f), createSize),
            CreateTownMaxPlayerButton(createRoot, "Button_MaxPlayers_4", 4, new Rect(150f, 242f, 108f, 38f), createSize),
            CreateTownMaxPlayerButton(createRoot, "Button_MaxPlayers_6", 6, new Rect(274f, 242f, 108f, 38f), createSize),
            CreateTownMaxPlayerButton(createRoot, "Button_MaxPlayers_8", 8, new Rect(398f, 242f, 108f, 38f), createSize)
        };
        var visibilityHint = CreateText(createRoot, "VisibilityHint", "Public towns appear in the existing town list and can be joined by other players.", new Rect(52f, 298f, 428f, 34f), 13f, TextAlignmentOptions.Center, ParchmentMutedText, createSize);
        var createCancel = CreateTownChoiceButton(createRoot, "Button_CreateCancel", "Cancel", new Rect(26f, 356f, 210f, 42f), createSize);
        var createButton = CreateTownChoiceButton(createRoot, "Button_CreateTown", "Create Town", new Rect(296f, 356f, 210f, 42f), createSize);
        createRoot.gameObject.SetActive(false);

        var keyRoot = CreateRect(panelRect, "JoinWithKeyRoot");
        SetRectFromTopLeft(keyRoot, new Rect(44f, 126f, 532f, 388f), panelSize);
        var keySize = new Vector2(532f, 388f);
        CreateTownSeparator(keyRoot, "KeyHeaderDivider", new Rect(76f, 0f, 380f, 12f), keySize);
        CreateText(keyRoot, "TownKeyLabel", "Town Key", new Rect(28f, 26f, 180f, 24f), 16f, TextAlignmentOptions.MidlineLeft, ParchmentText, keySize);
        var inviteInput = CreateTownChoiceInput(keyRoot, "Input_InviteCode", "7F3K-G2M9", new Rect(28f, 58f, 360f, 46f), keySize);
        var findKeyButton = CreateTownChoiceButton(keyRoot, "Button_JoinInvite", "Find", new Rect(402f, 58f, 104f, 46f), keySize);
        var keyStatus = CreateText(keyRoot, "KeyStatus", "Private towns require a valid key.", new Rect(60f, 118f, 412f, 24f), 14f, TextAlignmentOptions.Center, ParchmentMutedText, keySize);
        var keyResult = CreateTownTexture(keyRoot, "KeyResultPanel", "Key_Result_Row_Clean.png", new Rect(28f, 154f, 478f, 112f), keySize);
        keyResult.raycastTarget = false;
        var keyResultSize = new Vector2(478f, 112f);
        var keyResultTitle = CreateText(keyResult.rectTransform, "KeyResultTitle", "Moonlit Pine Camp", new Rect(132f, 20f, 270f, 28f), 18f, TextAlignmentOptions.MidlineLeft, ParchmentText, keyResultSize);
        var keyResultHost = CreateText(keyResult.rectTransform, "KeyResultHost", "Host: -", new Rect(132f, 64f, 150f, 18f), 10.5f, TextAlignmentOptions.MidlineLeft, ParchmentMutedText, keyResultSize);
        var keyResultPlayers = CreateText(keyResult.rectTransform, "KeyResultPlayers", "-", new Rect(286f, 64f, 72f, 18f), 10.5f, TextAlignmentOptions.MidlineRight, ParchmentMutedText, keyResultSize);
        var keyResultDescription = CreateText(keyResult.rectTransform, "KeyResultDescription", "", new Rect(372f, 64f, 84f, 18f), 9.5f, TextAlignmentOptions.MidlineRight, ParchmentMutedText, keyResultSize);
        var keyIcon = CreateTownTexture(keyResult.rectTransform, "KeyResultIcon", "Choice_Icon_Key.png", new Rect(24f, 19f, 86f, 74f), keyResultSize);
        keyIcon.raycastTarget = false;
        keyResult.gameObject.SetActive(false);
        CreateTownSeparator(keyRoot, "KeyClearDivider", new Rect(76f, 282f, 380f, 12f), keySize, "Clear");
        var keyCancel = CreateTownChoiceButton(keyRoot, "Button_KeyCancel", "Cancel", new Rect(28f, 346f, 210f, 42f), keySize);
        var joinKeyButton = CreateTownChoiceButton(keyRoot, "Button_JoinKeyTown", "Join Town", new Rect(296f, 346f, 210f, 42f), keySize);
        var clearKeyButton = CreateTownChoiceButton(keyRoot, "Button_ClearKey", "Clear", new Rect(220f, 278f, 92f, 28f), keySize);
        keyRoot.gameObject.SetActive(false);

        var so = new SerializedObject(mapUi);
        so.FindProperty("_choicePanel").objectReferenceValue = panelImage.gameObject;
        so.FindProperty("_choiceTitle").objectReferenceValue = title;
        so.FindProperty("_choiceStatus").objectReferenceValue = status;
        so.FindProperty("_closeChoiceButton").objectReferenceValue = closeButton;
        so.FindProperty("_entryChoiceRoot").objectReferenceValue = choiceRoot.gameObject;
        so.FindProperty("_choiceExistingButton").objectReferenceValue = choiceExisting;
        so.FindProperty("_choiceCreateButton").objectReferenceValue = choiceCreate;
        so.FindProperty("_choiceKeyButton").objectReferenceValue = choiceKey;
        so.FindProperty("_choiceOpenSelectedButton").objectReferenceValue = choiceOpen;
        so.FindProperty("_choiceBackButton").objectReferenceValue = choiceBack;
        so.FindProperty("_quickKeyInput").objectReferenceValue = quickKeyInput;
        so.FindProperty("_quickKeyJoinButton").objectReferenceValue = quickJoinButton;

        so.FindProperty("_existingTownsRoot").objectReferenceValue = existingRoot.gameObject;
        so.FindProperty("_townSearchInput").objectReferenceValue = searchInput;
        so.FindProperty("_refreshTownRoomsButton").objectReferenceValue = refreshButton;
        so.FindProperty("_existingBackButton").objectReferenceValue = existingBack;
        so.FindProperty("_existingStatus").objectReferenceValue = existingStatus;
        so.FindProperty("_emptyRoomText").objectReferenceValue = emptyText;
        so.FindProperty("_selectedTownTitle").objectReferenceValue = selectedTownTitle;
        so.FindProperty("_selectedTownHost").objectReferenceValue = selectedTownHost;
        so.FindProperty("_selectedTownPlayers").objectReferenceValue = selectedTownPlayers;
        so.FindProperty("_selectedTownDescription").objectReferenceValue = selectedTownDescription;
        so.FindProperty("_selectedTownKey").objectReferenceValue = selectedTownKey;
        so.FindProperty("_joinSelectedTownButton").objectReferenceValue = joinSelectedButton;

        so.FindProperty("_createTownRoot").objectReferenceValue = createRoot.gameObject;
        so.FindProperty("_townNameInput").objectReferenceValue = townNameInput;
        so.FindProperty("_privateVisibilityButton").objectReferenceValue = privateButton;
        so.FindProperty("_publicVisibilityButton").objectReferenceValue = publicButton;
        so.FindProperty("_visibilityHint").objectReferenceValue = visibilityHint;
        so.FindProperty("_createTownButton").objectReferenceValue = createButton;
        so.FindProperty("_createCancelButton").objectReferenceValue = createCancel;

        so.FindProperty("_keyTownRoot").objectReferenceValue = keyRoot.gameObject;
        so.FindProperty("_inviteCodeInput").objectReferenceValue = inviteInput;
        so.FindProperty("_joinInviteButton").objectReferenceValue = findKeyButton;
        so.FindProperty("_joinKeyTownButton").objectReferenceValue = joinKeyButton;
        so.FindProperty("_clearKeyButton").objectReferenceValue = clearKeyButton;
        so.FindProperty("_keyCancelButton").objectReferenceValue = keyCancel;
        so.FindProperty("_keyStatus").objectReferenceValue = keyStatus;
        so.FindProperty("_keyResultRoot").objectReferenceValue = keyResult.gameObject;
        so.FindProperty("_keyResultTitle").objectReferenceValue = keyResultTitle;
        so.FindProperty("_keyResultHost").objectReferenceValue = keyResultHost;
        so.FindProperty("_keyResultPlayers").objectReferenceValue = keyResultPlayers;
        so.FindProperty("_keyResultDescription").objectReferenceValue = keyResultDescription;

        var rowProperty = so.FindProperty("_roomRows");
        rowProperty.arraySize = rows.Count;
        for (var i = 0; i < rows.Count; i++)
        {
            var element = rowProperty.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Root").objectReferenceValue = rows[i].Root;
            element.FindPropertyRelative("Button").objectReferenceValue = rows[i].Button;
            element.FindPropertyRelative("JoinButton").objectReferenceValue = rows[i].JoinButton;
            element.FindPropertyRelative("Title").objectReferenceValue = rows[i].Title;
            element.FindPropertyRelative("Meta").objectReferenceValue = rows[i].Meta;
            element.FindPropertyRelative("SteamBadge").objectReferenceValue = rows[i].SteamBadge;
            element.FindPropertyRelative("SelectedFrame").objectReferenceValue = rows[i].SelectedFrame;
        }

        var maxPlayersProperty = so.FindProperty("_maxPlayerButtons");
        maxPlayersProperty.arraySize = maxButtons.Count;
        for (var i = 0; i < maxButtons.Count; i++)
        {
            var element = maxPlayersProperty.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Value").intValue = maxButtons[i].Value;
            element.FindPropertyRelative("Button").objectReferenceValue = maxButtons[i].Button;
            element.FindPropertyRelative("Label").objectReferenceValue = maxButtons[i].Label;
            element.FindPropertyRelative("Highlight").objectReferenceValue = maxButtons[i].Highlight;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        panelImage.gameObject.SetActive(false);
        ApplyNanumGothicFontToChildren(panelRect);
        EditorUtility.SetDirty(mapUi);
    }

    private static TMP_InputField CreateTownChoiceInput(RectTransform parent, string name, string placeholderValue, Rect rect, Vector2 sourceSize, string textureName = "Input_Wide.png")
    {
        var image = CreateTownTexture(parent, name, textureName, rect, sourceSize);
        image.raycastTarget = true;

        var input = image.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.characterLimit = 32;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.transition = Selectable.Transition.ColorTint;

        var inputSize = new Vector2(rect.width, rect.height);
        var leftPadding = string.Equals(textureName, "Search_Input.png", StringComparison.OrdinalIgnoreCase) ? 44f : 18f;
        var text = CreateText(image.rectTransform, "Text", "", new Rect(leftPadding, 2f, rect.width - leftPadding - 18f, rect.height - 4f), 15f, TextAlignmentOptions.MidlineLeft, ParchmentText, inputSize);
        var placeholder = CreateText(image.rectTransform, "Placeholder", placeholderValue, new Rect(leftPadding, 2f, rect.width - leftPadding - 18f, rect.height - 4f), 14f, TextAlignmentOptions.MidlineLeft, new Color(0.43f, 0.35f, 0.24f, 0.72f), inputSize);
        input.textComponent = text;
        input.placeholder = placeholder;

        return input;
    }

    private static Button CreateTownChoiceButton(RectTransform parent, string name, string label, Rect rect, Vector2 sourceSize, string textureName = null)
    {
        textureName ??= rect.width >= 200f
            ? "Button_Teal_Medium_Left.png"
            : rect.width >= 145f
                ? "Button_Teal_Small.png"
                : "Button_Teal_Short.png";
        var button = CreateTownTextureButton(parent, name, textureName, rect, sourceSize);
        var rectTransform = button.transform as RectTransform;
        CreateText(rectTransform, "Label", label, new Rect(8f, 0f, rect.width - 16f, rect.height), rect.height <= 30f ? 12f : 15f, TextAlignmentOptions.Center, ButtonLightText, new Vector2(rect.width, rect.height));
        return button;
    }

    private static TownEntryRowBuildBinding CreateTownRoomRow(RectTransform parent, string name, Rect rect, Vector2 sourceSize)
    {
        var image = CreateTownTexture(parent, name, "Room_Row.png", rect, sourceSize);
        image.raycastTarget = true;

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        AddButtonFeedback(button, image.rectTransform, image);

        var rowSize = new Vector2(rect.width, rect.height);
        var selectedFrame = CreateFlatSolid(image.rectTransform, "SelectedFrame", new Color(0f, 0f, 0f, 0f));
        Stretch(selectedFrame.rectTransform);
        selectedFrame.raycastTarget = false;
        var title = CreateText(image.rectTransform, "Title", "Town", new Rect(58f, 3f, 145f, 21f), 13f, TextAlignmentOptions.MidlineLeft, new Color(0.11f, 0.20f, 0.18f, 1f), rowSize);
        var meta = CreateText(image.rectTransform, "Meta", "0 / 0", new Rect(204f, 4f, 50f, 21f), 12f, TextAlignmentOptions.MidlineRight, new Color(0.32f, 0.28f, 0.20f, 1f), rowSize);
        var steamBadge = CreateText(image.rectTransform, "SteamBadge", "Steam", new Rect(58f, 23f, 54f, 16f), 10f, TextAlignmentOptions.MidlineLeft, new Color(0.04f, 0.38f, 0.58f, 1f), rowSize);
        steamBadge.gameObject.SetActive(false);
        var joinButton = CreateTownChoiceButton(image.rectTransform, "Button_RowJoin", "Join", new Rect(256f, 8f, 44f, 28f), rowSize, "Button_Teal_Short.png");

        image.gameObject.SetActive(false);
        return new TownEntryRowBuildBinding
        {
            Root = image.gameObject,
            Button = button,
            JoinButton = joinButton,
            Title = title,
            Meta = meta,
            SteamBadge = steamBadge,
            SelectedFrame = selectedFrame
        };
    }

    private static void CreateTownSeparator(RectTransform parent, string name, Rect rect, Vector2 sourceSize, string label = "")
    {
        var root = CreateRect(parent, name);
        SetRectFromTopLeft(root, rect, sourceSize);
        var size = new Vector2(rect.width, rect.height);
        var art = CreateTownTexture(root, "Art", "Separator_Short.png", new Rect(0f, 0f, rect.width, rect.height), size);
        art.raycastTarget = false;
        if (!string.IsNullOrWhiteSpace(label))
        {
            var labelWidth = Mathf.Clamp(rect.width * 0.28f, 88f, 132f);
            var labelX = (rect.width - labelWidth) * 0.5f;
            var labelBack = CreateFlatSolid(root, "LabelBack", new Color(0.80f, 0.68f, 0.48f, 0.88f));
            SetRectFromTopLeft(labelBack.rectTransform, new Rect(labelX, 0f, labelWidth, rect.height), size);
            CreateText(root, "Label", label, new Rect(labelX, 0f, labelWidth, rect.height), 12.5f, TextAlignmentOptions.Center, ParchmentText, size);
        }
    }

    private static Button CreateTownOptionButton(RectTransform parent, string name, string title, string subtitle, string iconLabel, Rect rect, Vector2 sourceSize)
    {
        var button = CreateTownTextureButton(parent, name, "Choice_Row.png", rect, sourceSize);
        var image = button.targetGraphic as RawImage;

        var optionSize = new Vector2(rect.width, rect.height);
        var iconTexture = iconLabel switch
        {
            "FIND" => "Choice_Icon_Find.png",
            "NEW" => "Choice_Icon_Create.png",
            "KEY" => "Choice_Icon_Key.png",
            _ => "Choice_Icon_Find.png"
        };
        var icon = CreateTownTexture(image.rectTransform, "Icon", iconTexture, new Rect(18f, 9f, 58f, 58f), optionSize);
        icon.raycastTarget = false;
        CreateText(image.rectTransform, "Title", title, new Rect(138f, 14f, 260f, 24f), 19f, TextAlignmentOptions.MidlineLeft, ParchmentText, optionSize);
        CreateText(image.rectTransform, "Subtitle", subtitle, new Rect(138f, 42f, 286f, 20f), 12.5f, TextAlignmentOptions.MidlineLeft, ParchmentMutedText, optionSize);
        return button;
    }

    private static MaxPlayerBuildBinding CreateTownMaxPlayerButton(RectTransform parent, string name, int value, Rect rect, Vector2 sourceSize)
    {
        var button = CreateTownTextureButton(parent, name, "Button_Parchment_Wide.png", rect, sourceSize);
        var image = button.targetGraphic as RawImage;
        var size = new Vector2(rect.width, rect.height);
        var highlight = CreateTownTexture(image.rectTransform, "SelectedHighlight", "Button_Teal_Small.png", new Rect(0f, 0f, rect.width, rect.height), size);
        Stretch(highlight.rectTransform);
        highlight.raycastTarget = false;
        highlight.color = new Color(1f, 1f, 1f, 0f);
        var label = CreateText(image.rectTransform, "Label", value.ToString(), new Rect(0f, 0f, rect.width, rect.height), 18f, TextAlignmentOptions.Center, ParchmentText, size);
        return new MaxPlayerBuildBinding
        {
            Value = value,
            Button = button,
            Label = label,
            Highlight = highlight
        };
    }

    private static HomeEquipSlotUI CreateEquipSlot(RectTransform parent, string name, EquipmentSlot slot, string label, string iconPath, Rect rect)
    {
        return CreateEquipSlot(parent, name, slot, label, iconPath, rect, LayoutSize);
    }

    private static HomeEquipSlotUI CreateEquipSlot(RectTransform parent, string name, EquipmentSlot slot, string label, string iconPath, Rect rect, Vector2 sourceSize)
    {
        var button = CreateButtonTexture(parent, name, "UI_Home_Equipment/UI_Panel_EquipmentSlot.png", rect);
        SetRectFromTopLeft(button.GetComponent<RectTransform>(), rect, sourceSize);
        var slotSize = new Vector2(rect.width, rect.height);
        var slotIconBackground = CreateFlatSolid(button.transform as RectTransform, "SlotIconBackground", EquipmentFrameBackground);
        SetRectFromTopLeft(slotIconBackground.rectTransform, new Rect(31f, 33f, 64f, 64f), slotSize);
        var slotIcon = CreateTexture(button.transform, "SlotIcon", iconPath, new Rect(40f, 42f, 46f, 46f), slotSize);
        var slotIconFrame = CreateImage(button.transform, "SlotIconFrame", new Rect(26f, 28f, 74f, 74f), slotSize);
        slotIconFrame.sprite = Resources.Load<Sprite>(DetailIconFrameResourcePath);
        slotIconFrame.preserveAspect = false;
        slotIconFrame.raycastTarget = false;
        slotIconBackground.transform.SetAsLastSibling();
        slotIcon.transform.SetAsLastSibling();
        slotIconFrame.transform.SetAsLastSibling();
        CreateText(button.transform as RectTransform, "SlotLabel", label, new Rect(136f, 38f, rect.width - 158f, 28f), 20f, TextAlignmentOptions.MidlineLeft, ParchmentText, slotSize);
        CreateText(button.transform as RectTransform, "SlotHint", "Tap to view", new Rect(138f, 74f, rect.width - 160f, 20f), 13f, TextAlignmentOptions.MidlineLeft, ParchmentMutedText, slotSize);

        var empty = CreateRect(button.transform, "Empty");
        Stretch(empty);
        CreateText(empty, "EmptyLabel", "EMPTY", new Rect(rect.width - 110f, rect.height - 34f, 78f, 18f), 13f, TextAlignmentOptions.MidlineRight, ParchmentMutedText, slotSize);

        var filled = CreateRect(button.transform, "Filled");
        Stretch(filled);
        filled.gameObject.SetActive(false);
        var iconBackground = CreateFlatSolid(filled, "IconBackground", EquipmentFrameBackground);
        SetRectFromTopLeft(iconBackground.rectTransform, new Rect(31f, 35f, 64f, 64f), slotSize);
        var icon = CreateImage(filled, "Icon", new Rect(34f, 38f, 58f, 58f), slotSize);
        var iconFrame = CreateImage(filled, "IconFrame", new Rect(24f, 28f, 78f, 78f), slotSize);
        iconFrame.sprite = Resources.Load<Sprite>(SlotIconFrameResourcePath);
        iconFrame.preserveAspect = false;
        iconFrame.raycastTarget = false;
        iconBackground.transform.SetAsLastSibling();
        icon.transform.SetAsLastSibling();
        iconFrame.transform.SetAsLastSibling();

        var slotUi = button.gameObject.AddComponent<HomeEquipSlotUI>();
        var so = new SerializedObject(slotUi);
        so.FindProperty("_targetSlot").enumValueIndex = (int)slot;
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_iconBackground").objectReferenceValue = iconBackground;
        so.FindProperty("_iconFrame").objectReferenceValue = iconFrame;
        so.FindProperty("_slotDecorationIcon").objectReferenceValue = slotIcon;
        so.FindProperty("_slotDecorationBackground").objectReferenceValue = slotIconBackground;
        so.FindProperty("_slotDecorationFrame").objectReferenceValue = slotIconFrame;
        so.FindProperty("_btn").objectReferenceValue = button;
        so.FindProperty("_emptyVisual").objectReferenceValue = empty.gameObject;
        so.FindProperty("_filledVisual").objectReferenceValue = filled.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        return slotUi;
    }

    private static HomeEquipSlotUI CreateEquipSlotHotspot(RectTransform parent, string name, EquipmentSlot slot, Rect rect, Vector2 sourceSize)
    {
        var button = CreateTransparentButton(parent, name, rect, sourceSize);
        var slotSize = new Vector2(rect.width, rect.height);
        var empty = CreateRect(button.transform, "Empty");
        Stretch(empty);
        empty.gameObject.SetActive(false);

        var filled = CreateRect(button.transform, "Filled");
        Stretch(filled);
        filled.gameObject.SetActive(false);

        var icon = CreateImage(filled, "Icon", new Rect(0f, 0f, rect.width, rect.height), slotSize);
        icon.color = new Color(1f, 1f, 1f, 0f);

        var slotUi = button.gameObject.AddComponent<HomeEquipSlotUI>();
        var so = new SerializedObject(slotUi);
        so.FindProperty("_targetSlot").enumValueIndex = (int)slot;
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_btn").objectReferenceValue = button;
        so.FindProperty("_emptyVisual").objectReferenceValue = empty.gameObject;
        so.FindProperty("_filledVisual").objectReferenceValue = filled.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        return slotUi;
    }

    private static RectTransform CreateDesignRoot(RectTransform parent, string name, Vector2 sourceSize)
    {
        var rect = CreateRect(parent, name);
        SetRectFromTopLeft(rect, GetAspectFitRect(sourceSize), LayoutSize);
        return rect;
    }

    private static Rect GetAspectFitRect(Vector2 sourceSize)
    {
        var scale = Mathf.Min(LayoutSize.x / sourceSize.x, LayoutSize.y / sourceSize.y);
        var width = sourceSize.x * scale;
        var height = sourceSize.y * scale;
        return new Rect((LayoutSize.x - width) * 0.5f, (LayoutSize.y - height) * 0.5f, width, height);
    }

    private static Button CreateHomeMenuCard(RectTransform parent, string buttonName, string iconPath, string title, string subtitle, string actionText, Rect rect)
    {
        return CreateHomeMenuCard(parent, buttonName, iconPath, title, subtitle, actionText, rect, LayoutSize);
    }

    private static Button CreateHomeMenuCard(RectTransform parent, string buttonName, string iconPath, string title, string subtitle, string actionText, Rect rect, Vector2 sourceSize)
    {
        var card = CreateRect(parent, $"{buttonName}_Card");
        SetRectFromTopLeft(card, rect, sourceSize);
        var cardSize = new Vector2(rect.width, rect.height);
        CreateTexture(card, "Frame", "UI_Home_Interface/UI_Panel.png", new Rect(0f, 0f, rect.width, rect.height), cardSize);
        CreateTexture(card, "Icon", iconPath, new Rect(30f, 24f, 98f, 132f), cardSize);
        CreateText(card, "Title", title, new Rect(146f, 36f, 136f, 30f), 23f, TextAlignmentOptions.MidlineLeft, ParchmentText, cardSize);
        var divider = CreateSolid(card, "Divider", new Color(0f, 0.35f, 0.36f, 1f));
        SetRectFromTopLeft(divider.rectTransform, new Rect(210f, 76f, 7f, 7f), cardSize);
        divider.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        CreateText(card, "Subtitle", subtitle, new Rect(146f, 92f, 128f, 44f), 13f, TextAlignmentOptions.TopLeft, ParchmentMutedText, cardSize);
        CreateTexture(card, "ActionFrame", "UI_Home_Interface/UI_Button.png", new Rect(142f, 132f, 142f, 38f), cardSize);
        CreateText(card, "ActionLabel", actionText, new Rect(150f, 140f, 108f, 18f), 12f, TextAlignmentOptions.Center, ParchmentText, cardSize);
        CreateText(card, "ActionArrow", "›", new Rect(260f, 134f, 18f, 28f), 22f, TextAlignmentOptions.Center, ParchmentText, cardSize);

        var button = CreateTransparentButton(card, buttonName, new Rect(0f, 0f, rect.width, rect.height), new Vector2(rect.width, rect.height));
        AddButtonFeedback(button, card, button.targetGraphic);
        return button;
    }

    private static Button CreateMenuButton(RectTransform parent, string name, string buttonPath, string iconPath, string title, string subtitle, Rect rect)
    {
        var button = CreateButtonTexture(parent, name, buttonPath, rect);
        CreateTexture(button.transform, "Icon", iconPath, new Rect(24f, 19f, 56f, 56f), new Vector2(rect.width, rect.height));
        CreateText(button.transform as RectTransform, "Title", title, new Rect(96f, 24f, 178f, 24f), 20f, TextAlignmentOptions.MidlineLeft, ParchmentText, new Vector2(rect.width, rect.height));
        CreateText(button.transform as RectTransform, "Subtitle", subtitle, new Rect(98f, 52f, 182f, 18f), 13f, TextAlignmentOptions.MidlineLeft, ParchmentMutedText, new Vector2(rect.width, rect.height));
        return button;
    }

    private static RealmBuildBinding CreateRealmHotspot(RectTransform root, string name, Rect rect, string realmId, string displayName, string description, string ticket, string sceneName)
    {
        var button = CreateTransparentButton(root, name, rect);
        var highlight = CreateSolid(button.transform as RectTransform, "SelectedHighlight", new Color(1f, 1f, 1f, 0f));
        Stretch(highlight.rectTransform);
        return new RealmBuildBinding
        {
            RealmId = realmId,
            DisplayName = displayName,
            Description = description,
            RequiredTicket = ticket,
            SceneName = sceneName,
            Button = button,
            Highlight = highlight
        };
    }

    private static RealmBuildBinding CreateRealmButton(RectTransform root, string name, string texturePath, Rect rect, string realmId, string displayName, string description, string ticket, string sceneName)
    {
        var button = CreateButtonTexture(root, name, texturePath, rect);
        ConfigureMapPieceHitArea(button);
        var highlight = CreateMapPieceHighlight(button.transform as RectTransform, "SelectedHighlight", texturePath);
        return new RealmBuildBinding
        {
            RealmId = realmId,
            DisplayName = displayName,
            Description = description,
            RequiredTicket = ticket,
            SceneName = sceneName,
            Button = button,
            Highlight = highlight
        };
    }

    private static Image CreateMapPieceHighlight(RectTransform parent, string name, string texturePath)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HomeMapPieceHighlight));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());

        var image = go.GetComponent<Image>();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiResourceTarget}/{texturePath}");
        if (sprite != null)
            ConfigureMapPieceHighlight(image, sprite);
        else
            image.color = new Color(1f, 1f, 1f, 0f);

        return image;
    }

    private static Button CreateBackButton(RectTransform root, string name)
    {
        return CreateButtonTexture(root, name, "UI_Map/UI_Button_Back.png", new Rect(28f, 24f, 76f, 58f));
    }

    private static GameObject CreatePopupItemPrefab(RectTransform parent)
    {
        var item = new GameObject("Prefab_PopupItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        item.transform.SetParent(parent, false);
        var itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(1f, 1f);
        itemRect.pivot = new Vector2(0.5f, 1f);
        itemRect.sizeDelta = new Vector2(0f, 68f);

        var background = item.GetComponent<Image>();
        ApplyDefaultSprite(background);
        background.sprite = Resources.Load<Sprite>("UI/UI_Home_Equipment_Detail/UI_01_default");
        background.type = Image.Type.Sliced;
        background.color = Color.white;

        var layout = item.GetComponent<LayoutElement>();
        layout.minHeight = 64f;
        layout.preferredHeight = 85f;
        layout.preferredWidth = 84f;
        layout.minHeight = 85f;
        layout.minWidth = 84f;

        var icon = CreateImage(item.transform, "Icon", new Rect(15f, 15f, 54f, 54f), new Vector2(84f, 85f));
        var nameText = CreateText(itemRect, "NameText", "Item", new Rect(4f, 66f, 76f, 16f), 10f, TextAlignmentOptions.Center, ParchmentText, new Vector2(84f, 85f));
        var levelText = CreateText(itemRect, "LevelText", "+0", new Rect(6f, 68f, 72f, 14f), 10f, TextAlignmentOptions.Center, ParchmentMutedText, new Vector2(84f, 85f));
        var markImage = CreateImage(item.transform, "EquippedMark", new Rect(54f, 5f, 24f, 18f), new Vector2(84f, 85f));
        markImage.preserveAspect = false;
        markImage.type = Image.Type.Sliced;
        markImage.color = new Color(0.10f, 0.36f, 0.34f, 0.96f);
        var mark = markImage.gameObject;
        var markText = CreateText(markImage.rectTransform, "Label", "E", new Rect(0f, 0f, 24f, 18f), 11f, TextAlignmentOptions.Center, ButtonLightText, new Vector2(24f, 18f));
        markText.fontStyle = FontStyles.Bold;
        mark.SetActive(false);
        icon.transform.SetAsLastSibling();
        mark.transform.SetAsLastSibling();

        var itemUi = item.AddComponent<HomeEquipPopupItemUI>();
        var so = new SerializedObject(itemUi);
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_nameText").objectReferenceValue = nameText;
        so.FindProperty("_levelText").objectReferenceValue = levelText;
        so.FindProperty("_btn").objectReferenceValue = item.GetComponent<Button>();
        so.FindProperty("_equippedMark").objectReferenceValue = mark;
        so.ApplyModifiedPropertiesWithoutUndo();

        AddButtonFeedback(item.GetComponent<Button>(), itemRect, background);
        item.SetActive(false);
        return item;
    }

    private static ScrollBuildBinding CreateScrollView(RectTransform parent, string name, Rect rect, Vector2 contentSize)
    {
        var viewport = CreateSolid(parent, name, new Color(1f, 1f, 1f, 0.01f));
        SetRectFromTopLeft(viewport.rectTransform, rect, LayoutSize);
        viewport.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.scrollSensitivity = 22f;

        var content = CreateRect(viewport.transform, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = contentSize;
        scroll.viewport = viewport.rectTransform;
        scroll.content = content;

        return new ScrollBuildBinding
        {
            Viewport = viewport.rectTransform,
            Content = content
        };
    }

    private static RawImage CreateTownTexture(Transform parent, string name, string textureName, Rect rect, Vector2 sourceSize)
    {
        return CreateTexture(parent, name, $"UI_TownEntry/{textureName}", rect, sourceSize);
    }

    private static Button CreateTownTextureButton(RectTransform parent, string name, string textureName, Rect rect, Vector2 sourceSize)
    {
        var image = CreateTownTexture(parent, name, textureName, rect, sourceSize);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        AddButtonFeedback(button, image.rectTransform, image);
        return button;
    }

    private static RawImage CreateTexture(Transform parent, string name, string relativePath, Rect rect)
    {
        return CreateTexture(parent, name, relativePath, rect, LayoutSize);
    }

    private static RawImage CreateTexture(Transform parent, string name, string relativePath, Rect rect, Vector2 sourceSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(parent, false);
        SetRectFromTopLeft(go.GetComponent<RectTransform>(), rect, sourceSize);
        var image = go.GetComponent<RawImage>();
        image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{UiResourceTarget}/{relativePath}");
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateImage(Transform parent, string name, Rect rect, Vector2 sourceSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        SetRectFromTopLeft(go.GetComponent<RectTransform>(), rect, sourceSize);
        var image = go.GetComponent<Image>();
        ApplyDefaultSprite(image);
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private static Button CreateButtonTexture(Transform parent, string name, string relativePath, Rect rect)
    {
        return CreateButtonTexture(parent, name, relativePath, rect, LayoutSize);
    }

    private static Button CreateButtonTexture(Transform parent, string name, string relativePath, Rect rect, Vector2 sourceSize)
    {
        var image = CreateTexture(parent, name, relativePath, rect, sourceSize);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        AddButtonFeedback(button, image.rectTransform, image);
        return button;
    }

    private static Button CreateTransparentButton(RectTransform parent, string name, Rect rect)
    {
        return CreateTransparentButton(parent, name, rect, LayoutSize);
    }

    private static Button CreateTransparentButton(RectTransform parent, string name, Rect rect, Vector2 sourceSize)
    {
        var image = CreateSolid(parent, name, new Color(1f, 1f, 1f, 0.01f));
        SetRectFromTopLeft(image.rectTransform, rect, sourceSize);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        AddButtonFeedback(button, image.rectTransform, image);
        return button;
    }

    private static Image CreateSolid(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        var image = go.GetComponent<Image>();
        ApplyDefaultSprite(image);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateFlatSolid(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        var image = go.GetComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void ApplyDefaultSprite(Image image)
    {
        if (image == null || image.sprite != null)
            return;

        if (_defaultUiSprite == null)
            _defaultUiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        if (_defaultUiSprite != null)
            image.sprite = _defaultUiSprite;
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, string name, string value, Rect rect, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        return CreateText(parent, name, value, rect, fontSize, alignment, color, LayoutSize);
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, string name, string value, Rect rect, float fontSize, TextAlignmentOptions alignment, Color color, Vector2 sourceSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        SetRectFromTopLeft(go.GetComponent<RectTransform>(), rect, sourceSize);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        var font = GetNanumGothicFont();
        if (font != null)
            text.font = font;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(8f, fontSize - 8f);
        text.fontSizeMax = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static TMP_FontAsset GetNanumGothicFont()
    {
        if (_nanumGothicFont != null)
            return _nanumGothicFont;

        _nanumGothicFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NanumGothicFontPath);
        if (_nanumGothicFont == null)
            _nanumGothicFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NanumGothicFontFallbackPath);

        if (_nanumGothicFont == null)
            Debug.LogWarning("[Home1SceneSpaceUiBuilder] NanumGothic SDF font asset was not found.");

        return _nanumGothicFont;
    }

    private static void ApplyNanumGothicFontToChildren(RectTransform root)
    {
        if (root == null)
            return;

        var font = GetNanumGothicFont();
        if (font == null)
            return;

        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
                continue;

            text.font = font;
            EditorUtility.SetDirty(text);
        }
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetRectFromTopLeft(RectTransform rect, Rect sourceRect, Vector2 sourceSize)
    {
        rect.anchorMin = new Vector2(sourceRect.xMin / sourceSize.x, 1f - sourceRect.yMax / sourceSize.y);
        rect.anchorMax = new Vector2(sourceRect.xMax / sourceSize.x, 1f - sourceRect.yMin / sourceSize.y);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static HomeUIButtonFeedback AddButtonFeedback(Button button, RectTransform scaleTarget = null, Graphic tintTarget = null)
    {
        if (button == null)
            return null;

        var feedback = button.GetComponent<HomeUIButtonFeedback>() ?? button.gameObject.AddComponent<HomeUIButtonFeedback>();
        feedback.Configure(scaleTarget != null ? scaleTarget : button.transform as RectTransform, tintTarget != null ? tintTarget : button.targetGraphic);
        EditorUtility.SetDirty(feedback);
        return feedback;
    }

    private static TextMeshProUGUI RequireTmp(Transform root, string name)
    {
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text != null && text.gameObject.name == name)
                return text;
        }

        throw new InvalidOperationException($"TextMeshProUGUI not found: {name}");
    }

    private static void ActivateOnlyHomeRoot(GameObject activeRoot)
    {
        var rootNames = new[]
        {
            "UI_Home_Interface",
            "UI_Home_Equipment",
            "UI_Home_Inventory",
            "UI_Home_Appearance",
            "UI_Home_Map",
            "UI_Home_Equipment_Detail"
        };

        foreach (var rootName in rootNames)
        {
            var root = RequireSceneObject(rootName);
            root.SetActive(root == activeRoot);
        }
    }

    private static void PrepareForestMapPreview(Transform mapRoot)
    {
        RequireTmp(mapRoot, "RealmTitle").text = "Whispering Forest";
        RequireTmp(mapRoot, "RealmTicket").text = $"Ticket: {TownPassTicket}";
        RequireTmp(mapRoot, "RealmDescription").text = "울창한 숲과 작은 마을이 있는 초반 영역입니다. 리듬 왜곡이 가장 약해 입장 준비에 적합합니다.";
        RequireTmp(mapRoot, "MapStatus").text = "Whispering Forest 입장 방식 선택 중...";
    }

    private static void SetTownPreviewView(Transform panel, string activeRootName, string title, string status)
    {
        var roots = new[] { "EntryChoiceRoot", "ExistingTownsRoot", "CreateTownRoot", "JoinWithKeyRoot" };
        foreach (var rootName in roots)
        {
            var root = RequireChild(panel, rootName);
            root.gameObject.SetActive(rootName == activeRootName);
        }

        RequireTmp(panel, "TownEntryTitle").text = title;
        RequireTmp(panel, "TownEntryStatus").text = status;
        Canvas.ForceUpdateCanvases();
    }

    private static void PrepareExistingTownPreview(Transform panel)
    {
        SetInputText(panel, "Input_SearchTown", "");
        RequireTmp(panel, "EmptyRoomText").gameObject.SetActive(false);

        var roomData = new[]
        {
            ("Moss Lantern Village", "18 / 30", true),
            ("Riverbend Hamlet", "12 / 25", false),
            ("Pinewatch Outpost", "9 / 20", false),
            ("Dewdrop Hollow", "7 / 20", false),
            ("Thornfield Settlement", "16 / 30", false)
        };

        for (var i = 0; i < roomData.Length; i++)
        {
            var row = RequireChild(panel, $"TownRoomRow_{i:00}");
            row.gameObject.SetActive(true);
            RequireTmp(row, "Title").text = roomData[i].Item1;
            RequireTmp(row, "Meta").text = roomData[i].Item2;
            var steamBadge = RequireTmp(row, "SteamBadge");
            steamBadge.text = i == 0 ? "Steam" : "";
            steamBadge.gameObject.SetActive(i == 0);

            var selectedFrame = RequireChild(row, "SelectedFrame").GetComponent<Graphic>();
            if (selectedFrame != null)
                selectedFrame.color = roomData[i].Item3 ? new Color(0.08f, 0.31f, 0.33f, 0.26f) : new Color(0f, 0f, 0f, 0f);
        }

        RequireTmp(panel, "SelectedTownTitle").text = "Moss Lantern Village";
        RequireTmp(panel, "SelectedTownHost").text = "Host: Elowen";
        RequireTmp(panel, "SelectedTownPlayers").text = "Players: 18 / 30";
        RequireTmp(panel, "SelectedTownDescription").text = "A peaceful woodland village surrounded by mossy trees and soft lantern light. Friendly folk welcome travelers and builders alike.";
        RequireTmp(panel, "SelectedTownKey").text = "Key: 7f3kg2m9";
    }

    private static void PrepareCreateTownPreview(Transform panel)
    {
        SetInputText(panel, "Input_TownName", "Whispering Nest");
        SetTownPreviewButtonSelected(panel, "Button_CreatePrivate", false);
        SetTownPreviewButtonSelected(panel, "Button_CreatePublic", true);
        RequireTmp(panel, "VisibilityHint").text = "Public towns appear in the existing town list and can be joined by other players.";

        foreach (var value in new[] { 2, 4, 6, 8 })
        {
            var selected = value == 4;
            var button = RequireChild(panel, $"Button_MaxPlayers_{value}");
            var highlight = RequireChild(button, "SelectedHighlight").GetComponent<Graphic>();
            if (highlight != null)
                highlight.color = selected ? new Color(0.08f, 0.31f, 0.33f, 0.95f) : new Color(0f, 0f, 0f, 0f);
            RequireTmp(button, "Label").color = selected ? new Color(0.98f, 0.91f, 0.70f, 1f) : ParchmentText;
        }
    }

    private static void PrepareKeyTownPreview(Transform panel)
    {
        SetInputText(panel, "Input_InviteCode", "7F3K-G2M9");
        RequireTmp(panel, "KeyStatus").text = "Private towns require a valid key.";

        var result = RequireChild(panel, "KeyResultPanel");
        result.gameObject.SetActive(true);
        RequireTmp(result, "KeyResultTitle").text = "Moonlit Pine Camp";
        RequireTmp(result, "KeyResultHost").text = "Host: Rhea";
        RequireTmp(result, "KeyResultPlayers").text = "3 / 4";
        RequireTmp(result, "KeyResultDescription").text = "Private";
    }

    private static void SetInputText(Transform root, string inputName, string value)
    {
        var input = RequireChild(root, inputName).GetComponent<TMP_InputField>();
        Require(input != null, $"{inputName} is missing TMP_InputField.");
        input.text = value;
        input.ForceLabelUpdate();
    }

    private static void SetTownPreviewButtonSelected(Transform root, string buttonName, bool selected)
    {
        var buttonRoot = RequireChild(root, buttonName);
        var graphic = buttonRoot.GetComponent<Graphic>();
        if (graphic != null)
            graphic.color = selected ? new Color(0.08f, 0.31f, 0.33f, 1f) : new Color(0.86f, 0.72f, 0.50f, 0.28f);

        RequireTmp(buttonRoot, "Label").color = selected ? new Color(0.98f, 0.91f, 0.70f, 1f) : ParchmentText;
    }

    private static Transform RequireChild(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == name)
                return child;
        }

        throw new InvalidOperationException($"Child transform not found: {name}");
    }

    private static void CaptureCanvasPreview(Canvas canvas, string fileName)
    {
        const int width = 1920;
        const int height = 1080;

        var screenshotsDirectory = Path.Combine(Application.dataPath, "Screenshots");
        Directory.CreateDirectory(screenshotsDirectory);
        var fullPath = Path.Combine(screenshotsDirectory, fileName);
        var assetPath = $"Assets/Screenshots/{fileName}";

        var previousRenderMode = canvas.renderMode;
        var previousCamera = canvas.worldCamera;
        var previousPlaneDistance = canvas.planeDistance;
        var previousActive = RenderTexture.active;

        var cameraObject = new GameObject("Temp_HomeMapTownEntryPreviewCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = ~0;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.targetTexture = renderTexture;
            Canvas.ForceUpdateCanvases();

            camera.Render();
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath);
        }
        finally
        {
            canvas.renderMode = previousRenderMode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousPlaneDistance;
            camera.targetTexture = null;
            RenderTexture.active = previousActive;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static T RequireComponent<T>(string gameObjectName) where T : Component
    {
        var go = RequireSceneObject(gameObjectName);
        var component = go.GetComponent<T>();
        Require(component != null, $"{gameObjectName} is missing {typeof(T).Name}.");
        return component;
    }

    private static GameObject RequireSceneObject(string name)
    {
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == name && go.scene.IsValid())
                return go;
        }

        throw new InvalidOperationException($"Scene object not found: {name}");
    }

    private static void VerifyHomeMapTownEntry(HomeMapRealmUI mapUi)
    {
        var so = new SerializedObject(mapUi);
        var panel = RequireSerializedReference<GameObject>(so, "_choicePanel");
        var panelRect = panel.GetComponent<RectTransform>();
        Require(panelRect != null, "TownEntryChoicePanel is missing RectTransform.");
        Require(!panel.activeSelf, "TownEntryChoicePanel must start inactive and open only after choosing a realm.");
        RequireAnchorsInsideParent(panelRect, "TownEntryChoicePanel");

        var entryRoot = RequireSerializedReference<GameObject>(so, "_entryChoiceRoot");
        var existingRoot = RequireSerializedReference<GameObject>(so, "_existingTownsRoot");
        var createRoot = RequireSerializedReference<GameObject>(so, "_createTownRoot");
        var keyRoot = RequireSerializedReference<GameObject>(so, "_keyTownRoot");
        Require(entryRoot.activeSelf, "EntryChoiceRoot should be the default active town entry view.");
        Require(!existingRoot.activeSelf, "ExistingTownsRoot should start inactive.");
        Require(!createRoot.activeSelf, "CreateTownRoot should start inactive.");
        Require(!keyRoot.activeSelf, "JoinWithKeyRoot should start inactive.");

        var requiredReferences = new[]
        {
            "_choiceTitle",
            "_choiceStatus",
            "_closeChoiceButton",
            "_choiceExistingButton",
            "_choiceCreateButton",
            "_choiceKeyButton",
            "_choiceOpenSelectedButton",
            "_choiceBackButton",
            "_quickKeyJoinButton",
            "_refreshTownRoomsButton",
            "_existingBackButton",
            "_existingStatus",
            "_emptyRoomText",
            "_selectedTownTitle",
            "_selectedTownHost",
            "_selectedTownPlayers",
            "_selectedTownDescription",
            "_selectedTownKey",
            "_joinSelectedTownButton",
            "_privateVisibilityButton",
            "_publicVisibilityButton",
            "_visibilityHint",
            "_createTownButton",
            "_createCancelButton",
            "_joinInviteButton",
            "_joinKeyTownButton",
            "_clearKeyButton",
            "_keyCancelButton",
            "_keyStatus",
            "_keyResultRoot",
            "_keyResultTitle",
            "_keyResultHost",
            "_keyResultPlayers",
            "_keyResultDescription"
        };

        foreach (var propertyName in requiredReferences)
            RequireSerializedReference(so, propertyName);

        VerifyInputField(RequireSerializedReference<TMP_InputField>(so, "_quickKeyInput"), "_quickKeyInput");
        VerifyInputField(RequireSerializedReference<TMP_InputField>(so, "_townSearchInput"), "_townSearchInput");
        VerifyInputField(RequireSerializedReference<TMP_InputField>(so, "_townNameInput"), "_townNameInput");
        VerifyInputField(RequireSerializedReference<TMP_InputField>(so, "_inviteCodeInput"), "_inviteCodeInput");

        var roomRows = so.FindProperty("_roomRows");
        Require(roomRows != null && roomRows.isArray, "_roomRows must be an array.");
        Require(roomRows.arraySize == 5, "_roomRows should provide five visible town rows.");
        for (var i = 0; i < roomRows.arraySize; i++)
        {
            var row = roomRows.GetArrayElementAtIndex(i);
            RequireSerializedRelativeReference(row, "Root", $"_roomRows[{i}].Root");
            RequireSerializedRelativeReference(row, "Button", $"_roomRows[{i}].Button");
            RequireSerializedRelativeReference(row, "JoinButton", $"_roomRows[{i}].JoinButton");
            RequireSerializedRelativeReference(row, "Title", $"_roomRows[{i}].Title");
            RequireSerializedRelativeReference(row, "Meta", $"_roomRows[{i}].Meta");
            RequireSerializedRelativeReference(row, "SteamBadge", $"_roomRows[{i}].SteamBadge");
            RequireSerializedRelativeReference(row, "SelectedFrame", $"_roomRows[{i}].SelectedFrame");
        }

        var expectedMaxPlayers = new HashSet<int> { 2, 4, 6, 8 };
        var maxPlayers = so.FindProperty("_maxPlayerButtons");
        Require(maxPlayers != null && maxPlayers.isArray, "_maxPlayerButtons must be an array.");
        Require(maxPlayers.arraySize == expectedMaxPlayers.Count, "_maxPlayerButtons should expose 2, 4, 6, and 8 player choices.");
        for (var i = 0; i < maxPlayers.arraySize; i++)
        {
            var binding = maxPlayers.GetArrayElementAtIndex(i);
            var value = binding.FindPropertyRelative("Value").intValue;
            Require(expectedMaxPlayers.Remove(value), $"Unexpected max player choice: {value}.");
            RequireSerializedRelativeReference(binding, "Button", $"_maxPlayerButtons[{i}].Button");
            RequireSerializedRelativeReference(binding, "Label", $"_maxPlayerButtons[{i}].Label");
            RequireSerializedRelativeReference(binding, "Highlight", $"_maxPlayerButtons[{i}].Highlight");
        }

        Require(expectedMaxPlayers.Count == 0, "Max player choices are missing one or more expected values.");
        VerifyTownPanelAnchors(panelRect);
    }

    private static void VerifyInputField(TMP_InputField input, string propertyName)
    {
        Require(input.contentType == TMP_InputField.ContentType.Standard, $"{propertyName} must allow standard text.");
        Require(input.lineType == TMP_InputField.LineType.SingleLine, $"{propertyName} must be single line.");
        Require(input.textComponent != null, $"{propertyName} is missing text component.");
        Require(input.placeholder != null, $"{propertyName} is missing placeholder.");
    }

    private static void VerifyTownPanelAnchors(RectTransform panelRect)
    {
        foreach (var rect in panelRect.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect == null || rect == panelRect || rect.parent is not RectTransform)
                continue;

            RequireAnchorsInsideParent(rect, rect.gameObject.name);
        }
    }

    private static void RequireAnchorsInsideParent(RectTransform rect, string label)
    {
        const float tolerance = 0.001f;
        Require(rect.anchorMin.x >= -tolerance && rect.anchorMin.y >= -tolerance, $"{label} anchorMin must stay inside its parent.");
        Require(rect.anchorMax.x <= 1f + tolerance && rect.anchorMax.y <= 1f + tolerance, $"{label} anchorMax must stay inside its parent.");
        Require(rect.anchorMax.x >= rect.anchorMin.x && rect.anchorMax.y >= rect.anchorMin.y, $"{label} anchors are inverted.");
    }

    private static UnityEngine.Object RequireSerializedReference(SerializedObject so, string propertyName)
    {
        var property = so.FindProperty(propertyName);
        Require(property != null, $"{propertyName} serialized property is missing.");
        Require(property.objectReferenceValue != null, $"{propertyName} is not assigned.");
        return property.objectReferenceValue;
    }

    private static T RequireSerializedReference<T>(SerializedObject so, string propertyName) where T : UnityEngine.Object
    {
        var value = RequireSerializedReference(so, propertyName) as T;
        Require(value != null, $"{propertyName} must reference {typeof(T).Name}.");
        return value;
    }

    private static UnityEngine.Object RequireSerializedRelativeReference(SerializedProperty parent, string relativePropertyName, string label)
    {
        var property = parent.FindPropertyRelative(relativePropertyName);
        Require(property != null, $"{label} serialized property is missing.");
        Require(property.objectReferenceValue != null, $"{label} is not assigned.");
        return property.objectReferenceValue;
    }

    private static void RequireBuildScene(string scenePath, bool shouldBeEnabled)
    {
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.path != scenePath)
            {
                continue;
            }

            Require(scene.enabled == shouldBeEnabled, $"{scenePath} enabled state should be {shouldBeEnabled}.");
            return;
        }

        Require(!shouldBeEnabled, $"{scenePath} is missing from build settings.");
    }

    private static void RequireAllButtonsHaveFeedback()
    {
        foreach (var button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (button == null || !button.gameObject.scene.IsValid())
                continue;

            Require(button.GetComponent<HomeUIButtonFeedback>() != null, $"{button.gameObject.name} is missing HomeUIButtonFeedback.");
        }
    }

    private static void RequireNoLoadingDependencies()
    {
        foreach (var dependency in AssetDatabase.GetDependencies(ScenePath, true))
            Require(dependency.IndexOf("UI_Lodaing", StringComparison.OrdinalIgnoreCase) < 0, $"Home 1 scene must not depend on Loading UI assets: {dependency}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void EnsureAppearancePreviewController()
    {
        var model = GameObject.Find("Barbarian");
        if (model != null && model.GetComponent<HomeAppearancePreviewController>() == null)
            model.AddComponent<HomeAppearancePreviewController>();
    }

    private struct HomeMenuButtons
    {
        public Button Equipment;
        public Button Inventory;
        public Button Appearance;
        public Button Map;
    }

    private struct BuiltHomeOverlay
    {
        public HomeUiPageNavigator Navigator;
        public HomeInventoryUI InventoryUi;
    }

    private struct RealmBuildBinding
    {
        public string RealmId;
        public string DisplayName;
        public string Description;
        public string RequiredTicket;
        public string SceneName;
        public Button Button;
        public Graphic Highlight;
    }

    private struct ScrollBuildBinding
    {
        public RectTransform Viewport;
        public RectTransform Content;
    }

    private struct TownEntryRowBuildBinding
    {
        public GameObject Root;
        public Button Button;
        public Button JoinButton;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Meta;
        public TextMeshProUGUI SteamBadge;
        public Graphic SelectedFrame;
    }

    private struct MaxPlayerBuildBinding
    {
        public int Value;
        public Button Button;
        public TextMeshProUGUI Label;
        public Graphic Highlight;
    }
}
