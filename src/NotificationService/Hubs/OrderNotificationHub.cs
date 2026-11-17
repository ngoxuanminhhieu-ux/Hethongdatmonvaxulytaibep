using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Hubs;

public class OrderNotificationHub : Hub
{
    private readonly ILogger<OrderNotificationHub> _logger;

    public OrderNotificationHub(ILogger<OrderNotificationHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinOrderGroup(string orderId)
    {
        var groupName = $"order-{orderId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} đã tham gia nhóm theo dõi đơn: {Group}", Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("JoinedGroup", groupName);
    }

    public async Task LeaveOrderGroup(string orderId)
    {
        var groupName = $"order-{orderId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} đã rời nhóm: {Group}", Context.ConnectionId, groupName);
    }

    public async Task JoinKitchenDashboard()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "kitchen-dashboard");
        _logger.LogInformation("Client {ConnectionId} đã kết nối vào màn hình Bếp (kitchen-dashboard)", Context.ConnectionId);
        await Clients.Caller.SendAsync("JoinedKitchenDashboard");
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("Client kết nối mới: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client ngắt kết nối: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
