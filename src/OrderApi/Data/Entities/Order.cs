using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Enums;

namespace OrderApi.Data.Entities;

[Table("orders")]
public class Order
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("customer_id")]
    public int? CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("table_number")]
    public string TableNumber { get; set; } = string.Empty;

    [Column("total_amount", TypeName = "numeric(12,2)")]
    public decimal TotalAmount { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = OrderStatus.Pending.ToString();

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<OrderDetail> OrderDetails { get; set; } = new();
    public List<OrderStatusHistory> StatusHistories { get; set; } = new();
}
