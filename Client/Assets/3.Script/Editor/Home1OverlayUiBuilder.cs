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

public static class Home1OverlayUiBuilder
{
    private const string ScenePath = "Assets/0.MainProject/Scenes/Home 1.unity";
    private const string UiResourceSource = "../Resource/UI";
    private const string UiResourceTarget = "Assets/Resources/UI";
    private const string HomeAtlasPath = "Assets/Resources/UI/UI_Home_Interface/UI_Home_Interface.png";
    private const string EquipmentAtlasPath = "Assets/Resources/UI/UI_Home_Equipment/UI_Home_Equipment_Set.png";
    private const string DetailAtlasPath = "Assets/Resources/UI/UI_Home_Equipment_Detail/UI_Home_Equipment_Detail.png";
    private const string GridIconFramePath = "Assets/Resources/UI/UI_Home_Equipment_Detail/UI_equipment_icon_frame_grid.png";
    private const string SlotIconFramePath = "Assets/Resources/UI/UI_Home_Equipment_Detail/UI_equipment_icon_frame_slot.png";

    private static readonly Vector2 HomeSize = new(1280f, 720f);
    private static readonly Vector2 EquipmentSize = new(1672f, 941f);
    private static readonly Vector2 DetailSize = new(1280f, 720f);
    private static readonly Color ParchmentText = new(0.10f, 0.22f, 0.20f, 1f);
    private static readonly Color ParchmentMutedText = new(0.30f, 0.24f, 0.18f, 1f);

    [MenuItem("RhythmRPG/Editors/UI/Rebuild Home1 Overlay UI")]
    public static void Build()
    {
        EnsureUiResources();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var cameraState = CaptureCameraState();
        RemoveExistingUiAndCameras();
        CreateMainCamera(cameraState);
        EnsureEventSystem();

        var canvas = CreateOverlayCanvas();
        var homeRoot = CreatePageRoot(canvas.transform, "UI_Home_Interface", true);
        var equipmentRoot = CreatePageRoot(canvas.transform, "UI_Home_Equipment", false);
        var detailRoot = CreatePageRoot(canvas.transform, "UI_Home_Equipment_Detail", false);

        var equipmentButton = BuildHomeInterface(homeRoot);
        var backButton = BuildEquipmentScreen(equipmentRoot, detailRoot, out var inventoryUi);
        BuildDetailScreen(detailRoot);

        var navigator = canvas.gameObject.AddComponent<HomeUiPageNavigator>();
        var navSo = new SerializedObject(navigator);
        navSo.FindProperty("_homeRoot").objectReferenceValue = homeRoot.gameObject;
        navSo.FindProperty("_equipmentRoot").objectReferenceValue = equipmentRoot.gameObject;
        navSo.FindProperty("_detailRoot").objectReferenceValue = detailRoot.gameObject;
        navSo.FindProperty("_equipmentButton").objectReferenceValue = equipmentButton;
        navSo.FindProperty("_equipmentBackButton").objectReferenceValue = backButton;
        navSo.ApplyModifiedPropertiesWithoutUndo();

        if (inventoryUi != null)
            EditorUtility.SetDirty(inventoryUi);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Home1OverlayUiBuilder] Rebuilt Home 1 with Screen Space - Overlay resource UI.");
    }

    [MenuItem("RhythmRPG/Editors/UI/Verify Home1 Flow")]
    public static void VerifyFlow()
    {
        Exception caught = null;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        try
        {
            Require(SceneNames.Home == "Home 1", $"SceneNames.Home must be 'Home 1' but was '{SceneNames.Home}'.");
            RequireBuildScene("Assets/0.MainProject/Scenes/Home 1.unity", true);
            RequireBuildScene("Assets/0.MainProject/Scenes/Home.unity", false);

            var canvas = RequireComponent<Canvas>("Canvas_Home1_Overlay");
            Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay, "Canvas_Home1_Overlay must be Screen Space - Overlay.");
            Require(RequireComponent<Image>("DimOverlay").color.a > 0.2f, "Detail dim overlay alpha is too low.");

            var navigator = canvas.GetComponent<HomeUiPageNavigator>();
            Require(navigator != null, "Canvas_Home1_Overlay is missing HomeUiPageNavigator.");

            var home = RequireSceneObject("UI_Home_Interface");
            var equipment = RequireSceneObject("UI_Home_Equipment");
            var detail = RequireSceneObject("UI_Home_Equipment_Detail");
            RequireDirectChild("UI_Home_Interface", "00_Background");
            RequireDirectChild("UI_Home_Interface", "01_Header");
            RequireDirectChild("UI_Home_Interface", "02_LeftContent");
            RequireDirectChild("UI_Home_Interface", "03_RightMenu");
            RequireDirectChild("UI_Home_Interface", "04_BottomNavigation");
            RequireDirectChild("UI_Home_Interface", "99_Hotspots");
            RequireDirectChild("UI_Home_Equipment", "00_Background");
            RequireDirectChild("UI_Home_Equipment", "01_Header");
            RequireDirectChild("UI_Home_Equipment", "02_LeftSlots");
            RequireDirectChild("UI_Home_Equipment", "03_RightSlots");
            RequireDirectChild("UI_Home_Equipment", "04_LoadoutSummary");
            RequireDirectChild("UI_Home_Equipment", "05_SelectedItem");
            RequireDirectChild("UI_Home_Equipment", "06_Actions");
            RequireDirectChild("UI_Home_Equipment_Detail", "Content");
            RequireDirectChild("Content", "ResourceChrome");
            RequireDirectChild("ResourceChrome", "01_LeftPanelChrome");
            RequireDirectChild("ResourceChrome", "02_RightPanelChrome");
            var inventory = equipment.GetComponent<HomeInventoryUI>();
            Require(inventory != null, "UI_Home_Equipment is missing HomeInventoryUI.");
            Require(detail.GetComponent<HomeEquipPopupUI>() != null, "UI_Home_Equipment_Detail is missing HomeEquipPopupUI.");

            navigator.ShowHome();
            Require(home.activeSelf, "Home root should be active after ShowHome.");
            Require(!equipment.activeSelf, "Equipment root should be inactive after ShowHome.");
            Require(!detail.activeSelf, "Detail root should be inactive after ShowHome.");

            navigator.ShowEquipment();
            Require(!home.activeSelf, "Home root should be inactive after ShowEquipment.");
            Require(equipment.activeSelf, "Equipment root should be active after ShowEquipment.");
            Require(!detail.activeSelf, "Detail root should be inactive before slot selection.");

            inventory.OnSlotClicked(EquipmentSlot.Weapon);
            Require(detail.activeSelf, "Detail root should open when an equipment slot is selected.");
            RequireComponent<RectMask2D>("ItemListViewport");
            RequireAllButtonsHaveFeedback();
            RequireNoSceneDependency("_" + "exam" + "ple");
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        finally
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        if (caught != null)
            throw caught;

        Debug.Log("[Home1OverlayUiBuilder] Home1 flow verification passed.");
    }

    private static void EnsureUiResources()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        var sourceRoot = Path.GetFullPath(Path.Combine(projectRoot, UiResourceSource));
        var targetRoot = Path.Combine(Application.dataPath, "Resources", "UI");

        var relativeFiles = new[]
        {
            "UI_Home_Interface/UI_Home_Interface.png",
            "UI_Home_Equipment/UI_Home_Equipment_Set.png",
            "UI_Home_Equipment_Detail/UI_Home_Equipment_Detail.png"
        };

        foreach (var relativeFile in relativeFiles)
        {
            var source = Path.Combine(sourceRoot, relativeFile.Replace("/", Path.DirectorySeparatorChar.ToString()));
            var target = Path.Combine(targetRoot, relativeFile.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (!File.Exists(source))
            {
                Debug.LogError($"[Home1OverlayUiBuilder] Missing UI source resource: {source}");
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!File.Exists(target) || new FileInfo(source).Length != new FileInfo(target).Length)
                File.Copy(source, target, true);

            var assetPath = $"{UiResourceTarget}/{relativeFile}";
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }
    }

    private static void RequireBuildScene(string scenePath, bool shouldBeEnabled)
    {
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.path != scenePath)
                continue;

            Require(scene.enabled == shouldBeEnabled, $"{scenePath} enabled state should be {shouldBeEnabled}.");
            return;
        }

        Require(!shouldBeEnabled, $"{scenePath} is missing from build settings.");
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

    private static void RequireDirectChild(string parentName, string childName)
    {
        var parent = RequireSceneObject(parentName);
        Require(parent.transform.Find(childName) != null, $"{parentName} is missing child group {childName}.");
    }

    private static void RequireNoSceneDependency(string token)
    {
        foreach (var dependency in AssetDatabase.GetDependencies(ScenePath, true))
            Require(!dependency.Contains(token), $"Scene must not depend on {token}: {dependency}");
    }

    private static void RequireAllButtonsHaveFeedback()
    {
        foreach (var button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (button == null || !button.gameObject.scene.IsValid())
                continue;

            Require(
                button.GetComponent<HomeUIButtonFeedback>() != null,
                $"{button.gameObject.name} is missing HomeUIButtonFeedback.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static CameraState CaptureCameraState()
    {
        var camera = UnityEngine.Object.FindObjectOfType<Camera>(true);
        if (camera == null)
            return CameraState.Default;

        return new CameraState
        {
            Position = camera.transform.position,
            Rotation = camera.transform.rotation,
            FieldOfView = camera.fieldOfView,
            NearClip = camera.nearClipPlane,
            FarClip = camera.farClipPlane,
            ClearFlags = camera.clearFlags,
            Background = camera.backgroundColor
        };
    }

    private static void RemoveExistingUiAndCameras()
    {
        foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
            UnityEngine.Object.DestroyImmediate(canvas.gameObject);

        foreach (var eventSystem in UnityEngine.Object.FindObjectsOfType<EventSystem>(true))
            UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);

        foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>(true))
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
    }

    private static void CreateMainCamera(CameraState state)
    {
        var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        cameraGo.transform.SetPositionAndRotation(state.Position, state.Rotation);

        var camera = cameraGo.GetComponent<Camera>();
        camera.fieldOfView = state.FieldOfView;
        camera.nearClipPlane = state.NearClip;
        camera.farClipPlane = state.FarClip;
        camera.clearFlags = state.ClearFlags;
        camera.backgroundColor = state.Background;
    }

    private static void EnsureEventSystem()
    {
        var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemGo.transform.SetAsLastSibling();
    }

    private static Canvas CreateOverlayCanvas()
    {
        var canvasGo = new GameObject("Canvas_Home1_Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = HomeSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
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

    private static RectTransform CreateGroup(Transform parent, string name)
    {
        var rect = CreateRect(parent, name);
        Stretch(rect);
        return rect;
    }

    private static Button BuildHomeInterface(RectTransform root)
    {
        var backgroundGroup = CreateGroup(root, "00_Background");
        var headerGroup = CreateGroup(root, "01_Header");
        var profileGroup = CreateGroup(headerGroup, "Profile");
        var currencyGroup = CreateGroup(headerGroup, "Currencies");
        var utilityGroup = CreateGroup(headerGroup, "UtilityButtons");
        var leftContentGroup = CreateGroup(root, "02_LeftContent");
        var dungeonGroup = CreateGroup(leftContentGroup, "Dungeon");
        var dailyQuestGroup = CreateGroup(leftContentGroup, "DailyQuests");
        var rightMenuGroup = CreateGroup(root, "03_RightMenu");
        var primaryCardsGroup = CreateGroup(rightMenuGroup, "PrimaryCards");
        var secondaryCardsGroup = CreateGroup(rightMenuGroup, "SecondaryCards");
        var startGroup = CreateGroup(rightMenuGroup, "StartExpedition");
        var bottomNavGroup = CreateGroup(root, "04_BottomNavigation");
        var hotspotsGroup = CreateGroup(root, "99_Hotspots");

        CreateSolidBackground(backgroundGroup, "Background", new Color(0f, 0f, 0f, 0f));

        CreateAtlasImage(profileGroup, "ProfileCrest", HomeAtlasPath, new Rect(15f, 12f, 136f, 143f), new Rect(16f, 12f, 92f, 98f), HomeSize);
        CreateAtlasImage(profileGroup, "ProfileNameFrame", HomeAtlasPath, new Rect(154f, 22f, 392f, 86f), new Rect(108f, 20f, 240f, 56f), HomeSize);
        CreateAtlasImage(profileGroup, "ProfileProgress", HomeAtlasPath, new Rect(172f, 122f, 349f, 14f), new Rect(110f, 80f, 222f, 10f), HomeSize);
        CreateTmpText(profileGroup, "ProfileName", "RANGER", new Rect(128f, 33f, 160f, 20f), HomeSize, 18f, TextAlignmentOptions.MidlineLeft);
        CreateTmpText(profileGroup, "ProfileLevel", "LEVEL 24", new Rect(128f, 56f, 100f, 16f), HomeSize, 12f, TextAlignmentOptions.MidlineLeft);

        CreateAtlasImage(currencyGroup, "CoinFrame", HomeAtlasPath, new Rect(607f, 24f, 230f, 67f), new Rect(618f, 18f, 196f, 54f), HomeSize);
        CreateAtlasImage(currencyGroup, "GemFrame", HomeAtlasPath, new Rect(861f, 24f, 230f, 67f), new Rect(846f, 18f, 196f, 54f), HomeSize);
        CreateAtlasImage(currencyGroup, "EnergyFrame", HomeAtlasPath, new Rect(1115f, 24f, 230f, 67f), new Rect(1074f, 18f, 196f, 54f), HomeSize);
        CreateTmpText(currencyGroup, "CoinValue", "24,680", new Rect(676f, 36f, 76f, 20f), HomeSize, 15f, TextAlignmentOptions.MidlineRight);
        CreateTmpText(currencyGroup, "GemValue", "1,245", new Rect(902f, 36f, 76f, 20f), HomeSize, 15f, TextAlignmentOptions.MidlineRight);
        CreateTmpText(currencyGroup, "EnergyValue", "78/90", new Rect(1128f, 36f, 76f, 20f), HomeSize, 15f, TextAlignmentOptions.MidlineRight);

        CreateAtlasImage(utilityGroup, "MailIcon", HomeAtlasPath, new Rect(1254f, 105f, 72f, 58f), new Rect(1128f, 28f, 44f, 38f), HomeSize);
        CreateAtlasImage(utilityGroup, "ProfileIcon", HomeAtlasPath, new Rect(1347f, 105f, 72f, 58f), new Rect(1184f, 28f, 44f, 38f), HomeSize);
        CreateAtlasImage(utilityGroup, "SettingsIcon", HomeAtlasPath, new Rect(1441f, 105f, 72f, 58f), new Rect(1238f, 28f, 44f, 38f), HomeSize);

        CreateAtlasImage(dungeonGroup, "DungeonCard", HomeAtlasPath, new Rect(22f, 166f, 472f, 332f), new Rect(24f, 116f, 346f, 252f), HomeSize);
        CreateTmpText(dungeonGroup, "DungeonEyebrow", "NEXT DUNGEON", new Rect(104f, 128f, 136f, 18f), HomeSize, 13f, TextAlignmentOptions.Center).color = ParchmentText;
        CreateTmpText(dungeonGroup, "DungeonName", "STONEWATCH DEPTHS", new Rect(92f, 294f, 250f, 26f), HomeSize, 22f, TextAlignmentOptions.MidlineLeft).color = ParchmentText;
        CreateTmpText(dungeonGroup, "DungeonDetail", "Recommended Level 24", new Rect(92f, 326f, 196f, 18f), HomeSize, 13f, TextAlignmentOptions.MidlineLeft).color = ParchmentMutedText;

        CreateAtlasImage(dailyQuestGroup, "DailyQuestCard", HomeAtlasPath, new Rect(22f, 518f, 471f, 259f), new Rect(24f, 386f, 346f, 210f), HomeSize);
        CreateTmpText(dailyQuestGroup, "DailyTitle", "DAILY TASKS", new Rect(64f, 420f, 130f, 20f), HomeSize, 18f, TextAlignmentOptions.MidlineLeft).color = ParchmentText;
        CreateTmpText(dailyQuestGroup, "DailyGold", "Clear 3 stages", new Rect(64f, 462f, 190f, 18f), HomeSize, 14f, TextAlignmentOptions.MidlineLeft).color = ParchmentMutedText;
        CreateTmpText(dailyQuestGroup, "DailyGem", "Upgrade equipment", new Rect(64f, 500f, 190f, 18f), HomeSize, 14f, TextAlignmentOptions.MidlineLeft).color = ParchmentMutedText;
        CreateTmpText(dailyQuestGroup, "DailyEnergy", "Spend 40 energy", new Rect(64f, 538f, 190f, 18f), HomeSize, 14f, TextAlignmentOptions.MidlineLeft).color = ParchmentMutedText;

        var equipmentCard = CreateAtlasImage(primaryCardsGroup, "EquipmentCard", HomeAtlasPath, new Rect(518f, 158f, 430f, 150f), new Rect(920f, 138f, 328f, 128f), HomeSize);
        CreateAtlasImage(primaryCardsGroup, "InventoryCard", HomeAtlasPath, new Rect(518f, 313f, 431f, 145f), new Rect(920f, 284f, 328f, 128f), HomeSize);
        CreateAtlasImage(primaryCardsGroup, "SocialCard", HomeAtlasPath, new Rect(518f, 459f, 431f, 95f), new Rect(920f, 430f, 328f, 74f), HomeSize);
        var equipmentButton = CreateHotspot(hotspotsGroup, "Button_Equipment", new Rect(920f, 138f, 328f, 128f), HomeSize);
        AddButtonFeedback(equipmentButton, equipmentCard.rectTransform, equipmentCard);
        CreateTmpText(primaryCardsGroup, "EquipmentLabel", "EQUIPMENT", new Rect(1012f, 178f, 160f, 24f), HomeSize, 22f, TextAlignmentOptions.MidlineLeft).color = ParchmentText;
        CreateTmpText(primaryCardsGroup, "EquipmentSubLabel", "Manage equipped gear", new Rect(1012f, 208f, 170f, 18f), HomeSize, 13f, TextAlignmentOptions.MidlineLeft).color = ParchmentMutedText;
        CreateTmpText(primaryCardsGroup, "InventoryLabel", "INVENTORY", new Rect(1012f, 326f, 160f, 24f), HomeSize, 22f, TextAlignmentOptions.MidlineLeft).color = ParchmentText;
        CreateTmpText(primaryCardsGroup, "InventorySubLabel", "Materials and loot", new Rect(1012f, 356f, 170f, 18f), HomeSize, 13f, TextAlignmentOptions.MidlineLeft).color = ParchmentMutedText;
        CreateTmpText(primaryCardsGroup, "PartyLabel", "PARTY", new Rect(974f, 456f, 90f, 22f), HomeSize, 18f, TextAlignmentOptions.MidlineLeft).color = ParchmentText;

        CreateAtlasImage(secondaryCardsGroup, "CompanionButton", HomeAtlasPath, new Rect(995f, 184f, 140f, 128f), new Rect(846f, 514f, 116f, 86f), HomeSize);
        CreateAtlasImage(secondaryCardsGroup, "WorkshopButton", HomeAtlasPath, new Rect(1168f, 184f, 140f, 128f), new Rect(982f, 514f, 116f, 86f), HomeSize);
        CreateAtlasImage(secondaryCardsGroup, "AchievementButton", HomeAtlasPath, new Rect(1338f, 184f, 140f, 128f), new Rect(1118f, 514f, 116f, 86f), HomeSize);

        CreateAtlasImage(startGroup, "StartButton", HomeAtlasPath, new Rect(995f, 331f, 512f, 143f), new Rect(852f, 616f, 396f, 76f), HomeSize);
        CreateTmpText(startGroup, "StartLabel", "START EXPEDITION", new Rect(944f, 638f, 210f, 26f), HomeSize, 22f, TextAlignmentOptions.Center).color = ParchmentText;

        CreateAtlasImage(bottomNavGroup, "BottomNav", HomeAtlasPath, new Rect(966f, 484f, 533f, 87f), new Rect(24f, 622f, 420f, 72f), HomeSize);
        CreateTmpText(bottomNavGroup, "BottomHome", "HOME", new Rect(84f, 662f, 54f, 14f), HomeSize, 11f, TextAlignmentOptions.Center);
        CreateTmpText(bottomNavGroup, "BottomQuest", "QUEST", new Rect(184f, 662f, 54f, 14f), HomeSize, 11f, TextAlignmentOptions.Center);
        CreateTmpText(bottomNavGroup, "BottomMap", "MAP", new Rect(290f, 662f, 54f, 14f), HomeSize, 11f, TextAlignmentOptions.Center);
        CreateTmpText(bottomNavGroup, "BottomShop", "SHOP", new Rect(386f, 662f, 54f, 14f), HomeSize, 11f, TextAlignmentOptions.Center);

        return equipmentButton;
    }

    private static Button BuildEquipmentScreen(RectTransform root, RectTransform detailRoot, out HomeInventoryUI inventoryUi)
    {
        var backgroundGroup = CreateGroup(root, "00_Background");
        var headerGroup = CreateGroup(root, "01_Header");
        var navigationGroup = CreateGroup(headerGroup, "Navigation");
        var titleGroup = CreateGroup(headerGroup, "Title");
        var currencyGroup = CreateGroup(headerGroup, "Currencies");
        var utilityGroup = CreateGroup(headerGroup, "UtilityButtons");
        var leftSlotsGroup = CreateGroup(root, "02_LeftSlots");
        var rightSlotsGroup = CreateGroup(root, "03_RightSlots");
        var loadoutSummaryGroup = CreateGroup(root, "04_LoadoutSummary");
        var selectedItemGroup = CreateGroup(root, "05_SelectedItem");
        var actionsGroup = CreateGroup(root, "06_Actions");

        CreateSolidBackground(backgroundGroup, "Background", new Color(0f, 0f, 0f, 0f));

        var backVisual = CreateAtlasImage(navigationGroup, "BackButtonVisual", EquipmentAtlasPath, new Rect(22f, 20f, 110f, 76f), new Rect(16f, 15f, 104f, 74f), EquipmentSize);
        var backButton = CreateHotspot(navigationGroup, "Button_Back", new Rect(16f, 15f, 104f, 74f), EquipmentSize);
        AddButtonFeedback(backButton, backVisual.rectTransform, backVisual);
        CreateAtlasImage(titleGroup, "TitleFrame", EquipmentAtlasPath, new Rect(134f, 20f, 604f, 75f), new Rect(148f, 20f, 555f, 70f), EquipmentSize);
        CreateTmpText(titleGroup, "EquipmentTitle", "EQUIPMENT", new Rect(242f, 42f, 260f, 24f), EquipmentSize, 24f, TextAlignmentOptions.MidlineLeft);
        CreateAtlasImage(currencyGroup, "TopCoinFrame", EquipmentAtlasPath, new Rect(770f, 21f, 210f, 60f), new Rect(828f, 18f, 205f, 58f), EquipmentSize);
        CreateAtlasImage(currencyGroup, "TopGemFrame", EquipmentAtlasPath, new Rect(1004f, 21f, 210f, 60f), new Rect(1046f, 18f, 205f, 58f), EquipmentSize);
        CreateAtlasImage(currencyGroup, "TopEnergyFrame", EquipmentAtlasPath, new Rect(1218f, 21f, 210f, 60f), new Rect(1264f, 18f, 205f, 58f), EquipmentSize);
        CreateTmpText(currencyGroup, "TopCoinValue", "24,680", new Rect(900f, 38f, 74f, 18f), EquipmentSize, 15f, TextAlignmentOptions.MidlineRight);
        CreateTmpText(currencyGroup, "TopGemValue", "1,245", new Rect(1120f, 38f, 74f, 18f), EquipmentSize, 15f, TextAlignmentOptions.MidlineRight);
        CreateTmpText(currencyGroup, "TopEnergyValue", "78/90", new Rect(1340f, 38f, 74f, 18f), EquipmentSize, 15f, TextAlignmentOptions.MidlineRight);

        CreateAtlasImage(utilityGroup, "TopMailIcon", EquipmentAtlasPath, new Rect(918f, 100f, 70f, 63f), new Rect(1476f, 26f, 42f, 38f), EquipmentSize);
        CreateAtlasImage(utilityGroup, "TopProfileIcon", EquipmentAtlasPath, new Rect(1008f, 100f, 70f, 63f), new Rect(1530f, 26f, 42f, 38f), EquipmentSize);
        CreateAtlasImage(utilityGroup, "TopSettingsIcon", EquipmentAtlasPath, new Rect(1098f, 100f, 70f, 63f), new Rect(1584f, 26f, 42f, 38f), EquipmentSize);

        var slots = new List<HomeEquipSlotUI>
        {
            CreateEquipSlot(leftSlotsGroup, "Slot_Weapon", EquipmentSlot.Weapon, "WEAPON", new Rect(74f, 126f, 382f, 176f), new Rect(24f, 116f, 404f, 176f), EquipmentSize),
            CreateEquipSlot(leftSlotsGroup, "Slot_Accessory", EquipmentSlot.Accessory, "ACCESSORY", new Rect(75f, 320f, 382f, 176f), new Rect(25f, 306f, 394f, 126f), EquipmentSize),
            CreateEquipSlot(leftSlotsGroup, "Slot_Pants", EquipmentSlot.Pants, "PANTS", new Rect(75f, 511f, 382f, 176f), new Rect(25f, 438f, 394f, 126f), EquipmentSize),
            CreateEquipSlot(rightSlotsGroup, "Slot_Head", EquipmentSlot.Head, "HEAD", new Rect(1223f, 124f, 325f, 170f), new Rect(455f, 124f, 281f, 123f), EquipmentSize),
            CreateEquipSlot(rightSlotsGroup, "Slot_Armor", EquipmentSlot.Armor, "ARMOR", new Rect(1224f, 313f, 325f, 170f), new Rect(455f, 252f, 281f, 123f), EquipmentSize),
            CreateEquipSlot(rightSlotsGroup, "Slot_Shoes", EquipmentSlot.Shoes, "SHOES", new Rect(1224f, 504f, 325f, 170f), new Rect(455f, 379f, 281f, 123f), EquipmentSize)
        };

        CreateAtlasImage(loadoutSummaryGroup, "StatSummaryPanel", EquipmentAtlasPath, new Rect(25f, 712f, 455f, 125f), new Rect(52f, 720f, 474f, 170f), EquipmentSize);
        CreateTmpText(loadoutSummaryGroup, "SummaryTitle", "CURRENT LOADOUT", new Rect(176f, 740f, 210f, 22f), EquipmentSize, 19f, TextAlignmentOptions.MidlineLeft).color = ParchmentText;
        CreateTmpText(loadoutSummaryGroup, "SummaryStats", "ATK  1,246\nDEF    382\nHP   4,920", new Rect(282f, 765f, 170f, 62f), EquipmentSize, 16f, TextAlignmentOptions.MidlineLeft).color = ParchmentMutedText;

        CreateAtlasImage(selectedItemGroup, "SelectedItemPanel", EquipmentAtlasPath, new Rect(497f, 724f, 423f, 112f), new Rect(585f, 760f, 505f, 135f), EquipmentSize);
        CreateTmpText(selectedItemGroup, "SelectedItemText", "Select a slot to change equipment", new Rect(650f, 800f, 330f, 26f), EquipmentSize, 19f, TextAlignmentOptions.Center).color = ParchmentText;

        CreateAtlasImage(actionsGroup, "ManageButtonVisual", EquipmentAtlasPath, new Rect(1004f, 184f, 206f, 57f), new Rect(1242f, 710f, 315f, 72f), EquipmentSize);
        CreateAtlasImage(actionsGroup, "EnhanceButtonVisual", EquipmentAtlasPath, new Rect(770f, 252f, 206f, 57f), new Rect(1242f, 806f, 315f, 72f), EquipmentSize);
        CreateTmpText(actionsGroup, "ManageText", "MANAGE", new Rect(1324f, 733f, 150f, 24f), EquipmentSize, 22f, TextAlignmentOptions.Center).color = ParchmentText;
        CreateTmpText(actionsGroup, "EnhanceText", "ENHANCE", new Rect(1324f, 829f, 150f, 24f), EquipmentSize, 22f, TextAlignmentOptions.Center);

        inventoryUi = root.gameObject.AddComponent<HomeInventoryUI>();
        var popup = detailRoot.GetComponent<HomeEquipPopupUI>();
        if (popup == null)
            popup = detailRoot.gameObject.AddComponent<HomeEquipPopupUI>();

        var inventorySo = new SerializedObject(inventoryUi);
        var slotsProperty = inventorySo.FindProperty("_slots");
        slotsProperty.arraySize = slots.Count;
        for (var i = 0; i < slots.Count; i++)
            slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        inventorySo.FindProperty("_popup").objectReferenceValue = popup;
        inventorySo.FindProperty("_enableAppearanceSelector").boolValue = false;
        inventorySo.ApplyModifiedPropertiesWithoutUndo();

        return backButton;
    }

    private static void BuildDetailScreen(RectTransform root)
    {
        var content = CreateRect(root, "Content");
        Stretch(content);

        var chrome = CreateGroup(content, "ResourceChrome");
        var dimGroup = CreateGroup(chrome, "00_Dim");
        var leftPanelChromeGroup = CreateGroup(chrome, "01_LeftPanelChrome");
        var rightPanelChromeGroup = CreateGroup(chrome, "02_RightPanelChrome");
        var headerChromeGroup = CreateGroup(chrome, "03_HeaderChrome");
        var filtersChromeGroup = CreateGroup(chrome, "04_FilterControls");
        var actionChromeGroup = CreateGroup(chrome, "05_ActionChrome");
        var closeChromeGroup = CreateGroup(chrome, "06_CloseChrome");
        var headerGroup = CreateGroup(root, "01_Header");
        var prefabGroup = CreateGroup(root, "99_Prefabs");

        var dim = CreateSolidBackground(dimGroup, "DimOverlay", new Color(0f, 0f, 0f, 0.36f));
        dim.raycastTarget = true;
        CreateAtlasImage(leftPanelChromeGroup, "OwnedItemsFrame", DetailAtlasPath, new Rect(14f, 15f, 325f, 435f), new Rect(16f, 40f, 374f, 650f), DetailSize);
        CreateAtlasImage(rightPanelChromeGroup, "DetailInfoFrame", DetailAtlasPath, new Rect(354f, 20f, 294f, 420f), new Rect(882f, 84f, 382f, 592f), DetailSize);
        CreateAtlasImage(headerChromeGroup, "CategoryTitleFrame", DetailAtlasPath, new Rect(672f, 19f, 335f, 78f), new Rect(88f, 24f, 390f, 60f), DetailSize);
        CreateAtlasImage(filtersChromeGroup, "FilterEquipped", DetailAtlasPath, new Rect(25f, 470f, 118f, 39f), new Rect(32f, 612f, 140f, 40f), DetailSize);
        CreateAtlasImage(filtersChromeGroup, "FilterAll", DetailAtlasPath, new Rect(156f, 470f, 118f, 39f), new Rect(184f, 612f, 140f, 40f), DetailSize);
        CreateAtlasImage(actionChromeGroup, "ActionButtonFrame", DetailAtlasPath, new Rect(1138f, 263f, 116f, 39f), new Rect(1080f, 624f, 130f, 40f), DetailSize);
        var closeVisual = CreateAtlasImage(closeChromeGroup, "CloseIcon", DetailAtlasPath, new Rect(1398f, 66f, 30f, 30f), new Rect(1230f, 22f, 32f, 32f), DetailSize);

        var title = CreateTmpText(headerGroup, "Title", "EQUIPMENT", new Rect(108f, 36f, 360f, 38f), DetailSize, 24f, TextAlignmentOptions.MidlineLeft);
        title.color = new Color(1f, 0.84f, 0f, 1f);

        var closeButton = CreateHotspot(headerGroup, "CloseBtn", new Rect(1218f, 14f, 46f, 46f), DetailSize);
        AddButtonFeedback(closeButton, closeVisual.rectTransform, closeVisual);
        var itemPrefab = CreatePopupItemPrefab(prefabGroup);

        var popup = root.GetComponent<HomeEquipPopupUI>();
        if (popup == null)
            popup = root.gameObject.AddComponent<HomeEquipPopupUI>();

        var popupSo = new SerializedObject(popup);
        popupSo.FindProperty("_content").objectReferenceValue = content.transform;
        popupSo.FindProperty("_itemPrefab").objectReferenceValue = itemPrefab;
        popupSo.FindProperty("_title").objectReferenceValue = title;
        popupSo.FindProperty("_closeBtn").objectReferenceValue = closeButton;
        popupSo.FindProperty("_useHomeDetailResourceLayout").boolValue = true;
        popupSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static HomeEquipSlotUI CreateEquipSlot(RectTransform parent, string name, EquipmentSlot slot, string label, Rect targetRect, Rect frameRect, Vector2 layoutSize)
    {
        var slotButton = CreateHotspot(parent, name, targetRect, layoutSize);
        var slotGo = slotButton.gameObject;
        var frame = CreateAtlasImage(slotGo.transform, "Frame", EquipmentAtlasPath, frameRect, new Rect(0f, 0f, targetRect.width, targetRect.height), new Vector2(targetRect.width, targetRect.height));
        AddButtonFeedback(slotButton, slotGo.GetComponent<RectTransform>(), frame);

        var emptyVisual = CreateRect(slotGo.transform, "Empty");
        Stretch(emptyVisual);
        CreateTmpText(emptyVisual, "EmptyLabel", "EMPTY", new Rect(targetRect.width - 128f, targetRect.height - 38f, 96f, 20f), new Vector2(targetRect.width, targetRect.height), 14f, TextAlignmentOptions.MidlineRight).color = ParchmentMutedText;

        var filledVisual = CreateRect(slotGo.transform, "Filled");
        Stretch(filledVisual);
        filledVisual.gameObject.SetActive(false);

        var icon = CreateImage(filledVisual, "Icon", new Rect(32f, 32f, 96f, 96f), new Vector2(targetRect.width, targetRect.height));
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var iconFrame = CreateImage(filledVisual, "IconFrame", new Rect(22f, 22f, 116f, 116f), new Vector2(targetRect.width, targetRect.height));
        iconFrame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SlotIconFramePath);
        iconFrame.preserveAspect = false;
        iconFrame.raycastTarget = false;
        icon.transform.SetAsLastSibling();
        iconFrame.transform.SetAsLastSibling();

        CreateTmpText(slotGo.transform, "SlotLabel", label, new Rect(150f, 38f, targetRect.width - 180f, 24f), new Vector2(targetRect.width, targetRect.height), 21f, TextAlignmentOptions.MidlineLeft).color = ParchmentText;
        CreateTmpText(slotGo.transform, "SlotHint", "Tap to view", new Rect(150f, 72f, targetRect.width - 180f, 20f), new Vector2(targetRect.width, targetRect.height), 13f, TextAlignmentOptions.MidlineLeft).color = ParchmentMutedText;

        var slotUi = slotGo.AddComponent<HomeEquipSlotUI>();
        var so = new SerializedObject(slotUi);
        so.FindProperty("_targetSlot").enumValueIndex = (int)slot;
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_iconFrame").objectReferenceValue = iconFrame;
        so.FindProperty("_btn").objectReferenceValue = slotGo.GetComponent<Button>();
        so.FindProperty("_emptyVisual").objectReferenceValue = emptyVisual.gameObject;
        so.FindProperty("_filledVisual").objectReferenceValue = filledVisual.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        return slotUi;
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
        background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/UI_Home_Equipment_Detail/UI_01_default.png");
        background.type = Image.Type.Sliced;
        background.color = Color.white;

        var layout = item.GetComponent<LayoutElement>();
        layout.minHeight = 64f;
        layout.preferredHeight = 85f;
        layout.preferredWidth = 84f;
        layout.minHeight = 85f;
        layout.minWidth = 84f;

        var icon = CreateImage(item.transform, "Icon", new Rect(16f, 13f, 52f, 52f), new Vector2(84f, 85f));
        var iconFrame = CreateImage(item.transform, "IconFrame", new Rect(9f, 6f, 66f, 66f), new Vector2(84f, 85f));
        iconFrame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GridIconFramePath);
        iconFrame.preserveAspect = false;
        iconFrame.raycastTarget = false;
        var nameText = CreateTmpText(item.transform, "NameText", "Item", new Rect(4f, 66f, 76f, 16f), new Vector2(84f, 85f), 10f, TextAlignmentOptions.Center);
        nameText.color = ParchmentText;
        var levelText = CreateTmpText(item.transform, "LevelText", "+0", new Rect(6f, 68f, 72f, 14f), new Vector2(84f, 85f), 10f, TextAlignmentOptions.Center);
        levelText.color = ParchmentMutedText;
        var mark = CreateTmpText(item.transform, "EquippedMark", "E", new Rect(58f, 5f, 20f, 15f), new Vector2(84f, 85f), 10f, TextAlignmentOptions.Center);
        mark.gameObject.SetActive(false);
        icon.transform.SetAsLastSibling();
        iconFrame.transform.SetAsLastSibling();
        mark.transform.SetAsLastSibling();

        var itemUi = item.AddComponent<HomeEquipPopupItemUI>();
        var so = new SerializedObject(itemUi);
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_iconFrame").objectReferenceValue = iconFrame;
        so.FindProperty("_nameText").objectReferenceValue = nameText;
        so.FindProperty("_levelText").objectReferenceValue = levelText;
        so.FindProperty("_btn").objectReferenceValue = item.GetComponent<Button>();
        so.FindProperty("_equippedMark").objectReferenceValue = mark.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();

        AddButtonFeedback(item.GetComponent<Button>(), itemRect, background);

        item.SetActive(false);
        return item;
    }

    private static RawImage CreateAtlasImage(Transform parent, string name, string texturePath, Rect atlasRect, Rect layoutRect, Vector2 layoutSize)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(parent, false);
        SetRectFromTopLeft(go.GetComponent<RectTransform>(), layoutRect, layoutSize);

        var image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;
        if (texture != null)
        {
            image.uvRect = new Rect(
                atlasRect.xMin / texture.width,
                1f - atlasRect.yMax / texture.height,
                atlasRect.width / texture.width,
                atlasRect.height / texture.height);
        }
        else
        {
            Debug.LogWarning($"[Home1OverlayUiBuilder] Missing UI texture: {texturePath}");
        }

        return image;
    }

    private static Image CreateSolidBackground(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Button CreateHotspot(RectTransform parent, string name, Rect sourceRect, Vector2 sourceSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetRectFromTopLeft(go.GetComponent<RectTransform>(), sourceRect, sourceSize);

        var image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        AddButtonFeedback(button, go.GetComponent<RectTransform>(), image);
        return button;
    }

    private static HomeUIButtonFeedback AddButtonFeedback(Button button, RectTransform scaleTarget = null, Graphic tintTarget = null)
    {
        if (button == null)
            return null;

        var feedback = button.GetComponent<HomeUIButtonFeedback>();
        if (feedback == null)
            feedback = button.gameObject.AddComponent<HomeUIButtonFeedback>();

        feedback.Configure(scaleTarget != null ? scaleTarget : button.transform as RectTransform, tintTarget != null ? tintTarget : button.targetGraphic);
        EditorUtility.SetDirty(feedback);
        return feedback;
    }

    private static Image CreateImage(Transform parent, string name, Rect sourceRect, Vector2 sourceSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        SetRectFromTopLeft(go.GetComponent<RectTransform>(), sourceRect, sourceSize);

        var image = go.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private static TextMeshProUGUI CreateTmpText(Transform parent, string name, string text, Rect sourceRect, Vector2 sourceSize, float fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        SetRectFromTopLeft(go.GetComponent<RectTransform>(), sourceRect, sourceSize);

        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(8f, fontSize - 8f);
        label.fontSizeMax = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
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

    private struct CameraState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float FieldOfView;
        public float NearClip;
        public float FarClip;
        public CameraClearFlags ClearFlags;
        public Color Background;

        public static CameraState Default => new()
        {
            Position = new Vector3(0f, 1.6f, -6f),
            Rotation = Quaternion.Euler(8f, 0f, 0f),
            FieldOfView = 50f,
            NearClip = 0.3f,
            FarClip = 1000f,
            ClearFlags = CameraClearFlags.Skybox,
            Background = Color.black
        };
    }
}
