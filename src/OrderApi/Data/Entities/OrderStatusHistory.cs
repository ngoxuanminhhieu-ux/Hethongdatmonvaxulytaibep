using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderApi.Data.Entities;

[Table("order_status_histories")]
public class OrderStatusHistory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("order_id")]
    public Guid OrderId { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }

    [MaxLength(20)]
    [Column("from_status")]
    public string? FromStatus { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("to_status")]
    public string ToStatus { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("changed_by")]
    public string ChangedBy { get; set; } = string.Empty;

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
