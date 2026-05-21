using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ComboCounterView : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI comboCountText;
    [SerializeField] private TextMeshProUGUI comboLabelText;

    public int ComboCount { get; private set; }

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        SetCombo(0);
    }

    public void AddHit()
    {
        SetCombo(ComboCount + 1);
    }

    public void ResetCombo()
    {
        SetCombo(0);
    }

    public void SetCombo(int value)
    {
        ComboCount = Mathf.Max(0, value);
        bool visible = ComboCount > 0;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (comboCountText != null)
            comboCountText.text = $"x{ComboCount}";

        if (comboLabelText != null)
            comboLabelText.text = "COMBO";
    }
}
