using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HudView : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private Image hpFill;
    [SerializeField] private TextMeshProUGUI hpText;

    public void SetHp(float rate, Color color)
    {
        hpFill.fillAmount = Mathf.Clamp01(rate);
        hpFill.color = color;

        if (hpText != null)
            hpText.text = $"{Mathf.RoundToInt(rate * 100)}%";
    }

    // 이후
    // public void SetSp(...)
    // public void SetSkillIcon(...)
}
