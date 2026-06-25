using System.Collections;
using GameServer.InGame.Director.Data;
using UnityEngine;
using UnityEngine.UI;

public sealed class StageTutorialPanelHud : MonoBehaviour
{
    private const int DefaultSortingOrder = 505;
    private const int DefaultWidth = 900;

    private static StageTutorialPanelHud _instance;

    private CanvasGroup _group;
    private RectTransform _panel;
    private Image _panelImage;
    private Coroutine _fadeRoutine;
    private string _currentPanelId = string.Empty;

    public static void Show(StageTutorialPanelData data)
    {
        EnsureInstance().ShowInternal(data ?? new StageTutorialPanelData());
    }

    public static void Hide(StageTutorialPanelData data)
    {
        EnsureInstance().HideInternal(data ?? new StageTutorialPanelData());
    }

    private static StageTutorialPanelHud EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var go = new GameObject(nameof(StageTutorialPanelHud));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<StageTutorialPanelHud>();
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
        canvas.sortingOrder = DefaultSortingOrder;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panelGo = new GameObject("TutorialPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        panelGo.transform.SetParent(transform, false);

        _panel = panelGo.GetComponent<RectTransform>();
        _panelImage = panelGo.GetComponent<Image>();
        _panelImage.preserveAspect = true;
        _panelImage.raycastTarget = false;

        _group = panelGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        _panel.gameObject.SetActive(false);
    }

    private void ShowInternal(StageTutorialPanelData data)
    {
        if (_panel == null)
            BuildUi();

        var sprite = LoadPanelSprite(data.ImageResource);
        if (sprite == null)
        {
            Debug.LogWarning($"[StageTutorialPanelHud] Tutorial panel image not found: Resources/{data.ImageResource}");
            return;
        }

        _currentPanelId = data.PanelId ?? string.Empty;
        _panelImage.sprite = sprite;
        ApplyLayout(data);

        _panel.gameObject.SetActive(true);
        StartFade(1f, ResolveFadeSeconds(data.FadeMs, 0.22f), deactivateWhenHidden: false);
    }

    private void HideInternal(StageTutorialPanelData data)
    {
        if (_panel == null || !_panel.gameObject.activeSelf)
            return;

        string requestedPanelId = data.PanelId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(requestedPanelId)
            && !string.Equals(requestedPanelId, _currentPanelId, System.StringComparison.Ordinal))
        {
            return;
        }

        StartFade(0f, ResolveFadeSeconds(data.FadeMs, 0.18f), deactivateWhenHidden: true);
    }

    private void ApplyLayout(StageTutorialPanelData data)
    {
        int width = data.Width > 0 ? data.Width : DefaultWidth;
        width = Mathf.Clamp(width, 480, 1040);
        float aspect = ResolveSpriteAspect();
        float height = width / aspect;

        _panel.sizeDelta = new Vector2(width, height);

        ResolveAnchor(data.AnchorPreset, out var anchor, out var pivot);
        _panel.anchorMin = anchor;
        _panel.anchorMax = anchor;
        _panel.pivot = pivot;

        Vector2 offset = ResolveOffset(data, anchor);
        _panel.anchoredPosition = offset;
    }

    private static void ResolveAnchor(int anchorPreset, out Vector2 anchor, out Vector2 pivot)
    {
        switch (anchorPreset)
        {
            case 1:
                anchor = new Vector2(0.5f, 0f);
                pivot = new Vector2(0.5f, 0f);
                break;
            case 2:
                anchor = new Vector2(1f, 1f);
                pivot = new Vector2(1f, 1f);
                break;
            case 3:
                anchor = new Vector2(0.5f, 1f);
                pivot = new Vector2(0.5f, 1f);
                break;
            case 4:
                anchor = new Vector2(0.5f, 0.5f);
                pivot = new Vector2(0.5f, 0.5f);
                break;
            case 5:
                anchor = new Vector2(0f, 0f);
                pivot = new Vector2(0f, 0f);
                break;
            case 6:
                anchor = new Vector2(0f, 1f);
                pivot = new Vector2(0f, 1f);
                break;
            case 7:
                anchor = new Vector2(0f, 0.5f);
                pivot = new Vector2(0f, 0.5f);
                break;
            default:
                anchor = new Vector2(0f, 0.5f);
                pivot = new Vector2(0f, 0.5f);
                break;
        }
    }

    private static Vector2 ResolveOffset(StageTutorialPanelData data, Vector2 anchor)
    {
        if (data.OffsetX != 0 || data.OffsetY != 0)
            return new Vector2(data.OffsetX, data.OffsetY);

        if (anchor.x >= 0.99f && anchor.y <= 0.01f)
            return new Vector2(-44f, 54f);
        if (anchor.x >= 0.99f && anchor.y >= 0.99f)
            return new Vector2(-44f, -54f);
        if (anchor.x <= 0.01f && anchor.y <= 0.01f)
            return new Vector2(44f, 54f);
        if (anchor.x <= 0.01f && anchor.y >= 0.99f)
            return new Vector2(44f, -54f);
        if (anchor.x <= 0.01f)
            return new Vector2(44f, 0f);
        if (anchor.y <= 0.01f)
            return new Vector2(0f, 54f);
        if (anchor.y >= 0.99f)
            return new Vector2(0f, -54f);

        return Vector2.zero;
    }

    private float ResolveSpriteAspect()
    {
        if (_panelImage != null && _panelImage.sprite != null && _panelImage.sprite.rect.height > 0f)
            return _panelImage.sprite.rect.width / _panelImage.sprite.rect.height;

        return 16f / 9f;
    }

    private static Sprite LoadPanelSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        var sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        var texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            return null;

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private void StartFade(float targetAlpha, float duration, bool deactivateWhenHidden)
    {
        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(CoFade(targetAlpha, duration, deactivateWhenHidden));
    }

    private IEnumerator CoFade(float targetAlpha, float duration, bool deactivateWhenHidden)
    {
        float startAlpha = _group.alpha;
        if (duration <= 0f)
        {
            _group.alpha = targetAlpha;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _group.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            _group.alpha = targetAlpha;
        }

        if (deactivateWhenHidden)
        {
            _panel.gameObject.SetActive(false);
            _currentPanelId = string.Empty;
        }

        _fadeRoutine = null;
    }

    private static float ResolveFadeSeconds(int fadeMs, float fallbackSeconds)
        => fadeMs > 0 ? fadeMs / 1000f : fallbackSeconds;
}
