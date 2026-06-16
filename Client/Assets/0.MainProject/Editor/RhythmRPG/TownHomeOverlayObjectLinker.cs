#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class TownHomeOverlayObjectLinker
{
    private const string MenuRoot = "RhythmRPG/Editors/UI";
    private const string TownMapScenePath = "Assets/0.MainProject/Scenes/Town/TownMap.unity";
    private const string TownForestScenePath = "Assets/0.MainProject/Scenes/Town/Town_Forest.unity";
    private const string TownHomeControllerName = "TownHomeUiController";
    private const string TownHomeOverlayCanvasName = "Canvas_TownHomeOverlay";
    private const string TownHomeOverlayPrefabPath = "Assets/0.MainProject/Prefabs/UI/Town/PF_Canvas_TownHomeOverlay.prefab";
    private const string TownInventoryName = "TownInventory_UI";
    private const string AppearanceRootName = "UI_Home_Appearance";
    private const string EquipmentDetailRootName = "UI_Home_Equipment_Detail";
    private const string EquipmentSelectedSpritePath = "Assets/Resources/UI/UI_EquimentDetail/Equipment_Selected.png";
    private const string BackButtonSpritePath = "Assets/Resources/UI/BackButton.png";
    private const int TownHomeOverlaySortingOrder = 30000;

    private static readonly string[] TownScenePaths =
    {
        TownMapScenePath,
        TownForestScenePath
    };

    private static readonly Vector2 LayoutSize = new(1280f, 720f);

    [MenuItem(MenuRoot + "/Ensure Town Home Overlay Object Links")]
    public static void EnsureTownHomeOverlayObjectLinks()
    {
        EnsurePrefabObjectLinks();

        ForEachTownScene(scene =>
        {
            EnsureScene(scene);
            Save(scene);
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TownHomeOverlayObjectLinker] Ensured Canvas_TownHomeOverlay scene objects and controller links in Town scenes.");
    }

    [MenuItem(MenuRoot + "/Verify Town Home Overlay Object Links")]
    public static void VerifyTownHomeOverlayObjectLinks()
    {
        VerifyPrefabObjectLinks();
        ForEachTownScene(VerifyScene);
        Debug.Log("[TownHomeOverlayObjectLinker] Town Home overlay object link verification passed.");
    }

    private static void ForEachTownScene(Action<Scene> action)
    {
        foreach (string scenePath in TownScenePaths)
        {
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
    }

    private static void EnsureScene(Scene scene)
    {
        if (!scene.IsValid())
            throw new InvalidOperationException("Town scene is invalid.");

        EnsureEventSystem(scene);

        var overlay = EnsureOverlayObject(scene);
        ConfigureOverlayCanvas(overlay);

        var navigator = overlay.GetComponent<HomeUiPageNavigator>();
        if (navigator == null)
            throw new InvalidOperationException($"{TownHomeOverlayCanvasName} is missing HomeUiPageNavigator in {scene.path}.");

        var controller = EnsureController(scene);
        var cameraDirector = EnsureCameraDirector(scene, controller);
        var townInventory = FindSceneComponentByName<TownInventoryUI>(scene, TownInventoryName);

        BindAppearancePage(overlay.transform, scene.path);
        BindEquipmentDetailPage(overlay.transform, scene.path);
        BindController(controller, overlay, navigator, cameraDirector, townInventory);
        BindNavigator(navigator, controller, cameraDirector, overlay.transform);

        overlay.SetActive(false);
        controller.gameObject.SetActive(true);
        cameraDirector.enabled = false;

        EditorUtility.SetDirty(overlay);
        EditorUtility.SetDirty(navigator);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(cameraDirector);
    }

    private static void EnsurePrefabObjectLinks()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TownHomeOverlayPrefabPath);
        if (prefab == null)
            throw new InvalidOperationException($"Town Home overlay prefab not found: {TownHomeOverlayPrefabPath}");

        var root = PrefabUtility.LoadPrefabContents(TownHomeOverlayPrefabPath);
        try
        {
            ConfigureOverlayCanvas(root);
            BindAppearancePage(root.transform, TownHomeOverlayPrefabPath);
            BindEquipmentDetailPage(root.transform, TownHomeOverlayPrefabPath);

            var navigator = root.GetComponent<HomeUiPageNavigator>();
            if (navigator != null)
                BindNavigatorAppearance(navigator, root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, TownHomeOverlayPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject EnsureOverlayObject(Scene scene)
    {
        var overlay = FindPreferredOverlay(scene);
        if (overlay != null)
        {
            overlay.name = TownHomeOverlayCanvasName;
            if (overlay.transform.parent != null)
                overlay.transform.SetParent(null, true);

            return overlay;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TownHomeOverlayPrefabPath);
        if (prefab == null)
            throw new InvalidOperationException($"Town Home overlay prefab not found: {TownHomeOverlayPrefabPath}");

        overlay = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (overlay == null)
            throw new InvalidOperationException($"Failed to instantiate Town Home overlay prefab: {TownHomeOverlayPrefabPath}");

        overlay.name = TownHomeOverlayCanvasName;
        overlay.SetActive(false);
        EditorUtility.SetDirty(overlay);
        return overlay;
    }

    private static GameObject FindPreferredOverlay(Scene scene)
    {
        var candidates = FindSceneObjects(scene, TownHomeOverlayCanvasName);
        var linkedCandidate = FindPrefabLinkedObject(scene, TownHomeOverlayPrefabPath);
        if (linkedCandidate != null && !candidates.Contains(linkedCandidate))
            candidates.Add(linkedCandidate);

        if (candidates.Count == 0)
            return null;

        candidates.Sort(CompareHierarchyOrder);
        foreach (var candidate in candidates)
        {
            if (IsConnectedToPrefab(candidate, TownHomeOverlayPrefabPath))
                return candidate;
        }

        return candidates[0];
    }

    private static void ConfigureOverlayCanvas(GameObject overlay)
    {
        var canvas = overlay.GetComponent<Canvas>();
        if (canvas == null)
            canvas = overlay.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.overrideSorting = true;
        canvas.sortingOrder = TownHomeOverlaySortingOrder;

        var scaler = overlay.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = overlay.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = LayoutSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 12f;
        scaler.referencePixelsPerUnit = 100f;

        if (overlay.GetComponent<GraphicRaycaster>() == null)
            overlay.AddComponent<GraphicRaycaster>();

        if (overlay.transform is RectTransform rect)
        {
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.sizeDelta = LayoutSize;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            overlay.layer = uiLayer;
    }

    private static TownHomeUiController EnsureController(Scene scene)
    {
        var controller = FindSceneComponent<TownHomeUiController>(scene);
        if (controller != null)
        {
            controller.gameObject.name = TownHomeControllerName;
            return controller;
        }

        var controllerGo = new GameObject(TownHomeControllerName);
        SceneManager.MoveGameObjectToScene(controllerGo, scene);
        return controllerGo.AddComponent<TownHomeUiController>();
    }

    private static HomeSceneCameraDirector EnsureCameraDirector(Scene scene, TownHomeUiController controller)
    {
        var cameraDirector = controller.GetComponent<HomeSceneCameraDirector>();
        if (cameraDirector == null)
            cameraDirector = controller.gameObject.AddComponent<HomeSceneCameraDirector>();

        var so = new SerializedObject(cameraDirector);
        SetObjectReference(so, "_camera", FindMainCamera(scene));
        SetObjectReference(so, "_modelRoot", null);
        SetFloat(so, "_blendSpeed", 2.7f);
        SetFloat(so, "_modelScreenLeftOffset", 0f);
        SetFloat(so, "_appearanceDistance", 5.2f);
        SetFloat(so, "_appearanceHeightOffset", 0.48f);
        SetFloat(so, "_presentationCameraHeightOffset", 0.65f);
        SetBool(so, "_useModelFacingForPresentation", true);
        SetBool(so, "_invertModelFacingForPresentation", true);
        SetBool(so, "_useCurrentCameraOppositeForPresentation", true);
        SetBool(so, "_useStagedPresentationEnter", true);
        SetFloat(so, "_entryApproachDuration", 0.32f);
        SetFloat(so, "_entryRotateDuration", 0.78f);
        SetFloat(so, "_entryApproachDistanceMultiplier", 0.78f);
        so.ApplyModifiedPropertiesWithoutUndo();

        return cameraDirector;
    }

    private static void BindController(
        TownHomeUiController controller,
        GameObject overlay,
        HomeUiPageNavigator navigator,
        HomeSceneCameraDirector cameraDirector,
        TownInventoryUI townInventory)
    {
        var so = new SerializedObject(controller);
        SetObjectReference(so, "_root", overlay);
        SetObjectReference(so, "_navigator", navigator);
        SetObjectReference(so, "_cameraDirector", cameraDirector);
        SetObjectReference(so, "_townInventoryUi", townInventory);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BindNavigator(
        HomeUiPageNavigator navigator,
        TownHomeUiController controller,
        HomeSceneCameraDirector cameraDirector,
        Transform overlayRoot)
    {
        var so = new SerializedObject(navigator);
        SetObjectReference(so, "_townHomeController", controller);
        SetObjectReference(so, "_cameraDirector", cameraDirector);
        BindNavigatorAppearance(so, overlayRoot);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BindNavigatorAppearance(HomeUiPageNavigator navigator, Transform overlayRoot)
    {
        var so = new SerializedObject(navigator);
        BindNavigatorAppearance(so, overlayRoot);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(navigator);
    }

    private static void BindNavigatorAppearance(SerializedObject navigatorSo, Transform overlayRoot)
    {
        if (overlayRoot == null)
            return;

        var appearanceRoot = FindChild(overlayRoot, AppearanceRootName);
        var detailRoot = FindChild(overlayRoot, EquipmentDetailRootName);
        SetObjectReference(navigatorSo, "_appearanceRoot", appearanceRoot != null ? appearanceRoot.gameObject : null);
        SetObjectReference(navigatorSo, "_detailRoot", detailRoot != null ? detailRoot.gameObject : null);
        SetObjectReference(navigatorSo, "_appearanceButton", FindChildComponent<Button>(overlayRoot, "Button_Appearance"));
        SetFloat(navigatorSo, "_appearancePresentationScreenLeftOffset", 2.35f);
    }

    private static void BindAppearancePage(Transform overlayRoot, string context)
    {
        var appearanceRoot = FindChild(overlayRoot, AppearanceRootName);
        if (appearanceRoot == null)
            throw new InvalidOperationException($"{AppearanceRootName} is missing in {context}.");

        var appearancePage = appearanceRoot.GetComponent<HomeAppearancePageUI>();
        if (appearancePage == null)
            appearancePage = appearanceRoot.gameObject.AddComponent<HomeAppearancePageUI>();

        var so = new SerializedObject(appearancePage);
        SetObjectReference(so, "_currentText", FindChildComponent<TextMeshProUGUI>(appearanceRoot, "CurrentText"));
        SetObjectReference(so, "_appliedText", FindChildComponent<TextMeshProUGUI>(appearanceRoot, "AppliedText"));
        SetObjectReference(so, "_statusText", FindChildComponent<TextMeshProUGUI>(appearanceRoot, "StatusText"));
        SetObjectReference(so, "_applyButton", FindChildComponent<Button>(appearanceRoot, "Button_ApplyAppearance"));
        SetBool(so, "_useManualObjectLayout", true);
        SetBool(so, "_autoCreateMissingPortraits", false);

        var optionsProperty = so.FindProperty("_options");
        if (optionsProperty == null)
            throw new InvalidOperationException("HomeAppearancePageUI is missing _options serialized property.");

        var bindings = BuildAppearanceOptionBindings(appearanceRoot);
        if (bindings.Count == 0)
            throw new InvalidOperationException($"{AppearanceRootName} has no AppearanceOption_* buttons in {context}.");

        optionsProperty.arraySize = bindings.Count;
        for (int i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            var element = optionsProperty.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("AppearanceId").intValue = binding.AppearanceId;
            element.FindPropertyRelative("Button").objectReferenceValue = binding.Button;
            element.FindPropertyRelative("Label").objectReferenceValue = binding.Label;
            element.FindPropertyRelative("Portrait").objectReferenceValue = binding.Portrait;
            element.FindPropertyRelative("Highlight").objectReferenceValue = binding.Highlight;
            element.FindPropertyRelative("EquippedMark").objectReferenceValue = binding.EquippedMark;
            element.FindPropertyRelative("LockedMark").objectReferenceValue = binding.LockedMark;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(appearancePage);
        EditorUtility.SetDirty(appearanceRoot.gameObject);
    }

    private static List<AppearanceOptionBinding> BuildAppearanceOptionBindings(Transform appearanceRoot)
    {
        var bindings = new List<AppearanceOptionBinding>();
        foreach (var option in AppearanceCatalog.Options)
        {
            if (!AppearanceCatalog.IsSelectableAppearanceId(option.Id))
                continue;

            var optionRoot = FindChild(appearanceRoot, $"AppearanceOption_{option.Id}");
            if (optionRoot == null)
                continue;

            var button = optionRoot.GetComponent<Button>();
            if (button == null)
                continue;

            bindings.Add(new AppearanceOptionBinding(
                option.Id,
                button,
                FindChildComponent<TextMeshProUGUI>(optionRoot, "Label"),
                FindChildComponent<RawImage>(optionRoot, "Portrait"),
                FindChildComponent<Graphic>(optionRoot, "SelectedHighlight"),
                FindChildGameObject(optionRoot, "EquippedMark"),
                FindChildGameObject(optionRoot, "LockedMark")));
        }

        return bindings;
    }

    private static void BindEquipmentDetailPage(Transform overlayRoot, string context)
    {
        var detailRoot = FindChild(overlayRoot, EquipmentDetailRootName);
        if (detailRoot == null)
            throw new InvalidOperationException($"{EquipmentDetailRootName} is missing in {context}.");

        var popup = detailRoot.GetComponent<HomeEquipPopupUI>();
        if (popup == null)
            popup = detailRoot.gameObject.AddComponent<HomeEquipPopupUI>();

        var content = FindDirectChild(detailRoot, "Content") ?? FindChild(detailRoot, "Content");
        var itemPrefab = FindChild(detailRoot, "Prefab_PopupItem");
        var title = FindDirectChildComponent<TextMeshProUGUI>(detailRoot, "Title") ?? FindChildComponent<TextMeshProUGUI>(detailRoot, "Title");
        var closeButton = FindDirectChildComponent<Button>(detailRoot, "CloseBtn") ?? FindChildComponent<Button>(detailRoot, "CloseBtn");
        ApplyButtonSprite(closeButton, BackButtonSpritePath);

        var so = new SerializedObject(popup);
        SetObjectReference(so, "_content", content);
        SetObjectReference(so, "_itemPrefab", itemPrefab != null ? itemPrefab.gameObject : null);
        SetObjectReference(so, "_title", title);
        SetObjectReference(so, "_closeBtn", closeButton);
        SetBool(so, "_useHomeDetailResourceLayout", true);
        SetBool(so, "_useManualObjectLayout", true);
        so.ApplyModifiedPropertiesWithoutUndo();

        if (itemPrefab != null)
            BindEquipmentDetailItemPrefab(itemPrefab);

        EditorUtility.SetDirty(popup);
        EditorUtility.SetDirty(detailRoot.gameObject);
    }

    private static void BindEquipmentDetailItemPrefab(Transform itemRoot)
    {
        var itemUi = itemRoot.GetComponent<HomeEquipPopupItemUI>();
        if (itemUi == null)
            itemUi = itemRoot.gameObject.AddComponent<HomeEquipPopupItemUI>();

        var selectionOutline = EnsureSelectionOutlineObject(itemRoot);
        var so = new SerializedObject(itemUi);
        SetObjectReference(so, "_icon", FindChildComponent<Image>(itemRoot, "Icon"));
        SetObjectReference(so, "_iconFrame", FindChildComponent<Image>(itemRoot, "IconFrame"));
        SetObjectReference(so, "_nameText", FindChildComponent<TextMeshProUGUI>(itemRoot, "NameText"));
        SetObjectReference(so, "_levelText", FindChildComponent<TextMeshProUGUI>(itemRoot, "LevelText"));
        SetObjectReference(so, "_btn", itemRoot.GetComponent<Button>() ?? itemRoot.GetComponentInChildren<Button>(true));
        SetObjectReference(so, "_equippedMark", FindChildGameObject(itemRoot, "EquippedMark"));
        SetObjectReference(so, "_selectionOutline", selectionOutline);
        SetBool(so, "_useManualObjectLayout", true);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(itemUi);
        EditorUtility.SetDirty(itemRoot.gameObject);
    }

    private static Image EnsureSelectionOutlineObject(Transform itemRoot)
    {
        if (itemRoot == null)
            return null;

        var selectedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EquipmentSelectedSpritePath);
        if (selectedSprite == null)
            throw new InvalidOperationException($"Selection outline sprite not found: {EquipmentSelectedSpritePath}");

        var outline = FindDirectChildComponent<Image>(itemRoot, "SelectionOutline")
            ?? FindChildComponent<Image>(itemRoot, "SelectionOutline");
        bool created = false;
        if (outline == null)
        {
            var outlineGo = new GameObject("SelectionOutline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            outlineGo.transform.SetParent(itemRoot, false);
            outline = outlineGo.GetComponent<Image>();
            created = true;
        }

        outline.sprite = selectedSprite;
        outline.type = Image.Type.Simple;
        outline.preserveAspect = false;
        outline.raycastTarget = false;
        outline.color = Color.white;
        outline.gameObject.SetActive(false);

        if (created && outline.transform is RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
        }

        outline.transform.SetAsLastSibling();
        EditorUtility.SetDirty(outline);
        return outline;
    }

    private static void ApplyButtonSprite(Button button, string spritePath)
    {
        if (button == null)
            return;

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
            throw new InvalidOperationException($"Button sprite not found: {spritePath}");

        var image = button.GetComponent<Image>();
        if (image == null)
            image = button.gameObject.AddComponent<Image>();

        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;
        image.raycastTarget = true;
        button.targetGraphic = image;
        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(image);
    }

    private static void EnsureEventSystem(Scene scene)
    {
        var eventSystem = FindSceneComponent<EventSystem>(scene);
        if (eventSystem == null)
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystemGo, scene);
            eventSystem = eventSystemGo.GetComponent<EventSystem>();
            EditorUtility.SetDirty(eventSystemGo);
        }

        eventSystem.gameObject.SetActive(true);
        eventSystem.enabled = true;

        var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        inputModule.enabled = true;

        var standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (standaloneModule != null)
            standaloneModule.enabled = false;

        EditorUtility.SetDirty(eventSystem);
        EditorUtility.SetDirty(inputModule);
    }

    private static void VerifyScene(Scene scene)
    {
        var overlay = FindPreferredOverlay(scene);
        Require(overlay != null, $"{TownHomeOverlayCanvasName} is missing in {scene.path}.");
        Require(overlay.transform.parent == null, $"{TownHomeOverlayCanvasName} must be a scene-root object in {scene.path}.");
        Require(!overlay.activeSelf, $"{TownHomeOverlayCanvasName} must be inactive by default in {scene.path}.");

        var canvas = overlay.GetComponent<Canvas>();
        Require(canvas != null, $"{TownHomeOverlayCanvasName} is missing Canvas in {scene.path}.");
        Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay, $"{TownHomeOverlayCanvasName} must be Screen Space Overlay in {scene.path}.");
        Require(canvas.sortingOrder == TownHomeOverlaySortingOrder, $"{TownHomeOverlayCanvasName} sorting order must be {TownHomeOverlaySortingOrder} in {scene.path}.");
        Require(overlay.GetComponent<GraphicRaycaster>() != null, $"{TownHomeOverlayCanvasName} is missing GraphicRaycaster in {scene.path}.");

        var navigator = overlay.GetComponent<HomeUiPageNavigator>();
        Require(navigator != null, $"{TownHomeOverlayCanvasName} is missing HomeUiPageNavigator in {scene.path}.");

        var controller = FindSceneComponent<TownHomeUiController>(scene);
        Require(controller != null, $"{TownHomeControllerName} is missing in {scene.path}.");
        Require(controller.gameObject.activeSelf, $"{TownHomeControllerName} must stay active in {scene.path}.");

        var cameraDirector = controller.GetComponent<HomeSceneCameraDirector>();
        Require(cameraDirector != null, $"{TownHomeControllerName} is missing HomeSceneCameraDirector in {scene.path}.");
        Require(!cameraDirector.enabled, "Town Home camera director must be disabled by default.");

        var controllerSo = new SerializedObject(controller);
        Require(GetObjectReference(controllerSo, "_root") == overlay, $"{TownHomeControllerName} _root must reference {TownHomeOverlayCanvasName} in {scene.path}.");
        Require(GetObjectReference(controllerSo, "_navigator") == navigator, $"{TownHomeControllerName} _navigator must reference the overlay navigator in {scene.path}.");
        Require(GetObjectReference(controllerSo, "_cameraDirector") == cameraDirector, $"{TownHomeControllerName} _cameraDirector must reference its camera director in {scene.path}.");

        var navigatorSo = new SerializedObject(navigator);
        Require(GetObjectReference(navigatorSo, "_townHomeController") == controller, $"{TownHomeOverlayCanvasName} navigator must reference {TownHomeControllerName} in {scene.path}.");
        Require(GetObjectReference(navigatorSo, "_cameraDirector") == cameraDirector, $"{TownHomeOverlayCanvasName} navigator must reference the Town camera director in {scene.path}.");
        VerifyAppearancePage(overlay.transform, navigatorSo, scene.path);
        VerifyEquipmentDetailPage(overlay.transform, navigatorSo, scene.path);
    }

    private static void VerifyPrefabObjectLinks()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TownHomeOverlayPrefabPath);
        if (prefab == null)
            throw new InvalidOperationException($"Town Home overlay prefab not found: {TownHomeOverlayPrefabPath}");

        var root = PrefabUtility.LoadPrefabContents(TownHomeOverlayPrefabPath);
        try
        {
            var navigator = root.GetComponent<HomeUiPageNavigator>();
            Require(navigator != null, $"{TownHomeOverlayPrefabPath} is missing HomeUiPageNavigator.");
            var navigatorSo = new SerializedObject(navigator);
            VerifyAppearancePage(root.transform, navigatorSo, TownHomeOverlayPrefabPath);
            VerifyEquipmentDetailPage(root.transform, navigatorSo, TownHomeOverlayPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void VerifyAppearancePage(Transform overlayRoot, SerializedObject navigatorSo, string context)
    {
        var appearanceRoot = FindChild(overlayRoot, AppearanceRootName);
        Require(appearanceRoot != null, $"{AppearanceRootName} is missing in {context}.");

        var appearancePage = appearanceRoot.GetComponent<HomeAppearancePageUI>();
        Require(appearancePage != null, $"{AppearanceRootName} is missing HomeAppearancePageUI in {context}.");

        var appearanceSo = new SerializedObject(appearancePage);
        Require(GetObjectReference(navigatorSo, "_appearanceRoot") == appearanceRoot.gameObject, "Navigator _appearanceRoot must reference UI_Home_Appearance.");
        Require(GetObjectReference(navigatorSo, "_appearanceButton") != null, "Navigator _appearanceButton must reference Button_Appearance.");
        Require(GetObjectReference(appearanceSo, "_currentText") != null, "Appearance page _currentText must be linked.");
        Require(GetObjectReference(appearanceSo, "_appliedText") != null, "Appearance page _appliedText must be linked.");
        Require(GetObjectReference(appearanceSo, "_applyButton") != null, "Appearance page _applyButton must be linked.");
        Require(GetBool(appearanceSo, "_useManualObjectLayout"), "Appearance page must keep manual object layout enabled.");
        Require(!GetBool(appearanceSo, "_autoCreateMissingPortraits"), "Appearance page must not auto-create missing portraits.");

        var optionsProperty = appearanceSo.FindProperty("_options");
        Require(optionsProperty != null && optionsProperty.arraySize > 0, "Appearance page must have option object bindings.");
        for (int i = 0; i < optionsProperty.arraySize; i++)
        {
            var element = optionsProperty.GetArrayElementAtIndex(i);
            Require(element.FindPropertyRelative("AppearanceId").intValue > 0, "Appearance option binding must have a selectable id.");
            Require(element.FindPropertyRelative("Button").objectReferenceValue != null, "Appearance option binding is missing Button.");
        }
    }

    private static void VerifyEquipmentDetailPage(Transform overlayRoot, SerializedObject navigatorSo, string context)
    {
        var detailRoot = FindChild(overlayRoot, EquipmentDetailRootName);
        Require(detailRoot != null, $"{EquipmentDetailRootName} is missing in {context}.");
        Require(GetObjectReference(navigatorSo, "_detailRoot") == detailRoot.gameObject, "Navigator _detailRoot must reference UI_Home_Equipment_Detail.");

        var popup = detailRoot.GetComponent<HomeEquipPopupUI>();
        Require(popup != null, $"{EquipmentDetailRootName} is missing HomeEquipPopupUI in {context}.");

        var popupSo = new SerializedObject(popup);
        Require(GetObjectReference(popupSo, "_content") != null, "Equipment Detail _content must be linked.");
        Require(GetObjectReference(popupSo, "_itemPrefab") != null, "Equipment Detail _itemPrefab must be linked.");
        Require(GetObjectReference(popupSo, "_closeBtn") != null, "Equipment Detail _closeBtn must be linked.");
        Require(GetBool(popupSo, "_useHomeDetailResourceLayout"), "Equipment Detail must keep resource layout mode enabled.");
        Require(GetBool(popupSo, "_useManualObjectLayout"), "Equipment Detail must keep manual object layout enabled.");

        var itemPrefab = GetObjectReference(popupSo, "_itemPrefab") as GameObject;
        var itemUi = itemPrefab != null ? itemPrefab.GetComponent<HomeEquipPopupItemUI>() : null;
        Require(itemUi != null, "Equipment Detail item prefab must have HomeEquipPopupItemUI.");

        var itemSo = new SerializedObject(itemUi);
        Require(GetBool(itemSo, "_useManualObjectLayout"), "Equipment Detail item prefab must keep manual object layout enabled.");
        Require(GetObjectReference(itemSo, "_btn") != null, "Equipment Detail item prefab _btn must be linked.");
        Require(GetObjectReference(itemSo, "_selectionOutline") != null, "Equipment Detail item prefab _selectionOutline must be linked.");
    }

    private static Camera FindMainCamera(Scene scene)
    {
        Camera fallback = null;
        foreach (var camera in FindSceneComponents<Camera>(scene))
        {
            if (camera == null)
                continue;

            fallback ??= camera;
            if (camera.CompareTag("MainCamera"))
                return camera;
        }

        return fallback;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        foreach (var component in FindSceneComponents<T>(scene))
        {
            if (component != null)
                return component;
        }

        return null;
    }

    private static T FindSceneComponentByName<T>(Scene scene, string objectName) where T : Component
    {
        foreach (var component in FindSceneComponents<T>(scene))
        {
            if (component != null && component.gameObject.name == objectName)
                return component;
        }

        return null;
    }

    private static List<T> FindSceneComponents<T>(Scene scene) where T : Component
    {
        var matches = new List<T>();
        foreach (var component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component == null || !component.gameObject.scene.IsValid())
                continue;

            if (component.gameObject.scene == scene)
                matches.Add(component);
        }

        matches.Sort((left, right) => CompareHierarchyOrder(left.gameObject, right.gameObject));
        return matches;
    }

    private static List<GameObject> FindSceneObjects(Scene scene, string objectName)
    {
        var matches = new List<GameObject>();
        foreach (var gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (gameObject == null || !gameObject.scene.IsValid())
                continue;

            if (gameObject.scene == scene && string.Equals(gameObject.name, objectName, StringComparison.Ordinal))
                matches.Add(gameObject);
        }

        matches.Sort(CompareHierarchyOrder);
        return matches;
    }

    private static GameObject FindPrefabLinkedObject(Scene scene, string prefabPath)
    {
        foreach (var gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (gameObject == null || !gameObject.scene.IsValid() || gameObject.scene != scene)
                continue;

            if (PrefabUtility.GetNearestPrefabInstanceRoot(gameObject) != gameObject)
                continue;

            if (IsConnectedToPrefab(gameObject, prefabPath))
                return gameObject;
        }

        return null;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    private static Transform FindDirectChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    private static T FindChildComponent<T>(Transform root, string childName) where T : Component
    {
        var child = FindChild(root, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static T FindDirectChildComponent<T>(Transform root, string childName) where T : Component
    {
        var child = FindDirectChild(root, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static GameObject FindChildGameObject(Transform root, string childName)
    {
        var child = FindChild(root, childName);
        return child != null ? child.gameObject : null;
    }

    private static bool IsConnectedToPrefab(GameObject root, string prefabPath)
    {
        if (root == null)
            return false;

        if (PrefabUtility.GetNearestPrefabInstanceRoot(root) != root)
            return false;

        return string.Equals(
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root),
            prefabPath,
            StringComparison.Ordinal);
    }

    private static int CompareHierarchyOrder(GameObject left, GameObject right)
    {
        int leftDepth = GetDepth(left.transform);
        int rightDepth = GetDepth(right.transform);
        if (leftDepth != rightDepth)
            return leftDepth.CompareTo(rightDepth);

        return left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
    }

    private static int GetDepth(Transform transform)
    {
        int depth = 0;
        while (transform.parent != null)
        {
            depth++;
            transform = transform.parent;
        }

        return depth;
    }

    private static void SetObjectReference(SerializedObject so, string propertyName, UnityEngine.Object value)
    {
        var property = so.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static UnityEngine.Object GetObjectReference(SerializedObject so, string propertyName)
    {
        var property = so.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue : null;
    }

    private static void SetFloat(SerializedObject so, string propertyName, float value)
    {
        var property = so.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        var property = so.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static bool GetBool(SerializedObject so, string propertyName)
    {
        var property = so.FindProperty(propertyName);
        return property != null && property.boolValue;
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

    private readonly struct AppearanceOptionBinding
    {
        public readonly int AppearanceId;
        public readonly Button Button;
        public readonly TextMeshProUGUI Label;
        public readonly RawImage Portrait;
        public readonly Graphic Highlight;
        public readonly GameObject EquippedMark;
        public readonly GameObject LockedMark;

        public AppearanceOptionBinding(
            int appearanceId,
            Button button,
            TextMeshProUGUI label,
            RawImage portrait,
            Graphic highlight,
            GameObject equippedMark,
            GameObject lockedMark)
        {
            AppearanceId = appearanceId;
            Button = button;
            Label = label;
            Portrait = portrait;
            Highlight = highlight;
            EquippedMark = equippedMark;
            LockedMark = lockedMark;
        }
    }
}
#endif
