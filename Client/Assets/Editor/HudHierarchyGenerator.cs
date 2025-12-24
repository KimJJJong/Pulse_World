#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class HudHierarchyGenerator
{
    [MenuItem("Tools/RhythmRPG/UI/Create Rhythm HUD")]
    public static void CreateHud()
    {
        // Canvas
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // HUD Root
        var hudRoot = CreateUI("HUDRoot", canvasGO.transform);
        SetAnchor(hudRoot, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(300, 300));

        // Hex HUD
        var hexHud = CreateUI("HexHud", hudRoot.transform);

        // HP Frame
        var hpFrame = CreateImage("HP_Frame", hexHud.transform);

        // HP Glow
        var hpGlow = CreateImage("HP_Glow", hexHud.transform);
        hpGlow.color = new Color(1, 1, 1, 0);

        // HP Mask
        var hpMask = CreateImage("HP_Mask", hexHud.transform);
        hpMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var hpFill = CreateImage("HP_Fill", hpMask.transform);
        SetupFill(hpFill, Image.FillMethod.Vertical);

        // SP Frame
        var spFrame = CreateImage("SP_Frame", hexHud.transform);
        SetSize(spFrame.rectTransform, 80, 80);

        var spMask = CreateImage("SP_Mask", hexHud.transform);
        spMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var spFill = CreateImage("SP_Fill", spMask.transform);
        SetupFill(spFill, Image.FillMethod.Vertical);

        // HP Text
        var hpText = CreateTMP("HP_Text", hexHud.transform, "100 / 100");
        var spText = CreateTMP("SP_Text", hexHud.transform, "50 / 50");

        // Skill Bar
        var skillBar = CreateUI("SkillBar", hudRoot.transform);
        SetAnchor(skillBar, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, -120), new Vector2(400, 80));

        for (int i = 0; i < 6; i++)
        {
            CreateSkillSlot(skillBar.transform, i);
        }

        Selection.activeGameObject = canvasGO;
    }

    // ---------- Helpers ----------

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

    static void SetupFill(Image img, Image.FillMethod method)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = method;
        img.fillOrigin = (int)Image.OriginVertical.Bottom;
        img.fillAmount = 1f;
    }

    static TextMeshProUGUI CreateTMP(string name, Transform parent, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    static void CreateSkillSlot(Transform parent, int index)
    {
        var slot = CreateUI($"Slot_{index}", parent);
        SetSize(slot.GetComponent<RectTransform>(), 60, 60);
        slot.transform.localPosition = new Vector3(index * 70, 0, 0);

        var icon = CreateImage("Icon", slot.transform);

        var cd = CreateImage("CooldownMask", slot.transform);
        cd.color = new Color(0, 0, 0, 0.6f);
        cd.type = Image.Type.Filled;
        cd.fillMethod = Image.FillMethod.Radial360;
        cd.fillAmount = 0f;
    }

    static void SetAnchor(GameObject go, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void SetSize(RectTransform rt, float w, float h)
    {
        rt.sizeDelta = new Vector2(w, h);
    }
}
#endif
