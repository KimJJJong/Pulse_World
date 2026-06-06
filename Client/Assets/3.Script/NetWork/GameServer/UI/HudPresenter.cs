using UnityEngine;
using UnityEngine.UI;
using Client.Content.Item;
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;

public class HudPresenter : MonoBehaviour
{
    public static HudPresenter Instance { get; private set; }

    [SerializeField] private HudConfig _config;
    [SerializeField] private HexHudView _view;
    [SerializeField] private SkillSlotView[] _skillSlots;
    [SerializeField] private PartyMemberPanelView[] _partyPanels;
    [SerializeField] private StageInfoPanelView _stageInfo;
    [SerializeField] private BeatGuideView _beatGuide;
    [SerializeField] private ComboCounterView _comboView;
    [SerializeField] private MinimapHudView _minimapView;

    // 슬롯 0~3 = H/J/K/L 스킬 슬롯. Space 일반공격은 입력 전용으로만 보관한다.
    // 현재 장비 기반으로 바인딩된 skillId 캐시
    private string[] _boundSkillIds;
    private RhythmInputController _inputController;
    private int _comboCount;
    private long _lastComboInputBeat = long.MinValue;

    private const int ComboIdleResetBeats = 3;

    void Awake()
    {
        if (Instance == null)
            Instance = this;

        if (_view == null)
            _view = GetComponentInChildren<HexHudView>(true);

        EnsureHudViews();
        EnsureSkillSlots();
        EnsureMinimapView();
    }

    void OnEnable()
    {
        EnsureMinimapView();

        var gs = ClientGameState.Instance;
        if (gs != null)
        {
            gs.MyEntityChanged += OnMyEntityChanged;
            gs.PartyStateChanged += RefreshPartyPanels;
        }

        // 인벤토리 업데이트 이벤트 구독
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryUpdated += OnInventoryUpdated;

        // 이미 인벤토리가 로드된 상태라면 즉시 반영
        OnInventoryUpdated();
        ApplyCurrentInputSkillSlots();
        TryBindInputController();
        RefreshPartyPanels();
        RefreshStagePanel();
        _comboView?.SetCombo(_comboCount);
    }

    void OnDisable()
    {
        var gs = ClientGameState.Instance;
        if (gs != null)
        {
            gs.MyEntityChanged -= OnMyEntityChanged;
            gs.PartyStateChanged -= RefreshPartyPanels;
        }

        var inventoryManager = InventoryManager.ExistingInstance;
        if (inventoryManager != null)
            inventoryManager.OnInventoryUpdated -= OnInventoryUpdated;

        UnbindInputController();

        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        EnsureMinimapView();

        if (ClientGameState.Instance != null)
        {
            ClientGameState.Instance.MyEntityChanged -= OnMyEntityChanged;
            ClientGameState.Instance.MyEntityChanged += OnMyEntityChanged;
            ClientGameState.Instance.PartyStateChanged -= RefreshPartyPanels;
            ClientGameState.Instance.PartyStateChanged += RefreshPartyPanels;

            if (ClientGameState.Instance.TryGetMyEntity(out var info))
                OnMyEntityChanged(info);
        }

        OnInventoryUpdated();
        ApplyCurrentInputSkillSlots();
        TryBindInputController();
        RefreshPartyPanels();
        RefreshStagePanel();
    }

    void Update()
    {
        TryBindInputController();
        ResetIdleComboIfNeeded();
        RefreshStagePanel();
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

        for (int i = 0; i < skillSlotIds.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(skillSlotIds[i]))
                ClearSlot(i);
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

    private Sprite ResolveWeaponIcon()
    {
        var inv = InventoryManager.Instance;
        var itemData = ItemDataManager.Instance;
        if (inv?.Equipments == null || itemData == null)
            return null;

        for (int i = 0; i < inv.Equipments.Count; i++)
        {
            var equipped = inv.Equipments[i];
            if (!equipped.IsEquipped)
                continue;

            var tmpl = itemData.GetEquipment(equipped.TemplateId);
            if (tmpl == null || tmpl.SlotEnum != EquipmentSlot.Weapon)
                continue;

            var icon = LoadEquipmentIcon(tmpl);
            if (icon != null)
                return icon;
        }

        return null;
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

    private void EnsureHudViews()
    {
        if (_partyPanels == null || _partyPanels.Length == 0)
            _partyPanels = GetComponentsInChildren<PartyMemberPanelView>(true);

        if (_stageInfo == null)
            _stageInfo = GetComponentInChildren<StageInfoPanelView>(true);

        if (_beatGuide == null)
            _beatGuide = GetComponentInChildren<BeatGuideView>(true);

        if (_comboView == null)
            _comboView = GetComponentInChildren<ComboCounterView>(true);
    }

    private void EnsureMinimapView()
    {
        if (_minimapView == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                _minimapView = canvas.GetComponentInChildren<MinimapHudView>(true);
        }

        if (_minimapView == null)
        {
            RectTransform parent = FindHudRootRect();
            if (parent == null)
                return;

            GameObject minimapObject = new GameObject("MinimapPanel", typeof(RectTransform));
            RectTransform minimapRect = minimapObject.GetComponent<RectTransform>();
            minimapRect.SetParent(parent, false);
            minimapRect.localScale = Vector3.one;
            _minimapView = minimapObject.AddComponent<MinimapHudView>();
        }

        _minimapView.EnsureRuntimeUi();
    }

    private RectTransform FindHudRootRect()
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == "HUDRoot" && current is RectTransform hudRoot)
                return hudRoot;

            current = current.parent;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            return canvas.transform as RectTransform;

        return transform.parent as RectTransform;
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

    private void TryBindInputController()
    {
        var currentInput = RhythmInputController.Instance;
        if (_inputController == currentInput)
            return;

        UnbindInputController();
        _inputController = currentInput;
        if (_inputController == null)
            return;

        _inputController.CombatInputAccepted += OnCombatInputAccepted;
        _inputController.CombatInputMissed += OnCombatInputMissed;
        _inputController.SkillSlotInputAccepted += OnSkillSlotInputAccepted;
    }

    private void UnbindInputController()
    {
        if (_inputController == null)
            return;

        _inputController.CombatInputAccepted -= OnCombatInputAccepted;
        _inputController.CombatInputMissed -= OnCombatInputMissed;
        _inputController.SkillSlotInputAccepted -= OnSkillSlotInputAccepted;
        _inputController = null;
    }

    private void OnCombatInputAccepted(long inputBeat)
    {
        _lastComboInputBeat = inputBeat;
        _comboCount++;
        _comboView?.SetCombo(_comboCount);
        _beatGuide?.NotifyInputAccepted(inputBeat);
    }

    private void OnCombatInputMissed()
    {
        HandleCombatInputMissed(true);
    }

    private void HandleCombatInputMissed(bool revealBeatGuide)
    {
        _lastComboInputBeat = long.MinValue;
        _comboCount = 0;
        _comboView?.ResetCombo();

        if (revealBeatGuide)
            _beatGuide?.NotifyInputMissed();
    }

    private void ResetIdleComboIfNeeded()
    {
        if (_comboCount <= 0 || _lastComboInputBeat == long.MinValue || RhythmClient.Instance == null)
            return;

        long currentBeat = RhythmClient.Instance.GetCurrentBeatIndex();
        if (currentBeat < _lastComboInputBeat)
            return;

        if (currentBeat - _lastComboInputBeat >= ComboIdleResetBeats)
            HandleCombatInputMissed(true);
    }

    public void BreakCombo()
    {
        HandleCombatInputMissed(true);
    }

    private void OnSkillSlotInputAccepted(int slotIndex, string skillId)
    {
        OnSkillUsed(slotIndex, ResolveVisualCooldownSeconds(skillId));
    }

    private float ResolveVisualCooldownSeconds(string skillId)
    {
        float beatSeconds = RhythmClient.Instance != null
            ? Mathf.Max(0.1f, (float)RhythmClient.Instance.GetBeatDurationMs() / 1000f)
            : 0.5f;

        var skillDefinition = P2PCombatContentCache.GetSkillDefinition(skillId);
        if (skillDefinition == null || skillDefinition.TotalDurationTicks <= 0)
            return beatSeconds;

        return Mathf.Max(0.1f, skillDefinition.TotalDurationTicks / 480f * beatSeconds);
    }

    private void RefreshPartyPanels()
    {
        EnsureHudViews();
        if (_partyPanels == null || _partyPanels.Length == 0)
            return;

        var gs = ClientGameState.Instance;
        if (gs == null)
        {
            HideAllPartyPanels();
            return;
        }

        List<int> actorIds = CollectPartyActorIds(gs);
        for (int i = 0; i < _partyPanels.Length; i++)
        {
            var panel = _partyPanels[i];
            if (panel == null)
                continue;

            if (i >= actorIds.Count)
            {
                panel.HideMember();
                continue;
            }

            int actorId = actorIds[i];
            int hp = 0;
            int maxHp = 0;
            if (gs.TryGetEntity(actorId, out var info))
            {
                hp = info.Hp;
                maxHp = info.MaxHp;
            }

            panel.SetMember(
                ResolvePartyMemberName(gs, actorId),
                hp,
                maxHp,
                actorId == gs.MyActorId);
        }
    }

    private List<int> CollectPartyActorIds(ClientGameState gs)
    {
        var actorIds = new List<int>();
        var seen = new HashSet<int>();

        AddPartyActor(gs.MyActorId, actorIds, seen);

        if (gs.PlayerActorIds != null)
        {
            for (int i = 0; i < gs.PlayerActorIds.Length; i++)
                AddPartyActor(gs.PlayerActorIds[i], actorIds, seen);
        }

        foreach (var roster in gs.EnumeratePlayerRoster())
            AddPartyActor(roster.ActorId, actorIds, seen);

        foreach (var entity in gs.EnumerateEntities())
        {
            if (entity.EntityType == (int)EntityType.Player)
                AddPartyActor(entity.EntityId, actorIds, seen);
        }

        return actorIds;
    }

    private static void AddPartyActor(int actorId, List<int> actorIds, HashSet<int> seen)
    {
        if (actorId <= 0 || !seen.Add(actorId))
            return;

        actorIds.Add(actorId);
    }

    private static string ResolvePartyMemberName(ClientGameState gs, int actorId)
    {
        if (gs.TryGetPlayerUid(actorId, out var uid) && !string.IsNullOrWhiteSpace(uid))
            return TrimHudLabel(uid);

        if (actorId == gs.MyActorId
            && SessionContext.Instance != null
            && !string.IsNullOrWhiteSpace(SessionContext.Instance.Uid))
        {
            return TrimHudLabel(SessionContext.Instance.Uid);
        }

        return $"Player {actorId}";
    }

    private static string TrimHudLabel(string label)
    {
        string clean = label.Trim();
        return clean.Length <= 15 ? clean : $"{clean.Substring(0, 13)}..";
    }

    private void HideAllPartyPanels()
    {
        for (int i = 0; i < _partyPanels.Length; i++)
            _partyPanels[i]?.HideMember();
    }

    private void RefreshStagePanel()
    {
        EnsureHudViews();
        if (_stageInfo == null)
            return;

        string stageName = SessionContext.Instance != null ? SessionContext.Instance.MapId : "";
        if (string.IsNullOrWhiteSpace(stageName))
            stageName = SceneManager.GetActiveScene().name;

        _stageInfo.SetStage(FormatStageLabel(stageName), RhythmClient.Instance != null ? RhythmClient.Instance.Bpm : 0d);
    }

    private static string FormatStageLabel(string stageName)
    {
        if (string.IsNullOrWhiteSpace(stageName))
            return "InGame";

        return stageName.Trim().Replace("_", " ");
    }

    public void OnSkillUsed(int slotIndex, float cooldownSec)
    {
        if (_skillSlots == null) return;
        if ((uint)slotIndex >= (uint)_skillSlots.Length) return;

        _skillSlots[slotIndex]?.StartCooldown(cooldownSec);
    }

    private void OnMyEntityChanged(ClientEntityInfo me)
    {
        if (_view == null) return;

        int maxHp = me.MaxHp > 0 ? me.MaxHp : Mathf.Max(1, me.Hp);
        int hp = me.Hp;
        float hpRate = (maxHp <= 0) ? 0f : (float)hp / maxHp;

        // HP_UI already carries the blue fill color. A red tint turns that cyan sprite nearly black.
        Color hpColor = Color.white;
        if (_config != null && hpRate <= _config.hpDangerRate)
            hpColor = _config.hpDangerColor;

        _view.SetHp(hp, maxHp, hpColor);
    }
}
