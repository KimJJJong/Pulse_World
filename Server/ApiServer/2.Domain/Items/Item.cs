using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiServer.Domain.Items;

[Table("items")]
public class Item
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

    [Column("amount")]
    public int Amount { get; set; }

    [Column("slot_index")]
    public int SlotIndex { get; set; }
}
