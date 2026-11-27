using RabbitMQ.Client;

namespace AppointmentApi.Services;

public interface IRabbitMqPersistentConnection : IDisposable
{
    bool IsConnected { get; }
    bool TryConnect();
    IModel CreateModel();
    string HostName { get; }
    int Port { get; }
    string VirtualHost { get; }
}
