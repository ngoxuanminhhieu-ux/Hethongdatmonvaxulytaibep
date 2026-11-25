using Shared.Contracts;

namespace OrderApi.Services;

public interface IRabbitMqProducer
{
    bool PublishOrderCreated(OrderCreatedEvent orderEvent);
    bool PublishOrderStatusChanged(OrderStatusChangedEvent statusEvent);
    bool PublishTestMessage(string routingKey, object payload);
    bool IsBrokerConnected { get; }
}
