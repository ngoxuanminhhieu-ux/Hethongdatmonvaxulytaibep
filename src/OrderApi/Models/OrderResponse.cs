namespace OrderApi.Models;

public class OrderResponse
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<OrderDetailResponse> Items { get; set; } = new();
    public List<OrderStatusHistoryResponse> StatusHistory { get; set; } = new();
}

public class OrderDetailResponse
{
    public int MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
    public string? Notes { get; set; }
}

public class OrderStatusHistoryResponse
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime Timestamp { get; set; }
}
