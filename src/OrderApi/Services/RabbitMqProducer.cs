using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared.Contracts;

namespace OrderApi.Services;

public interface IRabbitMqProducer
{
    void PublishOrderCreated(OrderCreatedEvent orderEvent);
    void PublishOrderStatusChanged(OrderStatusChangedEvent statusEvent);
}

public class RabbitMqProducer : IRabbitMqProducer
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqProducer> _logger;
    private readonly string _hostname;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private const string ExchangeName = "food.order.exchange";

    public RabbitMqProducer(IConfiguration configuration, ILogger<RabbitMqProducer> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _hostname = _configuration["RabbitMQ:Host"] ?? "localhost";
        _port = int.TryParse(_configuration["RabbitMQ:Port"], out var p) ? p : 5672;
        _username = _configuration["RabbitMQ:Username"] ?? "guest";
        _password = _configuration["RabbitMQ:Password"] ?? "guest";
    }

    public void PublishOrderCreated(OrderCreatedEvent orderEvent)
    {
        PublishMessage(orderEvent, "order.created", "OrderCreatedEvent");
    }

    public void PublishOrderStatusChanged(OrderStatusChangedEvent statusEvent)
    {
        PublishMessage(statusEvent, "order.status.changed", "OrderStatusChangedEvent");
    }

    private void PublishMessage<T>(T message, string routingKey, string eventName)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _hostname,
                Port = _port,
                UserName = _username,
                Password = _password,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(3)
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.ExchangeDeclare(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var body = Encoding.UTF8.GetBytes(json);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = eventName;
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: routingKey,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Đã publish thành công sự kiện {EventName} lên Exchange '{Exchange}' với RoutingKey '{Key}'",
                eventName, ExchangeName, routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kết nối RabbitMQ tại {Host}:{Port} để publish sự kiện {EventName}. Đơn hàng vẫn được lưu trong DB.",
                _hostname, _port, eventName);
        }
    }
}
