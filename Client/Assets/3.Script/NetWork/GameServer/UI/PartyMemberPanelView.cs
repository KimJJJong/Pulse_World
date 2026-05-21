using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartyMemberPanelView : MonoBehaviour
{
    private const int DefaultMaxHp = 100;

    [SerializeField] private Image hpFill;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI hpText;

    public void SetMember(string displayName, int hp, int maxHp, bool isLocalPlayer)
    {
        gameObject.SetActive(true);

        int safeMaxHp = maxHp > 0 ? maxHp : DefaultMaxHp;
        int safeHp = Mathf.Clamp(hp, 0, safeMaxHp);

        if (hpFill != null)
            hpFill.fillAmount = Mathf.Clamp01((float)safeHp / safeMaxHp);

        if (nameText != null)
        {
            nameText.text = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName;
            nameText.color = isLocalPlayer
                ? new Color(0.42f, 1f, 1f, 1f)
                : new Color(0.92f, 0.98f, 1f, 1f);
        }

        if (hpText != null)
            hpText.text = $"{safeHp}/{safeMaxHp}";
    }

    public void HideMember()
    {
        gameObject.SetActive(false);
    }
}
