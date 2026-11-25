using System.Net.Sockets;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace OrderApi.Services;

public class RabbitMqPersistentConnection : IRabbitMqPersistentConnection
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqPersistentConnection> _logger;
    private readonly int _retryCount;
    private IConnection? _connection;
    private bool _disposed;
    private readonly object _syncRoot = new();

    public string HostName { get; }
    public int Port { get; }
    public string VirtualHost { get; }

    public RabbitMqPersistentConnection(IConfiguration configuration, ILogger<RabbitMqPersistentConnection> logger)
    {
        _logger = logger;

        HostName = configuration["RabbitMQ:Host"] ?? "localhost";
        Port = int.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : 5672;
        var username = configuration["RabbitMQ:Username"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";
        VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/";
        _retryCount = int.TryParse(configuration["RabbitMQ:RetryCount"], out var r) ? r : 3;

        _connectionFactory = new ConnectionFactory
        {
            HostName = HostName,
            Port = Port,
            UserName = username,
            Password = password,
            VirtualHost = VirtualHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };
    }

    public bool IsConnected => _connection is { IsOpen: true } && !_disposed;

    public IModel CreateModel()
    {
        if (!IsConnected)
        {
            TryConnect();
        }

        if (!IsConnected || _connection == null)
        {
            throw new InvalidOperationException("Không có kết nối RabbitMQ khả dụng để tạo channel.");
        }

        return _connection.CreateModel();
    }

    public bool TryConnect()
    {
        lock (_syncRoot)
        {
            if (IsConnected) return true;

            _logger.LogInformation("Đang thiết lập kết nối bền bỉ (Persistent Connection) tới RabbitMQ tại {Host}:{Port}...", HostName, Port);

            for (var attempt = 1; attempt <= _retryCount; attempt++)
            {
                try
                {
                    _connection = _connectionFactory.CreateConnection();
                    break;
                }
                catch (BrokerUnreachableException ex)
                {
                    _logger.LogWarning(ex, "Thử kết nối RabbitMQ lần {Attempt}/{Max} thất bại: Broker không phản hồi.", attempt, _retryCount);
                    if (attempt < _retryCount) Thread.Sleep(1000 * attempt);
                }
                catch (SocketException ex)
                {
                    _logger.LogWarning(ex, "Thử kết nối RabbitMQ lần {Attempt}/{Max} thất bại do Socket error.", attempt, _retryCount);
                    if (attempt < _retryCount) Thread.Sleep(1000 * attempt);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lỗi không xác định khi kết nối RabbitMQ lần {Attempt}/{Max}.", attempt, _retryCount);
                    if (attempt < _retryCount) Thread.Sleep(1000 * attempt);
                }
            }

            if (IsConnected && _connection != null)
            {
                _connection.ConnectionShutdown += OnConnectionShutdown;
                _connection.CallbackException += OnCallbackException;
                _connection.ConnectionBlocked += OnConnectionBlocked;
                _connection.ConnectionUnblocked += OnConnectionUnblocked;

                _logger.LogInformation(">>> [RABBITMQ] Kết nối thành công tới {Host}:{Port} (VirtualHost: '{VHost}')", HostName, Port, VirtualHost);
                return true;
            }

            _logger.LogWarning("Không thể kết nối RabbitMQ sau {RetryCount} lần thử. Hệ thống sẽ chuyển sang chế độ tự phục hồi khi có request tiếp theo.", _retryCount);
            return false;
        }
    }

    private void OnConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
    {
        if (_disposed) return;
        _logger.LogWarning("RabbitMQ connection bị chặn (Blocked). Lý do: {Reason}", e.Reason);
    }

    private void OnConnectionUnblocked(object? sender, EventArgs e)
    {
        if (_disposed) return;
        _logger.LogInformation("RabbitMQ connection đã được giải phóng (Unblocked).");
    }

    private void OnCallbackException(object? sender, CallbackExceptionEventArgs e)
    {
        if (_disposed) return;
        _logger.LogWarning(e.Exception, "Ngoại lệ xảy ra trong callback của RabbitMQ connection.");
    }

    private void OnConnectionShutdown(object? sender, ShutdownEventArgs reason)
    {
        if (_disposed) return;
        _logger.LogWarning("RabbitMQ connection bị ngắt kết nối: {ReplyText} (Code: {Code})", reason.ReplyText, reason.ReplyCode);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _connection?.Dispose();
        }
        catch (IOException ex)
        {
            _logger.LogCritical(ex, "Lỗi khi giải phóng RabbitMQ connection.");
        }
    }
}
