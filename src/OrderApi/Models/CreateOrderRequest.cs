using System.ComponentModel.DataAnnotations;

namespace OrderApi.Models;

public class CreateOrderRequest
{
    [Required(ErrorMessage = "Tên khách hàng không được để trống")]
    [MaxLength(100, ErrorMessage = "Tên khách hàng tối đa 100 ký tự")]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
    [Phone(ErrorMessage = "Số điện thoại không đúng định dạng")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Số bàn không được để trống")]
    [MaxLength(20, ErrorMessage = "Số bàn tối đa 20 ký tự")]
    public string TableNumber { get; set; } = string.Empty;

    public string? Notes { get; set; }

    [Required(ErrorMessage = "Đơn hàng phải có ít nhất 1 món")]
    [MinLength(1, ErrorMessage = "Đơn hàng phải có ít nhất 1 món")]
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    [Required(ErrorMessage = "Mã món ăn (menuItemId) là bắt buộc")]
    [Range(1, int.MaxValue, ErrorMessage = "Mã món ăn không hợp lệ")]
    public int MenuItemId { get; set; }

    [Required(ErrorMessage = "Số lượng là bắt buộc")]
    [Range(1, 100, ErrorMessage = "Số lượng món đặt phải từ 1 đến 100")]
    public int Quantity { get; set; }

    [MaxLength(255, ErrorMessage = "Ghi chú món tối đa 255 ký tự")]
    public string? Notes { get; set; }
}

public class UpdateOrderStatusRequest
{
    [Required(ErrorMessage = "Trạng thái mới không được để trống")]
    public string Status { get; set; } = string.Empty;

    [Required(ErrorMessage = "Người cập nhật không được để trống")]
    [MaxLength(50)]
    public string ChangedBy { get; set; } = string.Empty;

    public string? Notes { get; set; }
}

public class CancelOrderRequest
{
    [Required(ErrorMessage = "Lý do hủy đơn không được để trống")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "Người thực hiện không được để trống")]
    public string CancelledBy { get; set; } = string.Empty;
}

public class OrderQueryParameters
{
    public string? Status { get; set; }
    public string? TableNumber { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } = "createdAt";
    public string? SortOrder { get; set; } = "desc";
}
