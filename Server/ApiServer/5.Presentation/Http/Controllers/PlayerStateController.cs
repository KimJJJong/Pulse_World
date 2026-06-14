using ApiServer.Application.Services;
using ApiServer.Application.Ports;
using ApiServer.Domain.Items;
using ApiServer.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers.Game;

[ApiController]
[Route("api/game/player-state")]
public class PlayerStateController : ControllerBase
{
    private const int BaseCombatHp = 140;
    private const int ActiveSkillSlotCount = 4;
    private static readonly string[] EquipmentSlotOrder = { "Weapon", "Head", "Armor", "Shoes" };

    private readonly IInventoryRepository _inventoryRepository;
    private readonly IItemTemplateService _templateService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<PlayerStateController> _logger;

    public PlayerStateController(
        IInventoryRepository inventoryRepository,
        IItemTemplateService templateService,
        IUserRepository userRepository,
        ILogger<PlayerStateController> logger)
    {
        _inventoryRepository = inventoryRepository;
        _templateService = templateService;
        _userRepository = userRepository;
        _logger = logger;
    }

    [HttpGet("{uid}")]
    public async Task<IActionResult> GetPlayerState(string uid, CancellationToken ct)
    {
        var response = await BuildPlayerStateAsync(uid, ct);
        return Ok(response);
    }

    [HttpPost("{uid}/appearance")]
    public async Task<IActionResult> SetAppearance(string uid, [FromBody] SetAppearanceRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest("Invalid payload");

        if (!IsAllowedAppearanceId(request.AppearanceId))
            return BadRequest($"Unsupported appearance id: {request.AppearanceId}");

        var user = await _userRepository.FindByUidAsync(uid, ct);
        if (user == null)
            return NotFound();

        await _userRepository.UpdateAppearanceIdAsync(uid, request.AppearanceId, ct);

        var response = await BuildPlayerStateAsync(uid, ct);
        _logger.LogInformation("[PlayerState] appearance updated uid={Uid} AppearanceId={AppearanceId}", uid, request.AppearanceId);
        return Ok(response);
    }

    private async Task<PlayerStateResponse> BuildPlayerStateAsync(string uid, CancellationToken ct)
    {
        var items = await _inventoryRepository.GetInventoryAsync(uid);
        var equippedBySlot = ResolveEquippedItems(items.Where(x => x.IsEquipped));
        var user = await _userRepository.FindByUidAsync(uid, ct);

        var response = new PlayerStateResponse
        {
            BaseHp = BaseCombatHp,
            BaseAtk = 0,
            BaseDef = 0,
            TotalHp = BaseCombatHp,
            TotalAtk = 0,
            TotalDef = 0,
            SavedAppearanceId = user?.AppearanceId ?? 0,
            AppearanceId = user?.AppearanceId ?? 0,
            Gears = new List<EquippedGearDto>()
        };

        bool appearanceOverridden = response.AppearanceId > 0;
        int effectiveAppearanceId = response.AppearanceId;

        foreach (string slot in EquipmentSlotOrder)
        {
            if (!equippedBySlot.TryGetValue(slot, out var equipped))
                continue;

            var tmpl = equipped.Template;
            int skillSlot = GetActiveSkillSlotIndex(slot);
            response.TotalHp  += tmpl.Hp;
            response.TotalAtk += tmpl.Atk;
            response.TotalDef += tmpl.Def;

            response.Gears.Add(new EquippedGearDto
            {
                TemplateId = tmpl.Id,
                SlotType   = skillSlot,
                SkillId    = tmpl.SkillId ?? "",
                BonusHp    = tmpl.Hp,
                BonusAtk   = tmpl.Atk,
                BonusDef   = tmpl.Def
            });

            if (slot == "Weapon")
            {
                response.ActiveSkillSlots[0] = tmpl.SkillId ?? "";

                if (!string.IsNullOrEmpty(tmpl.NormalAttackSkillId))
                    response.NormalAttackSkillId = tmpl.NormalAttackSkillId;
                else if (!string.IsNullOrEmpty(tmpl.SkillId))
                    response.NormalAttackSkillId = tmpl.SkillId;

                if (!appearanceOverridden && tmpl.AppearanceId > 0)
                    effectiveAppearanceId = tmpl.AppearanceId;
            }
            else if (skillSlot > 0 && skillSlot < ActiveSkillSlotCount && !string.IsNullOrEmpty(tmpl.SkillId))
            {
                response.ActiveSkillSlots[skillSlot] = tmpl.SkillId;
            }
        }

        response.AppearanceId = effectiveAppearanceId;

        _logger.LogInformation(
            "[PlayerState] uid={Uid} SavedAppearanceId={SavedAppearanceId} AppearanceId={AppearanceId} TotalHp={Hp} TotalAtk={Atk}",
            uid, response.SavedAppearanceId, response.AppearanceId, response.TotalHp, response.TotalAtk);

        return response;
    }

    private Dictionary<string, EquippedItemInfo> ResolveEquippedItems(IEnumerable<GameItem> equippedItems)
    {
        var result = new Dictionary<string, EquippedItemInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in equippedItems)
        {
            var tmpl = _templateService.GetEquipment(item.TemplateId);
            if (tmpl == null)
                continue;

            string slot = NormalizeSlot(tmpl.SlotType);
            if (GetActiveSkillSlotIndex(slot) < 0)
                continue;

            if (!result.TryGetValue(slot, out var current) || item.Id > current.Item.Id)
                result[slot] = new EquippedItemInfo(item, tmpl);
        }

        return result;
    }

    private static string NormalizeSlot(string slot)
    {
        foreach (string allowedSlot in EquipmentSlotOrder)
        {
            if (string.Equals(slot, allowedSlot, StringComparison.OrdinalIgnoreCase))
                return allowedSlot;
        }

        return string.Empty;
    }

    private static int GetActiveSkillSlotIndex(string slot)
    {
        return slot switch
        {
            "Weapon" => 0,
            "Head" => 1,
            "Armor" => 2,
            "Shoes" => 3,
            _ => -1
        };
    }

    private static bool IsAllowedAppearanceId(int appearanceId)
    {
        return appearanceId is 0 or 10 or 11 or 12;
    }

    private sealed class EquippedItemInfo
    {
        public EquippedItemInfo(GameItem item, EquipmentTemplateDto template)
        {
            Item = item;
            Template = template;
        }

        public GameItem Item { get; }
        public EquipmentTemplateDto Template { get; }
    }
}

public sealed class SetAppearanceRequest
{
    public int AppearanceId { get; set; }
}

public class PlayerStateResponse
{
    public int BaseHp { get; set; } = 140;
    public int BaseAtk { get; set; } = 0;
    public int BaseDef { get; set; } = 0;
    public int TotalHp { get; set; } = 140;
    public int TotalAtk { get; set; } = 0;
    public int TotalDef { get; set; } = 0;

    public List<EquippedGearDto> Gears { get; set; } = new();

    public string NormalAttackSkillId { get; set; } = "Attack";
    public string[] ActiveSkillSlots { get; set; } = new string[4] { "", "", "", "" };

    /// <summary>
    /// 저장된 캐릭터 외견 ID.
    /// 0 = Auto/Default, 10 = Barbarian, 11 = Mage
    /// </summary>
    public int SavedAppearanceId { get; set; } = 0;

    /// <summary>
    /// 실제 적용되는 외견 ID.
    /// 0이면 장비 기반 자동 적용이 아니라, 현재는 저장된 값 또는 장비 대체값이 반환됩니다.
    /// </summary>
    public int AppearanceId { get; set; } = 0;
}

public class EquippedGearDto
{
    public int TemplateId { get; set; }
    public int SlotType { get; set; }
    public string SkillId { get; set; } = "";
    public int BonusHp { get; set; }
    public int BonusAtk { get; set; }
    public int BonusDef { get; set; }
}
