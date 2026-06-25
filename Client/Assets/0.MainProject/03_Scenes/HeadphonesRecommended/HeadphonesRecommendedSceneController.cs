using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class HeadphonesRecommendedSceneController : MonoBehaviour
{
    private const int CanvasSortingOrder = 32766;
    private const float ReferenceWidth = 1280f;
    private const float ReferenceHeight = 720f;

    [SerializeField] private string nextSceneName = SceneNames.Login;
    [SerializeField, Min(0f)] private float startDelay = 0.15f;
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.7f;
    [SerializeField, Min(0f)] private float holdDuration = 2.0f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.75f;
    [SerializeField] private bool continueThroughLoadingScene = false;

    private CanvasGroup _contentGroup;
    private bool _isTransitioning;

    private void Awake()
    {
        BuildUi();
        SetContentAlpha(0f);
    }

    private void Start()
    {
        StartCoroutine(Co_PlaySequence());
    }

    private IEnumerator Co_PlaySequence()
    {
        if (startDelay > 0f)
            yield return WaitRealtime(startDelay);

        yield return Co_FadeContent(0f, 1f, fadeInDuration);

        if (holdDuration > 0f)
            yield return WaitRealtime(holdDuration);

        yield return Co_FadeContent(1f, 0f, fadeOutDuration);

        LoadNextScene();
    }

    private void LoadNextScene()
    {
        if (_isTransitioning)
            return;

        _isTransitioning = true;

        if (AppBootstrap.Instance == null)
        {
            SceneRouter.Load(SceneNames.Bootstrap);
            return;
        }

        string targetScene = string.IsNullOrWhiteSpace(nextSceneName) ? SceneNames.WorldMap : nextSceneName;

        if (continueThroughLoadingScene)
        {
            SceneRouter.ChangeSceneAsync(targetScene);
            return;
        }

        SceneManager.LoadScene(targetScene);
    }

    private IEnumerator Co_FadeContent(float from, float to, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetContentAlpha(Mathf.Lerp(from, to, Smooth01(t)));
            yield return null;
        }

        SetContentAlpha(to);
    }

    private static IEnumerator WaitRealtime(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void SetContentAlpha(float alpha)
    {
        if (_contentGroup != null)
            _contentGroup.alpha = Mathf.Clamp01(alpha);
    }

    private void BuildUi()
    {
        Canvas canvas = GetOrCreateComponent<Canvas>("HeadphonesRecommendedCanvas");
        GameObject canvasObject = canvas.gameObject;
        canvasObject.layer = UiLayer();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.overrideSorting = true;
        canvas.sortingOrder = CanvasSortingOrder;

        CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GetOrAdd<GraphicRaycaster>(canvasObject).enabled = false;
        ClearChildren(canvasObject.transform);

        Image background = CreateImage("BlackBackground", canvasObject.transform, Color.black);
        Stretch(background.rectTransform);

        RectTransform content = CreateRect("Content", canvasObject.transform, Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));
        Stretch(content);
        _contentGroup = GetOrAdd<CanvasGroup>(content.gameObject);
        _contentGroup.interactable = false;
        _contentGroup.blocksRaycasts = false;

        RectTransform centerGroup = CreateRect("HeadphonesRecommendedGroup", content, new Vector2(0f, -2f), new Vector2(560f, 190f));
        CreateHeadphonesIcon(centerGroup);

        TextMeshProUGUI label = CreateText(
            "HeadphonesRecommendedText",
            centerGroup,
            "HEADPHONES RECOMMENDED",
            new Vector2(0f, -52f),
            new Vector2(560f, 42f),
            22f,
            TextAlignmentOptions.Center);

        label.color = new Color(0.94f, 0.94f, 0.94f, 1f);
        label.fontStyle = FontStyles.Normal;
        label.characterSpacing = 8f;
        label.enableWordWrapping = false;
    }

    private static void CreateHeadphonesIcon(RectTransform parent)
    {
        RectTransform iconRoot = CreateRect("HeadphonesLineIcon", parent, new Vector2(0f, 28f), new Vector2(104f, 104f));
        Color color = new Color(0.94f, 0.94f, 0.94f, 1f);

        const float radius = 35f;
        const float centerY = -3f;
        const float thickness = 2.3f;
        const int segments = 24;

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.Lerp(22f, 158f, (i + 0.5f) / segments);
            float radians = angle * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(radians) * radius, centerY + Mathf.Sin(radians) * radius);
            float rotation = angle - 90f;
            CreateLine("Headband", iconRoot, position, new Vector2(6.3f, thickness), rotation, color);
        }

        CreateLine("LeftStem", iconRoot, new Vector2(-36f, -3f), new Vector2(thickness, 38f), 0f, color);
        CreateLine("RightStem", iconRoot, new Vector2(36f, -3f), new Vector2(thickness, 38f), 0f, color);

        CreateLine("LeftCupOuter", iconRoot, new Vector2(-45f, -11f), new Vector2(thickness, 31f), 0f, color);
        CreateLine("LeftCupInner", iconRoot, new Vector2(-38f, -11f), new Vector2(thickness, 31f), 0f, color);
        CreateLine("LeftCupTop", iconRoot, new Vector2(-41.5f, 4.5f), new Vector2(9f, thickness), 0f, color);
        CreateLine("LeftCupBottom", iconRoot, new Vector2(-41.5f, -26.5f), new Vector2(9f, thickness), 0f, color);

        CreateLine("RightCupOuter", iconRoot, new Vector2(45f, -11f), new Vector2(thickness, 31f), 0f, color);
        CreateLine("RightCupInner", iconRoot, new Vector2(38f, -11f), new Vector2(thickness, 31f), 0f, color);
        CreateLine("RightCupTop", iconRoot, new Vector2(41.5f, 4.5f), new Vector2(9f, thickness), 0f, color);
        CreateLine("RightCupBottom", iconRoot, new Vector2(41.5f, -26.5f), new Vector2(9f, thickness), 0f, color);
    }

    private static Image CreateLine(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, float rotation, Color color)
    {
        Image line = CreateImage(name, parent, color);
        RectTransform rect = line.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        return line;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string text,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent, anchoredPosition, size);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.zero);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = UiLayer();
        gameObject.transform.SetParent(parent, false);

        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static T GetOrCreateComponent<T>(string objectName) where T : Component
    {
        GameObject gameObject = GameObject.Find(objectName);
        if (gameObject == null)
            gameObject = new GameObject(objectName);

        return GetOrAdd<T>(gameObject);
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
            component = gameObject.AddComponent<T>();
        return component;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private static int UiLayer()
    {
        int layer = LayerMask.NameToLayer("UI");
        return layer >= 0 ? layer : 0;
    }
}
