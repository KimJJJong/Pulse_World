using UnityEngine;
using UnityEngine.UI;
using Client.Content.Item;
using System.Collections.Generic;
using System.IO;

public class HudPresenter : MonoBehaviour
{
    public static HudPresenter Instance { get; private set; }

    [SerializeField] private HudConfig _config;
    [SerializeField] private HexHudView _view;
    [SerializeField] private SkillSlotView[] _skillSlots;

    // 슬롯 0~3 = H/J/K/L 스킬 슬롯. Space 일반공격은 입력 전용으로만 보관한다.
    // 현재 장비 기반으로 바인딩된 skillId 캐시
    private string[] _boundSkillIds;

    void Awake()
    {
        if (Instance == null)
            Instance = this;

        if (_view == null)
            _view = GetComponentInChildren<HexHudView>(true);

        EnsureSkillSlots();
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
        ApplyCurrentInputSkillSlots();
    }

    void OnDisable()
    {
        var gs = ClientGameState.Instance;
        if (gs != null)
            gs.MyEntityChanged -= OnMyEntityChanged;

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryUpdated -= OnInventoryUpdated;

        if (Instance == this)
            Instance = null;
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
        ApplyCurrentInputSkillSlots();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  장착 장비 → SkillSlot 자동 Bind
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 인벤토리가 갱신될 때마다 호출.
    /// 장착된 장비의 skill_id와 아이콘을 SkillSlot에 바인딩한다.
    ///
    /// 슬롯 배치 정책:
    ///   Space   = 무기의 normal_attack_skill_id  (HUD 슬롯에는 표시하지 않음)
    ///   Slot 0~ = 장착 장비의 skill_id 순서대로 (H / J / K / L)
    /// </summary>
    private void OnInventoryUpdated()
    {
        EnsureSkillSlots();
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

            Sprite icon = LoadEquipmentIcon(tmpl);

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

                // UI: Slot 0 = H키 무기 스킬. Space 일반공격은 입력에만 반영한다.
                SetSlot(0, icon, weaponSkillId);
                _boundSkillIds[0] = weaponSkillId;
            }
            else
            {
                // 비무기 장비: skill_id → 서버 Slot 1, 2, 3 순서 (J, K, L키)
                if (string.IsNullOrEmpty(tmpl.skill_id)) continue;
                if (nonWeaponSlotIndex >= SKILL_SLOT_COUNT) break;

                skillSlotIds[nonWeaponSlotIndex] = tmpl.skill_id;

                // UI: 비무기 slot은 UI Slot 1부터 (J/K/L)
                int uiSlot = nonWeaponSlotIndex;
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

    public void ApplyServerSkillSlots(string normalAttackSkillId, IReadOnlyList<string> activeSkillIds)
    {
        EnsureSkillSlots();
        if (_skillSlots == null || _skillSlots.Length == 0)
            return;

        normalAttackSkillId = string.IsNullOrWhiteSpace(normalAttackSkillId) ? "Attack" : normalAttackSkillId;
        if (!HasAnyPlayableSkill(activeSkillIds))
        {
            Debug.LogWarning("[HudPresenter] Server SkillSlots are empty. Keeping existing HUD skill binding.");
            return;
        }

        int maxInputSlots = 4;
        for (int i = 0; i < maxInputSlots; i++)
        {
            string skillId = activeSkillIds != null && i < activeSkillIds.Count ? activeSkillIds[i] : "";
            int uiSlot = i;
            if (uiSlot >= _skillSlots.Length)
                continue;

            if (string.IsNullOrWhiteSpace(skillId))
                ClearSlot(uiSlot);
            else
                SetSlot(uiSlot, ResolveSkillIcon(skillId, uiSlot), skillId);
        }

        for (int i = maxInputSlots; i < _skillSlots.Length; i++)
            ClearSlot(i);

        Debug.Log($"[HudPresenter] Server SkillSlots applied: NormalAttack={normalAttackSkillId} Skills=[{string.Join(",", ToArray(activeSkillIds))}]");
    }

    private void ApplyCurrentInputSkillSlots()
    {
        var input = RhythmInputController.Instance;
        if (input == null)
            return;

        var skillIds = new List<string>();
        bool hasAnySkill = false;
        for (int i = 0; i < 4; i++)
        {
            string skillId = input.GetSkillSlotId(i) ?? "";
            skillIds.Add(skillId);
            if (!IsPlaceholderSkillId(skillId))
                hasAnySkill = true;
        }

        string normalAttackSkillId = input.GetNormalAttackSkillId();
        if (!hasAnySkill)
            return;

        ApplyServerSkillSlots(normalAttackSkillId, skillIds);
    }

    private static bool HasAnyPlayableSkill(IReadOnlyList<string> skillIds)
    {
        if (skillIds == null)
            return false;

        for (int i = 0; i < skillIds.Count; i++)
        {
            if (!IsPlaceholderSkillId(skillIds[i]))
                return true;
        }

        return false;
    }

    private static bool IsPlaceholderSkillId(string skillId)
    {
        return string.IsNullOrWhiteSpace(skillId)
            || string.Equals(skillId.Trim(), "Attack", System.StringComparison.OrdinalIgnoreCase);
    }

    private void SetSlot(int index, Sprite icon, string skillId)
    {
        if (index < 0 || index >= _skillSlots.Length) return;
        var slot = _skillSlots[index];
        if (slot == null) return;

        if (icon != null)
            slot.SetIcon(icon);
        else if (!TrySetFallbackIcon(index, slot))
        {
            bool sameSkillAlreadyBound = _boundSkillIds != null
                && index < _boundSkillIds.Length
                && string.Equals(_boundSkillIds[index], skillId, System.StringComparison.OrdinalIgnoreCase);

            if (!sameSkillAlreadyBound)
                slot.ClearIcon();
        }

        _boundSkillIds[index] = skillId;
    }

    private void ClearSlot(int index)
    {
        if (index < 0 || index >= _skillSlots.Length) return;
        _skillSlots[index]?.ClearIcon();
        if (_boundSkillIds != null && index < _boundSkillIds.Length)
            _boundSkillIds[index] = "";
    }

    /// <summary>
    /// icon_path 기반으로 Sprite 로드.
    /// Resources 폴더 기준 경로 (확장자 없이).
    /// </summary>
    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath)) return null;

        string path = iconPath.Trim().Replace("\\", "/");
        const string assetsResourcesPrefix = "Assets/Resources/";
        const string resourcesPrefix = "Resources/";
        if (path.StartsWith(assetsResourcesPrefix, System.StringComparison.OrdinalIgnoreCase))
            path = path.Substring(assetsResourcesPrefix.Length);
        else if (path.StartsWith(resourcesPrefix, System.StringComparison.OrdinalIgnoreCase))
            path = path.Substring(resourcesPrefix.Length);

        path = Path.ChangeExtension(path, null);

        var sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
            return sprite;

        string fileName = Path.GetFileNameWithoutExtension(path);
        if (!string.IsNullOrEmpty(fileName) && !path.StartsWith("Icons/", System.StringComparison.OrdinalIgnoreCase))
            sprite = Resources.Load<Sprite>($"Icons/{fileName}");

        if (sprite == null)
            Debug.LogWarning($"[HudPresenter] Icon not found: {iconPath}");
        return sprite;
    }

    private Sprite LoadEquipmentIcon(EquipmentTemplate tmpl)
    {
        if (tmpl == null)
            return null;

        var icon = RhythmRPG.Managers.GameResourceManager.Instance != null
            ? RhythmRPG.Managers.GameResourceManager.Instance.GetIcon(tmpl.id)
            : null;

        return icon != null ? icon : LoadIcon(tmpl.icon_path);
    }

    private Sprite ResolveSkillIcon(string skillId, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return null;

        string clean = skillId.Trim();
        var equipmentIcon = ResolveEquipmentIconForSkill(clean);
        if (equipmentIcon != null)
            return equipmentIcon;

        string[] candidates =
        {
            $"SkillIcons/{clean}",
            $"Icons/{clean}",
            $"UI/Skills/{clean}",
            $"Data/NewSkills/Icons/{clean}"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            var sprite = Resources.Load<Sprite>(candidates[i]);
            if (sprite != null)
                return sprite;
        }

        if (_config?.skillIcons != null && slotIndex >= 0 && slotIndex < _config.skillIcons.Length)
            return _config.skillIcons[slotIndex];

        return null;
    }

    private Sprite ResolveEquipmentIconForSkill(string skillId)
    {
        var inv = InventoryManager.Instance;
        var itemData = ItemDataManager.Instance;
        if (itemData == null)
            return null;

        if (inv?.Equipments != null)
        {
            for (int i = 0; i < inv.Equipments.Count; i++)
            {
                var equipped = inv.Equipments[i];
                if (!equipped.IsEquipped)
                    continue;

                var tmpl = itemData.GetEquipment(equipped.TemplateId);
                if (tmpl == null)
                    continue;

                bool matchesSkill = string.Equals(tmpl.skill_id, skillId, System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tmpl.normal_attack_skill_id, skillId, System.StringComparison.OrdinalIgnoreCase);

                if (!matchesSkill)
                    continue;

                var icon = LoadEquipmentIcon(tmpl);
                if (icon != null)
                    return icon;
            }
        }

        var templateBySkill = itemData.FindEquipmentBySkillId(skillId);
        return LoadEquipmentIcon(templateBySkill);
    }

    /// <summary>HudConfig 기본 아이콘 폴백</summary>
    private void ApplyFallbackIcons()
    {
        EnsureSkillSlots();
        if (_config?.skillIcons == null) return;
        int n = Mathf.Min(_skillSlots.Length, _config.skillIcons.Length);
        for (int i = 0; i < n; i++)
        {
            if (_skillSlots[i] != null)
                _skillSlots[i].SetIcon(_config.skillIcons[i]);
        }
    }

    private bool TrySetFallbackIcon(int index, SkillSlotView slot)
    {
        if (_config?.skillIcons == null) return false;
        if (index >= _config.skillIcons.Length) return false;
        if (_config.skillIcons[index] == null) return false;

        slot.SetIcon(_config.skillIcons[index]);
        return true;
    }

    private void EnsureSkillSlots()
    {
        if (_skillSlots == null || _skillSlots.Length == 0)
            _skillSlots = GetComponentsInChildren<SkillSlotView>(true);

        if (_skillSlots != null && _skillSlots.Length > 1)
            System.Array.Sort(_skillSlots, CompareSkillSlots);

        int count = _skillSlots != null ? _skillSlots.Length : 0;
        if (_boundSkillIds == null || _boundSkillIds.Length != count)
            _boundSkillIds = new string[count];
    }

    private static int CompareSkillSlots(SkillSlotView a, SkillSlotView b)
    {
        return GetSlotIndex(a).CompareTo(GetSlotIndex(b));
    }

    private static int GetSlotIndex(SkillSlotView slot)
    {
        if (slot == null)
            return int.MaxValue;

        string name = slot.gameObject.name;
        int underscore = name.LastIndexOf('_');
        if (underscore >= 0 && underscore + 1 < name.Length
            && int.TryParse(name.Substring(underscore + 1), out int parsed))
        {
            return parsed;
        }

        return int.MaxValue;
    }

    private static string[] ToArray(IReadOnlyList<string> list)
    {
        if (list == null)
            return new string[0];

        var result = new string[list.Count];
        for (int i = 0; i < list.Count; i++)
            result[i] = list[i];
        return result;
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
