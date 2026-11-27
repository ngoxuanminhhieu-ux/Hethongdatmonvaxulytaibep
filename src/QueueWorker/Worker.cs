using System.Text;
using System.Text.Json;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;
using Shared.Enums;

namespace QueueWorker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IModel? _channel;

    private const string ExchangeName = "clinic.appointment.exchange";
    private const string QueueName = "queue.process.queue";
    private const string RoutingKey = "appointment.requested";

    private const string DlxExchange = "clinic.appointment.dlx";
    private const string DlqQueue = "queue.dlq";
    private const string DlqRoutingKey = "queue.failed";

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue Worker (Dịch vụ cấp số thứ tự khám) đang khởi động...");

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
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    _logger.LogInformation(">>> [QUEUE WORKER] Tiếp nhận yêu cầu cấp số mới! DeliveryTag: {Tag}", ea.DeliveryTag);

                    var requestedEvent = JsonSerializer.Deserialize<AppointmentRequestedEvent>(message, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (requestedEvent != null)
                    {
                        var data = requestedEvent.Data;
                        _logger.LogInformation("[QUEUE WORKER] Đang xử lý cấp số cho Bệnh nhân '{Patient}' khám tại {Room} ({Doctor})...",
                            data.PatientName, data.RoomNumber, data.DoctorName);

                        // 1. Sinh số thứ tự mới và lưu CSDL
                        var assignedNumber = await AssignQueueNumberInDatabaseAsync(data.AppointmentId, data.DoctorId);

                        _logger.LogInformation(">>> [CẤP SỐ THÀNH CÔNG] Lịch hẹn {ApptId} được cấp SỐ THỨ TỰ #{Num:D2} (Phòng: {Room})",
                            data.AppointmentId, assignedNumber, data.RoomNumber);

                        // 2. Publish sự kiện QueueNumberAssignedEvent lên RabbitMQ để Notification Service đẩy realtime cho bệnh nhân
                        PublishQueueAssignedEvent(new QueueNumberAssignedEvent
                        {
                            CorrelationId = data.AppointmentId,
                            Data = new QueueNumberAssignedData
                            {
                                AppointmentId = data.AppointmentId,
                                PatientId = data.PatientId,
                                PatientName = data.PatientName,
                                DoctorId = data.DoctorId,
                                DoctorName = data.DoctorName,
                                RoomNumber = data.RoomNumber,
                                QueueNumber = assignedNumber,
                                Status = AppointmentStatus.Confirmed,
                                Message = $"Bạn đã được cấp số thứ tự #{assignedNumber:D2} khám tại {data.RoomNumber} ({data.DoctorName})",
                                AssignedAt = DateTime.UtcNow
                            }
                        });
                    }

                    // Gửi xác nhận xử lý thành công (Manual Ack)
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    _logger.LogInformation("<<< [QUEUE WORKER] Đã gửi BasicAck hoàn tất cấp số!");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra khi xử lý cấp số thứ tự. Chuyển vào Dead Letter Queue (DLQ)...");
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("Queue Worker đã bắt đầu lắng nghe hàng đợi '{QueueName}'...", QueueName);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task<int> AssignQueueNumberInDatabaseAsync(Guid appointmentId, int doctorId)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=clinic_queue_db;Username=postgres;Password=postgres";

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Lấy số thứ tự lớn nhất hiện tại của bác sĩ trong ngày hôm nay
        var queryMaxSql = @"
            SELECT COALESCE(MAX(queue_number), 0) + 1 
            FROM appointments 
            WHERE doctor_id = @doctorId 
              AND DATE(created_at) = CURRENT_DATE 
              AND queue_number IS NOT NULL";

        int nextQueueNumber = 1;
        await using (var cmd = new NpgsqlCommand(queryMaxSql, conn))
        {
            cmd.Parameters.AddWithValue("doctorId", doctorId);
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && int.TryParse(result.ToString(), out var num))
            {
                nextQueueNumber = num;
            }
        }

        // Cập nhật appointment sang Confirmed với queue_number vừa sinh
        var updateSql = @"
            UPDATE appointments 
            SET queue_number = @queueNumber, status = 'Confirmed', updated_at = CURRENT_TIMESTAMP
            WHERE id = @id";

        await using (var cmd = new NpgsqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("queueNumber", nextQueueNumber);
            cmd.Parameters.AddWithValue("id", appointmentId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Ghi nhật ký vào queue_logs
        var logSql = @"
            INSERT INTO queue_logs (doctor_id, appointment_id, queue_number, from_status, to_status, action, changed_by, notes)
            VALUES (@doctorId, @appointmentId, @queueNumber, 'Pending', 'Confirmed', 'QueueAssigned', 'QueueWorker', 'Cấp số thứ tự tự động qua RabbitMQ')";

        await using (var cmd = new NpgsqlCommand(logSql, conn))
        {
            cmd.Parameters.AddWithValue("doctorId", doctorId);
            cmd.Parameters.AddWithValue("appointmentId", appointmentId);
            cmd.Parameters.AddWithValue("queueNumber", nextQueueNumber);
            await cmd.ExecuteNonQueryAsync();
        }

        return nextQueueNumber;
    }

    private void PublishQueueAssignedEvent(QueueNumberAssignedEvent ev)
    {
        try
        {
            var json = JsonSerializer.Serialize(ev, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var body = Encoding.UTF8.GetBytes(json);

            var props = _channel!.CreateBasicProperties();
            props.Persistent = true;
            props.DeliveryMode = 2;
            props.ContentType = "application/json";
            props.Type = nameof(QueueNumberAssignedEvent);
            props.CorrelationId = ev.CorrelationId.ToString();
            props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: "queue.number.assigned",
                basicProperties: props,
                body: body);

            _logger.LogInformation(">>> [QUEUE WORKER] Đã publish 'QueueNumberAssignedEvent' cho Bệnh nhân {Patient} (Số #{Num})",
                ev.Data.PatientName, ev.Data.QueueNumber);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi publish sự kiện QueueNumberAssignedEvent.");
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

        // 1. Khai báo DLX & DLQ
        _channel.ExchangeDeclare(DlxExchange, ExchangeType.Direct, durable: true, autoDelete: false);
        _channel.QueueDeclare(DlqQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(DlqQueue, DlxExchange, DlqRoutingKey);

        // 2. Khai báo Main Topic Exchange
        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);

        // 3. Khai báo Process Queue gắn DLX
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

        // 4. Phân phối công bằng (Prefetch = 1)
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        _logger.LogInformation("Đã khởi tạo xong RabbitMQ Topology cho Queue Worker.");
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
