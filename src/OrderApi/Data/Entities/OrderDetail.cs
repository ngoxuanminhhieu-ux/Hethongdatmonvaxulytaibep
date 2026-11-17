using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderApi.Data.Entities;

[Table("order_details")]
public class OrderDetail
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("order_id")]
    public Guid OrderId { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }

    [Required]
    [Column("menu_item_id")]
    public int MenuItemId { get; set; }

    [ForeignKey(nameof(MenuItemId))]
    public MenuItem? MenuItem { get; set; }

    [Column("unit_price", TypeName = "numeric(12,2)")]
    public decimal UnitPrice { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("subtotal", TypeName = "numeric(12,2)")]
    public decimal Subtotal { get; set; }

    [MaxLength(255)]
    [Column("item_notes")]
    public string? ItemNotes { get; set; }
}
