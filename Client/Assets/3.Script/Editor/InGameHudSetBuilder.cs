using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class InGameHudSetBuilder
{
    private const string MenuPath = "Tools/RhythmRPG/UI/Rebuild InGame HUD Set";
    private const string ResourceRoot = "Assets/Resources/UI/UI_InGame/";

    private static readonly string[] HudPrefabPaths =
    {
        "Assets/0.MainProject/Resources/GameInit/Canvas_RhythmHUD.prefab",
        "Assets/0.MainProject/Resources/Canvas_RhythmHUD.prefab"
    };

    private static readonly Color FillGlow = new Color(0.35f, 1f, 1f, 0.95f);
    private static readonly Color KeyText = new Color(0.88f, 0.98f, 1f, 1f);

    [MenuItem(MenuPath)]
    public static void RebuildInGameHudSet()
    {
        EnsureSpriteSlices();
        var sprites = LoadSprites();

        foreach (string prefabPath in HudPrefabPaths)
            RebuildPrefab(prefabPath, sprites);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[InGameHudSetBuilder] Rebuilt in-game HUD prefabs from UI_InGame resources.");
    }

    private static void RebuildPrefab(string prefabPath, HudSprites sprites)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            HudConfig previousConfig = FindHudConfig(root);

            ConfigureCanvas(root);
            ClearChildren(root.transform);

            RectTransform hudRoot = CreateRect("HUDRoot", root.transform);
            Stretch(hudRoot);

            PartyMemberPanelView[] partyPanels = BuildPartyPanels(hudRoot, sprites);
            StageInfoPanelView stageInfo = BuildStagePanel(hudRoot, sprites);
            BeatGuideView beatGuide = BuildBeatGuide(hudRoot);
            ComboCounterView comboView = BuildComboFlourish(hudRoot, sprites);
            MinimapHudView minimapView = BuildMinimap(hudRoot);

            RectTransform combatDock = CreateRect("CombatDock", hudRoot);
            Anchor(combatDock, new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(1120f, 320f), new Vector2(0.5f, 0f));

            HexHudView hexHud = BuildHexHud(combatDock, sprites);
            SkillSlotView[] skillSlots = BuildSkillBar(combatDock, sprites);
            BuildPresenter(combatDock, previousConfig, hexHud, skillSlots, partyPanels, stageInfo, beatGuide, comboView, minimapView);

            SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureCanvas(GameObject root)
    {
        RectTransform rect = RequireRect(root);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;

        Canvas canvas = GetOrAdd<Canvas>(root);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = GetOrAdd<CanvasScaler>(root);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GetOrAdd<GraphicRaycaster>(root);
    }

    private static PartyMemberPanelView[] BuildPartyPanels(RectTransform parent, HudSprites sprites)
    {
        RectTransform party = CreateRect("PartyPanels", parent);
        Anchor(party, new Vector2(0f, 1f), new Vector2(66f, -52f), new Vector2(382f, 312f), new Vector2(0f, 1f));

        float[] fillSamples = { 0.96f, 0.82f, 0.68f, 0.54f };
        var panels = new PartyMemberPanelView[fillSamples.Length];
        for (int i = 0; i < fillSamples.Length; i++)
        {
            panels[i] = BuildPartyPanel(
                party,
                sprites,
                $"PartyPanel_{i}",
                new Vector2(0f, -4f - 74f * i),
                fillSamples[i],
                i);
        }

        return panels;
    }

    private static PartyMemberPanelView BuildPartyPanel(
        RectTransform parent,
        HudSprites sprites,
        string name,
        Vector2 position,
        float fillScale,
        int index)
    {
        RectTransform panel = CreateImage(name, parent, sprites.PlayerInfo);
        Anchor(panel, new Vector2(0f, 1f), position, new Vector2(360f, 68f), new Vector2(0f, 1f));

        RectTransform fill = CreateImage("Fill", panel, sprites.FillBar);
        Anchor(fill, new Vector2(0f, 0.5f), new Vector2(101f, -11f), new Vector2(210f, 12f), new Vector2(0f, 0.5f));
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = FillGlow;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = fillScale;

        TextMeshProUGUI nameText = CreateText("Name", panel, $"Player {index + 1}", 18f);
        Anchor(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(101f, 8f), new Vector2(176f, 22f), new Vector2(0f, 0.5f));
        nameText.alignment = TextAlignmentOptions.Left;
        nameText.color = new Color(0.92f, 0.98f, 1f, 1f);

        TextMeshProUGUI hpText = CreateText("HpText", panel, "100/100", 14f);
        Anchor(hpText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-27f, 8f), new Vector2(70f, 20f), new Vector2(1f, 0.5f));
        hpText.alignment = TextAlignmentOptions.Right;
        hpText.color = new Color(0.78f, 0.92f, 0.96f, 1f);

        PartyMemberPanelView view = panel.gameObject.AddComponent<PartyMemberPanelView>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("hpFill").objectReferenceValue = fillImage;
        serializedView.FindProperty("nameText").objectReferenceValue = nameText;
        serializedView.FindProperty("hpText").objectReferenceValue = hpText;
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static StageInfoPanelView BuildStagePanel(RectTransform parent, HudSprites sprites)
    {
        RectTransform stage = CreateImage("StagePanel", parent, sprites.StageInfo);
        Anchor(stage, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(790f, 72f), new Vector2(0.5f, 1f));

        TextMeshProUGUI stageText = CreateText("StageText", stage, "Crystal Cavern", 19f);
        Anchor(stageText.rectTransform, new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(204f, 28f), new Vector2(0f, 0.5f));
        stageText.alignment = TextAlignmentOptions.Left;
        stageText.color = new Color(0.92f, 0.98f, 1f, 1f);

        TextMeshProUGUI bpmText = CreateText("BpmText", stage, "BPM 128", 17f);
        Anchor(bpmText.rectTransform, new Vector2(0f, 0.5f), new Vector2(244f, 0f), new Vector2(94f, 24f), new Vector2(0f, 0.5f));
        bpmText.alignment = TextAlignmentOptions.Left;
        bpmText.color = new Color(0.84f, 0.96f, 1f, 1f);
        bpmText.gameObject.SetActive(false);

        RectTransform marker = CreateImage("BeatMarker", stage, sprites.DecorationActive);
        Anchor(marker, new Vector2(0.5f, 0.5f), new Vector2(94f, -1f), new Vector2(24f, 24f), new Vector2(0.5f, 0.5f));
        marker.gameObject.SetActive(false);

        StageInfoPanelView view = stage.gameObject.AddComponent<StageInfoPanelView>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("stageText").objectReferenceValue = stageText;
        serializedView.FindProperty("bpmText").objectReferenceValue = bpmText;
        serializedView.FindProperty("beatMarker").objectReferenceValue = marker;
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static BeatGuideView BuildBeatGuide(RectTransform parent)
    {
        RectTransform root = CreateRect("BeatGuide", parent);
        Anchor(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 170f), new Vector2(0.5f, 0.5f));

        CanvasGroup canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        TextMeshProUGUI[] leftGuides = BuildGuideTexts(root, "LeftGuide", "<", -310f);
        TextMeshProUGUI[] rightGuides = BuildGuideTexts(root, "RightGuide", ">", 310f);
        TextMeshProUGUI[] inputPositionGuides = BuildInputPositionGuides(root);

        BeatGuideView view = root.gameObject.AddComponent<BeatGuideView>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;

        SerializedProperty left = serializedView.FindProperty("leftGuides");
        left.arraySize = leftGuides.Length;
        for (int i = 0; i < leftGuides.Length; i++)
            left.GetArrayElementAtIndex(i).objectReferenceValue = leftGuides[i];

        SerializedProperty right = serializedView.FindProperty("rightGuides");
        right.arraySize = rightGuides.Length;
        for (int i = 0; i < rightGuides.Length; i++)
            right.GetArrayElementAtIndex(i).objectReferenceValue = rightGuides[i];

        SerializedProperty inputPosition = serializedView.FindProperty("inputPositionGuides");
        inputPosition.arraySize = inputPositionGuides.Length;
        for (int i = 0; i < inputPositionGuides.Length; i++)
            inputPosition.GetArrayElementAtIndex(i).objectReferenceValue = inputPositionGuides[i];

        serializedView.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static TextMeshProUGUI[] BuildGuideTexts(RectTransform parent, string prefix, string glyph, float anchoredX)
    {
        const int guideCount = 4;
        var guides = new TextMeshProUGUI[guideCount];
        for (int i = 0; i < guideCount; i++)
        {
            TextMeshProUGUI guide = CreateText($"{prefix}_{i}", parent, glyph, 56f);
            Anchor(guide.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(anchoredX, 0f), new Vector2(66f, 78f), new Vector2(0.5f, 0.5f));
            guide.alignment = TextAlignmentOptions.Center;
            guide.fontStyle = FontStyles.Bold;
            guide.color = new Color(0.88f, 0.98f, 0.92f, 0.82f);
            guides[i] = guide;
        }

        return guides;
    }

    private static TextMeshProUGUI[] BuildInputPositionGuides(RectTransform parent)
    {
        TextMeshProUGUI left = CreateText("InputPositionLeft", parent, "<", 56f);
        Anchor(left.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-62f, 0f), new Vector2(66f, 78f), new Vector2(0.5f, 0.5f));
        ConfigureInputPositionGuide(left);

        TextMeshProUGUI right = CreateText("InputPositionRight", parent, ">", 56f);
        Anchor(right.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(62f, 0f), new Vector2(66f, 78f), new Vector2(0.5f, 0.5f));
        ConfigureInputPositionGuide(right);

        return new[] { left, right };
    }

    private static void ConfigureInputPositionGuide(TextMeshProUGUI guide)
    {
        guide.alignment = TextAlignmentOptions.Center;
        guide.fontStyle = FontStyles.Bold;
        guide.color = new Color(0.88f, 0.98f, 0.92f, 1f);
        guide.faceColor = new Color(0.88f, 0.98f, 0.92f, 0f);
        guide.outlineColor = new Color(0.88f, 0.98f, 0.92f, 0.42f);
        guide.outlineWidth = 0.24f;
    }

    private static ComboCounterView BuildComboFlourish(RectTransform parent, HudSprites sprites)
    {
        RectTransform combo = CreateRect("ComboFlourish", parent);
        Anchor(combo, new Vector2(1f, 0.5f), new Vector2(-250f, 92f), new Vector2(360f, 170f), new Vector2(0.5f, 0.5f));

        RectTransform underbar = CreateImage("ComboUnderbar", combo, sprites.ComboUnderbar);
        Anchor(underbar, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(290f, 28f), new Vector2(0.5f, 0.5f));

        RectTransform effect = CreateImage("ComboEffect", combo, sprites.ComboEffect);
        Anchor(effect, new Vector2(0.5f, 0.5f), new Vector2(42f, 0f), new Vector2(230f, 120f), new Vector2(0.5f, 0.5f));
        effect.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.7f);

        TextMeshProUGUI countText = CreateText("ComboCount", combo, "x23", 42f);
        Anchor(countText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-30f, 12f), new Vector2(150f, 54f), new Vector2(0.5f, 0.5f));
        countText.alignment = TextAlignmentOptions.Right;
        countText.color = new Color(0.95f, 1f, 1f, 1f);

        TextMeshProUGUI labelText = CreateText("ComboLabel", combo, "COMBO", 18f);
        Anchor(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(69f, -3f), new Vector2(100f, 28f), new Vector2(0.5f, 0.5f));
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.color = new Color(0.84f, 0.96f, 1f, 1f);

        CanvasGroup canvasGroup = combo.gameObject.AddComponent<CanvasGroup>();
        ComboCounterView view = combo.gameObject.AddComponent<ComboCounterView>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("rootTransform").objectReferenceValue = combo;
        serializedView.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        serializedView.FindProperty("comboCountText").objectReferenceValue = countText;
        serializedView.FindProperty("comboLabelText").objectReferenceValue = labelText;
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static MinimapHudView BuildMinimap(RectTransform parent)
    {
        RectTransform minimap = CreateRect("MinimapPanel", parent);
        Anchor(minimap, new Vector2(1f, 1f), new Vector2(-56f, -52f), new Vector2(304f, 304f), new Vector2(1f, 1f));

        MinimapHudView view = minimap.gameObject.AddComponent<MinimapHudView>();
        view.EnsureRuntimeUi();
        return view;
    }

    private static HexHudView BuildHexHud(RectTransform parent, HudSprites sprites)
    {
        RectTransform hex = CreateRect("HexHud", parent);
        Anchor(hex, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(252f, 252f), new Vector2(0.5f, 0f));

        HexHudView view = hex.gameObject.AddComponent<HexHudView>();

        RectTransform pulse = CreateImage("HP_Pulse", hex, sprites.HpFill);
        Stretch(pulse);
        Image pulseImage = pulse.GetComponent<Image>();
        pulseImage.color = new Color(0.3f, 1f, 1f, 0f);

        RectTransform body = CreateImage("HP_Body", hex, sprites.HpBody);
        Stretch(body);

        RectTransform fill = CreateImage("HP_Fill", hex, sprites.HpFill);
        Stretch(fill);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Radial360;
        fillImage.fillOrigin = (int)Image.Origin360.Top;
        fillImage.fillClockwise = false;
        fillImage.fillAmount = 1f;

        RectTransform frame = CreateImage("HP_Frame", hex, sprites.HpFrame);
        Stretch(frame);

        TextMeshProUGUI hpText = CreateText("HP_DebugText", hex, "100/100", 30f);
        Anchor(hpText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(160f, 44f), new Vector2(0.5f, 0.5f));
        hpText.color = new Color(0.9f, 1f, 1f, 1f);

        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("hpFill").objectReferenceValue = fillImage;
        serializedView.FindProperty("hpFrame").objectReferenceValue = frame.GetComponent<Image>();
        serializedView.FindProperty("hpGlow").objectReferenceValue = pulseImage;
        serializedView.FindProperty("hpText").objectReferenceValue = hpText;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        BeatPulse beatPulse = hex.gameObject.AddComponent<BeatPulse>();
        SerializedObject serializedPulse = new SerializedObject(beatPulse);
        serializedPulse.FindProperty("hud").objectReferenceValue = view;
        serializedPulse.FindProperty("pulseScale").floatValue = 1.12f;
        serializedPulse.FindProperty("pulseDuration").floatValue = 0.18f;
        serializedPulse.FindProperty("glowPeakAlpha").floatValue = 0.68f;
        serializedPulse.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static SkillSlotView[] BuildSkillBar(RectTransform parent, HudSprites sprites)
    {
        RectTransform bar = CreateRect("SkillBar", parent);
        Anchor(bar, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(1080f, 252f), new Vector2(0.5f, 0f));

        float[] positions = { -382f, -194f, 194f, 382f };
        string[] keyLabels = { "H", "J", "K", "L" };
        var slots = new SkillSlotView[positions.Length];

        for (int i = 0; i < positions.Length; i++)
            slots[i] = BuildSkillSlot(bar, sprites, i, positions[i], keyLabels[i]);

        return slots;
    }

    private static SkillSlotView BuildSkillSlot(RectTransform parent, HudSprites sprites, int index, float x, string keyLabel)
    {
        RectTransform slot = CreateRect($"Slot_{index}", parent);
        Anchor(slot, new Vector2(0.5f, 0f), new Vector2(x, 10f), new Vector2(176f, 204f), new Vector2(0.5f, 0f));

        RectTransform frame = CreateImage("BG", slot, sprites.SkillFrame);
        Anchor(frame, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(166f, 166f), new Vector2(0.5f, 0.5f));

        RectTransform icon = CreateImage("Icon", slot, null);
        Anchor(icon, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(96f, 96f), new Vector2(0.5f, 0.5f));
        Image iconImage = icon.GetComponent<Image>();
        iconImage.enabled = false;
        iconImage.color = new Color(1f, 1f, 1f, 0f);

        RectTransform ring = CreateImage("Ring", slot, sprites.SkillRing);
        Anchor(ring, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(122f, 122f), new Vector2(0.5f, 0.5f));

        RectTransform cooldown = CreateImage("CooldownMask", slot, sprites.SkillCooldown);
        Anchor(cooldown, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(122f, 122f), new Vector2(0.5f, 0.5f));
        Image cooldownImage = cooldown.GetComponent<Image>();
        cooldownImage.type = Image.Type.Filled;
        cooldownImage.fillMethod = Image.FillMethod.Radial360;
        cooldownImage.fillOrigin = (int)Image.Origin360.Top;
        cooldownImage.fillClockwise = false;
        cooldown.gameObject.SetActive(false);

        RectTransform keyCap = CreateImage("KeyCap", slot, sprites.SkillKeyCap);
        Anchor(keyCap, new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(46f, 34f), new Vector2(0.5f, 0f));

        TextMeshProUGUI cooldownText = CreateText("CooldownText", slot, keyLabel, 21f);
        Anchor(cooldownText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(42f, 26f), new Vector2(0.5f, 0f));
        cooldownText.color = KeyText;

        SkillSlotView slotView = slot.gameObject.AddComponent<SkillSlotView>();
        SerializedObject serializedSlot = new SerializedObject(slotView);
        serializedSlot.FindProperty("icon").objectReferenceValue = iconImage;
        serializedSlot.FindProperty("cooldownMask").objectReferenceValue = cooldownImage;
        serializedSlot.FindProperty("cooldownText").objectReferenceValue = cooldownText;
        serializedSlot.ApplyModifiedPropertiesWithoutUndo();
        return slotView;
    }

    private static void BuildPresenter(
        RectTransform parent,
        HudConfig config,
        HexHudView view,
        SkillSlotView[] skillSlots,
        PartyMemberPanelView[] partyPanels,
        StageInfoPanelView stageInfo,
        BeatGuideView beatGuide,
        ComboCounterView comboView,
        MinimapHudView minimapView)
    {
        RectTransform presenterRect = CreateRect("HudPresenter", parent);
        Anchor(presenterRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));

        HudPresenter presenter = presenterRect.gameObject.AddComponent<HudPresenter>();
        SerializedObject serializedPresenter = new SerializedObject(presenter);
        serializedPresenter.FindProperty("_config").objectReferenceValue = config;
        serializedPresenter.FindProperty("_view").objectReferenceValue = view;

        SerializedProperty slots = serializedPresenter.FindProperty("_skillSlots");
        slots.arraySize = skillSlots.Length;
        for (int i = 0; i < skillSlots.Length; i++)
            slots.GetArrayElementAtIndex(i).objectReferenceValue = skillSlots[i];

        SerializedProperty party = serializedPresenter.FindProperty("_partyPanels");
        party.arraySize = partyPanels.Length;
        for (int i = 0; i < partyPanels.Length; i++)
            party.GetArrayElementAtIndex(i).objectReferenceValue = partyPanels[i];

        serializedPresenter.FindProperty("_stageInfo").objectReferenceValue = stageInfo;
        serializedPresenter.FindProperty("_beatGuide").objectReferenceValue = beatGuide;
        serializedPresenter.FindProperty("_comboView").objectReferenceValue = comboView;
        serializedPresenter.FindProperty("_minimapView").objectReferenceValue = minimapView;
        serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
    }

    private static HudConfig FindHudConfig(GameObject root)
    {
        HudPresenter presenter = root.GetComponentInChildren<HudPresenter>(true);
        if (presenter == null)
            return null;

        SerializedObject serializedPresenter = new SerializedObject(presenter);
        return serializedPresenter.FindProperty("_config").objectReferenceValue as HudConfig;
    }

    private static void EnsureSpriteSlices()
    {
        SliceSingle("Player_Info.png", "Player_Info_Frame");
        SliceSingle("Stage_Info.png", "Stage_Info_Frame");
        SliceSingle("Fill_Bar.png", "Fill_Bar");
        SliceSingle("Skill_Fill.png", "Skill_Fill");
        SliceSingle("Combo_UnderBar.png", "Combo_UnderBar");
        SliceSingle("Combo_Effect.png", "Combo_Effect");
        SliceColumns("HP_UI.png", "HP_Frame", "HP_Body", "HP_Fill");
        SliceColumns("Skill_Slot.png", "Skill_Frame", "Skill_Ring", "Skill_Cooldown", "Skill_KeyCap");
        SliceColumns("UI_Decoration.png", "Decoration_Idle", "Decoration_Active");
    }

    private static HudSprites LoadSprites()
    {
        return new HudSprites
        {
            PlayerInfo = LoadSprite("Player_Info.png", "Player_Info_Frame"),
            StageInfo = LoadSprite("Stage_Info.png", "Stage_Info_Frame"),
            FillBar = LoadSprite("Fill_Bar.png", "Fill_Bar"),
            ComboUnderbar = LoadSprite("Combo_UnderBar.png", "Combo_UnderBar"),
            ComboEffect = LoadSprite("Combo_Effect.png", "Combo_Effect"),
            HpFrame = LoadSprite("HP_UI.png", "HP_Frame"),
            HpBody = LoadSprite("HP_UI.png", "HP_Body"),
            HpFill = LoadSprite("HP_UI.png", "HP_Fill"),
            SkillFrame = LoadSprite("Skill_Slot.png", "Skill_Frame"),
            SkillRing = LoadSprite("Skill_Slot.png", "Skill_Ring"),
            SkillCooldown = LoadSprite("Skill_Fill.png", "Skill_Fill"),
            SkillKeyCap = LoadSprite("Skill_Slot.png", "Skill_KeyCap"),
            DecorationActive = LoadSprite("UI_Decoration.png", "Decoration_Active")
        };
    }

    private static void SliceSingle(string fileName, string spriteName)
    {
        SliceTexture(fileName, new[] { spriteName }, false);
    }

    private static void SliceColumns(string fileName, params string[] spriteNames)
    {
        SliceTexture(fileName, spriteNames, true);
    }

    private static void SliceTexture(string fileName, IReadOnlyList<string> spriteNames, bool splitColumns)
    {
        string path = ResourceRoot + fileName;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"Texture importer missing for {path}.");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = true;
        importer.SaveAndReimport();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        List<Rect> rects = splitColumns ? FindColumnSlices(texture) : new List<Rect> { FindVisibleRect(texture, 0, texture.width) };
        if (rects.Count != spriteNames.Count)
            throw new InvalidOperationException($"{fileName} expected {spriteNames.Count} visible slice(s), found {rects.Count}.");

        var metadata = new SpriteMetaData[rects.Count];
        for (int i = 0; i < rects.Count; i++)
        {
            metadata[i] = new SpriteMetaData
            {
                name = spriteNames[i],
                rect = rects[i],
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            };
        }

        importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable 0618
        importer.spritesheet = metadata;
#pragma warning restore 0618
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    private static List<Rect> FindColumnSlices(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        bool[] columns = new bool[texture.width];
        for (int y = 0; y < texture.height; y++)
        {
            int row = y * texture.width;
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[row + x].a > 8)
                    columns[x] = true;
            }
        }

        var slices = new List<Rect>();
        int bandStart = -1;
        for (int x = 0; x < columns.Length; x++)
        {
            if (columns[x] && bandStart < 0)
                bandStart = x;

            bool atBandEnd = bandStart >= 0 && (!columns[x] || x == columns.Length - 1);
            if (!atBandEnd)
                continue;

            int bandEnd = columns[x] ? x + 1 : x;
            slices.Add(FindVisibleRect(texture, bandStart, bandEnd));
            bandStart = -1;
        }

        return slices;
    }

    private static Rect FindVisibleRect(Texture2D texture, int xStart, int xEnd)
    {
        Color32[] pixels = texture.GetPixels32();
        int minX = texture.width;
        int maxX = -1;
        int minY = texture.height;
        int maxY = -1;

        for (int y = 0; y < texture.height; y++)
        {
            int row = y * texture.width;
            for (int x = Mathf.Max(0, xStart); x < Mathf.Min(texture.width, xEnd); x++)
            {
                if (pixels[row + x].a <= 8)
                    continue;

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
            throw new InvalidOperationException($"No visible pixels found in {texture.name}.");

        const int padding = 2;
        int rectX = Mathf.Max(0, minX - padding);
        int rectY = Mathf.Max(0, minY - padding);
        int rectMaxX = Mathf.Min(texture.width, maxX + padding + 1);
        int rectMaxY = Mathf.Min(texture.height, maxY + padding + 1);
        return new Rect(rectX, rectY, rectMaxX - rectX, rectMaxY - rectY);
    }

    private static Sprite LoadSprite(string fileName, string spriteName)
    {
        string path = ResourceRoot + fileName;
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .FirstOrDefault(item => item.name == spriteName);

        if (sprite == null)
            throw new InvalidOperationException($"Sprite {spriteName} missing from {path}.");

        return sprite;
    }

    private static RectTransform CreateImage(string name, Transform parent, Sprite sprite)
    {
        RectTransform rect = CreateRect(name, parent);
        CanvasRenderer renderer = rect.gameObject.AddComponent<CanvasRenderer>();
        renderer.cullTransparentMesh = true;

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return rect;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.gameObject.AddComponent<CanvasRenderer>();

        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static RectTransform RequireRect(GameObject gameObject)
    {
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        return rect != null ? rect : gameObject.AddComponent<RectTransform>();
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        if (layer < 0)
            return;

        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private sealed class HudSprites
    {
        public Sprite PlayerInfo;
        public Sprite StageInfo;
        public Sprite FillBar;
        public Sprite ComboUnderbar;
        public Sprite ComboEffect;
        public Sprite HpFrame;
        public Sprite HpBody;
        public Sprite HpFill;
        public Sprite SkillFrame;
        public Sprite SkillRing;
        public Sprite SkillCooldown;
        public Sprite SkillKeyCap;
        public Sprite DecorationActive;
    }
}
