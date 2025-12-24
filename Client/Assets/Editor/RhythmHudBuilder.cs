#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

public static class RhythmHudUltraBuilder
{
    // ----------------- USER SETTINGS -----------------
    const string CANVAS_NAME = "Canvas_RhythmHUD";
    const string CONFIG_ASSET_PATH = "Assets/Configs/UI/HudConfig.asset";
    const string PREFAB_PATH = "Assets/Prefabs/UI/RhythmHUD.prefab";

    // Resources auto-load paths
    const string RES_BASE = "RhythmHUD/";
    const string RES_HEX_FRAME = RES_BASE + "HexFrame";
    const string RES_HEX_MASK = RES_BASE + "HexMask";
    const string RES_HEX_GLOW = RES_BASE + "HexGlow";
    const string RES_BAR_FILL = RES_BASE + "BarFill";
    const string RES_CIRCLE = RES_BASE + "Circle";

    // ----------------- FIELD NAME MAP (edit if needed) -----------------
    // HexHudView fields (private [SerializeField])
    const string HEX_FIELD_HP_FILL = "hpFill";
    const string HEX_FIELD_HP_FRAME = "hpFrame";
    const string HEX_FIELD_HP_GLOW = "hpGlow";
    const string HEX_FIELD_HP_TEXT = "hpText";
    const string HEX_FIELD_SP_FILL = "spFill";
    const string HEX_FIELD_SP_TEXT = "spText";

    // BeatPulse field
    const string PULSE_FIELD_HUD = "hud";

    // SkillSlotView fields
    const string SLOT_FIELD_ICON = "icon";
    const string SLOT_FIELD_CD_MASK = "cooldownMask";
    const string SLOT_FIELD_CD_TEXT = "cooldownText";

    // HudPresenter fields (your runtime)
    const string PRES_FIELD_CONFIG = "config";
    const string PRES_FIELD_CFG2 = "_config";
    const string PRES_FIELD_HEXHUD = "hexHud";
    const string PRES_FIELD_VIEW = "_view";
    const string PRES_FIELD_SLOTS = "skillSlots";

    // HudConfig fields (ScriptableObject)
    const string CFG_FIELD_SKILL_ICONS = "skillIcons";
    const string CFG_FIELD_MAX_SLOTS = "maxSkillSlots";
    const string CFG_FIELD_HP_COLOR = "hpColor";
    const string CFG_FIELD_SP_COLOR = "spColor";

    // ----------------- MENU -----------------
    [MenuItem("RhythmRPG/UI/Build Rhythm HUD (ULTRA)")]
    public static void BuildHud_Ultra()
    {
        try
        {
            var existing = GameObject.Find(CANVAS_NAME);

            if (existing != null)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Rhythm HUD Exists",
                    $"{CANVAS_NAME} already exists.\n\nChoose an action:",
                    "Update Only (Rebind)",   // 0
                    "Cancel",                 // 1
                    "Recreate (Delete & Build)" // 2
                );

                if (choice == 1) return;

                if (choice == 2)
                {
                    UnityEngine.Object.DestroyImmediate(existing);
                    BuildFresh();
                    return;
                }

                // Update Only
                UpdateOnly(existing);
                return;
            }

            BuildFresh();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            // 대화상자는 delayCall로 띄우면 안전
            EditorApplication.delayCall += () =>
                EditorUtility.DisplayDialog("Rhythm HUD Builder Error", e.Message, "OK");
        }
    }

    // ----------------- BUILD FRESH -----------------
    static void BuildFresh()
    {
        var cfg = EnsureHudConfigAsset(CONFIG_ASSET_PATH);
        int slotCount = GetIntFieldOr(cfg, CFG_FIELD_MAX_SLOTS, 6);
        slotCount = Mathf.Clamp(slotCount, 1, 12);

        // Load sprites
        var sprites = LoadSprites();

        // Create Canvas
        var canvasGO = CreateCanvas(CANVAS_NAME);
        ConfigureCanvasScaler(canvasGO.GetComponent<CanvasScaler>());

        // Root
        var hudRoot = CreateUI("HUDRoot", canvasGO.transform);
        SetupHudRoot(hudRoot.GetComponent<RectTransform>());

        // HexHud + SkillBar
        var hexHudGO = CreateHexHud(hudRoot.transform, sprites);
        var skillBarGO = CreateSkillBar(hudRoot.transform);

        // Create slots
        var slotViews = CreateSkillSlots(skillBarGO.transform, slotCount, sprites);

        // Add runtime components + bind
        var hexHudView = EnsureComponent<HexHudView>(hexHudGO);
        BindHexHudView(hexHudGO, hexHudView);

        var beatPulse = EnsureComponent<BeatPulse>(hexHudGO);
        SetPrivateField(beatPulse, PULSE_FIELD_HUD, hexHudView);

        var presenterGO = CreateUI("HudPresenter", hudRoot.transform);
        var presenter = EnsureComponent<HudPresenter>(presenterGO);
        BindPresenter(presenter, cfg, hexHudView, slotViews);

        // Apply config visuals (colors/icons)
        ApplyConfigToVisuals(cfg, hexHudGO, slotViews);

        // Save Prefab
        SavePrefab(canvasGO, PREFAB_PATH);

        Selection.activeGameObject = canvasGO;

        EditorUtility.DisplayDialog("Done",
            "Rhythm HUD built (ULTRA).\n\n" +
            "- Components auto-bound\n" +
            "- Config auto-created/assigned\n" +
            "- Icons & colors applied\n" +
            "- Prefab saved to Assets/Prefabs/UI/RhythmHUD.prefab\n\n" +
            "Sprites auto-load from Resources/RhythmHUD/ (optional but recommended).",
            "OK");
    }

    // ----------------- UPDATE ONLY (REBIND) -----------------
    static void UpdateOnly(GameObject canvasGO)
    {
        var cfg = EnsureHudConfigAsset(CONFIG_ASSET_PATH);
        int slotCount = GetIntFieldOr(cfg, CFG_FIELD_MAX_SLOTS, 6);
        slotCount = Mathf.Clamp(slotCount, 1, 12);

        var sprites = LoadSprites();

        // Ensure scaler
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasGO.AddComponent<CanvasScaler>();
        ConfigureCanvasScaler(scaler);

        // Find/Create HUDRoot
        var hudRoot = FindOrCreate(canvasGO.transform, "HUDRoot", CreateUI);
        SetupHudRoot(hudRoot.GetComponent<RectTransform>());

        // Find/Create HexHud
        var hexHudGO = FindOrCreate(hudRoot.transform, "HexHud", CreateUI);
        EnsureHexHudChildren(hexHudGO.transform, sprites);

        // Find/Create SkillBar
        var skillBarGO = FindOrCreate(hudRoot.transform, "SkillBar", CreateUI);
        SetupSkillBarRect(skillBarGO.GetComponent<RectTransform>());

        // Ensure slot count
        var slotViews = EnsureSkillSlots(skillBarGO.transform, slotCount, sprites);

        // Ensure runtime components + rebind
        var hexHudView = EnsureComponent<HexHudView>(hexHudGO);
        BindHexHudView(hexHudGO, hexHudView);

        var beatPulse = EnsureComponent<BeatPulse>(hexHudGO);
        SetPrivateField(beatPulse, PULSE_FIELD_HUD, hexHudView);

        var presenterGO = FindOrCreate(hudRoot.transform, "HudPresenter", CreateUI);
        var presenter = EnsureComponent<HudPresenter>(presenterGO);
        BindPresenter(presenter, cfg, hexHudView, slotViews);

        // Apply config to visuals (colors/icons)
        ApplyConfigToVisuals(cfg, hexHudGO, slotViews);

        // Save Prefab (update prefab too)
        SavePrefab(canvasGO, PREFAB_PATH);

        Selection.activeGameObject = canvasGO;

        EditorUtility.DisplayDialog("Updated",
            "Update Only (Rebind) completed.\n\n" +
            "- Reconnected references\n" +
            "- Updated slot count\n" +
            "- Applied config icons/colors\n" +
            "- Prefab overwritten",
            "OK");
    }

    // ----------------- APPLY CONFIG (icons & colors) -----------------
    static void ApplyConfigToVisuals(HudConfig cfg, GameObject hexHudGO, SkillSlotView[] slotViews)
    {
        if (cfg == null || hexHudGO == null) return;

        // ---- HP/SP 색 적용 (없으면 스킵) ----
        Color hpColor = GetColorFieldOr(cfg, "hpColor", Color.red);
        Color spColor = GetColorFieldOr(cfg, "spColor", Color.blue);

        var hpFill = hexHudGO.transform.Find("HP_Mask/HP_Fill")?.GetComponent<Image>();
        if (hpFill != null) hpFill.color = hpColor;

        var spFill = hexHudGO.transform.Find("SP_Mask/SP_Fill")?.GetComponent<Image>();
        if (spFill != null) spFill.color = spColor;

        // ---- Skill 아이콘 적용 (null-safe) ----
        var icons = GetSpriteArrayFieldOr(cfg, "skillIcons", Array.Empty<Sprite>()) ?? Array.Empty<Sprite>();

        if (slotViews == null) return;

        for (int i = 0; i < slotViews.Length; i++)
        {
            var slot = slotViews[i];
            if (slot == null) continue;

            // reflection 대신 안전하게 Hierarchy에서 직접 찾기
            var iconImg = slot.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImg == null) continue;

            if (i < icons.Length && icons[i] != null)
            {
                iconImg.sprite = icons[i];
                iconImg.color = Color.white;
            }
            else
            {
                iconImg.color = new Color(1, 1, 1, 0.35f); // 아이콘 없으면 흐리게
            }
        }
    }


    // ----------------- CREATE HEX HUD -----------------
    static GameObject CreateHexHud(Transform parent, SpriteSet sprites)
    {
        var hexHudGO = CreateUI("HexHud", parent);
        var rt = hexHudGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(0, 0);
        rt.sizeDelta = new Vector2(260, 260);

        EnsureHexHudChildren(hexHudGO.transform, sprites);
        return hexHudGO;
    }

    static void EnsureHexHudChildren(Transform hexHud, SpriteSet sprites)
    {
        // HP_Frame
        var hpFrame = FindOrCreate(hexHud, "HP_Frame", CreateImage);
        SetFullStretch(hpFrame.rectTransform);
        hpFrame.sprite = sprites.HexFrame;
        hpFrame.type = Image.Type.Simple;
        hpFrame.raycastTarget = false;

        // HP_Glow
        var hpGlow = FindOrCreate(hexHud, "HP_Glow", CreateImage);
        SetFullStretch(hpGlow.rectTransform);
        hpGlow.sprite = sprites.HexGlow != null ? sprites.HexGlow : sprites.HexFrame;
        hpGlow.raycastTarget = false;
        if (hpGlow.color.a <= 0.0001f) hpGlow.color = new Color(1, 1, 1, 0f);

        // HP_Mask
        var hpMask = FindOrCreate(hexHud, "HP_Mask", CreateImage);
        SetFullStretch(hpMask.rectTransform);
        hpMask.sprite = sprites.HexMask != null ? sprites.HexMask : sprites.HexFrame;
        var mask = hpMask.GetComponent<Mask>();
        if (mask == null) mask = hpMask.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var hpFill = FindOrCreate(hpMask.transform, "HP_Fill", CreateImage);
        SetFullStretch(hpFill.rectTransform);
        hpFill.sprite = sprites.BarFill;
        SetupFill(hpFill, Image.FillMethod.Vertical, originBottom: true);
        hpFill.raycastTarget = false;

        // SP_Frame (right-bottom)
        var spFrame = FindOrCreate(hexHud, "SP_Frame", CreateImage);
        spFrame.raycastTarget = false;
        spFrame.sprite = sprites.HexFrame;
        var spFrameRT = spFrame.rectTransform;
        spFrameRT.anchorMin = new Vector2(1, 0);
        spFrameRT.anchorMax = new Vector2(1, 0);
        spFrameRT.pivot = new Vector2(1, 0);
        spFrameRT.anchoredPosition = new Vector2(-10, 10);
        spFrameRT.sizeDelta = new Vector2(90, 90);

        // SP_Mask
        var spMask = FindOrCreate(hexHud, "SP_Mask", CreateImage);
        spMask.sprite = sprites.HexMask != null ? sprites.HexMask : sprites.HexFrame;
        var spMaskRT = spMask.rectTransform;
        spMaskRT.anchorMin = spFrameRT.anchorMin;
        spMaskRT.anchorMax = spFrameRT.anchorMax;
        spMaskRT.pivot = spFrameRT.pivot;
        spMaskRT.anchoredPosition = spFrameRT.anchoredPosition;
        spMaskRT.sizeDelta = spFrameRT.sizeDelta;

        var spMaskComp = spMask.GetComponent<Mask>();
        if (spMaskComp == null) spMaskComp = spMask.gameObject.AddComponent<Mask>();
        spMaskComp.showMaskGraphic = false;

        var spFill = FindOrCreate(spMask.transform, "SP_Fill", CreateImage);
        SetFullStretch(spFill.rectTransform);
        spFill.sprite = sprites.BarFill;
        SetupFill(spFill, Image.FillMethod.Vertical, originBottom: true);
        spFill.raycastTarget = false;

        // Texts
        var hpText = FindOrCreate(hexHud, "HP_Text", CreateTMP);
        hpText.text = string.IsNullOrWhiteSpace(hpText.text) ? "HP 100/100" : hpText.text;
        hpText.color = Color.white;
        hpText.raycastTarget = false;
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.fontSize = 20;

        var hpTextRT = hpText.GetComponent<RectTransform>();
        hpTextRT.anchorMin = new Vector2(0.5f, 0.18f);
        hpTextRT.anchorMax = new Vector2(0.5f, 0.18f);
        hpTextRT.pivot = new Vector2(0.5f, 0.5f);
        hpTextRT.anchoredPosition = Vector2.zero;
        hpTextRT.sizeDelta = new Vector2(200, 30);

        var spText = FindOrCreate(hexHud, "SP_Text", CreateTMP);
        spText.text = string.IsNullOrWhiteSpace(spText.text) ? "SP 50/50" : spText.text;
        spText.color = Color.white;
        spText.raycastTarget = false;
        spText.alignment = TextAlignmentOptions.Right;
        spText.fontSize = 16;

        var spTextRT = spText.GetComponent<RectTransform>();
        spTextRT.anchorMin = new Vector2(1, 0);
        spTextRT.anchorMax = new Vector2(1, 0);
        spTextRT.pivot = new Vector2(1, 0);
        spTextRT.anchoredPosition = new Vector2(-10, 100);
        spTextRT.sizeDelta = new Vector2(130, 24);
    }

    // ----------------- CREATE / ENSURE SKILL BAR -----------------
    static GameObject CreateSkillBar(Transform parent)
    {
        var skillBar = CreateUI("SkillBar", parent);
        SetupSkillBarRect(skillBar.GetComponent<RectTransform>());
        return skillBar;
    }

    static void SetupSkillBarRect(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(280, 10);
        rt.sizeDelta = new Vector2(520, 80);
    }

    static SkillSlotView[] CreateSkillSlots(Transform parent, int count, SpriteSet sprites)
    {
        var arr = new SkillSlotView[count];
        for (int i = 0; i < count; i++)
            arr[i] = CreateOrEnsureOneSlot(parent, i, sprites);
        return arr;
    }

    static SkillSlotView[] EnsureSkillSlots(Transform parent, int count, SpriteSet sprites)
    {
        // remove extra
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (!child.name.StartsWith("Slot_")) continue;

            if (TryParseSlotIndex(child.name, out int idx))
            {
                if (idx >= count)
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        // ensure required
        var arr = new SkillSlotView[count];
        for (int i = 0; i < count; i++)
            arr[i] = CreateOrEnsureOneSlot(parent, i, sprites);

        return arr;
    }

    static SkillSlotView CreateOrEnsureOneSlot(Transform parent, int index, SpriteSet sprites)
    {
        var slotGO = parent.Find($"Slot_{index}")?.gameObject;
        if (slotGO == null)
        {
            slotGO = CreateUI($"Slot_{index}", parent);
        }

        var rt = slotGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(index * 72, 0);
        rt.sizeDelta = new Vector2(64, 64);

        var builtinUISprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var bg = FindOrCreate(slotGO.transform, "BG", CreateImage);
        SetFullStretch(bg.rectTransform);
        bg.sprite = builtinUISprite;
        bg.color = new Color(1, 1, 1, 0.08f);
        bg.raycastTarget = false;

        var icon = FindOrCreate(slotGO.transform, "Icon", CreateImage);
        SetFullStretch(icon.rectTransform);
        icon.sprite = icon.sprite != null ? icon.sprite : builtinUISprite;
        icon.raycastTarget = false;

        var cd = FindOrCreate(slotGO.transform, "CooldownMask", CreateImage);
        SetFullStretch(cd.rectTransform);
        cd.raycastTarget = false;
        cd.color = new Color(0, 0, 0, 0.6f);
        cd.sprite = sprites.Circle != null ? sprites.Circle : builtinUISprite;
        cd.type = Image.Type.Filled;
        cd.fillMethod = Image.FillMethod.Radial360;
        cd.fillOrigin = (int)Image.Origin360.Top;
        cd.fillClockwise = false;
        cd.fillAmount = 0f;

        var cdText = FindOrCreate(slotGO.transform, "CooldownText", CreateTMP);
        cdText.text = "";
        cdText.raycastTarget = false;
        cdText.color = Color.white;
        cdText.alignment = TextAlignmentOptions.Center;
        cdText.fontSize = 20;
        SetFullStretch(cdText.GetComponent<RectTransform>());

        var slotView = EnsureComponent<SkillSlotView>(slotGO);
        SetPrivateField(slotView, SLOT_FIELD_ICON, icon);
        SetPrivateField(slotView, SLOT_FIELD_CD_MASK, cd);
        SetPrivateField(slotView, SLOT_FIELD_CD_TEXT, cdText);

        return slotView;
    }

    static bool TryParseSlotIndex(string name, out int index)
    {
        index = -1;
        if (!name.StartsWith("Slot_")) return false;
        return int.TryParse(name.Substring("Slot_".Length), out index);
    }

    // ----------------- BIND RUNTIME COMPONENTS -----------------
    static void BindHexHudView(GameObject hexHudGO, HexHudView hexHudView)
    {
        var hpFill = hexHudGO.transform.Find("HP_Mask/HP_Fill")?.GetComponent<Image>();
        var hpFrame = hexHudGO.transform.Find("HP_Frame")?.GetComponent<Image>();
        var hpGlow = hexHudGO.transform.Find("HP_Glow")?.GetComponent<Image>();
        var hpText = hexHudGO.transform.Find("HP_Text")?.GetComponent<TextMeshProUGUI>();
        var spFill = hexHudGO.transform.Find("SP_Mask/SP_Fill")?.GetComponent<Image>();
        var spText = hexHudGO.transform.Find("SP_Text")?.GetComponent<TextMeshProUGUI>();

        SetPrivateField(hexHudView, HEX_FIELD_HP_FILL, hpFill);
        SetPrivateField(hexHudView, HEX_FIELD_HP_FRAME, hpFrame);
        SetPrivateField(hexHudView, HEX_FIELD_HP_GLOW, hpGlow);
        SetPrivateField(hexHudView, HEX_FIELD_HP_TEXT, hpText);
        SetPrivateField(hexHudView, HEX_FIELD_SP_FILL, spFill);
        SetPrivateField(hexHudView, HEX_FIELD_SP_TEXT, spText);
    }

    static void BindPresenter(HudPresenter presenter, HudConfig cfg, HexHudView hexHudView, SkillSlotView[] slots)
    {
        if (!TrySetPrivateField(presenter, PRES_FIELD_HEXHUD, hexHudView))
            TrySetPrivateField(presenter, PRES_FIELD_VIEW, hexHudView);

        TrySetPrivateField(presenter, PRES_FIELD_SLOTS, slots);

        if (!TrySetPrivateField(presenter, PRES_FIELD_CONFIG, cfg))
            TrySetPrivateField(presenter, PRES_FIELD_CFG2, cfg);
    }

    // ----------------- PREFAB SAVE -----------------
    static void SavePrefab(GameObject root, string prefabPath)
    {
        EnsureFolderRecursive(Path.GetDirectoryName(prefabPath).Replace("\\", "/"));

        // 저장/덮어쓰기
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ----------------- CONFIG / SPRITES -----------------
    struct SpriteSet
    {
        public Sprite HexFrame;
        public Sprite HexMask;
        public Sprite HexGlow;
        public Sprite BarFill;
        public Sprite Circle;
    }

    static SpriteSet LoadSprites()
    {
        var sHexFrame = Resources.Load<Sprite>(RES_HEX_FRAME);
        var sHexMask = Resources.Load<Sprite>(RES_HEX_MASK);
        var sHexGlow = Resources.Load<Sprite>(RES_HEX_GLOW);
        var sBarFill = Resources.Load<Sprite>(RES_BAR_FILL);
        var sCircle = Resources.Load<Sprite>(RES_CIRCLE);

        var builtinUISprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        return new SpriteSet
        {
            HexFrame = sHexFrame != null ? sHexFrame : builtinUISprite,
            HexMask = sHexMask,
            HexGlow = sHexGlow,
            BarFill = sBarFill != null ? sBarFill : builtinUISprite,
            Circle = sCircle
        };
    }

    static HudConfig EnsureHudConfigAsset(string assetPath)
    {
        var cfg = AssetDatabase.LoadAssetAtPath<HudConfig>(assetPath);
        if (cfg != null) return cfg;

        var dir = Path.GetDirectoryName(assetPath).Replace("\\", "/");
        EnsureFolderRecursive(dir);

        cfg = ScriptableObject.CreateInstance<HudConfig>();
        cfg.name = "HudConfig";

        AssetDatabase.CreateAsset(cfg, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return cfg;
    }

    static void EnsureFolderRecursive(string dir)
    {
        if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir)) return;

        var parent = Path.GetDirectoryName(dir).Replace("\\", "/");
        var name = Path.GetFileName(dir);
        EnsureFolderRecursive(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static int GetIntFieldOr(object obj, string fieldName, int fallback)
    {
        var f = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
        return fallback;
    }

    static Color GetColorFieldOr(object obj, string fieldName, Color fallback)
    {
        var f = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(Color)) return (Color)f.GetValue(obj);
        return fallback;
    }

    static Sprite[] GetSpriteArrayFieldOr(object obj, string fieldName, Sprite[] fallback)
    {
        if (obj == null) return fallback ?? Array.Empty<Sprite>();

        var f = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(Sprite[]))
        {
            return (Sprite[])f.GetValue(obj) ?? (fallback ?? Array.Empty<Sprite>());
        }
        return fallback ?? Array.Empty<Sprite>();
    }


    // ----------------- UI BASICS -----------------
    static GameObject CreateCanvas(string name)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        return go;
    }

    static void ConfigureCanvasScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
    }

    static GameObject CreateUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image CreateImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    static TextMeshProUGUI CreateTMP(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        return go.GetComponent<TextMeshProUGUI>();
    }

    static void SetupHudRoot(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(20, 20);
        rt.sizeDelta = new Vector2(800, 360);
    }

    static void SetFullStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetupFill(Image img, Image.FillMethod method, bool originBottom)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = method;
        img.fillOrigin = originBottom ? (int)Image.OriginVertical.Bottom : (int)Image.OriginVertical.Top;
        img.fillAmount = 1f;
    }

    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }

    static T FindOrCreate<T>(Transform parent, string name, Func<string, Transform, T> createFn) where T : Component
    {
        var tr = parent.Find(name);
        if (tr != null) return tr.GetComponent<T>() ?? tr.gameObject.AddComponent<T>();
        return createFn(name, parent);
    }

    static GameObject FindOrCreate(Transform parent, string name, Func<string, Transform, GameObject> createFn)
    {
        var tr = parent.Find(name);
        if (tr != null) return tr.gameObject;
        return createFn(name, parent);
    }

    static Image FindOrCreate(Transform parent, string name, Func<string, Transform, Image> createFn)
    {
        var tr = parent.Find(name);
        if (tr != null) return tr.GetComponent<Image>() ?? tr.gameObject.AddComponent<Image>();
        return createFn(name, parent);
    }

    static TextMeshProUGUI FindOrCreate(Transform parent, string name, Func<string, Transform, TextMeshProUGUI> createFn)
    {
        var tr = parent.Find(name);
        if (tr != null) return tr.GetComponent<TextMeshProUGUI>() ?? tr.gameObject.AddComponent<TextMeshProUGUI>();
        return createFn(name, parent);
    }

    // ----------------- Reflection bind -----------------
    static void SetPrivateField(Component obj, string fieldName, object value)
    {
        if (!TrySetPrivateField(obj, fieldName, value))
            Debug.LogWarning($"[RhythmHudUltraBuilder] Bind failed: {obj.GetType().Name}.{fieldName}");
    }

    static bool TrySetPrivateField(Component obj, string fieldName, object value)
    {
        if (obj == null || string.IsNullOrEmpty(fieldName)) return false;

        var t = obj.GetType();
        var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null) return false;

        if (f.FieldType.IsArray && value is Array)
        {
            f.SetValue(obj, value);
            EditorUtility.SetDirty(obj);
            return true;
        }

        if (value == null || f.FieldType.IsAssignableFrom(value.GetType()))
        {
            f.SetValue(obj, value);
            EditorUtility.SetDirty(obj);
            return true;
        }

        return false;
    }
}
#endif
