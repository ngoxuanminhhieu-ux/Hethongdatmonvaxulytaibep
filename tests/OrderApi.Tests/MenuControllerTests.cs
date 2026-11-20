using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using OrderApi.Controllers;
using OrderApi.Data;
using OrderApi.Data.Entities;
using OrderApi.Models;
using Xunit;

namespace OrderApi.Tests;

public class MenuControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IDisposable _connection;
    private readonly MenuController _controller;

    public MenuControllerTests()
    {
        var (context, connection) = TestDbContextFactory.CreateSqliteInMemoryDbContext();
        _context = context;
        _connection = connection;

        var mockLogger = new Mock<ILogger<MenuController>>();
        _controller = new MenuController(_context, mockLogger.Object);

        // Seed dữ liệu mẫu
        _context.MenuItems.AddRange(
            new MenuItem { Id = 1, Name = "Phở Bò Tái", Category = "Món chính", Price = 65000, IsAvailable = true },
            new MenuItem { Id = 2, Name = "Bún Chả", Category = "Món chính", Price = 55000, IsAvailable = true },
            new MenuItem { Id = 3, Name = "Cà Phê Sữa Đá", Category = "Đồ uống", Price = 25000, IsAvailable = true },
            new MenuItem { Id = 4, Name = "Trà Sữa Trân Châu (Tạm hết)", Category = "Đồ uống", Price = 30000, IsAvailable = false }
        );
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetMenuItems_ReturnsOnlyAvailable_ByDefault()
    {
        // Act
        var result = await _controller.GetMenuItems(includeUnavailable: false);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<MenuItemResponse>>(okResult.Value);
        Assert.Equal(3, items.Count());
        Assert.All(items, item => Assert.True(item.IsAvailable));
    }

    [Fact]
    public async Task GetMenuItems_FiltersByCategory()
    {
        // Act
        var result = await _controller.GetMenuItems(category: "Đồ uống", includeUnavailable: true);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<MenuItemResponse>>(okResult.Value);
        Assert.Equal(2, items.Count());
        Assert.All(items, item => Assert.Equal("Đồ uống", item.Category));
    }

    [Fact]
    public async Task GetMenuItemById_ExistingId_ReturnsOkWithItem()
    {
        // Act
        var result = await _controller.GetMenuItemById(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var item = Assert.IsType<MenuItemResponse>(okResult.Value);
        Assert.Equal(1, item.Id);
        Assert.Equal("Phở Bò Tái", item.Name);
    }

    [Fact]
    public async Task GetMenuItemById_NonExistingId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetMenuItemById(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateMenuItem_ValidRequest_ReturnsCreatedAndSavesToDb()
    {
        // Arrange
        var request = new CreateMenuItemRequest
        {
            Name = "Cơm Tấm Sườn Nướng",
            Category = "Món chính",
            Price = 60000,
            Description = "Cơm tấm nóng dẻo kèm sườn nướng",
            IsAvailable = true
        };

        // Act
        var result = await _controller.CreateMenuItem(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var item = Assert.IsType<MenuItemResponse>(createdResult.Value);
        Assert.Equal("Cơm Tấm Sườn Nướng", item.Name);
        Assert.True(item.Id > 0);

        var dbItem = await _context.MenuItems.FindAsync(item.Id);
        Assert.NotNull(dbItem);
        Assert.Equal(60000, dbItem.Price);
    }

    [Fact]
    public async Task CreateMenuItem_DuplicateName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateMenuItemRequest
        {
            Name = "Phở Bò Tái", // Đã tồn tại trong seed
            Category = "Món chính",
            Price = 70000
        };

        // Act
        var result = await _controller.CreateMenuItem(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateMenuItem_ValidRequest_UpdatesItemInDb()
    {
        // Arrange
        var request = new UpdateMenuItemRequest
        {
            Name = "Phở Bò Tái Đặc Biệt",
            Category = "Món chính",
            Price = 75000,
            Description = "Nhiều thịt bò hơn",
            IsAvailable = true
        };

        // Act
        var result = await _controller.UpdateMenuItem(1, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var updated = Assert.IsType<MenuItemResponse>(okResult.Value);
        Assert.Equal("Phở Bò Tái Đặc Biệt", updated.Name);
        Assert.Equal(75000, updated.Price);

        var dbItem = await _context.MenuItems.FindAsync(1);
        Assert.Equal(75000, dbItem!.Price);
    }

    [Fact]
    public async Task DeleteMenuItem_WhenUsedInOrders_SoftDeletesItem()
    {
        // Arrange: Tạo đơn hàng có sử dụng món ID = 1
        var orderId = Guid.NewGuid();
        _context.Orders.Add(new Order { Id = orderId, TableNumber = "Bàn 01", Status = "Pending" });
        _context.OrderDetails.Add(new OrderDetail { OrderId = orderId, MenuItemId = 1, Quantity = 1, UnitPrice = 65000, Subtotal = 65000 });
        await _context.SaveChangesAsync();

        // Act: Thử xóa món ID = 1
        var result = await _controller.DeleteMenuItem(1);

        // Assert: Không bị xóa cứng (hard delete), mà chuyển sang IsAvailable = false để bảo toàn tính toàn vẹn tham chiếu
        var okResult = Assert.IsType<OkObjectResult>(result);
        var itemInDb = await _context.MenuItems.FindAsync(1);
        Assert.NotNull(itemInDb);
        Assert.False(itemInDb.IsAvailable);
    }

    [Fact]
    public async Task ToggleAvailability_TogglesIsAvailableProperty()
    {
        // Act: Đang true chuyển thành false
        var result1 = await _controller.ToggleAvailability(1);
        Assert.IsType<OkObjectResult>(result1);
        var item1 = await _context.MenuItems.FindAsync(1);
        Assert.False(item1!.IsAvailable);

        // Act: Đang false chuyển lại thành true
        var result2 = await _controller.ToggleAvailability(1);
        Assert.IsType<OkObjectResult>(result2);
        var item2 = await _context.MenuItems.FindAsync(1);
        Assert.True(item2!.IsAvailable);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
