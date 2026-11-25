using RabbitMQ.Client;

namespace OrderApi.Services;

public class RabbitMqTopologyInitializer : IHostedService
{
    private readonly IRabbitMqPersistentConnection _persistentConnection;
    private readonly ILogger<RabbitMqTopologyInitializer> _logger;

    public const string MainExchange = "food.order.exchange";
    public const string DlxExchange = "food.order.dlx";

    public const string KitchenQueue = "order.process.queue";
    public const string NotificationQueue = "order.notification.queue";
    public const string DeadLetterQueue = "order.dlq";

    public const string OrderCreatedRoutingKey = "order.created";
    public const string OrderStatusChangedRoutingKey = "order.status.changed";
    public const string AllOrderEventsRoutingKey = "order.*";
    public const string DeadLetterRoutingKey = "order.failed";

    public RabbitMqTopologyInitializer(
        IRabbitMqPersistentConnection persistentConnection,
        ILogger<RabbitMqTopologyInitializer> logger)
    {
        _persistentConnection = persistentConnection;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Khởi tạo RabbitMQ Topology (Exchanges, Queues, DLQ, Bindings)...");

        try
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            if (_persistentConnection.IsConnected)
            {
                using var channel = _persistentConnection.CreateModel();

                // 1. Khai báo Dead Letter Exchange & Dead Letter Queue (DLQ)
                channel.ExchangeDeclare(
                    exchange: DlxExchange,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);

                channel.QueueDeclare(
                    queue: DeadLetterQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);

                channel.QueueBind(
                    queue: DeadLetterQueue,
                    exchange: DlxExchange,
                    routingKey: DeadLetterRoutingKey);

                // 2. Khai báo Main Topic Exchange
                channel.ExchangeDeclare(
                    exchange: MainExchange,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                // 3. Khai báo Hàng đợi Bếp (kèm cấu hình đẩy vào DLQ khi lỗi)
                var kitchenQueueArgs = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", DlxExchange },
                    { "x-dead-letter-routing-key", DeadLetterRoutingKey }
                };

                channel.QueueDeclare(
                    queue: KitchenQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: kitchenQueueArgs);

                channel.QueueBind(
                    queue: KitchenQueue,
                    exchange: MainExchange,
                    routingKey: OrderCreatedRoutingKey);

                // 4. Khai báo Hàng đợi Thông báo Realtime
                channel.QueueDeclare(
                    queue: NotificationQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);

                channel.QueueBind(
                    queue: NotificationQueue,
                    exchange: MainExchange,
                    routingKey: AllOrderEventsRoutingKey);

                _logger.LogInformation(">>> [RABBITMQ TOPOLOGY] Đã cấu hình thành công: Main Exchange '{Exchange}', DLX '{Dlx}', Queues: ['{Q1}', '{Q2}', '{Q3}']",
                    MainExchange, DlxExchange, KitchenQueue, NotificationQueue, DeadLetterQueue);
            }
            else
            {
                _logger.LogWarning("RabbitMQ chưa sẵn sàng lúc khởi động. Topology sẽ tự động được khởi tạo lại khi kết nối thành công.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chưa thể khởi tạo RabbitMQ Topology tại thời điểm khởi động.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
