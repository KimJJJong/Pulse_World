using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HexHudView : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private Image hpFill;
    [SerializeField] private Image hpFrame;
    [SerializeField] private Image hpGlow; // optional
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("SP")]
    [SerializeField] private Image spFill;
    [SerializeField] private TextMeshProUGUI spText;

    public void SetHp(int hp, int maxHp, Color fillColor)
    {
        Debug.Log("INHEXHUDVIEW");
        float rate = (maxHp <= 0) ? 0f : (float)hp / maxHp;
        hpFill.fillAmount = Mathf.Clamp01(rate);
        hpFill.color = fillColor;

        if (hpText != null)
            hpText.text = $"{hp}/{maxHp}";
    }

    public void SetSp(int sp, int maxSp, Color fillColor)
    {
        float rate = (maxSp <= 0) ? 0f : (float)sp / maxSp;
        spFill.fillAmount = Mathf.Clamp01(rate);
        spFill.color = fillColor;

        if (spText != null)
            spText.text = $"{sp}/{maxSp}";
    }

    // 펄스가 프레임/글로우에만 적용되도록 외부에서 접근
    public void SetGlowAlpha(float a)
    {
        if (hpGlow == null) return;
        var c = hpGlow.color;
        c.a = Mathf.Clamp01(a);
        hpGlow.color = c;
    }

    public void SetFrameScale(float s)
    {
        if (hpFrame == null) return;
        hpFrame.rectTransform.localScale = Vector3.one * s;
        if (hpGlow != null)
            hpGlow.rectTransform.localScale = Vector3.one * s;
    }
}
