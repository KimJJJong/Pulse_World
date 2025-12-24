using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillSlotView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Image cooldownMask;   // Radial360 Filled
    [SerializeField] private TextMeshProUGUI cooldownText;

    private float _cooldownEndTime;
    private float _cooldownDuration;
    private bool _running;

    void Awake()
    {
        if (cooldownMask != null)
        {
            cooldownMask.type = Image.Type.Filled;
            cooldownMask.fillMethod = Image.FillMethod.Radial360;
            cooldownMask.fillOrigin = (int)Image.Origin360.Top;
            cooldownMask.fillClockwise = false; // 취향
            cooldownMask.fillAmount = 0f;
            cooldownMask.gameObject.SetActive(false);
        }
        if (cooldownText != null) cooldownText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_running) return;

        float remain = _cooldownEndTime - Time.time;
        if (remain <= 0f)
        {
            StopCooldown();
            return;
        }

        float rate = remain / _cooldownDuration; // 1 -> 0
        if (cooldownMask != null) cooldownMask.fillAmount = Mathf.Clamp01(rate);
        if (cooldownText != null) cooldownText.text = $"{Mathf.CeilToInt(remain)}";
    }

    public void SetIcon(Sprite s)
    {
        if (icon != null) icon.sprite = s;
    }

    public void StartCooldown(float duration)
    {
        _cooldownDuration = Mathf.Max(0.01f, duration);
        _cooldownEndTime = Time.time + _cooldownDuration;
        _running = true;

        if (cooldownMask != null)
        {
            cooldownMask.gameObject.SetActive(true);
            cooldownMask.fillAmount = 1f;
        }
        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(true);
            cooldownText.text = $"{Mathf.CeilToInt(duration)}";
        }
    }

    public void StopCooldown()
    {
        _running = false;
        if (cooldownMask != null) cooldownMask.gameObject.SetActive(false);
        if (cooldownText != null) cooldownText.gameObject.SetActive(false);
    }
}
