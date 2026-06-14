using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiServer.Application.Services;

public interface IItemTemplateService
{
    ItemTemplateDto? GetItem(int id);
    EquipmentTemplateDto? GetEquipment(int id);
    IReadOnlyCollection<EquipmentTemplateDto> GetAllEquipments();
}

public class ItemTemplateService : IItemTemplateService
{
    private readonly string _basePath;
    private Dictionary<int, ItemTemplateDto> _items = new();
    private Dictionary<int, EquipmentTemplateDto> _equipments = new();
    private readonly ILogger<ItemTemplateService> _logger;

    public ItemTemplateService(IConfiguration config, ILogger<ItemTemplateService> logger)
    {
        _logger = logger;
        _basePath = config["ItemDataPath"] ?? "../GameServer/Content/Data/Json";
        LoadData();
    }

    private void LoadData()
    {
        try
        {
            // Load Items
            string itemPath = Path.Combine(_basePath, "Items.json");
            if (File.Exists(itemPath))
            {
                string json = File.ReadAllText(itemPath);
                var items = JsonSerializer.Deserialize<List<ItemTemplateDto>>(json);
                if (items != null)
                {
                    foreach (var i in items) _items[i.Id] = i;
                }
            }
            else
            {
                _logger.LogWarning($"Items.json not found at {Path.GetFullPath(itemPath)}");
            }

            // Load Equipments
            string equipPath = Path.Combine(_basePath, "Equipments.json");
            if (File.Exists(equipPath))
            {
                string json = File.ReadAllText(equipPath);
                var equips = JsonSerializer.Deserialize<List<EquipmentTemplateDto>>(json);
                if (equips != null)
                {
                    foreach (var e in equips) _equipments[e.Id] = e;
                }
            }
            else
            {
                _logger.LogWarning($"Equipments.json not found at {Path.GetFullPath(equipPath)}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load item data");
        }
    }

    public ItemTemplateDto? GetItem(int id) => _items.GetValueOrDefault(id);
    public EquipmentTemplateDto? GetEquipment(int id) => _equipments.GetValueOrDefault(id);
    public IReadOnlyCollection<EquipmentTemplateDto> GetAllEquipments() => _equipments.Values.ToList();
}

public class ItemTemplateDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("max_stack")] public int MaxStack { get; set; }
}

public class EquipmentTemplateDto : ItemTemplateDto
{
    [JsonPropertyName("equip_slot")] public string SlotType { get; set; } = string.Empty;
    [JsonPropertyName("base_atk")] public int Atk { get; set; }
    [JsonPropertyName("base_def")] public int Def { get; set; }
    [JsonPropertyName("base_hp")] public int Hp { get; set; }
    [JsonPropertyName("base_str")] public int Str { get; set; }
    [JsonPropertyName("base_dex")] public int Dex { get; set; }

    [JsonPropertyName("normal_attack_skill_id")] public string NormalAttackSkillId { get; set; } = string.Empty;
    [JsonPropertyName("skill_id")] public string SkillId { get; set; } = string.Empty;

    /// <summary>
    /// 이 무기를 장착했을 때 사용할 플레이어 캐릭터 외견 ID.
    /// Equipments.json의 appearance_id 필드와 매핑.
    /// 0 = 기본값(폴백)
    /// </summary>
    [JsonPropertyName("appearance_id")] public int AppearanceId { get; set; } = 0;
}
