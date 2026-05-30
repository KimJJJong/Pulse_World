using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ComboCounterView : MonoBehaviour
{
    [SerializeField] private RectTransform rootTransform;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI comboCountText;
    [SerializeField] private TextMeshProUGUI comboLabelText;
    [SerializeField] private float fadeOutDuration = 0.28f;
    [SerializeField] private float hitMoveDuration = 0.16f;
    [SerializeField] private Vector2 hitMoveOffset = new Vector2(0f, 12f);
    [SerializeField] private Vector2 maxHitMoveOffset = new Vector2(0f, 36f);
    [SerializeField] private Vector2 resetMoveOffset = new Vector2(0f, 18f);

    public int ComboCount { get; private set; }

    private bool _isFadingOut;
    private float _fadeOutElapsed;
    private float _fadeOutStartAlpha;
    private Vector2 _baseAnchoredPosition;
    private Vector2 _fadeOutStartPosition;
    private Vector2 _hitMoveStartOffset;
    private bool _isHitMoving;
    private float _hitMoveElapsed;

    private void Awake()
    {
        if (rootTransform == null)
            rootTransform = GetComponent<RectTransform>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (rootTransform != null)
            _baseAnchoredPosition = rootTransform.anchoredPosition;

        UpdateLabels(0);
        HideImmediately();
    }

    private void Update()
    {
        UpdateHitMove();
        UpdateFadeOut();
    }

    private void UpdateHitMove()
    {
        if (!_isHitMoving || rootTransform == null)
            return;

        _hitMoveElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.01f, hitMoveDuration);
        float progress = Mathf.Clamp01(_hitMoveElapsed / duration);
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        rootTransform.anchoredPosition = _baseAnchoredPosition + Vector2.Lerp(_hitMoveStartOffset, Vector2.zero, eased);

        if (progress >= 1f)
        {
            _isHitMoving = false;
            _hitMoveStartOffset = Vector2.zero;
        }
    }

    private void UpdateFadeOut()
    {
        if (!_isFadingOut || canvasGroup == null)
            return;

        _fadeOutElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.01f, fadeOutDuration);
        float progress = Mathf.Clamp01(_fadeOutElapsed / duration);
        canvasGroup.alpha = Mathf.Lerp(_fadeOutStartAlpha, 0f, progress);

        if (rootTransform != null)
            rootTransform.anchoredPosition = Vector2.Lerp(_fadeOutStartPosition, _baseAnchoredPosition + resetMoveOffset, progress);

        if (progress >= 1f)
        {
            _isFadingOut = false;
            HideImmediately();
        }
    }

    public void AddHit()
    {
        SetCombo(ComboCount + 1);
    }

    public void ResetCombo()
    {
        ComboCount = 0;

        if (canvasGroup == null || canvasGroup.alpha <= 0.001f)
        {
            UpdateLabels(0);
            HideImmediately();
            return;
        }

        _isFadingOut = true;
        _isHitMoving = false;
        _hitMoveStartOffset = Vector2.zero;
        _fadeOutElapsed = 0f;
        _fadeOutStartAlpha = canvasGroup.alpha;
        _fadeOutStartPosition = rootTransform != null ? rootTransform.anchoredPosition : _baseAnchoredPosition;
    }

    public void SetCombo(int value)
    {
        ComboCount = Mathf.Max(0, value);
        if (ComboCount <= 0)
        {
            ResetCombo();
            return;
        }

        if (canvasGroup != null)
        {
            _isFadingOut = false;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        UpdateLabels(ComboCount);
        TriggerHitMove();
    }

    private void UpdateLabels(int value)
    {
        if (comboCountText != null)
            comboCountText.text = $"x{Mathf.Max(0, value)}";

        if (comboLabelText != null)
            comboLabelText.text = "COMBO";
    }

    private void HideImmediately()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (rootTransform != null)
            rootTransform.anchoredPosition = _baseAnchoredPosition;

        _hitMoveStartOffset = Vector2.zero;
    }

    private void TriggerHitMove()
    {
        if (rootTransform == null)
            return;

        Vector2 currentOffset = rootTransform.anchoredPosition - _baseAnchoredPosition;
        _hitMoveStartOffset = ClampHitMoveOffset(currentOffset + hitMoveOffset);
        rootTransform.anchoredPosition = _baseAnchoredPosition + _hitMoveStartOffset;
        _hitMoveElapsed = 0f;
        _isHitMoving = true;
    }

    private Vector2 ClampHitMoveOffset(Vector2 offset)
    {
        float maxX = Mathf.Abs(maxHitMoveOffset.x);
        float maxY = Mathf.Abs(maxHitMoveOffset.y);
        return new Vector2(
            maxX > 0f ? Mathf.Clamp(offset.x, -maxX, maxX) : 0f,
            maxY > 0f ? Mathf.Clamp(offset.y, -maxY, maxY) : 0f);
    }
}
