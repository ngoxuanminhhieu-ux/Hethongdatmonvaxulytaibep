using Shared.Contracts;

namespace AppointmentApi.Services;

public interface IRabbitMqProducer
{
    bool PublishAppointmentRequested(AppointmentRequestedEvent ev);
    bool PublishQueueServingChanged(QueueServingChangedEvent ev);
    bool PublishTestMessage(string routingKey, object payload);
    bool IsBrokerConnected { get; }
}
