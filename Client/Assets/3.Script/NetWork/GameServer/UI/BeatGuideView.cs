using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BeatGuideView : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI[] leftGuides;
    [SerializeField] private TextMeshProUGUI[] rightGuides;
    [SerializeField] private TextMeshProUGUI[] inputPositionGuides;
    [SerializeField] private float outerX = 310f;
    [SerializeField] private float targetX = 62f;
    [SerializeField] private float postHitX = 24f;
    [SerializeField] private float guideFontSize = 56f;
    [SerializeField] private Vector2 guideSize = new Vector2(66f, 78f);
    [SerializeField] private float visibleLeadBeats = 3f;
    [SerializeField] private float fadeInBeats = 0.55f;
    [SerializeField] private float fadeOutMs = 145f;
    [SerializeField] private float trailAlpha = 0.38f;
    [SerializeField] private float inputMarkerAlpha = 0.42f;
    [SerializeField] private float faintInputMarkerAlpha = 0.035f;
    [SerializeField] private float inputMarkerOutlineWidth = 0.24f;
    [SerializeField] private float faintAlpha = 0.035f;
    [SerializeField] private float clearAlpha = 0.82f;
    [SerializeField] private float fadeToFaintSeconds = 0.45f;
    [SerializeField] private float riseToClearSeconds = 0.08f;
    [SerializeField] private Color guideColor = new Color(0.88f, 0.98f, 0.92f, 1f);
    [SerializeField] private Color inputMarkerColor = new Color(0.88f, 0.98f, 0.92f, 1f);

    private float _visibilityAlpha;
    private float _targetAlpha;
    private readonly HashSet<long> _clearedBeats = new HashSet<long>();

    private void Awake()
    {
        EnsureReferences();
        ConfigureGuideLayout();
        ApplyVisualState();
    }

    private void OnEnable()
    {
        EnsureReferences();
        ConfigureGuideLayout();
        ApplyVisualState();
    }

    private void Update()
    {
        UpdateAttentionAlpha();
        ApplyVisualState();
    }

    public void NotifyInputAccepted(long inputBeat)
    {
        _targetAlpha = faintAlpha;
        _clearedBeats.Add(inputBeat);
    }

    public void NotifyInputMissed()
    {
        _targetAlpha = clearAlpha;
    }

    private void EnsureReferences()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        EnsureInputPositionGuideReferences();
    }

    private void EnsureInputPositionGuideReferences()
    {
        if (HasValidGuideReferences(inputPositionGuides))
            return;

        TextMeshProUGUI[] textComponents = GetComponentsInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI left = null;
        TextMeshProUGUI right = null;
        for (int i = 0; i < textComponents.Length; i++)
        {
            TextMeshProUGUI text = textComponents[i];
            if (text == null)
                continue;

            if (text.name == "InputPositionLeft")
                left = text;
            else if (text.name == "InputPositionRight")
                right = text;
        }

        if (left != null && right != null)
            inputPositionGuides = new[] { left, right };
        else if (left != null)
            inputPositionGuides = new[] { left };
        else if (right != null)
            inputPositionGuides = new[] { right };
    }

    private static bool HasValidGuideReferences(TextMeshProUGUI[] guides)
    {
        if (guides == null || guides.Length == 0)
            return false;

        for (int i = 0; i < guides.Length; i++)
        {
            if (guides[i] == null)
                return false;
        }

        return true;
    }

    private void ConfigureGuideLayout()
    {
        ConfigureGuideTexts(leftGuides);
        ConfigureGuideTexts(rightGuides);
        ConfigureGuideTexts(inputPositionGuides);

        _targetAlpha = Mathf.Approximately(_targetAlpha, 0f) ? clearAlpha : _targetAlpha;
        _visibilityAlpha = Mathf.Approximately(_visibilityAlpha, 0f) ? _targetAlpha : _visibilityAlpha;
    }

    private void ConfigureGuideTexts(TextMeshProUGUI[] guides)
    {
        if (guides == null)
            return;

        for (int i = 0; i < guides.Length; i++)
        {
            TextMeshProUGUI guide = guides[i];
            if (guide == null)
                continue;

            guide.gameObject.SetActive(true);
            guide.fontSize = guideFontSize;
            guide.rectTransform.sizeDelta = guideSize;
        }
    }

    private void UpdateAttentionAlpha()
    {
        float duration = _targetAlpha > _visibilityAlpha ? riseToClearSeconds : fadeToFaintSeconds;
        float rate = Mathf.Abs(clearAlpha - faintAlpha) / Mathf.Max(0.01f, duration);
        _visibilityAlpha = Mathf.MoveTowards(_visibilityAlpha, _targetAlpha, rate * Time.unscaledDeltaTime);
    }

    private void ApplyVisualState()
    {
        RhythmClient rhythm = RhythmClient.Instance;
        if (rhythm != null && rhythm.ServerSongStartMs > 0)
            ApplySyncedVisualState(rhythm);
        else
            ApplyPreviewVisualState();

        ApplyInputPositionGuides();
    }

    private void ApplySyncedVisualState(RhythmClient rhythm)
    {
        long nowMs = rhythm.GetCurrentServerTimeMs();
        double beatMs = rhythm.GetBeatDurationMs();
        double beatPosition = (nowMs - rhythm.ServerSongStartMs) / beatMs;
        long anchorBeat = System.Math.Max(0, (long)System.Math.Floor(beatPosition));
        float judgeWindowMs = Mathf.Max(40f, rhythm.judgeWindowMs);

        CleanupClearedBeats(anchorBeat);
        ApplySyncedGuideSide(leftGuides, true, anchorBeat, nowMs, beatMs, judgeWindowMs, rhythm);
        ApplySyncedGuideSide(rightGuides, false, anchorBeat, nowMs, beatMs, judgeWindowMs, rhythm);
    }

    private void ApplySyncedGuideSide(
        TextMeshProUGUI[] guides,
        bool leftSide,
        long anchorBeat,
        long nowMs,
        double beatMs,
        float judgeWindowMs,
        RhythmClient rhythm)
    {
        if (guides == null)
            return;

        for (int i = 0; i < guides.Length; i++)
        {
            TextMeshProUGUI guide = guides[i];
            if (guide == null)
                continue;

            long targetBeat = anchorBeat + i;
            long targetTimeMs = rhythm.GetBeatTimeMs(targetBeat);
            float timeToTargetMs = targetTimeMs - nowMs;
            float timeToTargetBeats = (float)(timeToTargetMs / beatMs);

            float x = EvaluateGuideX(timeToTargetBeats);
            guide.rectTransform.anchoredPosition = new Vector2(leftSide ? -x : x, 0f);

            float alpha = _clearedBeats.Contains(targetBeat)
                ? 0f
                : _visibilityAlpha * EvaluateGuideEnvelope(timeToTargetBeats, timeToTargetMs, judgeWindowMs);
            guide.color = WithAlpha(guideColor, alpha);
        }
    }

    private void ApplyPreviewVisualState()
    {
        double beatPosition = Time.unscaledTime * 1.35f;
        long anchorBeat = (long)System.Math.Floor(beatPosition);
        ApplyPreviewGuideSide(leftGuides, true, anchorBeat, beatPosition);
        ApplyPreviewGuideSide(rightGuides, false, anchorBeat, beatPosition);
    }

    private void ApplyPreviewGuideSide(TextMeshProUGUI[] guides, bool leftSide, long anchorBeat, double beatPosition)
    {
        if (guides == null)
            return;

        for (int i = 0; i < guides.Length; i++)
        {
            TextMeshProUGUI guide = guides[i];
            if (guide == null)
                continue;

            float timeToTargetBeats = (float)(anchorBeat + i - beatPosition);
            float previewBeatMs = 500f;
            float timeToTargetMs = timeToTargetBeats * previewBeatMs;
            float x = EvaluateGuideX(timeToTargetBeats);
            guide.rectTransform.anchoredPosition = new Vector2(leftSide ? -x : x, 0f);
            guide.color = WithAlpha(
                guideColor,
                _visibilityAlpha * EvaluateGuideEnvelope(timeToTargetBeats, timeToTargetMs, 100f));
        }
    }

    private float EvaluateGuideX(float timeToTargetBeats)
    {
        float normalized = timeToTargetBeats / Mathf.Max(0.01f, visibleLeadBeats);
        float x = Mathf.LerpUnclamped(targetX, outerX, normalized);
        return Mathf.Clamp(x, postHitX, outerX);
    }

    private float EvaluateGuideEnvelope(float timeToTargetBeats, float timeToTargetMs, float judgeWindowMs)
    {
        if (timeToTargetBeats > visibleLeadBeats)
            return 0f;

        if (timeToTargetMs < -fadeOutMs)
            return 0f;

        float fadeIn = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(visibleLeadBeats, visibleLeadBeats - fadeInBeats, timeToTargetBeats));
        float fadeOut = timeToTargetMs < 0f
            ? 1f - Mathf.InverseLerp(0f, fadeOutMs, -timeToTargetMs)
            : 1f;
        float windowRamp = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(judgeWindowMs * 2.5f, 0f, Mathf.Abs(timeToTargetMs)));
        float windowBoost = Mathf.Lerp(trailAlpha, 1f, windowRamp);

        return Mathf.Clamp01(fadeIn) * Mathf.Clamp01(fadeOut) * windowBoost;
    }

    private void ApplyInputPositionGuides()
    {
        if (inputPositionGuides == null)
            return;

        float visibilityT = Mathf.InverseLerp(faintAlpha, clearAlpha, _visibilityAlpha);
        float alpha = Mathf.Lerp(faintInputMarkerAlpha, inputMarkerAlpha, visibilityT);
        for (int i = 0; i < inputPositionGuides.Length; i++)
        {
            TextMeshProUGUI guide = inputPositionGuides[i];
            if (guide == null)
                continue;

            float x = i == 0 ? -targetX : targetX;
            guide.rectTransform.anchoredPosition = new Vector2(x, 0f);
            guide.alpha = alpha;
            guide.color = WithAlpha(inputMarkerColor, alpha);
            guide.faceColor = WithAlpha(inputMarkerColor, 0f);
            guide.outlineColor = WithAlpha(inputMarkerColor, alpha);
            guide.outlineWidth = inputMarkerOutlineWidth;
            float rendererAlpha = inputMarkerAlpha > 0.001f ? alpha / inputMarkerAlpha : alpha;
            guide.canvasRenderer.SetAlpha(Mathf.Clamp01(rendererAlpha));
        }
    }

    private void CleanupClearedBeats(long anchorBeat)
    {
        _clearedBeats.RemoveWhere(beat => beat < anchorBeat - 1 || beat > anchorBeat + 16);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
