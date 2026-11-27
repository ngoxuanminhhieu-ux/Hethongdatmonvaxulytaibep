using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Hubs;

public class QueueNotificationHub : Hub
{
    private readonly ILogger<QueueNotificationHub> _logger;

    public QueueNotificationHub(ILogger<QueueNotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Bệnh nhân hoặc màn hình phòng khám tham gia theo dõi tiến trình hàng đợi của Bác sĩ.
    /// </summary>
    public async Task JoinDoctorQueueGroup(string doctorId)
    {
        var groupName = $"doctor-queue-{doctorId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnId} tham gia nhóm phòng khám: {Group}", Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("JoinedGroup", groupName);
    }

    /// <summary>
    /// Bệnh nhân tham gia theo dõi trạng thái của đúng lịch hẹn của mình.
    /// </summary>
    public async Task JoinAppointmentGroup(string appointmentId)
    {
        var groupName = $"appointment-{appointmentId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnId} tham gia theo dõi lịch hẹn: {Group}", Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("JoinedGroup", groupName);
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("Client kết nối mới tới SignalR Queue Hub: {ConnId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client ngắt kết nối: {ConnId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
