using ApiServer.Application.Services;
using ApiServer.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ApiServer.Presentation.Http.Controllers.Game;

[ApiController]
[Route("api/game/player-state")]
public class PlayerStateController : ControllerBase
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IItemTemplateService _templateService;
    private readonly ILogger<PlayerStateController> _logger;

    public PlayerStateController(
        IInventoryRepository inventoryRepository,
        IItemTemplateService templateService,
        ILogger<PlayerStateController> logger)
    {
        _inventoryRepository = inventoryRepository;
        _templateService = templateService;
        _logger = logger;
    }

    [HttpGet("{uid}")]
    public async Task<IActionResult> GetPlayerState(string uid)
    {
        var items = await _inventoryRepository.GetInventoryAsync(uid);
        var equippedItems = items.Where(x => x.IsEquipped).ToList();

        var response = new PlayerStateResponse
        {
            BaseHp = 10000,
            BaseAtk = 0,
            BaseDef = 0,
            TotalHp = 10000,
            TotalAtk = 0,
            TotalDef = 0,
            Gears = new List<EquippedGearDto>()
        };

        // 슬롯 배치 정책 (클라이언트 HudPresenter 및 서버 BeatActionManager와 동기화)
        //   ActiveSkillSlots[0] = 무기 skill_id          (H키 / 서버 Slot=0)
        //   ActiveSkillSlots[1] = 비무기 첫 번째 skill_id (J키 / 서버 Slot=1)
        //   ActiveSkillSlots[2] = 비무기 두 번째 skill_id (K키 / 서버 Slot=2)
        //   ActiveSkillSlots[3] = 비무기 세 번째 skill_id (L키 / 서버 Slot=3)
        //   NormalAttackSkillId = 무기 normal_attack_skill_id (Space키)

        // 무기 먼저 처리, 나머지 비무기 장비는 순서대로
        int nonWeaponSlotIndex = 1; // 비무기 장비는 Slot 1부터

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

            // Slot 0 = 무기 skill_id
            response.ActiveSkillSlots[0] = tmpl.SkillId ?? "";

            // NormalAttack = normal_attack_skill_id (없으면 skill_id 폴백)
            if (!string.IsNullOrEmpty(tmpl.NormalAttackSkillId))
                response.NormalAttackSkillId = tmpl.NormalAttackSkillId;
            else if (!string.IsNullOrEmpty(tmpl.SkillId))
                response.NormalAttackSkillId = tmpl.SkillId;
        }

        // 비무기 장비: Slot 1, 2, 3 순서대로
        foreach (var item in equippedItems)
        {
            var tmpl = _templateService.GetEquipment(item.TemplateId);
            if (tmpl == null) continue;
            if (string.Equals(tmpl.SlotType, "Weapon", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrEmpty(tmpl.SkillId)) continue; // skill_id 없는 장비는 슬롯 차지 안 함
            if (nonWeaponSlotIndex >= 4) break; // 최대 4슬롯

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
            if (!string.IsNullOrEmpty(tmpl.SkillId)) continue; // 이미 위에서 처리됨

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

        return Ok(response);
    }
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

    public int AppearanceId { get; set; } = 0; // 향후 코스튬 등 처리
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
