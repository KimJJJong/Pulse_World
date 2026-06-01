using System.Collections.Generic;
using NetClient.Room.UI;
using RhythmRPG.Game.Visual.SceneEffects;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class TownSceneUiBuilder
{
    private const string OverlayCanvasName = "Canvas_TownExpeditionOverlay";
    private const string ApiClientProviderPrefabPath = "Assets/0.MainProject/Resources/ApiClientProvider.prefab";
    private const string RoomUiRootPrefabPath = "Assets/0.MainProject/Resources/RoomUIRoot.prefab";
    private const int ExpeditionSortingOrder = 7000;
    private const int InventorySortingOrder = 8000;

    private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
    private static readonly Color32 PanelDark = new(7, 14, 18, 232);
    private static readonly Color32 PanelSoft = new(17, 25, 31, 230);
    private static readonly Color32 Border = new(106, 126, 134, 210);
    private static readonly Color32 Cyan = new(0, 224, 240, 255);
    private static readonly Color32 TextMain = new(236, 244, 239, 255);
    private static readonly Color32 TextMuted = new(169, 184, 190, 255);
    private static readonly Color32 Green = new(136, 226, 47, 255);
    private static TMP_FontAsset _font;

    private sealed class PartySlotBuildBinding
    {
        public GameObject Root;
        public TMP_Text IndexText;
        public TMP_Text NameText;
        public TMP_Text HpText;
        public TMP_Text HostBadgeText;
        public TMP_Text ReadyText;
        public Graphic ReadyGraphic;
        public Graphic LocalFrame;
        public Slider HpSlider;
    }

    private sealed class MapOptionBuildBinding
    {
        public Button Button;
        public TMP_Text TitleText;
        public TMP_Text DescriptionText;
        public TMP_Text DifficultyText;
        public TMP_Text MetaText;
        public Graphic SelectedFrame;
    }

    [MenuItem("RhythmRPG/Editors/UI/Ensure Town Scene UI Objects")]
    public static void EnsureTownSceneUiObjects()
    {
        EnsureEventSystem();
        var apiProvider = EnsureApiClientProvider();
        EnsureRoomUiRoot(apiProvider);
        EnsureTownExpeditionOverlay();
        DisableTownCombatRhythmHud();
        ConfigureTownLightPulses();
        InventoryUIBuilder.CreateTownInventoryMenu();
        ConfigureTownInventory();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[TownSceneUiBuilder] Town lobby UI was rebuilt with Host/Client panels, map selection, invite code, and visible scene objects.");
    }

    private static void EnsureTownExpeditionOverlay()
    {
        var canvasGo = GameObject.Find(OverlayCanvasName);
        if (canvasGo == null)
            canvasGo = new GameObject(OverlayCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            SetLayerRecursive(canvasGo, uiLayer);

        var canvas = GetOrAdd<Canvas>(canvasGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = ExpeditionSortingOrder;

        var scaler = GetOrAdd<CanvasScaler>(canvasGo);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GetOrAdd<GraphicRaycaster>(canvasGo);
        var panel = GetOrAdd<TownExpeditionPanel>(canvasGo);

        ClearChildren(canvasGo.transform);

        var root = CreateRect(canvasGo.transform, "TownLobbyRoot");
        Stretch(root);

        // Existing Town HUD already owns the party list, town title, minimap,
        // and combat dock. Keep this overlay scoped to lobby-only controls.
        PartySlotBuildBinding[] partySlots = null;
        TMP_Text topTownTitle = null;
        TMP_Text topStatus = null;
        TMP_Text minimapCount = null;
        var side = BuildSidePanel(
            root,
            out var roleBadge,
            out var partyCount,
            out var partyPanelBody,
            out var sidePartySlots,
            out var selectedMapTitle,
            out var selectedMapDifficulty,
            out var selectedMapMeta,
            out var selectedMapGoal,
            out var inviteCode,
            out var copyInviteButton,
            out var mapInfoButton,
            out var status,
            out var readySummary,
            out var hostControls,
            out var clientControls,
            out var gameSelectButton,
            out var hostStartButton,
            out var hostCancelButton,
            out var partyManageButton,
            out var readyButton,
            out var clientHint,
            out var partyMinimizeButton,
            out var partyMinimizeLabel);

        Button inventoryButton = null;
        var mapSelectWindow = BuildMapSelectWindow(root, out var mapSelectOptions, out var mapSelectConfirmButton, out var mapSelectPartyButton, out var mapSelectCloseButton);
        var mapInfoWindow = BuildMapInfoWindow(root, out var mapInfoTitle, out var mapInfoDescription, out var mapInfoStats, out var mapInfoFeatureTexts, out var mapInfoCloseButton);

        BindPanel(
            panel,
            canvas,
            root,
            topTownTitle,
            topStatus,
            status,
            side,
            partyPanelBody,
            roleBadge,
            partyCount,
            selectedMapTitle,
            selectedMapMeta,
            selectedMapDifficulty,
            selectedMapGoal,
            inviteCode,
            clientHint,
            minimapCount,
            readySummary,
            hostControls,
            clientControls,
            mapSelectWindow,
            mapInfoWindow,
            mapInfoTitle,
            mapInfoDescription,
            mapInfoStats,
            mapInfoFeatureTexts,
            partySlots,
            sidePartySlots,
            mapSelectOptions,
            inventoryButton,
            gameSelectButton,
            mapSelectConfirmButton,
            mapSelectPartyButton,
            mapSelectCloseButton,
            mapInfoButton,
            mapInfoCloseButton,
            copyInviteButton,
            partyMinimizeButton,
            partyMinimizeLabel,
            readyButton,
            hostStartButton,
            hostCancelButton,
            partyManageButton);

        mapSelectWindow.gameObject.SetActive(false);
        mapInfoWindow.gameObject.SetActive(false);
        EditorUtility.SetDirty(side.gameObject);
        EditorUtility.SetDirty(canvasGo);
    }

    private static void DisableTownCombatRhythmHud()
    {
        foreach (var guide in Object.FindObjectsByType<BeatGuideView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetSceneObjectActive(guide != null ? guide.gameObject : null, false);

        foreach (var combo in Object.FindObjectsByType<ComboCounterView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetSceneObjectActive(combo != null ? combo.gameObject : null, false);

        foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rect == null)
                continue;

            var name = rect.gameObject.name;
            if (string.Equals(name, "BeatGuide", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "ComboFlourish", System.StringComparison.OrdinalIgnoreCase))
            {
                SetSceneObjectActive(rect.gameObject, false);
            }
        }
    }

    private static void SetSceneObjectActive(GameObject target, bool active)
    {
        if (target == null || !target.scene.IsValid() || !target.scene.isLoaded)
            return;

        target.SetActive(active);
        EditorUtility.SetDirty(target);
    }

    private static void ConfigureTownLightPulses()
    {
        foreach (var pulse in Object.FindObjectsByType<ForestBeatLightPulse>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pulse == null || !pulse.gameObject.scene.IsValid() || !pulse.gameObject.scene.isLoaded)
                continue;

            pulse.ConfigureTiming(TownLightPulseProfile.UseRhythmClient, TownLightPulseProfile.Bpm);
            EditorUtility.SetDirty(pulse);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pulse);
        }
    }

    private static PartySlotBuildBinding[] BuildLeftPartyHud(RectTransform root)
    {
        var slots = new List<PartySlotBuildBinding>();
        var list = CreateRect(root, "LeftPartyHud");
        SetTopLeft(list, 24f, 26f, 374f, 320f);

        for (var i = 0; i < 4; i++)
        {
            var slot = CreatePartySlot(list, $"PartySlot_{i + 1:00}", i + 1, 0f, i * 74f, 360f, 66f, false);
            slots.Add(slot);
        }

        return slots.ToArray();
    }

    private static void BuildTopTownBar(RectTransform root, out TMP_Text title, out TMP_Text status)
    {
        var bar = CreatePanel(root, "TopTownBar", new Color32(10, 15, 18, 232), true);
        SetTopCenter(bar, 0f, 20f, 780f, 74f);

        title = CreateText(bar, "TopTownTitle", "타운 포레스트", 26f, TextAlignmentOptions.MidlineLeft, TextMain);
        SetStretch(title.rectTransform, 34f, 0f, 380f, 0f);

        var divider = CreateSolid(bar, "Divider", new Color32(88, 104, 112, 160));
        SetCenter(divider.rectTransform, -6f, 0f, 1.5f, 46f);

        var progressLine = CreateSolid(bar, "ProgressLine", new Color32(78, 98, 104, 220));
        SetRect(progressLine.rectTransform, new Vector2(0.50f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-82f, 3f), new Vector2(-34f, 0f));
        var leftGem = CreateSolid(bar, "LeftGem", Cyan);
        SetCenter(leftGem.rectTransform, -70f, 0f, 8f, 8f);
        leftGem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var rightGem = CreateSolid(bar, "RightGem", Cyan);
        SetRightCenter(rightGem.rectTransform, 34f, 0f, 8f, 8f);
        rightGem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        status = CreateText(bar, "TopStatus", "Host 대기 중", 14f, TextAlignmentOptions.MidlineRight, TextMuted);
        SetRect(status.rectTransform, new Vector2(0.50f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-86f, 0f), new Vector2(-36f, 0f));
    }

    private static void BuildMinimap(RectTransform root, out TMP_Text countText)
    {
        var panel = CreatePanel(root, "MinimapPanel", new Color32(4, 12, 16, 225), true);
        SetTopRight(panel, 42f, 28f, 330f, 312f);

        CreateText(panel, "MinimapTitle", "MINIMAP", 20f, TextAlignmentOptions.MidlineLeft, Cyan).rectTransform
            .anchoredPosition = new Vector2(20f, -20f);
        var title = panel.Find("MinimapTitle") as RectTransform;
        SetTopLeft(title, 20f, 14f, 170f, 34f);

        countText = CreateText(panel, "MinimapCount", "1", 24f, TextAlignmentOptions.MidlineRight, Cyan);
        SetTopRight(countText.rectTransform, 18f, 14f, 60f, 34f);

        var mapArea = CreateSolid(panel, "MapArea", new Color32(2, 18, 23, 226));
        SetTopLeft(mapArea.rectTransform, 18f, 62f, 294f, 228f);
        AddBorder(mapArea.rectTransform, new Color32(95, 123, 132, 200), 1.5f);

        for (var i = 1; i < 7; i++)
        {
            var lineX = CreateSolid(mapArea.rectTransform, $"GridV_{i}", new Color32(25, 74, 84, 86));
            SetRect(lineX.rectTransform, new Vector2(i / 7f, 0f), new Vector2(i / 7f, 1f), new Vector2(0.5f, 0.5f), new Vector2(1f, 0f), Vector2.zero);

            var lineY = CreateSolid(mapArea.rectTransform, $"GridH_{i}", new Color32(25, 74, 84, 86));
            SetRect(lineY.rectTransform, new Vector2(0f, i / 7f), new Vector2(1f, i / 7f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero);
        }

        var mapBlob = CreateSolid(mapArea.rectTransform, "MapShapePlaceholder", new Color32(18, 68, 78, 150));
        SetCenter(mapBlob.rectTransform, 8f, 2f, 148f, 92f);
        mapBlob.raycastTarget = false;

        var player = CreateText(mapArea.rectTransform, "PlayerMarker", "▲", 26f, TextAlignmentOptions.Center, Cyan);
        SetCenter(player.rectTransform, 8f, -5f, 36f, 34f);
        var markerA = CreateText(mapArea.rectTransform, "MarkerA", "•", 24f, TextAlignmentOptions.Center, Cyan);
        SetCenter(markerA.rectTransform, -62f, 22f, 24f, 24f);
        var markerB = CreateText(mapArea.rectTransform, "MarkerB", "•", 24f, TextAlignmentOptions.Center, Cyan);
        SetCenter(markerB.rectTransform, 66f, 34f, 24f, 24f);
    }

    private static RectTransform BuildSidePanel(
        RectTransform root,
        out TMP_Text roleBadge,
        out TMP_Text partyCount,
        out RectTransform partyPanelBody,
        out PartySlotBuildBinding[] sideSlots,
        out TMP_Text selectedMapTitle,
        out TMP_Text selectedMapDifficulty,
        out TMP_Text selectedMapMeta,
        out TMP_Text selectedMapGoal,
        out TMP_Text inviteCode,
        out Button copyInviteButton,
        out Button mapInfoButton,
        out TMP_Text status,
        out TMP_Text readySummary,
        out RectTransform hostControls,
        out RectTransform clientControls,
        out Button gameSelectButton,
        out Button hostStartButton,
        out Button hostCancelButton,
        out Button partyManageButton,
        out Button readyButton,
        out TMP_Text clientHint,
        out Button partyMinimizeButton,
        out TMP_Text partyMinimizeLabel)
    {
        var panel = CreatePanel(root, "TownPartyPanel", PanelDark, true);
        SetTopRight(panel, 42f, 354f, 430f, 640f);

        CreateText(panel, "PartyIcon", "●●", 17f, TextAlignmentOptions.MidlineLeft, TextMuted);
        SetTopLeft(panel.Find("PartyIcon") as RectTransform, 22f, 18f, 42f, 30f);
        partyCount = CreateText(panel, "PartyCount", "파티 현황 (4/4)", 21f, TextAlignmentOptions.MidlineLeft, TextMain);
        SetTopLeft(partyCount.rectTransform, 66f, 16f, 240f, 34f);
        roleBadge = CreateText(panel, "RoleBadge", "HOST", 15f, TextAlignmentOptions.Center, Color.black);
        SetTopRight(roleBadge.rectTransform, 20f, 18f, 74f, 26f);
        AddTextBackplate(roleBadge.rectTransform, Cyan);

        partyMinimizeButton = CreateButton(panel, "PartyPanelMinimizeButton", "-", 30f, 26f);
        SetTopRight(partyMinimizeButton.GetComponent<RectTransform>(), 102f, 18f, 30f, 26f);
        partyMinimizeLabel = partyMinimizeButton.GetComponentInChildren<TMP_Text>(true);
        if (partyMinimizeLabel)
            partyMinimizeLabel.gameObject.name = "PartyPanelMinimizeLabel";

        partyPanelBody = CreateRect(panel, "PartyPanelBody");
        Stretch(partyPanelBody);

        var sideSlotRoot = CreateRect(partyPanelBody, "SidePartySlots");
        SetTopLeft(sideSlotRoot, 18f, 56f, 394f, 160f);
        var sideList = new List<PartySlotBuildBinding>();
        for (var i = 0; i < 4; i++)
            sideList.Add(CreatePartySlot(sideSlotRoot, $"SidePartySlot_{i + 1:00}", i + 1, 0f, i * 39f, 394f, 34f, true));
        sideSlots = sideList.ToArray();

        var selectedFrame = CreatePanel(partyPanelBody, "SelectedMapPanel", new Color32(8, 16, 20, 212), true);
        SetTopLeft(selectedFrame, 18f, 224f, 394f, 102f);
        CreateText(selectedFrame, "SelectedMapLabel", "선택된 맵", 14f, TextAlignmentOptions.MidlineLeft, TextMuted);
        SetTopLeft(selectedFrame.Find("SelectedMapLabel") as RectTransform, 12f, 4f, 120f, 24f);
        var preview = CreateSolid(selectedFrame, "SelectedMapPreview", new Color32(12, 63, 74, 220));
        SetTopLeft(preview.rectTransform, 12f, 34f, 132f, 54f);
        AddBorder(preview.rectTransform, new Color32(70, 116, 126, 180), 1f);

        selectedMapTitle = CreateText(selectedFrame, "SelectedMapTitle", "포레스트 튜토리얼", 17f, TextAlignmentOptions.MidlineLeft, TextMain);
        SetTopLeft(selectedMapTitle.rectTransform, 156f, 30f, 172f, 24f);
        selectedMapDifficulty = CreateText(selectedFrame, "SelectedMapDifficulty", "쉬움", 13f, TextAlignmentOptions.Center, Green);
        SetTopRight(selectedMapDifficulty.rectTransform, 12f, 32f, 46f, 24f);
        AddTextBackplate(selectedMapDifficulty.rectTransform, new Color32(24, 60, 30, 230));
        selectedMapMeta = CreateText(selectedFrame, "SelectedMapMeta", "1~4명   5~10분", 14f, TextAlignmentOptions.MidlineLeft, TextMuted);
        SetTopLeft(selectedMapMeta.rectTransform, 156f, 58f, 210f, 20f);
        selectedMapGoal = CreateText(selectedFrame, "SelectedMapGoal", "모든 적 처치", 13f, TextAlignmentOptions.MidlineLeft, TextMuted);
        SetTopLeft(selectedMapGoal.rectTransform, 156f, 78f, 210f, 20f);

        gameSelectButton = CreateButton(partyPanelBody, "GameSelectButton", "맵 변경", 390f, 36f, true);
        SetTopLeft(gameSelectButton.GetComponent<RectTransform>(), 20f, 336f, 390f, 36f);

        var inviteLabel = CreateText(partyPanelBody, "InviteLabel", "초대 코드", 14f, TextAlignmentOptions.MidlineLeft, TextMuted);
        SetTopLeft(inviteLabel.rectTransform, 22f, 384f, 160f, 22f);
        var inviteBox = CreateSolid(partyPanelBody, "InviteCodeBox", new Color32(4, 16, 18, 235));
        SetTopLeft(inviteBox.rectTransform, 22f, 408f, 260f, 38f);
        AddBorder(inviteBox.rectTransform, new Color32(61, 89, 95, 210), 1f);
        inviteCode = CreateText(inviteBox.rectTransform, "InviteCode", "----", 20f, TextAlignmentOptions.Center, Cyan);
        Stretch(inviteCode.rectTransform);
        copyInviteButton = CreateButton(partyPanelBody, "CopyInviteButton", "복사", 104f, 38f);
        SetTopRight(copyInviteButton.GetComponent<RectTransform>(), 24f, 408f, 104f, 38f);

        mapInfoButton = CreateButton(partyPanelBody, "MapInfoButton", "맵 정보", 390f, 36f);
        SetTopLeft(mapInfoButton.GetComponent<RectTransform>(), 20f, 456f, 390f, 36f);

        status = CreateText(partyPanelBody, "Status", "Town 정보를 불러오는 중...", 14f, TextAlignmentOptions.Center, TextMuted);
        SetTopLeft(status.rectTransform, 22f, 494f, 386f, 22f);
        readySummary = CreateText(partyPanelBody, "ReadySummary", "Game 대기방 없음", 13f, TextAlignmentOptions.Center, TextMuted);
        SetTopLeft(readySummary.rectTransform, 22f, 518f, 386f, 20f);

        hostControls = CreateRect(partyPanelBody, "HostControls");
        SetBottomLeft(hostControls, 18f, 12f, 394f, 84f);
        hostStartButton = CreateButton(hostControls, "HostStartGameButton", "시작", 238f, 54f, true);
        SetBottomLeft(hostStartButton.GetComponent<RectTransform>(), 0f, 0f, 238f, 54f);
        partyManageButton = CreateButton(hostControls, "PartyManageButton", "파티 관리", 144f, 54f);
        SetBottomRight(partyManageButton.GetComponent<RectTransform>(), 0f, 0f, 144f, 54f);
        hostCancelButton = CreateButton(hostControls, "HostCancelGameButton", "대기 취소", 120f, 24f);
        SetBottomLeft(hostCancelButton.GetComponent<RectTransform>(), 0f, 58f, 120f, 24f);
        hostCancelButton.gameObject.SetActive(false);

        clientControls = CreateRect(partyPanelBody, "ClientControls");
        SetBottomLeft(clientControls, 18f, 12f, 394f, 84f);
        readyButton = CreateButton(clientControls, "ReadyWindowButton", "준비", 394f, 54f, true);
        SetBottomLeft(readyButton.GetComponent<RectTransform>(), 0f, 26f, 394f, 54f);
        clientHint = CreateText(clientControls, "ClientReadyHint", "토글하여 준비 / 해제", 13f, TextAlignmentOptions.Center, TextMuted);
        SetBottomLeft(clientHint.rectTransform, 0f, 0f, 394f, 22f);
        clientControls.gameObject.SetActive(false);

        return panel;
    }

    private static Button BuildBottomHud(RectTransform root)
    {
        var bottom = CreateRect(root, "BottomActionHud");
        SetBottomCenter(bottom, 0f, 24f, 900f, 150f);

        var inventoryButton = CreateActionSlot(bottom, "InventoryButton", "I", "INV", -340f);
        CreateActionSlot(bottom, "SkillSlot_H", "H", "ATK", -170f);
        var hp = CreatePanel(bottom, "HudHpHex", new Color32(17, 25, 31, 235), true);
        SetCenter(hp, 0f, 6f, 140f, 116f);
        CreateText(hp, "HpLabel", "100/100", 30f, TextAlignmentOptions.Center, TextMain);
        Stretch(hp.Find("HpLabel") as RectTransform);
        CreateActionSlot(bottom, "SkillSlot_K", "K", "DEF", 170f);
        CreateActionSlot(bottom, "SkillSlot_L", "L", "RUN", 340f);
        return inventoryButton;
    }

    private static Button CreateActionSlot(RectTransform parent, string name, string hotkey, string label, float x)
    {
        var button = CreateButton(parent, name, "", 128f, 128f);
        var rect = button.GetComponent<RectTransform>();
        SetCenter(rect, x, 6f, 128f, 128f);
        var circle = CreateSolid(rect, "IconCircle", new Color32(20, 29, 34, 245));
        SetCenter(circle.rectTransform, 0f, 6f, 76f, 76f);
        AddBorder(circle.rectTransform, new Color32(96, 111, 118, 220), 3f);
        CreateText(rect, "IconLabel", label, 19f, TextAlignmentOptions.Center, TextMuted);
        SetCenter(rect.Find("IconLabel") as RectTransform, 0f, 8f, 80f, 36f);
        CreateText(rect, "Hotkey", hotkey, 15f, TextAlignmentOptions.Center, TextMain);
        SetBottomCenter(rect.Find("Hotkey") as RectTransform, 0f, -18f, 30f, 28f);
        return button;
    }

    private static RectTransform BuildMapSelectWindow(RectTransform root, out MapOptionBuildBinding[] options, out Button confirmButton, out Button partyButton, out Button closeButton)
    {
        var window = CreatePanel(root, "TownGameSelectWindow", new Color32(7, 13, 17, 244), true);
        SetCenter(window, -170f, 18f, 840f, 650f);

        CreateText(window, "MapSelectTitle", "맵 선택", 27f, TextAlignmentOptions.MidlineLeft, TextMain);
        SetTopLeft(window.Find("MapSelectTitle") as RectTransform, 34f, 18f, 300f, 44f);
        closeButton = CreateButton(window, "CloseButton", "X", 42f, 38f);
        SetTopRight(closeButton.GetComponent<RectTransform>(), 24f, 18f, 42f, 38f);

        var preview = CreateSolid(window, "LargePreview", new Color32(8, 56, 68, 230));
        SetTopLeft(preview.rectTransform, 34f, 78f, 410f, 268f);
        AddBorder(preview.rectTransform, new Color32(88, 117, 126, 220), 2f);
        CreateText(preview.rectTransform, "PreviewLabel", "FOREST", 40f, TextAlignmentOptions.Center, new Color32(46, 119, 132, 160));
        Stretch(preview.rectTransform.Find("PreviewLabel") as RectTransform);

        CreateText(window, "MapSelectDetailTitle", "포레스트 튜토리얼", 27f, TextAlignmentOptions.MidlineLeft, TextMain);
        SetTopLeft(window.Find("MapSelectDetailTitle") as RectTransform, 474f, 82f, 320f, 44f);
        CreateText(window, "MapSelectDetailDesc", "깊고 울창한 포레스트에서 기본 조작을 익힐 수 있는 입문 맵입니다.", 17f, TextAlignmentOptions.TopLeft, TextMuted);
        SetTopLeft(window.Find("MapSelectDetailDesc") as RectTransform, 474f, 136f, 314f, 72f);

        var statBox = CreatePanel(window, "MapSelectStats", new Color32(13, 24, 28, 225), true);
        SetTopLeft(statBox, 474f, 224f, 318f, 122f);
        CreateText(statBox, "Stats", "난이도           쉬움\n권장 플레이어     1~4명\n예상 플레이 시간  5~10분\n목표             모든 적 처치", 16f, TextAlignmentOptions.MidlineLeft, TextMuted);
        SetStretch(statBox.Find("Stats") as RectTransform, 16f, 10f, 16f, 10f);

        var optionList = CreateRect(window, "MapOptionList");
        SetTopLeft(optionList, 34f, 364f, 760f, 188f);
        var builtOptions = new List<MapOptionBuildBinding>();
        var titles = new[] { "포레스트 튜토리얼", "위스퍼링 포레스트", "크리스탈 카번", "테스트 던전" };
        var desc = new[] { "입문 전투와 기본 조작", "숲길 전투와 균열 정리", "강한 적이 등장하는 동굴", "빠른 로컬 테스트" };
        var diff = new[] { "쉬움", "보통", "어려움", "테스트" };
        var meta = new[] { "1~4명  |  5~10분", "1~4명  |  10~15분", "2~4명  |  15~20분", "1~4명  |  5~10분" };
        for (var i = 0; i < 4; i++)
        {
            builtOptions.Add(CreateMapOption(optionList, $"MapOption_{i:00}", titles[i], desc[i], diff[i], meta[i], 0f, i * 47f, 760f, 42f));
        }

        confirmButton = CreateButton(window, "MapSelectConfirmButton", "선택", 210f, 48f, true);
        SetBottomLeft(confirmButton.GetComponent<RectTransform>(), 254f, 22f, 210f, 48f);
        partyButton = CreateButton(window, "MapSelectPartyButton", "파티 관리", 210f, 48f);
        SetBottomLeft(partyButton.GetComponent<RectTransform>(), 492f, 22f, 210f, 48f);

        options = builtOptions.ToArray();
        return window;
    }

    private static RectTransform BuildMapInfoWindow(
        RectTransform root,
        out TMP_Text title,
        out TMP_Text description,
        out TMP_Text stats,
        out TMP_Text[] features,
        out Button closeButton)
    {
        var window = CreatePanel(root, "TownMapInfoWindow", new Color32(7, 13, 17, 244), true);
        SetCenter(window, -150f, 18f, 860f, 620f);

        CreateText(window, "MapInfoHeader", "맵 정보", 26f, TextAlignmentOptions.MidlineLeft, TextMain);
        SetTopLeft(window.Find("MapInfoHeader") as RectTransform, 34f, 18f, 280f, 42f);
        closeButton = CreateButton(window, "MapInfoCloseButton", "X", 42f, 38f);
        SetTopRight(closeButton.GetComponent<RectTransform>(), 24f, 18f, 42f, 38f);

        var preview = CreateSolid(window, "MapInfoPreview", new Color32(8, 56, 68, 230));
        SetTopLeft(preview.rectTransform, 34f, 78f, 386f, 250f);
        AddBorder(preview.rectTransform, new Color32(88, 117, 126, 220), 2f);
        title = CreateText(window, "MapInfoTitle", "타운 포레스트", 28f, TextAlignmentOptions.MidlineLeft, TextMain);
        SetTopLeft(title.rectTransform, 450f, 82f, 350f, 42f);
        description = CreateText(window, "MapInfoDescription", "숲 속 작은 마을과 울창한 숲길이 이어진 지역입니다.", 17f, TextAlignmentOptions.TopLeft, TextMuted);
        SetTopLeft(description.rectTransform, 450f, 136f, 350f, 82f);
        stats = CreateText(window, "MapInfoStats", "난이도  쉬움\n권장 플레이어  1~4명\n예상 시간  5~10분\n목표  모든 적 처치", 16f, TextAlignmentOptions.TopLeft, TextMuted);
        SetTopLeft(stats.rectTransform, 450f, 234f, 350f, 106f);

        CreateText(window, "FeatureTitle", "지역 특징", 20f, TextAlignmentOptions.MidlineLeft, TextMain);
        SetTopLeft(window.Find("FeatureTitle") as RectTransform, 34f, 354f, 200f, 34f);
        var builtFeatures = new List<TMP_Text>();
        for (var i = 0; i < 3; i++)
        {
            var card = CreatePanel(window, $"MapInfoFeature_{i:00}", new Color32(13, 24, 28, 225), true);
            SetTopLeft(card, 34f + i * 266f, 404f, 244f, 116f);
            var ft = CreateText(card, "FeatureText", "마을 입구\n여정의 시작점입니다.", 16f, TextAlignmentOptions.TopLeft, TextMuted);
            SetStretch(ft.rectTransform, 16f, 14f, 16f, 14f);
            builtFeatures.Add(ft);
        }

        var closeWide = CreateButton(window, "MapInfoCloseWideButton", "닫기", 260f, 48f);
        SetBottomCenter(closeWide.GetComponent<RectTransform>(), 0f, 22f, 260f, 48f);
        closeWide.onClick.AddListener(() => window.gameObject.SetActive(false));

        features = builtFeatures.ToArray();
        return window;
    }

    private static MapOptionBuildBinding CreateMapOption(RectTransform parent, string name, string title, string desc, string difficulty, string meta, float x, float y, float w, float h)
    {
        var button = CreateButton(parent, name, "", w, h);
        SetTopLeft(button.GetComponent<RectTransform>(), x, y, w, h);
        var rect = button.GetComponent<RectTransform>();
        var selected = CreateSolid(rect, "SelectedFrame", new Color32(0, 224, 240, 92));
        Stretch(selected.rectTransform);
        selected.gameObject.SetActive(false);
        selected.raycastTarget = false;
        var titleText = CreateText(rect, "Title", title, 17f, TextAlignmentOptions.MidlineLeft, TextMain);
        SetTopLeft(titleText.rectTransform, 86f, 4f, 210f, 20f);
        var descText = CreateText(rect, "Description", desc, 13f, TextAlignmentOptions.MidlineLeft, TextMuted);
        SetTopLeft(descText.rectTransform, 86f, 22f, 250f, 18f);
        var difficultyText = CreateText(rect, "Difficulty", difficulty, 13f, TextAlignmentOptions.Center, Green);
        SetTopLeft(difficultyText.rectTransform, 364f, 10f, 62f, 22f);
        AddTextBackplate(difficultyText.rectTransform, new Color32(24, 60, 30, 230));
        var metaText = CreateText(rect, "Meta", meta, 14f, TextAlignmentOptions.MidlineRight, TextMuted);
        SetTopRight(metaText.rectTransform, 24f, 8f, 220f, 26f);
        var thumb = CreateSolid(rect, "Thumb", new Color32(10, 66, 77, 220));
        SetTopLeft(thumb.rectTransform, 10f, 7f, 64f, 28f);
        return new MapOptionBuildBinding
        {
            Button = button,
            TitleText = titleText,
            DescriptionText = descText,
            DifficultyText = difficultyText,
            MetaText = metaText,
            SelectedFrame = selected
        };
    }

    private static PartySlotBuildBinding CreatePartySlot(RectTransform parent, string name, int index, float x, float y, float w, float h, bool compact)
    {
        var root = CreatePanel(parent, name, new Color32(13, 20, 25, 214), true);
        SetTopLeft(root, x, y, w, h);

        var localFrame = CreateSolid(root, "LocalFrame", new Color32(0, 224, 240, 70));
        Stretch(localFrame.rectTransform);
        localFrame.gameObject.SetActive(false);
        localFrame.raycastTarget = false;

        var indexText = CreateText(root, "Index", index.ToString(), compact ? 17f : 30f, TextAlignmentOptions.Center, TextMain);
        SetTopLeft(indexText.rectTransform, compact ? 12f : 12f, compact ? 7f : 8f, compact ? 34f : 54f, compact ? 24f : 48f);
        AddTextBackplate(indexText.rectTransform, new Color32(27, 35, 42, 255));

        var nameText = CreateText(root, "Name", $"Player {index}", compact ? 15f : 18f, TextAlignmentOptions.MidlineLeft, TextMain);
        SetTopLeft(nameText.rectTransform, compact ? 58f : 76f, compact ? 6f : 10f, compact ? 150f : 176f, compact ? 26f : 28f);

        var hostBadge = CreateText(root, "HostBadge", "HOST", compact ? 11f : 12f, TextAlignmentOptions.Center, Color.black);
        SetTopLeft(hostBadge.rectTransform, compact ? 212f : 228f, compact ? 8f : 12f, 46f, compact ? 20f : 22f);
        AddTextBackplate(hostBadge.rectTransform, Cyan);

        var readyBack = CreateSolid(root, "ReadyGraphic", Green);
        SetTopRight(readyBack.rectTransform, compact ? 14f : 18f, compact ? 10f : 16f, compact ? 20f : 24f, compact ? 20f : 24f);
        var readyText = CreateText(readyBack.rectTransform, "ReadyText", "OK", compact ? 8f : 9f, TextAlignmentOptions.Center, Color.black);
        Stretch(readyText.rectTransform);

        var hpSlider = CreateHpSlider(root, compact ? 58f : 76f, compact ? 28f : 42f, compact ? 226f : 190f, compact ? 5f : 6f);
        var hpText = CreateText(root, "HpText", "100/100", compact ? 11f : 13f, TextAlignmentOptions.MidlineRight, TextMain);
        SetTopRight(hpText.rectTransform, compact ? 44f : 18f, compact ? 6f : 14f, compact ? 60f : 74f, compact ? 22f : 22f);

        return new PartySlotBuildBinding
        {
            Root = root.gameObject,
            IndexText = indexText,
            NameText = nameText,
            HpText = hpText,
            HostBadgeText = hostBadge,
            ReadyText = readyText,
            ReadyGraphic = readyBack,
            LocalFrame = localFrame,
            HpSlider = hpSlider
        };
    }

    private static Slider CreateHpSlider(RectTransform parent, float x, float y, float w, float h)
    {
        var back = CreateSolid(parent, "HpBar", new Color32(23, 42, 45, 255));
        SetTopLeft(back.rectTransform, x, y, w, h);
        var fillArea = CreateRect(back.rectTransform, "Fill Area");
        Stretch(fillArea);
        var fill = CreateSolid(fillArea, "Fill", Cyan);
        Stretch(fill.rectTransform);
        var slider = back.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.fillRect = fill.rectTransform;
        slider.targetGraphic = fill;
        return slider;
    }

    private static void BindPanel(
        TownExpeditionPanel panel,
        Canvas canvas,
        RectTransform root,
        TMP_Text topTownTitle,
        TMP_Text topStatus,
        TMP_Text status,
        RectTransform partyPanelRoot,
        RectTransform partyPanelBody,
        TMP_Text roleBadge,
        TMP_Text partyCount,
        TMP_Text selectedMapTitle,
        TMP_Text selectedMapMeta,
        TMP_Text selectedMapDifficulty,
        TMP_Text selectedMapGoal,
        TMP_Text inviteCode,
        TMP_Text clientHint,
        TMP_Text minimapCount,
        TMP_Text readySummary,
        RectTransform hostControls,
        RectTransform clientControls,
        RectTransform mapSelectWindow,
        RectTransform mapInfoWindow,
        TMP_Text mapInfoTitle,
        TMP_Text mapInfoDescription,
        TMP_Text mapInfoStats,
        TMP_Text[] mapInfoFeatureTexts,
        PartySlotBuildBinding[] partySlots,
        PartySlotBuildBinding[] sidePartySlots,
        MapOptionBuildBinding[] mapOptions,
        Button inventoryButton,
        Button gameSelectButton,
        Button mapSelectConfirmButton,
        Button mapSelectPartyButton,
        Button mapSelectCloseButton,
        Button mapInfoButton,
        Button mapInfoCloseButton,
        Button copyInviteButton,
        Button partyMinimizeButton,
        TMP_Text partyMinimizeLabel,
        Button readyButton,
        Button hostStartButton,
        Button hostCancelButton,
        Button partyManageButton)
    {
        var so = new SerializedObject(panel);
        so.FindProperty("_canvas").objectReferenceValue = canvas;
        so.FindProperty("_root").objectReferenceValue = root;
        so.FindProperty("_topTownTitleText").objectReferenceValue = topTownTitle;
        so.FindProperty("_topStatusText").objectReferenceValue = topStatus;
        so.FindProperty("_statusText").objectReferenceValue = status;
        so.FindProperty("_roleBadgeText").objectReferenceValue = roleBadge;
        so.FindProperty("_partyCountText").objectReferenceValue = partyCount;
        so.FindProperty("_selectedMapTitleText").objectReferenceValue = selectedMapTitle;
        so.FindProperty("_selectedMapMetaText").objectReferenceValue = selectedMapMeta;
        so.FindProperty("_selectedMapDifficultyText").objectReferenceValue = selectedMapDifficulty;
        so.FindProperty("_selectedMapGoalText").objectReferenceValue = selectedMapGoal;
        so.FindProperty("_inviteCodeText").objectReferenceValue = inviteCode;
        so.FindProperty("_clientHintText").objectReferenceValue = clientHint;
        so.FindProperty("_minimapCountText").objectReferenceValue = minimapCount;
        so.FindProperty("_readySummaryText").objectReferenceValue = readySummary;
        so.FindProperty("_partyPanelRoot").objectReferenceValue = partyPanelRoot;
        so.FindProperty("_partyPanelBodyRoot").objectReferenceValue = partyPanelBody;
        so.FindProperty("_partyMinimizeLabelText").objectReferenceValue = partyMinimizeLabel;
        so.FindProperty("_hostControlsRoot").objectReferenceValue = hostControls;
        so.FindProperty("_clientControlsRoot").objectReferenceValue = clientControls;
        so.FindProperty("_gameSelectWindow").objectReferenceValue = mapSelectWindow;
        so.FindProperty("_mapInfoWindow").objectReferenceValue = mapInfoWindow;
        so.FindProperty("_mapInfoTitleText").objectReferenceValue = mapInfoTitle;
        so.FindProperty("_mapInfoDescriptionText").objectReferenceValue = mapInfoDescription;
        so.FindProperty("_mapInfoStatsText").objectReferenceValue = mapInfoStats;
        BindObjectArray(so.FindProperty("_mapInfoFeatureTexts"), mapInfoFeatureTexts);
        BindPartySlots(so.FindProperty("_partySlots"), partySlots);
        BindPartySlots(so.FindProperty("_sidePartySlots"), sidePartySlots);
        BindMapOptions(so.FindProperty("_mapOptions"), mapOptions);
        so.FindProperty("_inventoryButton").objectReferenceValue = inventoryButton;
        so.FindProperty("_gameSelectButton").objectReferenceValue = gameSelectButton;
        so.FindProperty("_mapSelectConfirmButton").objectReferenceValue = mapSelectConfirmButton;
        so.FindProperty("_mapSelectPartyButton").objectReferenceValue = mapSelectPartyButton;
        so.FindProperty("_gameSelectCloseButton").objectReferenceValue = mapSelectCloseButton;
        so.FindProperty("_mapInfoButton").objectReferenceValue = mapInfoButton;
        so.FindProperty("_mapInfoCloseButton").objectReferenceValue = mapInfoCloseButton;
        so.FindProperty("_copyInviteButton").objectReferenceValue = copyInviteButton;
        so.FindProperty("_partyMinimizeButton").objectReferenceValue = partyMinimizeButton;
        so.FindProperty("_readyWindowButton").objectReferenceValue = readyButton;
        so.FindProperty("_hostStartGameButton").objectReferenceValue = hostStartButton;
        so.FindProperty("_hostCancelGameButton").objectReferenceValue = hostCancelButton;
        so.FindProperty("_partyManageButton").objectReferenceValue = partyManageButton;
        so.FindProperty("_pollIntervalMs").intValue = 500;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(panel);
    }

    private static void BindObjectArray<T>(SerializedProperty property, T[] values) where T : Object
    {
        property.arraySize = values?.Length ?? 0;
        for (var i = 0; i < property.arraySize; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void BindPartySlots(SerializedProperty property, PartySlotBuildBinding[] slots)
    {
        property.arraySize = slots?.Length ?? 0;
        for (var i = 0; i < property.arraySize; i++)
        {
            var slot = slots[i];
            var element = property.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Root").objectReferenceValue = slot.Root;
            element.FindPropertyRelative("IndexText").objectReferenceValue = slot.IndexText;
            element.FindPropertyRelative("NameText").objectReferenceValue = slot.NameText;
            element.FindPropertyRelative("HpText").objectReferenceValue = slot.HpText;
            element.FindPropertyRelative("HostBadgeText").objectReferenceValue = slot.HostBadgeText;
            element.FindPropertyRelative("ReadyText").objectReferenceValue = slot.ReadyText;
            element.FindPropertyRelative("ReadyGraphic").objectReferenceValue = slot.ReadyGraphic;
            element.FindPropertyRelative("LocalFrame").objectReferenceValue = slot.LocalFrame;
            element.FindPropertyRelative("HpSlider").objectReferenceValue = slot.HpSlider;
        }
    }

    private static void BindMapOptions(SerializedProperty property, MapOptionBuildBinding[] options)
    {
        property.arraySize = options?.Length ?? 0;
        for (var i = 0; i < property.arraySize; i++)
        {
            var option = options[i];
            var element = property.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Button").objectReferenceValue = option.Button;
            element.FindPropertyRelative("TitleText").objectReferenceValue = option.TitleText;
            element.FindPropertyRelative("DescriptionText").objectReferenceValue = option.DescriptionText;
            element.FindPropertyRelative("DifficultyText").objectReferenceValue = option.DifficultyText;
            element.FindPropertyRelative("MetaText").objectReferenceValue = option.MetaText;
            element.FindPropertyRelative("SelectedFrame").objectReferenceValue = option.SelectedFrame;
        }
    }

    private static void ConfigureTownInventory()
    {
        var root = GameObject.Find("TownInventory_UI");
        if (root == null)
            return;

        var canvas = GetOrAdd<Canvas>(root);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = InventorySortingOrder;

        var scaler = GetOrAdd<CanvasScaler>(root);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        GetOrAdd<GraphicRaycaster>(root);

        var panel = root.transform.Find("Panel") as RectTransform;
        if (panel != null)
        {
            panel.anchorMin = new Vector2(0.04f, 0.08f);
            panel.anchorMax = new Vector2(0.62f, 0.92f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.gameObject.SetActive(false);
        }

        EditorUtility.SetDirty(root);
    }

    private static ApiClientProvider EnsureApiClientProvider()
    {
        var provider = Object.FindFirstObjectByType<ApiClientProvider>(FindObjectsInactive.Include);
        if (provider != null)
        {
            provider.gameObject.SetActive(true);
            EditorUtility.SetDirty(provider.gameObject);
            return provider;
        }

        GameObject go = null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ApiClientProviderPrefabPath);
        if (prefab != null)
            go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        if (go == null)
            go = new GameObject("ApiClientProvider", typeof(ApiClientProvider));

        go.name = "ApiClientProvider";
        go.SetActive(true);
        EditorUtility.SetDirty(go);
        return go.GetComponent<ApiClientProvider>() ?? go.AddComponent<ApiClientProvider>();
    }

    private static void EnsureRoomUiRoot(ApiClientProvider apiProvider)
    {
        var controller = Object.FindFirstObjectByType<RoomUiController>(FindObjectsInactive.Include);
        GameObject root = controller != null ? controller.gameObject : null;

        if (root == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomUiRootPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[TownSceneUiBuilder] Missing Room UI prefab: {RoomUiRootPrefabPath}");
                return;
            }

            root = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (root == null)
            {
                Debug.LogError("[TownSceneUiBuilder] Failed to instantiate RoomUIRoot prefab.");
                return;
            }
        }

        root.name = "RoomUIRoot";
        controller = root.GetComponentInChildren<RoomUiController>(true);
        if (controller != null && apiProvider != null)
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("apiProvider").objectReferenceValue = apiProvider;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        root.SetActive(false);
        EditorUtility.SetDirty(root);
    }

    private static void EnsureEventSystem()
    {
        var eventSystem = Object.FindObjectOfType<EventSystem>(true);
        GameObject eventSystemGo;
        if (eventSystem == null)
        {
            eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
        else
        {
            eventSystemGo = eventSystem.gameObject;
            eventSystemGo.SetActive(true);
        }

        GetOrAdd<EventSystem>(eventSystemGo).enabled = true;
        GetOrAdd<InputSystemUIInputModule>(eventSystemGo).enabled = true;
        var standalone = eventSystemGo.GetComponent<StandaloneInputModule>();
        if (standalone != null)
            standalone.enabled = false;

        EditorUtility.SetDirty(eventSystemGo);
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static Image CreateSolid(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreatePanel(Transform parent, string name, Color color, bool raycastTarget)
    {
        var image = CreateSolid(parent, name, color);
        image.raycastTarget = raycastTarget;
        AddBorder(image.rectTransform, Border, 2f);
        return image.rectTransform;
    }

    private static Button CreateButton(Transform parent, string name, string label, float width, float height, bool primary = false)
    {
        var image = CreateSolid(parent, name, primary ? new Color32(0, 98, 112, 230) : new Color32(18, 28, 34, 238));
        image.raycastTarget = true;
        image.rectTransform.sizeDelta = new Vector2(width, height);
        AddBorder(image.rectTransform, primary ? Cyan : Border, primary ? 2.5f : 1.5f);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
        colors.pressedColor = new Color(0.78f, 0.92f, 0.96f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.42f);
        button.colors = colors;

        if (!string.IsNullOrWhiteSpace(label))
        {
            var labelSize = primary
                ? Mathf.Min(26f, Mathf.Max(15f, height * 0.45f))
                : Mathf.Min(18f, Mathf.Max(13f, height * 0.45f));
            var text = CreateText(image.rectTransform, "Label", label, labelSize, TextAlignmentOptions.Center, TextMain);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(text.rectTransform);
        }

        return button;
    }

    private static TMP_Text CreateText(RectTransform parent, string name, string value, float size, TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(6f, size - 10f);
        text.fontSizeMax = size;
        text.margin = new Vector4(2f, 1f, 2f, 1f);
        var font = GetFont();
        if (font != null)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }

        return text;
    }

    private static void AddTextBackplate(RectTransform textRect, Color color)
    {
        if (textRect == null || textRect.parent == null)
            return;

        var back = CreateSolid(textRect.parent, $"{textRect.name}_Backplate", color);
        back.transform.SetSiblingIndex(textRect.GetSiblingIndex());
        back.raycastTarget = false;
        back.rectTransform.anchorMin = textRect.anchorMin;
        back.rectTransform.anchorMax = textRect.anchorMax;
        back.rectTransform.pivot = textRect.pivot;
        back.rectTransform.sizeDelta = textRect.sizeDelta;
        back.rectTransform.anchoredPosition = textRect.anchoredPosition;
    }

    private static void AddBorder(RectTransform parent, Color color, float thickness)
    {
        var top = CreateSolid(parent, "BorderTop", color);
        SetRect(top.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness), Vector2.zero);
        var bottom = CreateSolid(parent, "BorderBottom", color);
        SetRect(bottom.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness), Vector2.zero);
        var left = CreateSolid(parent, "BorderLeft", color);
        SetRect(left.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f), Vector2.zero);
        var right = CreateSolid(parent, "BorderRight", color);
        SetRect(right.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f), Vector2.zero);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetStretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 anchoredPosition)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void SetTopLeft(RectTransform rect, float x, float y, float w, float h)
    {
        SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(w, h), new Vector2(x, -y));
    }

    private static void SetTopRight(RectTransform rect, float x, float y, float w, float h)
    {
        SetRect(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(w, h), new Vector2(-x, -y));
    }

    private static void SetBottomLeft(RectTransform rect, float x, float y, float w, float h)
    {
        SetRect(rect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(w, h), new Vector2(x, y));
    }

    private static void SetBottomRight(RectTransform rect, float x, float y, float w, float h)
    {
        SetRect(rect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(w, h), new Vector2(-x, y));
    }

    private static void SetTopCenter(RectTransform rect, float x, float y, float w, float h)
    {
        SetRect(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(w, h), new Vector2(x, -y));
    }

    private static void SetBottomCenter(RectTransform rect, float x, float y, float w, float h)
    {
        SetRect(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(w, h), new Vector2(x, y));
    }

    private static void SetCenter(RectTransform rect, float x, float y, float w, float h)
    {
        SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(w, h), new Vector2(x, y));
    }

    private static void SetRightCenter(RectTransform rect, float x, float y, float w, float h)
    {
        SetRect(rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(w, h), new Vector2(-x, y));
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private static TMP_FontAsset GetFont()
    {
        if (_font != null)
            return _font;

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/NanumGothic SDF.asset");
        if (_font == null)
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/NanumGothic SDF.asset");
        return _font;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }
}
