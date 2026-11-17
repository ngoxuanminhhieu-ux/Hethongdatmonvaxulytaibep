using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Hubs;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;

namespace NotificationService.Services;

public class RabbitMqNotificationConsumer : BackgroundService
{
    private readonly ILogger<RabbitMqNotificationConsumer> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHubContext<OrderNotificationHub> _hubContext;
    private IConnection? _connection;
    private IModel? _channel;

    private const string ExchangeName = "food.order.exchange";
    private const string QueueName = "order.notification.queue";

    public RabbitMqNotificationConsumer(
        ILogger<RabbitMqNotificationConsumer> logger,
        IConfiguration configuration,
        IHubContext<OrderNotificationHub> hubContext)
    {
        _logger = logger;
        _configuration = configuration;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Consumer đang khởi động...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                InitRabbitMQ();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Notification Service chưa kết nối được RabbitMQ: {Message}. Đang thử lại sau 5 giây...", ex.Message);
                await Task.Delay(5000, stoppingToken);
            }
        }

        if (_channel != null)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var routingKey = ea.RoutingKey;

                try
                {
                    _logger.LogInformation(">>> [NOTIFICATION SERVICE] Nhận sự kiện với routing key '{Key}'", routingKey);

                    using var doc = JsonDocument.Parse(message);
                    var root = doc.RootElement;

                    string orderId = string.Empty;

                    if (root.TryGetProperty("data", out var dataElement) &&
                        dataElement.TryGetProperty("orderId", out var idElement))
                    {
                        orderId = idElement.GetString() ?? string.Empty;
                    }

                    // 1. Gửi realtime thông báo cho đúng khách hàng đang theo dõi đơn đó
                    if (!string.IsNullOrEmpty(orderId))
                    {
                        var groupName = $"order-{orderId}";
                        await _hubContext.Clients.Group(groupName).SendAsync("ReceiveOrderStatusUpdate", new
                        {
                            routingKey,
                            rawMessage = message,
                            timestamp = DateTime.UtcNow
                        });

                        _logger.LogInformation("<<< Đã đẩy thông báo Realtime cho Group '{GroupName}'", groupName);
                    }

                    // 2. Gửi realtime cho màn hình bếp để cập nhật danh sách
                    await _hubContext.Clients.Group("kitchen-dashboard").SendAsync("ReceiveKitchenUpdate", new
                    {
                        routingKey,
                        rawMessage = message,
                        timestamp = DateTime.UtcNow
                    });

                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi phân phối thông báo realtime từ message.");
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("Notification Consumer đang lắng nghe hàng đợi '{QueueName}'...", QueueName);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private void InitRabbitMQ()
    {
        var hostname = _configuration["RabbitMQ:Host"] ?? "localhost";
        var port = int.TryParse(_configuration["RabbitMQ:Port"], out var p) ? p : 5672;
        var username = _configuration["RabbitMQ:Username"] ?? "guest";
        var password = _configuration["RabbitMQ:Password"] ?? "guest";

        var factory = new ConnectionFactory
        {
            HostName = hostname,
            Port = port,
            UserName = username,
            Password = password,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);

        // Lắng nghe tất cả các sự kiện order: order.created, order.status.changed, ...
        _channel.QueueBind(QueueName, ExchangeName, "order.*");

        _logger.LogInformation("Đã khởi tạo xong RabbitMQ Topology cho Notification Service.");
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
