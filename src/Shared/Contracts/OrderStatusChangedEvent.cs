using Shared.Enums;

namespace Shared.Contracts;

public class OrderStatusChangedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; }
    public string EventType { get; set; } = nameof(OrderStatusChangedEvent);
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string SourceService { get; set; } = "KitchenWorker";
    public OrderStatusChangedData Data { get; set; } = new();
}

public class OrderStatusChangedData
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string TableNumber { get; set; } = string.Empty;
    public OrderStatus PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
