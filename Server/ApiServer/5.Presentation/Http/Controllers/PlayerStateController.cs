using ApiServer.Application.Services;
using ApiServer.Application.Ports;
using ApiServer.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers.Game;

[ApiController]
[Route("api/game/player-state")]
public class PlayerStateController : ControllerBase
{
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
        var equippedItems = items.Where(x => x.IsEquipped).ToList();
        var user = await _userRepository.FindByUidAsync(uid, ct);

        var response = new PlayerStateResponse
        {
            BaseHp = 10000,
            BaseAtk = 0,
            BaseDef = 0,
            TotalHp = 10000,
            TotalAtk = 0,
            TotalDef = 0,
            SavedAppearanceId = user?.AppearanceId ?? 0,
            AppearanceId = user?.AppearanceId ?? 0,
            Gears = new List<EquippedGearDto>()
        };

        // 슬롯 배치 정책
        //   ActiveSkillSlots[0] = 무기 skill_id          (H키 / 서버 Slot=0)
        //   ActiveSkillSlots[1] = 비무기 첫 번째 skill_id (J키 / 서버 Slot=1)
        //   ActiveSkillSlots[2] = 비무기 두 번째 skill_id (K키 / 서버 Slot=2)
        //   ActiveSkillSlots[3] = 비무기 세 번째 skill_id (L키 / 서버 Slot=3)
        //   NormalAttackSkillId = 무기 normal_attack_skill_id (Space키)

        int nonWeaponSlotIndex = 1;
        bool appearanceOverridden = response.AppearanceId > 0;
        int effectiveAppearanceId = response.AppearanceId;

        // 무기 우선 처리
        foreach (var item in equippedItems)
        {
            var tmpl = _templateService.GetEquipment(item.TemplateId);
            if (tmpl == null) continue;
            if (!string.Equals(tmpl.SlotType, "Weapon", StringComparison.OrdinalIgnoreCase)) continue;

            response.TotalHp  += tmpl.Hp;
            response.TotalAtk += tmpl.Atk;
            response.TotalDef += tmpl.Def;

            response.Gears.Add(new EquippedGearDto
            {
                TemplateId = tmpl.Id,
                SlotType   = 0,
                SkillId    = tmpl.SkillId,
                BonusHp    = tmpl.Hp,
                BonusAtk   = tmpl.Atk,
                BonusDef   = tmpl.Def
            });

            response.ActiveSkillSlots[0] = tmpl.SkillId ?? "";

            if (!string.IsNullOrEmpty(tmpl.NormalAttackSkillId))
                response.NormalAttackSkillId = tmpl.NormalAttackSkillId;
            else if (!string.IsNullOrEmpty(tmpl.SkillId))
                response.NormalAttackSkillId = tmpl.SkillId;

            if (!appearanceOverridden && tmpl.AppearanceId > 0)
                effectiveAppearanceId = tmpl.AppearanceId;
        }

        // 비무기 장비: Slot 1, 2, 3 순서대로
        foreach (var item in equippedItems)
        {
            var tmpl = _templateService.GetEquipment(item.TemplateId);
            if (tmpl == null) continue;
            if (string.Equals(tmpl.SlotType, "Weapon", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrEmpty(tmpl.SkillId)) continue;
            if (nonWeaponSlotIndex >= 4) break;

            response.TotalHp  += tmpl.Hp;
            response.TotalAtk += tmpl.Atk;
            response.TotalDef += tmpl.Def;

            response.Gears.Add(new EquippedGearDto
            {
                TemplateId = tmpl.Id,
                SlotType   = nonWeaponSlotIndex,
                SkillId    = tmpl.SkillId,
                BonusHp    = tmpl.Hp,
                BonusAtk   = tmpl.Atk,
                BonusDef   = tmpl.Def
            });

            response.ActiveSkillSlots[nonWeaponSlotIndex] = tmpl.SkillId;
            nonWeaponSlotIndex++;
        }

        // skill_id 없는 비무기 장비는 스탯만 적용
        foreach (var item in equippedItems)
        {
            var tmpl = _templateService.GetEquipment(item.TemplateId);
            if (tmpl == null) continue;
            if (string.Equals(tmpl.SlotType, "Weapon", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrEmpty(tmpl.SkillId)) continue;

            response.TotalHp  += tmpl.Hp;
            response.TotalAtk += tmpl.Atk;
            response.TotalDef += tmpl.Def;

            response.Gears.Add(new EquippedGearDto
            {
                TemplateId = tmpl.Id,
                SlotType   = -1,
                SkillId    = "",
                BonusHp    = tmpl.Hp,
                BonusAtk   = tmpl.Atk,
                BonusDef   = tmpl.Def
            });
        }

        response.AppearanceId = effectiveAppearanceId;

        _logger.LogInformation(
            "[PlayerState] uid={Uid} SavedAppearanceId={SavedAppearanceId} AppearanceId={AppearanceId} TotalHp={Hp} TotalAtk={Atk}",
            uid, response.SavedAppearanceId, response.AppearanceId, response.TotalHp, response.TotalAtk);

        return response;
    }

    private static bool IsAllowedAppearanceId(int appearanceId)
    {
        return appearanceId is 0 or 10 or 11 or 12;
    }
}

public sealed class SetAppearanceRequest
{
    public int AppearanceId { get; set; }
}

public class PlayerStateResponse
{
    public int BaseHp { get; set; } = 10;
    public int BaseAtk { get; set; } = 0;
    public int BaseDef { get; set; } = 0;
    public int TotalHp { get; set; } = 10;
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
