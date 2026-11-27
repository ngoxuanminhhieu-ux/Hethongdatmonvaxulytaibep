using Microsoft.AspNetCore.Mvc;
using AppointmentApi.Services;

namespace AppointmentApi.Controllers;

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
    /// Kiểm tra trạng thái kết nối tới RabbitMQ Broker.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        var isConnected = _connection.IsConnected;
        if (!isConnected)
        {
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
                : "Không thể kết nối tới RabbitMQ Broker."
        });
    }

    /// <summary>
    /// Tra cứu thông tin Topology RabbitMQ được cấu hình trong hệ thống (Exchanges, Queues, DLQ).
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
                description = "Topic Exchange chính phân phối sự kiện đặt lịch và hàng đợi"
            },
            deadLetterExchange = new
            {
                name = RabbitMqTopologyInitializer.DlxExchange,
                type = "direct",
                durable = true,
                description = "Dead Letter Exchange (DLX) hứng các thông điệp lỗi cấp số"
            },
            queues = new object[]
            {
                new
                {
                    name = RabbitMqTopologyInitializer.QueueProcessQueue,
                    durable = true,
                    bindingKey = RabbitMqTopologyInitializer.AppointmentRequestedRoutingKey,
                    deadLetterExchange = (string?)RabbitMqTopologyInitializer.DlxExchange,
                    deadLetterRoutingKey = (string?)RabbitMqTopologyInitializer.DeadLetterRoutingKey,
                    consumer = "Queue Worker (.NET Background Service - Cấp số thứ tự)"
                },
                new
                {
                    name = RabbitMqTopologyInitializer.QueueNotificationQueue,
                    durable = true,
                    bindingKey = "queue.* & appointment.*",
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
                "Dead Letter Queue (Hứng các message lỗi cấp số)"
            }
        });
    }

    /// <summary>
    /// Gửi thông điệp kiểm thử (Test Message) vào RabbitMQ để kiểm chứng trên Web Management.
    /// </summary>
    [HttpPost("test-publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult PublishTestMessage([FromBody] TestPublishRequest? request)
    {
        var routingKey = string.IsNullOrWhiteSpace(request?.RoutingKey) ? "appointment.requested" : request.RoutingKey.Trim();
        var payload = request?.Payload ?? new
        {
            testId = Guid.NewGuid(),
            timestamp = DateTime.UtcNow,
            message = "Thông điệp kiểm chứng RabbitMQ - Đề tài 09 Đặt lịch khám và Hàng đợi",
            doctor = "BS. CKII Nguyễn Văn Hùng",
            patient = "Nguyễn Văn An"
        };

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
    public string? RoutingKey { get; set; } = "appointment.requested";
    public object? Payload { get; set; }
}
