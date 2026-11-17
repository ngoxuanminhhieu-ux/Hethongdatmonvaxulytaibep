using System.ComponentModel.DataAnnotations;

namespace OrderApi.Models;

public class CreateOrderRequest
{
    [Required(ErrorMessage = "Tên khách hàng không được để trống")]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Số bàn không được để trống")]
    [MaxLength(20)]
    public string TableNumber { get; set; } = string.Empty;

    public string? Notes { get; set; }

    [Required(ErrorMessage = "Đơn hàng phải có ít nhất 1 món")]
    [MinLength(1, ErrorMessage = "Đơn hàng phải có ít nhất 1 món")]
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    [Required]
    public int MenuItemId { get; set; }

    [Range(1, 100, ErrorMessage = "Số lượng phải từ 1 đến 100")]
    public int Quantity { get; set; }

    public string? Notes { get; set; }
}

public class UpdateOrderStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;

    [Required]
    public string ChangedBy { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
