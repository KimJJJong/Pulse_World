using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ComboCounterView : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI comboCountText;
    [SerializeField] private TextMeshProUGUI comboLabelText;
    [SerializeField] private float fadeOutDuration = 0.28f;

    public int ComboCount { get; private set; }

    private bool _isFadingOut;
    private float _fadeOutElapsed;
    private float _fadeOutStartAlpha;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        UpdateLabels(0);
        HideImmediately();
    }

    private void Update()
    {
        if (!_isFadingOut || canvasGroup == null)
            return;

        _fadeOutElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.01f, fadeOutDuration);
        float progress = Mathf.Clamp01(_fadeOutElapsed / duration);
        canvasGroup.alpha = Mathf.Lerp(_fadeOutStartAlpha, 0f, progress);

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
        _fadeOutElapsed = 0f;
        _fadeOutStartAlpha = canvasGroup.alpha;
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
    }
}
