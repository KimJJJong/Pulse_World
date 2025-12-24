using UnityEngine;
using static UnityEngine.Rendering.STP;

public class HudPresenter : MonoBehaviour
{
    [SerializeField] private HudConfig _config;
    [SerializeField] private HudView _view;
    [SerializeField] private SkillSlotView[] _skillSlots;

    void Start()
    {
        // 아이콘 세팅
        for (int i = 0; i < _skillSlots.Length && i < _config.skillIcons.Length; i++)
            _skillSlots[i].SetIcon(_config.skillIcons[i]);
    }


    void OnEnable()
    {
        ClientGameState.Instance.MyEntityChanged += OnMyEntityChanged;
    }

    void OnDisable()
    {
        if (ClientGameState.Instance != null)
            ClientGameState.Instance.MyEntityChanged -= OnMyEntityChanged;
    }
    // 서버에서 스킬 사용 같은 이벤트에서 호출
    public void OnSkillUsed(int slotIndex, float cooldownSec)
    {
        if ((uint)slotIndex >= (uint)_skillSlots.Length) return;
        _skillSlots[slotIndex].StartCooldown(cooldownSec);
    }
    private void OnMyEntityChanged(ClientEntityInfo me)
    {
        // 예시: MaxHp가 있다고 가정
        float hpRate = me.Hp / 100f;

        Color hpColor =
            hpRate <= _config.hpDangerRate
            ? _config.hpDangerColor
            : _config.hpColor;

        _view.SetHp(hpRate, hpColor);

        // 확장 포인트
        // _view.SetSp(...)
        // _view.SetSkillCooldown(...)
    }
}
