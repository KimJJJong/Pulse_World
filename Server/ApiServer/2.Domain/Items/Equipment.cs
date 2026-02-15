using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiServer.Domain.Items;

[Table("equipments")]
public class Equipment
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [Column("owner_uid")]
    [MaxLength(50)]
    public string OwnerUid { get; set; } = string.Empty;

    [Column("template_id")]
    public int TemplateId { get; set; }

    [Column("slot_index")]
    public int SlotIndex { get; set; }

    [Column("enhancement_level")]
    public int EnhancementLevel { get; set; }

    [Column("base_stats", TypeName = "jsonb")]
    public string BaseStats { get; set; } = "{}";

    [Column("random_options", TypeName = "jsonb")]
    public string RandomOptions { get; set; } = "{}";

    [Column("is_equipped")]
    public bool IsEquipped { get; set; }

    [Column("acquired_at")]
    public DateTimeOffset AcquiredAt { get; set; } = DateTimeOffset.UtcNow;
}
