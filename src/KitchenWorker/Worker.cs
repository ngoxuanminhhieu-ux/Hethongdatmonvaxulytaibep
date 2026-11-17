using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;
using Shared.Enums;

namespace KitchenWorker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IModel? _channel;

    private const string ExchangeName = "food.order.exchange";
    private const string QueueName = "order.process.queue";
    private const string RoutingKey = "order.created";

    private const string DlxExchange = "food.order.dlx";
    private const string DlqQueue = "order.dlq";
    private const string DlqRoutingKey = "order.failed";

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Kitchen Worker Service đang khởi động...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                InitRabbitMQ();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Chưa kết nối được RabbitMQ: {Message}. Đang thử lại sau 5 giây...", ex.Message);
                await Task.Delay(5000, stoppingToken);
            }
        }

        if (_channel != null)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    _logger.LogInformation(">>> [KITCHEN WORKER] Nhận được message mới! DeliveryTag: {Tag}", ea.DeliveryTag);

                    var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (orderEvent != null)
                    {
                        var data = orderEvent.Data;
                        _logger.LogInformation("[KITCHEN] Tiếp nhận đơn #{OrderId} của khách '{Customer}' tại {Table}. Tổng tiền: {Amount} VND. Số lượng món: {Count}",
                            data.OrderId, data.CustomerName, data.TableNumber, data.TotalAmount, data.Items.Count);

                        foreach (var item in data.Items)
                        {
                            _logger.LogInformation("   -> Món: {Name} x{Qty} | Ghi chú: {Notes}", item.Name, item.Quantity, item.Notes ?? "Không");
                        }

                        // Giả lập bếp bắt đầu nấu và hoàn thành (sẽ tích hợp chi tiết trong Tuần 5)
                        _logger.LogInformation("[KITCHEN] Bếp đang chế biến đơn #{OrderId}...", data.OrderId);
                    }

                    // Gửi xác nhận xử lý thành công (Manual Ack)
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    _logger.LogInformation("<<< [KITCHEN WORKER] Đã gửi BasicAck cho đơn hàng!");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý đơn hàng trong Kitchen Worker. Đẩy vào DLQ...");
                    // Từ chối và không requeue để chuyển sang DLQ
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("Kitchen Worker đã bắt đầu lắng nghe hàng đợi '{QueueName}'...", QueueName);
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

        // 1. Khai báo Dead Letter Exchange & Queue
        _channel.ExchangeDeclare(DlxExchange, ExchangeType.Direct, durable: true, autoDelete: false);
        _channel.QueueDeclare(DlqQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(DlqQueue, DlxExchange, DlqRoutingKey);

        // 2. Khai báo Main Topic Exchange
        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);

        // 3. Khai báo Process Queue gắn kèm Dead Letter Exchange
        var queueArgs = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", DlxExchange },
            { "x-dead-letter-routing-key", DlqRoutingKey }
        };

        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs);

        _channel.QueueBind(QueueName, ExchangeName, RoutingKey);

        // 4. Phân phối công bằng: mỗi lần chỉ lấy 1 message
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        _logger.LogInformation("Đã khởi tạo xong RabbitMQ Topology cho Kitchen Worker.");
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
