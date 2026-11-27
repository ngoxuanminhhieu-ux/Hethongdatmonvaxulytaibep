using RabbitMQ.Client;

namespace AppointmentApi.Services;

public class RabbitMqTopologyInitializer : IHostedService
{
    private readonly IRabbitMqPersistentConnection _persistentConnection;
    private readonly ILogger<RabbitMqTopologyInitializer> _logger;

    public const string MainExchange = "clinic.appointment.exchange";
    public const string DlxExchange = "clinic.appointment.dlx";

    public const string QueueProcessQueue = "queue.process.queue";
    public const string QueueNotificationQueue = "queue.notification.queue";
    public const string DeadLetterQueue = "queue.dlq";

    public const string AppointmentRequestedRoutingKey = "appointment.requested";
    public const string QueueServingChangedRoutingKey = "queue.serving.changed";
    public const string AllQueueEventsRoutingKey = "queue.*";
    public const string DeadLetterRoutingKey = "queue.failed";

    public RabbitMqTopologyInitializer(
        IRabbitMqPersistentConnection persistentConnection,
        ILogger<RabbitMqTopologyInitializer> logger)
    {
        _persistentConnection = persistentConnection;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Khởi tạo RabbitMQ Topology cho Phòng khám (Exchanges, Queues, DLQ, Bindings)...");

        try
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            if (_persistentConnection.IsConnected)
            {
                using var channel = _persistentConnection.CreateModel();

                // 1. Khai báo Dead Letter Exchange & Queue
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

                // 3. Khai báo Hàng đợi cấp số thứ tự (kèm DLX)
                var queueArgs = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", DlxExchange },
                    { "x-dead-letter-routing-key", DeadLetterRoutingKey }
                };

                channel.QueueDeclare(
                    queue: QueueProcessQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: queueArgs);

                channel.QueueBind(
                    queue: QueueProcessQueue,
                    exchange: MainExchange,
                    routingKey: AppointmentRequestedRoutingKey);

                // 4. Khai báo Hàng đợi thông báo Realtime
                channel.QueueDeclare(
                    queue: QueueNotificationQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);

                channel.QueueBind(
                    queue: QueueNotificationQueue,
                    exchange: MainExchange,
                    routingKey: "queue.*");

                channel.QueueBind(
                    queue: QueueNotificationQueue,
                    exchange: MainExchange,
                    routingKey: "appointment.*");

                _logger.LogInformation(">>> [RABBITMQ TOPOLOGY] Đã cấu hình thành công: Main Exchange '{Exchange}', DLX '{Dlx}', Queues: ['{Q1}', '{Q2}', '{Q3}']",
                    MainExchange, DlxExchange, QueueProcessQueue, QueueNotificationQueue, DeadLetterQueue);
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
