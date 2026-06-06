using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorldMapEntryOverlay : MonoBehaviour
{
    private readonly struct EntryVariant
    {
        public EntryVariant(string title, string body)
        {
            Title = title;
            Body = body;
        }

        public string Title { get; }
        public string Body { get; }
    }

    private static readonly EntryVariant[] EntryVariants =
    {
        new("Town Pass 확인", "입장 가능한 지역을 확인하고 월드맵을 펼칩니다."),
        new("리듬 신호 동기화", "타운 서버와 박자를 맞추고 안전한 이동 경로를 찾습니다."),
        new("탐험 준비 완료", "장비와 외형 정보가 유지된 상태로 목적지를 선택합니다."),
        new("초대 키 대기", "공개 타운, 새 타운, 비공개 키 입장을 준비합니다.")
    };

    private CanvasGroup _group;

    public static void Play(Canvas canvas)
    {
        if (canvas == null || canvas.GetComponentInChildren<WorldMapEntryOverlay>(true) != null)
            return;

        var root = new GameObject("WorldMapEntryOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.transform.SetParent(canvas.transform, false);

        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();

        var background = root.GetComponent<Image>();
        background.color = new Color(0.025f, 0.035f, 0.045f, 0.92f);
        background.raycastTarget = true;

        root.GetComponent<CanvasGroup>().alpha = 0f;
        root.AddComponent<WorldMapEntryOverlay>().Build(canvas);
    }

    private void Build(Canvas canvas)
    {
        _group = GetComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = true;

        var sourceText = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
        var font = sourceText != null ? sourceText.font : null;
        var variant = EntryVariants[Random.Range(0, EntryVariants.Length)];

        var panel = CreateRect("EntryPanel", transform, new Vector2(0.5f, 0.5f), new Vector2(720f, 260f));
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.88f, 0.72f, 0.48f, 0.18f);
        panelImage.raycastTarget = false;

        var title = CreateText("EntryTitle", panel, font, 42f, FontStyles.Bold);
        title.text = variant.Title;
        title.rectTransform.anchoredPosition = new Vector2(0f, 58f);
        title.rectTransform.sizeDelta = new Vector2(640f, 72f);

        var body = CreateText("EntryBody", panel, font, 23f, FontStyles.Normal);
        body.text = variant.Body;
        body.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        body.rectTransform.sizeDelta = new Vector2(620f, 80f);

        var hint = CreateText("EntryHint", panel, font, 18f, FontStyles.Normal);
        hint.text = "World Map";
        hint.color = new Color(0.53f, 1f, 0.95f, 0.92f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, -86f);
        hint.rectTransform.sizeDelta = new Vector2(420f, 42f);

        StartCoroutine(Co_Play());
    }

    private IEnumerator Co_Play()
    {
        yield return Fade(0f, 1f, 0.18f);
        yield return new WaitForSecondsRealtime(0.95f);
        yield return Fade(1f, 0f, 0.38f);
        Destroy(gameObject);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        var elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        _group.alpha = to;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style)
    {
        var rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;

        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.98f, 0.90f, 0.70f, 1f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }
}
