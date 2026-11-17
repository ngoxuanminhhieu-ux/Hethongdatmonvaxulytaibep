using Shared.Enums;

namespace Shared.Contracts;

public class OrderCreatedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; }
    public string EventType { get; set; } = nameof(OrderCreatedEvent);
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string SourceService { get; set; } = "OrderApi";
    public OrderCreatedData Data { get; set; } = new();
}

public class OrderCreatedData
{
    public Guid OrderId { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string TableNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<OrderItemData> Items { get; set; } = new();
}

public class OrderItemData
{
    public int MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal => UnitPrice * Quantity;
    public string? Notes { get; set; }
}
