using System.Collections;
using GameServer.InGame.Director.Data;
using RhythmRPG.Game.Stage;
using RhythmRPG.Game.Visual.SceneEffects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StageGuideHud : MonoBehaviour
{
    private static StageGuideHud _instance;

    private CanvasGroup _group;
    private RectTransform _panel;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _bodyText;
    private Image _guideImage;
    private Coroutine _hideRoutine;

    public static bool TryHandleWarn(int code, string payload)
    {
        if (code == StageSignalCodec.GuideWarnCode
            && StageSignalCodec.TryDecodeGuide(payload, out var guide))
        {
            Show(guide);
            return true;
        }

        if (code == StageSignalCodec.VfxWarnCode
            && StageSignalCodec.TryDecodeVfx(payload, out var vfx))
        {
            PlayVfx(vfx);
            return true;
        }

        if (code == StageSignalCodec.TutorialPanelWarnCode
            && StageSignalCodec.TryDecodeTutorialPanel(payload, out var tutorialPanel))
        {
            if (tutorialPanel.Visible)
                StageTutorialPanelHud.Show(tutorialPanel);
            else
                StageTutorialPanelHud.Hide(tutorialPanel);
            return true;
        }

        if (code == StageSignalCodec.SceneObjectWarnCode
            && StageSignalCodec.TryDecodeSceneObject(payload, out var sceneObject))
        {
            StageSceneObjectTarget.SetActive(sceneObject);
            return true;
        }

        if (code == StageSignalCodec.GateDoorWarnCode
            && StageSignalCodec.TryDecodeGateDoor(payload, out var gateDoor))
        {
            StageGateStoneDoorTarget.SetOpen(gateDoor);
            return true;
        }

        return false;
    }

    public static void Show(StageGuideData data)
    {
        EnsureInstance().ShowInternal(data ?? new StageGuideData());
    }

    public static void PlayVfx(StageVfxData data)
    {
        EnsureInstance().PlayVfxInternal(data ?? new StageVfxData());
    }

    private static StageGuideHud EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var go = new GameObject(nameof(StageGuideHud));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<StageGuideHud>();
        _instance.BuildUi();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void BuildUi()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        gameObject.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("GuidePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        panelGo.transform.SetParent(transform, false);
        _panel = panelGo.GetComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0.5f, 0f);
        _panel.anchorMax = new Vector2(0.5f, 0f);
        _panel.pivot = new Vector2(0.5f, 0f);
        _panel.anchoredPosition = new Vector2(0f, 76f);
        _panel.sizeDelta = new Vector2(760f, 178f);

        var panelImage = panelGo.GetComponent<Image>();
        panelImage.color = new Color(0.04f, 0.055f, 0.07f, 0.88f);
        panelImage.raycastTarget = false;

        _group = panelGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        _guideImage = CreateImage("GuideImage", _panel, new Vector2(94f, 94f), new Vector2(80f, 88f));
        _titleText = CreateText("Title", _panel, 28f, FontStyles.Bold, new Vector2(260f, 124f), new Vector2(500f, 44f));
        _bodyText = CreateText("Body", _panel, 21f, FontStyles.Normal, new Vector2(260f, 54f), new Vector2(500f, 86f));

        _titleText.color = new Color(0.90f, 0.96f, 1f, 1f);
        _bodyText.color = new Color(0.80f, 0.86f, 0.90f, 1f);
        _panel.gameObject.SetActive(false);
    }

    private static Image CreateImage(string name, Transform parent, Vector2 size, Vector2 anchoredPosition)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.92f);
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles style, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private void ShowInternal(StageGuideData data)
    {
        if (_panel == null)
            BuildUi();

        string title = data.Title ?? string.Empty;
        string body = data.Body ?? string.Empty;

        _titleText.text = title;
        _titleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(title));
        _bodyText.text = body;

        bool hasImage = TryApplyImage(data.ImageResource);
        float textX = hasImage ? 170f : 42f;
        float textWidth = hasImage ? 560f : 680f;
        SetTextRect(_titleText.rectTransform, textX, 124f, textWidth, 44f);
        SetTextRect(_bodyText.rectTransform, textX, string.IsNullOrWhiteSpace(title) ? 88f : 54f, textWidth, string.IsNullOrWhiteSpace(title) ? 124f : 86f);

        _panel.gameObject.SetActive(true);
        _group.alpha = 1f;

        if (_hideRoutine != null)
            StopCoroutine(_hideRoutine);

        int durationMs = data.DurationMs > 0 ? data.DurationMs : 3500;
        _hideRoutine = StartCoroutine(CoHideAfter(durationMs / 1000f));
    }

    private static void SetTextRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private bool TryApplyImage(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            _guideImage.gameObject.SetActive(false);
            return false;
        }

        var sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            _guideImage.gameObject.SetActive(false);
            Debug.LogWarning($"[StageGuideHud] Guide image not found: Resources/{resourcePath}");
            return false;
        }

        _guideImage.sprite = sprite;
        _guideImage.preserveAspect = true;
        _guideImage.gameObject.SetActive(true);
        return true;
    }

    private IEnumerator CoHideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        const float fadeSeconds = 0.18f;
        float elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.deltaTime;
            _group.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeSeconds);
            yield return null;
        }

        _group.alpha = 0f;
        _panel.gameObject.SetActive(false);
        _hideRoutine = null;
    }

    private void PlayVfxInternal(StageVfxData data)
    {
        if (TryApplySceneObjectVfx(data))
            return;

        int mapY = data.Z != 0 ? data.Z : data.Y;
        Vector3 position = BoardView.Instance != null
            ? BoardView.Instance.GridToWorldPublic(data.X, mapY) + Vector3.up * 0.7f
            : new Vector3(data.X, data.Y, data.Z);

        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = string.IsNullOrWhiteSpace(data.VfxKey) ? "StageVfx" : $"StageVfx_{data.VfxKey}";
        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * 0.55f;

        var collider = marker.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        var renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.25f, 0.95f, 1f, 0.85f);

        Destroy(marker, data.DurationMs > 0 ? data.DurationMs / 1000f : 1.2f);
    }

    private static bool TryApplySceneObjectVfx(StageVfxData data)
    {
        if (!StageVfxCatalog.TryGetDefinition(data.VfxKey, out var definition)
            || definition.TargetMode != StageVfxTargetMode.ObjectPulseColor
            || !TryResolvePulseColor(definition.Key, out Color color))
            return false;

        bool applied = false;
        applied |= ApplyPulseToTarget(data.TargetId, color);
        applied |= ApplyPulseToTarget(data.SecondaryTargetId, color);

        if (!applied)
        {
            int mapY = data.Z != 0 ? data.Z : data.Y;
            applied |= ApplyPulseAtPosition(data.X, mapY, color);
        }

        return applied;
    }

    private static bool TryResolvePulseColor(string vfxKey, out Color color)
    {
        switch (vfxKey)
        {
            case StageVfxKeys.CrystalPulseRed:
                color = new Color(1f, 0.12f, 0.08f, 1f);
                return true;

            case StageVfxKeys.CrystalPulseBlue:
                color = new Color(0.196f, 0.784f, 1f, 1f);
                return true;

            default:
                color = default;
                return false;
        }
    }

    private static bool ApplyPulseToTarget(int targetId, Color color)
    {
        if (targetId <= 0 || ClientGameState.Instance == null)
            return false;

        bool applied = false;
        foreach (var entity in ClientGameState.Instance.EnumerateEntities())
        {
            if (entity.EntityType != (int)EntityType.Object)
                continue;

            if (entity.GroupId == targetId || entity.EntityId == targetId)
                applied |= ApplyPulseToEntity(entity.EntityId, color);
        }

        return applied;
    }

    private static bool ApplyPulseAtPosition(int x, int y, Color color)
    {
        if (ClientGameState.Instance == null)
            return false;

        bool applied = false;
        foreach (var entity in ClientGameState.Instance.EnumerateEntities())
        {
            if (entity.EntityType != (int)EntityType.Object)
                continue;

            if (entity.X == x && entity.Y == y)
                applied |= ApplyPulseToEntity(entity.EntityId, color);
        }

        return applied;
    }

    private static bool ApplyPulseToEntity(int entityId, Color color)
    {
        if (BoardView.Instance == null || !BoardView.Instance.TryGetEntityView(entityId, out var visual) || visual == null)
            return false;

        bool applied = false;
        foreach (var pulse in visual.GetComponentsInChildren<ForestBeatLightPulse>(true))
        {
            if (pulse == null)
                continue;

            pulse.SetPulseColor(color);
            applied = true;
        }

        return applied;
    }
}
