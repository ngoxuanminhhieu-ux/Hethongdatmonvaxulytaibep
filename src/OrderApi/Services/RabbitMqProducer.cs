using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared.Contracts;

namespace OrderApi.Services;

public class RabbitMqProducer : IRabbitMqProducer
{
    private readonly IRabbitMqPersistentConnection _persistentConnection;
    private readonly ILogger<RabbitMqProducer> _logger;
    private const string ExchangeName = "food.order.exchange";
    private readonly int _maxPublishRetries = 3;

    public RabbitMqProducer(
        IRabbitMqPersistentConnection persistentConnection,
        ILogger<RabbitMqProducer> logger)
    {
        _persistentConnection = persistentConnection;
        _logger = logger;
    }

    public bool IsBrokerConnected => _persistentConnection.IsConnected;

    public bool PublishOrderCreated(OrderCreatedEvent orderEvent)
    {
        return PublishWithResilience(
            orderEvent,
            routingKey: "order.created",
            eventName: nameof(OrderCreatedEvent),
            correlationId: orderEvent.CorrelationId.ToString());
    }

    public bool PublishOrderStatusChanged(OrderStatusChangedEvent statusEvent)
    {
        return PublishWithResilience(
            statusEvent,
            routingKey: "order.status.changed",
            eventName: nameof(OrderStatusChangedEvent),
            correlationId: statusEvent.CorrelationId.ToString());
    }

    public bool PublishTestMessage(string routingKey, object payload)
    {
        var correlationId = Guid.NewGuid().ToString();
        return PublishWithResilience(
            payload,
            routingKey: routingKey,
            eventName: "DiagnosticTestEvent",
            correlationId: correlationId);
    }

    private bool PublishWithResilience<T>(T message, string routingKey, string eventName, string correlationId)
    {
        if (!_persistentConnection.IsConnected)
        {
            _persistentConnection.TryConnect();
        }

        if (!_persistentConnection.IsConnected)
        {
            _logger.LogWarning("Không thể gửi sự kiện {EventName} vì chưa kết nối được tới RabbitMQ broker.", eventName);
            return false;
        }

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        var body = Encoding.UTF8.GetBytes(json);

        for (var attempt = 1; attempt <= _maxPublishRetries; attempt++)
        {
            try
            {
                using var channel = _persistentConnection.CreateModel();

                // 1. Kích hoạt chế độ Publisher Confirms
                channel.ConfirmSelect();

                // Đảm bảo Exchange tồn tại
                channel.ExchangeDeclare(
                    exchange: ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                // 2. Thiết lập Message Properties đảm bảo Message Durability
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true; // DeliveryMode = 2 (Lưu an toàn xuống ổ đĩa broker)
                properties.DeliveryMode = 2;
                properties.ContentType = "application/json";
                properties.ContentEncoding = "utf-8";
                properties.Type = eventName;
                properties.MessageId = Guid.NewGuid().ToString();
                properties.CorrelationId = correlationId;
                properties.AppId = "OrderApi";
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                properties.Headers = new Dictionary<string, object>
                {
                    { "x-source-service", "OrderApi" },
                    { "x-publish-attempt", attempt },
                    { "x-client-timestamp", DateTime.UtcNow.ToString("o") }
                };

                // 3. Publish message vào Topic Exchange
                channel.BasicPublish(
                    exchange: ExchangeName,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                // 4. Chờ xác nhận từ broker (Publisher Confirms)
                var confirmed = channel.WaitForConfirms(TimeSpan.FromSeconds(5), out var timedOut);

                if (confirmed && !timedOut)
                {
                    _logger.LogInformation(">>> [RABBITMQ CONFIRMED] Broker đã xác nhận lưu an toàn sự kiện {EventName} [CorrelationId: {CorrId}] vào Exchange '{Exchange}' với RoutingKey '{Key}'",
                        eventName, correlationId, ExchangeName, routingKey);
                    return true;
                }

                _logger.LogWarning("Publisher Confirm không nhận được phản hồi xác nhận (TimedOut: {TimedOut}) cho sự kiện {EventName} ở lần thử {Attempt}.",
                    timedOut, eventName, attempt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi xảy ra khi publish sự kiện {EventName} lần {Attempt}/{Max}. Đang thử lại...",
                    eventName, attempt, _maxPublishRetries);

                if (attempt < _maxPublishRetries)
                {
                    Thread.Sleep(500 * attempt);
                }
            }
        }

        _logger.LogError("Publish sự kiện {EventName} thất bại sau {Max} lần thử.", eventName, _maxPublishRetries);
        return false;
    }
}
