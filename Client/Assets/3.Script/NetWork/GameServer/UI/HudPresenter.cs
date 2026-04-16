using UnityEngine;
using UnityEngine.UI;
using Client.Content.Item;

public class HudPresenter : MonoBehaviour
{
    [SerializeField] private HudConfig _config;
    [SerializeField] private HexHudView _view;
    [SerializeField] private SkillSlotView[] _skillSlots;

    // 슬롯 0 = 일반공격(무기), 슬롯 1~N = 스킬 슬롯
    // 현재 장비 기반으로 바인딩된 skillId 캐시
    private string[] _boundSkillIds;

    void Awake()
    {
        if (_view == null)
            _view = GetComponentInChildren<HexHudView>(true);

        if (_skillSlots == null || _skillSlots.Length == 0)
            _skillSlots = GetComponentsInChildren<SkillSlotView>(true);

        _boundSkillIds = new string[_skillSlots != null ? _skillSlots.Length : 0];
    }

    void OnEnable()
    {
        var gs = ClientGameState.Instance;
        if (gs != null)
            gs.MyEntityChanged += OnMyEntityChanged;

        // 인벤토리 업데이트 이벤트 구독
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryUpdated += OnInventoryUpdated;

        // 이미 인벤토리가 로드된 상태라면 즉시 반영
        OnInventoryUpdated();
    }

    void OnDisable()
    {
        var gs = ClientGameState.Instance;
        if (gs != null)
            gs.MyEntityChanged -= OnMyEntityChanged;

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryUpdated -= OnInventoryUpdated;
    }

    void Start()
    {
        if (ClientGameState.Instance != null)
        {
            ClientGameState.Instance.MyEntityChanged -= OnMyEntityChanged;
            ClientGameState.Instance.MyEntityChanged += OnMyEntityChanged;

            if (ClientGameState.Instance.TryGetMyEntity(out var info))
                OnMyEntityChanged(info);
        }

        OnInventoryUpdated();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  장착 장비 → SkillSlot 자동 Bind
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 인벤토리가 갱신될 때마다 호출.
    /// 장착된 장비의 skill_id와 아이콘을 SkillSlot에 바인딩한다.
    ///
    /// 슬롯 배치 정책:
    ///   Slot 0  = 무기의 normal_attack_skill_id  (Space키 일반공격)
    ///   Slot 1~ = 장착 장비의 skill_id 순서대로 (H / J / K / L)
    /// </summary>
    private void OnInventoryUpdated()
    {
        if (_skillSlots == null || _skillSlots.Length == 0) return;

        var inv = InventoryManager.Instance;
        var itemData = ItemDataManager.Instance;
        if (inv == null || itemData == null) return;

        // 장착된 장비 목록 수집
        var equippedList = inv.Equipments.FindAll(e => e.IsEquipped);
        if (equippedList.Count == 0)
        {
            ApplyFallbackIcons();
            return;
        }

        // 무기 먼저, 나머지 장비 순으로 정렬
        equippedList.Sort((a, b) =>
        {
            var tA = itemData.GetEquipment(a.TemplateId);
            var tB = itemData.GetEquipment(b.TemplateId);
            bool aIsWeapon = tA?.SlotEnum == EquipmentSlot.Weapon;
            bool bIsWeapon = tB?.SlotEnum == EquipmentSlot.Weapon;
            if (aIsWeapon && !bIsWeapon) return -1;
            if (!aIsWeapon && bIsWeapon) return 1;
            return 0;
        });

        // 슬롯 초기화
        // 서버 _activeSkillSlots 구조와 동일:
        //   [0] = 무기 skill_id (H키)
        //   [1] = 비무기 장비 첫 번째 skill_id (J키)
        //   [2] = 비무기 장비 두 번째 skill_id (K키)
        //   [3] = 비무기 장비 세 번째 skill_id (L키)
        string normalAttackSkillId = "Attack"; // Space키 폴백
        const int SKILL_SLOT_COUNT = 4;        // RhythmInputController._skillSlotIds 크기와 동일
        var skillSlotIds = new string[SKILL_SLOT_COUNT];
        for (int i = 0; i < SKILL_SLOT_COUNT; i++)
            skillSlotIds[i] = "";

        // 서버 SlotIndex와 매핑:
        //   skillSlotIds[0] → 서버 Slot=0 (H키) — 무기 skill_id
        //   skillSlotIds[1] → 서버 Slot=1 (J키) — 비무기 장비 순서대로
        string weaponSkillId = "";
        int nonWeaponSlotIndex = 1; // 비무기 장비는 Slot 1부터

        foreach (var equipped in equippedList)
        {
            var tmpl = itemData.GetEquipment(equipped.TemplateId);
            if (tmpl == null) continue;

            Sprite icon = LoadIcon(tmpl.icon_path);

            if (tmpl.SlotEnum == EquipmentSlot.Weapon)
            {
                // normal_attack_skill_id → Space키(일반공격)
                if (!string.IsNullOrEmpty(tmpl.normal_attack_skill_id))
                    normalAttackSkillId = tmpl.normal_attack_skill_id;
                else if (!string.IsNullOrEmpty(tmpl.skill_id))
                    normalAttackSkillId = tmpl.skill_id;

                // skill_id → H키 (서버 Slot=0)
                weaponSkillId = tmpl.skill_id ?? "";
                skillSlotIds[0] = weaponSkillId;

                // UI: Slot 0 = 일반공격 아이콘 (Space)
                SetSlot(0, icon, normalAttackSkillId);
                _boundSkillIds[0] = normalAttackSkillId;
            }
            else
            {
                // 비무기 장비: skill_id → 서버 Slot 1, 2, 3 순서 (J, K, L키)
                if (string.IsNullOrEmpty(tmpl.skill_id)) continue;
                if (nonWeaponSlotIndex >= SKILL_SLOT_COUNT) break;

                skillSlotIds[nonWeaponSlotIndex] = tmpl.skill_id;

                // UI: 비무기 slot은 UI Slot 1부터 (무기 아이콘이 UI Slot 0)
                int uiSlot = nonWeaponSlotIndex; // UI Slot 1 = H+1 = J
                SetSlot(uiSlot, icon, tmpl.skill_id);
                if (uiSlot < _boundSkillIds.Length)
                    _boundSkillIds[uiSlot] = tmpl.skill_id;

                nonWeaponSlotIndex++;
            }
        }

        // RhythmInputController에 주입
        // skillSlotIds[i] → _skillSlotIds[i] (i=0:H, 1:J, 2:K, 3:L)
        var input = RhythmInputController.Instance;
        if (input != null)
        {
            input.SetNormalAttackSkill(normalAttackSkillId);
            for (int i = 0; i < skillSlotIds.Length; i++)
            {
                // 빈 슬롯도 명시적으로 빈 문자열로 초기화해서 이전 값 잔류 방지
                input.SetSkillSlot(i, skillSlotIds[i] ?? "");
            }
        }

        Debug.Log($"[HudPresenter] SkillSlots bound: NormalAttack={normalAttackSkillId} Skills=[{string.Join(",", skillSlotIds)}]");
    }

    private void SetSlot(int index, Sprite icon, string skillId)
    {
        if (index < 0 || index >= _skillSlots.Length) return;
        var slot = _skillSlots[index];
        if (slot == null) return;

        if (icon != null)
            slot.SetIcon(icon);
        else
            TrySetFallbackIcon(index, slot);

        _boundSkillIds[index] = skillId;
    }

    /// <summary>
    /// icon_path 기반으로 Sprite 로드.
    /// Resources 폴더 기준 경로 (확장자 없이).
    /// </summary>
    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath)) return null;

        // 확장자 제거
        string path = iconPath;
        if (path.EndsWith(".png") || path.EndsWith(".jpg"))
            path = path.Substring(0, path.LastIndexOf('.'));

        var sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning($"[HudPresenter] Icon not found: {path}");
        return sprite;
    }

    /// <summary>HudConfig 기본 아이콘 폴백</summary>
    private void ApplyFallbackIcons()
    {
        if (_config?.skillIcons == null) return;
        int n = Mathf.Min(_skillSlots.Length, _config.skillIcons.Length);
        for (int i = 0; i < n; i++)
        {
            if (_skillSlots[i] != null)
                _skillSlots[i].SetIcon(_config.skillIcons[i]);
        }
    }

    private void TrySetFallbackIcon(int index, SkillSlotView slot)
    {
        if (_config?.skillIcons == null) return;
        if (index >= _config.skillIcons.Length) return;
        slot.SetIcon(_config.skillIcons[index]);
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void OnSkillUsed(int slotIndex, float cooldownSec)
    {
        if (_skillSlots == null) return;
        if ((uint)slotIndex >= (uint)_skillSlots.Length) return;

        _skillSlots[slotIndex]?.StartCooldown(cooldownSec);
    }

    private void OnMyEntityChanged(ClientEntityInfo me)
    {
        if (_config == null || _view == null) return;

        int maxHp = 100;
        int hp = me.Hp;
        float hpRate = (maxHp <= 0) ? 0f : (float)hp / maxHp;

        Color hpColor = hpRate <= _config.hpDangerRate
            ? _config.hpDangerColor
            : _config.hpColor;

        _view.SetHp(hp, maxHp, hpColor);
    }
}
