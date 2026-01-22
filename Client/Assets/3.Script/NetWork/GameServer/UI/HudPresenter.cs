using UnityEngine;

public class HudPresenter : MonoBehaviour
{
    [SerializeField] private HudConfig _config;
    [SerializeField] private HexHudView _view;          //  HexHudView
    [SerializeField] private SkillSlotView[] _skillSlots;

    private bool _iconsApplied;

    void Awake()
    {
        if (_view == null)
            _view = GetComponentInChildren<HexHudView>(true);

        if (_skillSlots == null || _skillSlots.Length == 0)
            _skillSlots = GetComponentsInChildren<SkillSlotView>(true);
    }

    void OnEnable()
    {
        var gs = ClientGameState.Instance;
        if (gs != null)
        {
            Debug.Log("[HudPresenter] Subscribed to MyEntityChanged");
            gs.MyEntityChanged += OnMyEntityChanged;
        }
        else
        {
            Debug.LogWarning("[HudPresenter] ClientGameState.Instance is NULL in OnEnable!");
        }

        TryApplySkillIcons();
    }

    void OnDisable()
    {
        var gs = ClientGameState.Instance;
        if (gs != null)
            gs.MyEntityChanged -= OnMyEntityChanged;
    }

    void Start()
    {
        // 1. OnEnable 시점에 GS가 null이었을 수 있으므로 다시 체크
        if (ClientGameState.Instance != null)
        {
            // 중복 구독 방지를 위해 제거 후 추가 (안전장치)
            ClientGameState.Instance.MyEntityChanged -= OnMyEntityChanged;
            ClientGameState.Instance.MyEntityChanged += OnMyEntityChanged;
            
            // 초기 상태 반영
            if (ClientGameState.Instance.TryGetMyEntity(out var info))
                OnMyEntityChanged(info);
        }
        else
        {
             Debug.LogError("[HudPresenter] ClientGameState is still null in Start!");
        }

        TryApplySkillIcons();
    }

    private void TryApplySkillIcons()
    {
        if (_iconsApplied) return;
        if (_config == null) return;
        if (_skillSlots == null) return;

        var icons = _config.skillIcons;
        if (icons == null) return;

        int n = Mathf.Min(_skillSlots.Length, icons.Length);
        for (int i = 0; i < n; i++)
        {
            if (_skillSlots[i] == null) continue;
            _skillSlots[i].SetIcon(icons[i]);
        }

        _iconsApplied = true;
    }

    public void OnSkillUsed(int slotIndex, float cooldownSec)
    {
        if (_skillSlots == null) return;
        if ((uint)slotIndex >= (uint)_skillSlots.Length) return;

        var slot = _skillSlots[slotIndex];
        if (slot == null) return;

        slot.StartCooldown(cooldownSec);
    }

    private void OnMyEntityChanged(ClientEntityInfo me)
    {

        if (_config == null || _view == null)
        {
            Debug.Log($"[OnMyEntityChanged] _config == null || _view == null");
            return;
        }

        // 지금 me에 MaxHp가 없으니 임시 max=100
        int maxHp = 100;
        int hp = me.Hp;

        float hpRate = (maxHp <= 0) ? 0f : (float)hp / maxHp;

        Color hpColor =
            hpRate <= _config.hpDangerRate
                ? _config.hpDangerColor
                : _config.hpColor;

        _view.SetHp(hp, maxHp, hpColor);

        // SP도 현재 데이터가 없으면 임시로 0/100
        // _view.SetSp(0, 100, _config.spColor);
    }
}
