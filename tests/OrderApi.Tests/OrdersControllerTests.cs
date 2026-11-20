using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using OrderApi.Controllers;
using OrderApi.Data;
using OrderApi.Data.Entities;
using OrderApi.Models;
using OrderApi.Services;
using Shared.Contracts;
using Shared.Enums;
using Xunit;

namespace OrderApi.Tests;

public class OrdersControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IDisposable _connection;
    private readonly Mock<IRabbitMqProducer> _mockProducer;
    private readonly OrdersController _controller;

    public OrdersControllerTests()
    {
        var (context, connection) = TestDbContextFactory.CreateSqliteInMemoryDbContext();
        _context = context;
        _connection = connection;

        _mockProducer = new Mock<IRabbitMqProducer>();
        var mockLogger = new Mock<ILogger<OrdersController>>();

        _controller = new OrdersController(_context, _mockProducer.Object, mockLogger.Object);

        // Seed món ăn mẫu
        _context.MenuItems.AddRange(
            new MenuItem { Id = 1, Name = "Phở Bò Tái Nạm", Price = 65000, Category = "Món chính", IsAvailable = true },
            new MenuItem { Id = 2, Name = "Cà Phê Sữa Đá", Price = 25000, Category = "Đồ uống", IsAvailable = true },
            new MenuItem { Id = 3, Name = "Bánh Flan (Hết)", Price = 20000, Category = "Tráng miệng", IsAvailable = false }
        );
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_CalculatesTotalAmountAccurately()
    {
        // Arrange: 2 tô phở (65.000 * 2 = 130.000) + 1 ly cà phê (25.000) = 155.000 VND
        var request = new CreateOrderRequest
        {
            CustomerName = "Nguyễn Văn An",
            Phone = "0901234567",
            TableNumber = "Bàn 01",
            Notes = "Ăn tại chỗ",
            Items = new List<CreateOrderItemRequest>
            {
                new() { MenuItemId = 1, Quantity = 2, Notes = "Không hành" },
                new() { MenuItemId = 2, Quantity = 1, Notes = "Ít đá" }
            }
        };

        // Act
        var result = await _controller.CreateOrder(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var order = Assert.IsType<OrderResponse>(createdResult.Value);

        Assert.Equal(155000, order.TotalAmount);
        Assert.Equal(OrderStatus.Pending.ToString(), order.Status);
        Assert.Equal("Bàn 01", order.TableNumber);
        Assert.Equal(2, order.Items.Count);

        // Kiểm tra lưu DB
        var dbOrder = await _context.Orders.FindAsync(order.OrderId);
        Assert.NotNull(dbOrder);
        Assert.Equal(155000, dbOrder.TotalAmount);

        // Kiểm tra phát sự kiện RabbitMQ
        _mockProducer.Verify(p => p.PublishOrderCreated(It.Is<OrderCreatedEvent>(e =>
            e.Data.OrderId == order.OrderId &&
            e.Data.TotalAmount == 155000 &&
            e.Data.Items.Count == 2
        )), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WhenMenuItemNotFound_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerName = "Trần Thị Bình",
            TableNumber = "Bàn 02",
            Items = new List<CreateOrderItemRequest>
            {
                new() { MenuItemId = 999, Quantity = 1 } // ID 999 không tồn tại
            }
        };

        // Act
        var result = await _controller.CreateOrder(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
        _mockProducer.Verify(p => p.PublishOrderCreated(It.IsAny<OrderCreatedEvent>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrder_WhenMenuItemUnavailable_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerName = "Lê Hoàng Nam",
            TableNumber = "Bàn 03",
            Items = new List<CreateOrderItemRequest>
            {
                new() { MenuItemId = 3, Quantity = 1 } // Món ID 3 có IsAvailable = false
            }
        };

        // Act
        var result = await _controller.CreateOrder(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
        _mockProducer.Verify(p => p.PublishOrderCreated(It.IsAny<OrderCreatedEvent>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrder_RecordsInitialPendingStatusHistory()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerName = "Khách Hàng Test",
            TableNumber = "Bàn 04",
            Items = new List<CreateOrderItemRequest>
            {
                new() { MenuItemId = 1, Quantity = 1 }
            }
        };

        // Act
        var result = await _controller.CreateOrder(request);
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var order = Assert.IsType<OrderResponse>(createdResult.Value);

        // Assert: Lịch sử phải có bản ghi Pending ban đầu
        var histories = _context.OrderStatusHistories.Where(h => h.OrderId == order.OrderId).ToList();
        Assert.Single(histories);
        Assert.Equal(OrderStatus.Pending.ToString(), histories[0].ToStatus);
        Assert.Equal("OrderApi", histories[0].ChangedBy);
    }

    [Fact]
    public async Task GetOrderById_ExistingOrder_ReturnsOrderWithItemsAndTimeline()
    {
        // Arrange: Tạo trước 1 đơn hàng
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            TableNumber = "Bàn 05",
            TotalAmount = 65000,
            Status = "Preparing",
            CreatedAt = DateTime.UtcNow
        };
        _context.Orders.Add(order);
        _context.OrderDetails.Add(new OrderDetail { OrderId = orderId, MenuItemId = 1, Quantity = 1, UnitPrice = 65000, Subtotal = 65000 });
        _context.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = orderId, FromStatus = null, ToStatus = "Pending", ChangedBy = "OrderApi" });
        _context.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = orderId, FromStatus = "Pending", ToStatus = "Preparing", ChangedBy = "KitchenStaff" });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrderById(orderId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var res = Assert.IsType<OrderResponse>(okResult.Value);
        Assert.Equal(orderId, res.OrderId);
        Assert.Equal("Preparing", res.Status);
        Assert.Single(res.Items);
        Assert.Equal(2, res.StatusHistory.Count);
    }

    [Fact]
    public async Task UpdateOrderStatus_TransitionsPendingToPreparing_PublishesEvent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _context.Orders.Add(new Order
        {
            Id = orderId,
            TableNumber = "Bàn 01",
            Status = OrderStatus.Pending.ToString(),
            TotalAmount = 50000
        });
        await _context.SaveChangesAsync();

        var request = new UpdateOrderStatusRequest
        {
            Status = "Preparing",
            ChangedBy = "Chef Long",
            Notes = "Bếp bắt đầu chế biến"
        };

        // Act
        var result = await _controller.UpdateOrderStatus(orderId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var dbOrder = await _context.Orders.FindAsync(orderId);
        Assert.Equal("Preparing", dbOrder!.Status);

        // Kiểm tra phát sự kiện OrderStatusChanged
        _mockProducer.Verify(p => p.PublishOrderStatusChanged(It.Is<OrderStatusChangedEvent>(e =>
            e.Data.OrderId == orderId &&
            e.Data.PreviousStatus == OrderStatus.Pending &&
            e.Data.NewStatus == OrderStatus.Preparing
        )), Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatus_WhenTransitionReadyToPending_RejectsBadRequest()
    {
        // Arrange: Đơn hàng đã Ready
        var orderId = Guid.NewGuid();
        _context.Orders.Add(new Order
        {
            Id = orderId,
            TableNumber = "Bàn 01",
            Status = OrderStatus.Ready.ToString()
        });
        await _context.SaveChangesAsync();

        var request = new UpdateOrderStatusRequest
        {
            Status = "Pending",
            ChangedBy = "Test"
        };

        // Act
        var result = await _controller.UpdateOrderStatus(orderId, request);

        // Assert: Không thể chuyển từ Ready ngược về Pending
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CancelOrder_WhenOrderPending_CancelsSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _context.Orders.Add(new Order
        {
            Id = orderId,
            TableNumber = "Bàn 02",
            Status = OrderStatus.Pending.ToString()
        });
        await _context.SaveChangesAsync();

        var cancelRequest = new CancelOrderRequest
        {
            CancelledBy = "Khách hàng",
            Reason = "Đổi ý muốn gọi món khác"
        };

        // Act
        var result = await _controller.CancelOrder(orderId, cancelRequest);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var dbOrder = await _context.Orders.FindAsync(orderId);
        Assert.Equal(OrderStatus.Cancelled.ToString(), dbOrder!.Status);

        _mockProducer.Verify(p => p.PublishOrderStatusChanged(It.Is<OrderStatusChangedEvent>(e =>
            e.Data.OrderId == orderId &&
            e.Data.NewStatus == OrderStatus.Cancelled
        )), Times.Once);
    }

    [Fact]
    public async Task CancelOrder_WhenOrderReady_RejectsBadRequest()
    {
        // Arrange: Đơn hàng đã nấu xong (Ready)
        var orderId = Guid.NewGuid();
        _context.Orders.Add(new Order
        {
            Id = orderId,
            TableNumber = "Bàn 03",
            Status = OrderStatus.Ready.ToString()
        });
        await _context.SaveChangesAsync();

        var cancelRequest = new CancelOrderRequest
        {
            CancelledBy = "Khách hàng",
            Reason = "Không muốn ăn nữa"
        };

        // Act
        var result = await _controller.CancelOrder(orderId, cancelRequest);

        // Assert: Món ăn đã làm xong thì không thể hủy
        Assert.IsType<BadRequestObjectResult>(result);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
