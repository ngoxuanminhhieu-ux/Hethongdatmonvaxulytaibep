using Microsoft.AspNetCore.Mvc;
using OrderApi.Services;

namespace OrderApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RabbitMqDiagnosticsController : ControllerBase
{
    private readonly IRabbitMqPersistentConnection _connection;
    private readonly IRabbitMqProducer _producer;
    private readonly ILogger<RabbitMqDiagnosticsController> _logger;

    public RabbitMqDiagnosticsController(
        IRabbitMqPersistentConnection connection,
        IRabbitMqProducer producer,
        ILogger<RabbitMqDiagnosticsController> logger)
    {
        _connection = connection;
        _producer = producer;
        _logger = logger;
    }

    /// <summary>
    /// Kiểm tra trạng thái kết nối tới cụm Message Broker RabbitMQ.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        var isConnected = _connection.IsConnected;
        if (!isConnected)
        {
            // Thử kết nối lại
            isConnected = _connection.TryConnect();
        }

        return Ok(new
        {
            status = isConnected ? "Healthy" : "Unhealthy",
            brokerHost = _connection.HostName,
            brokerPort = _connection.Port,
            virtualHost = _connection.VirtualHost,
            isConnected = isConnected,
            checkedAt = DateTime.UtcNow,
            message = isConnected
                ? "Kết nối RabbitMQ đang hoạt động ổn định với chế độ Persistent Connection."
                : "Không thể kết nối tới RabbitMQ Broker. Kiểm tra xem container rabbitmq đã khởi động chưa."
        });
    }

    /// <summary>
    /// Tra cứu thông tin Topology RabbitMQ được cấu hình trong hệ thống (Exchanges, Queues, DLQ, Routing Keys).
    /// </summary>
    [HttpGet("topology")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetTopology()
    {
        return Ok(new
        {
            mainExchange = new
            {
                name = RabbitMqTopologyInitializer.MainExchange,
                type = "topic",
                durable = true,
                description = "Topic Exchange chính phân phối sự kiện đơn hàng"
            },
            deadLetterExchange = new
            {
                name = RabbitMqTopologyInitializer.DlxExchange,
                type = "direct",
                durable = true,
                description = "Dead Letter Exchange (DLX) hứng các thông điệp lỗi"
            },
            queues = new object[]
            {
                new
                {
                    name = RabbitMqTopologyInitializer.KitchenQueue,
                    durable = true,
                    bindingKey = RabbitMqTopologyInitializer.OrderCreatedRoutingKey,
                    deadLetterExchange = (string?)RabbitMqTopologyInitializer.DlxExchange,
                    deadLetterRoutingKey = (string?)RabbitMqTopologyInitializer.DeadLetterRoutingKey,
                    consumer = "Kitchen Worker (.NET Background Service)"
                },
                new
                {
                    name = RabbitMqTopologyInitializer.NotificationQueue,
                    durable = true,
                    bindingKey = RabbitMqTopologyInitializer.AllOrderEventsRoutingKey,
                    deadLetterExchange = (string?)null,
                    deadLetterRoutingKey = (string?)null,
                    consumer = "Notification Service (SignalR Realtime Hub)"
                },
                new
                {
                    name = RabbitMqTopologyInitializer.DeadLetterQueue,
                    durable = true,
                    bindingKey = RabbitMqTopologyInitializer.DeadLetterRoutingKey,
                    deadLetterExchange = (string?)null,
                    deadLetterRoutingKey = (string?)null,
                    consumer = "Administrator / DLQ Inspector"
                }
            },
            features = new[]
            {
                "Publisher Confirms (Xác nhận an toàn từ Broker)",
                "Message Durability (DeliveryMode = 2 - ghi xuống đĩa)",
                "Resilient Singleton Connection (Tự động kết nối lại khi rớt mạng)",
                "Exponential Backoff Retries (Thử lại khi có sự cố tạm thời)",
                "Dead Letter Queue (Hứng các message lỗi xử lý)"
            }
        });
    }

    /// <summary>
    /// Gửi thông điệp kiểm thử (Test Message) vào RabbitMQ để kiểm chứng trực tiếp trên Web Management.
    /// </summary>
    [HttpPost("test-publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult PublishTestMessage([FromBody] TestPublishRequest? request)
    {
        var routingKey = string.IsNullOrWhiteSpace(request?.RoutingKey) ? "order.created" : request.RoutingKey.Trim();
        var payload = request?.Payload ?? new
        {
            testId = Guid.NewGuid(),
            timestamp = DateTime.UtcNow,
            message = "Thông điệp kiểm chứng RabbitMQ Producer - Tuần 4",
            sampleItem = "Phở Bò Tái Nạm",
            tableNumber = "Bàn VIP 01"
        };

        _logger.LogInformation("Gửi thông điệp kiểm thử vào RabbitMQ với RoutingKey: {Key}", routingKey);

        var success = _producer.PublishTestMessage(routingKey, payload);

        if (!success)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                success = false,
                message = "Không thể publish thông điệp vào RabbitMQ. Hãy đảm bảo container RabbitMQ đang chạy."
            });
        }

        return Ok(new
        {
            success = true,
            routingKey,
            publishedAt = DateTime.UtcNow,
            exchange = RabbitMqTopologyInitializer.MainExchange,
            message = "Đã publish thành công với Publisher Confirms và DeliveryMode=2 (Persistent). Hãy mở http://localhost:15672 để kiểm tra queue!",
            payload
        });
    }
}

public class TestPublishRequest
{
    public string? RoutingKey { get; set; } = "order.created";
    public object? Payload { get; set; }
}
