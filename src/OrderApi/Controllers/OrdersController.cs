using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderApi.Data;
using OrderApi.Data.Entities;
using OrderApi.Models;
using OrderApi.Services;
using Shared.Contracts;
using Shared.Enums;

namespace OrderApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IRabbitMqProducer _producer;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(AppDbContext context, IRabbitMqProducer producer, ILogger<OrdersController> logger)
    {
        _context = context;
        _producer = producer;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // 1. Kiểm tra danh sách món
        var itemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _context.MenuItems
            .Where(m => itemIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        var missingIds = itemIds.Except(menuItems.Keys).ToList();
        if (missingIds.Any())
        {
            return BadRequest(new { message = $"Món ăn với ID [{string.Join(", ", missingIds)}] không tồn tại trong thực đơn." });
        }

        // 2. Tìm hoặc tạo mới khách hàng
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Name == request.CustomerName && c.TableNumber == request.TableNumber);

        if (customer == null)
        {
            customer = new Customer
            {
                Name = request.CustomerName,
                Phone = request.Phone,
                TableNumber = request.TableNumber,
                CreatedAt = DateTime.UtcNow
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
        }

        // 3. Tạo Order
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            TableNumber = request.TableNumber,
            Status = OrderStatus.Pending.ToString(),
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        decimal total = 0;
        var orderDetails = new List<OrderDetail>();
        var eventItems = new List<OrderItemData>();

        foreach (var reqItem in request.Items)
        {
            var menuItem = menuItems[reqItem.MenuItemId];
            var subtotal = menuItem.Price * reqItem.Quantity;
            total += subtotal;

            orderDetails.Add(new OrderDetail
            {
                OrderId = order.Id,
                MenuItemId = menuItem.Id,
                UnitPrice = menuItem.Price,
                Quantity = reqItem.Quantity,
                Subtotal = subtotal,
                ItemNotes = reqItem.Notes
            });

            eventItems.Add(new OrderItemData
            {
                MenuItemId = menuItem.Id,
                Name = menuItem.Name,
                UnitPrice = menuItem.Price,
                Quantity = reqItem.Quantity,
                Notes = reqItem.Notes
            });
        }

        order.TotalAmount = total;
        order.OrderDetails = orderDetails;

        // 4. Ghi nhận lịch sử trạng thái ban đầu
        order.StatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = null,
            ToStatus = OrderStatus.Pending.ToString(),
            ChangedBy = "OrderApi",
            Notes = "Đơn hàng mới tạo bởi khách hàng",
            CreatedAt = DateTime.UtcNow
        });

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Đã lưu đơn hàng {OrderId} vào CSDL với tổng tiền {Total} VND", order.Id, total);

        // 5. Gửi sự kiện OrderCreated vào RabbitMQ
        var orderCreatedEvent = new OrderCreatedEvent
        {
            CorrelationId = order.Id,
            Data = new OrderCreatedData
            {
                OrderId = order.Id,
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                TableNumber = order.TableNumber,
                TotalAmount = order.TotalAmount,
                Status = OrderStatus.Pending,
                Notes = order.Notes,
                CreatedAt = order.CreatedAt,
                Items = eventItems
            }
        };

        _producer.PublishOrderCreated(orderCreatedEvent);

        var response = MapToResponse(order, customer.Name, customer.Phone, orderDetails, menuItems);
        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetOrderById(Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.MenuItem)
            .Include(o => o.StatusHistories)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new { message = $"Không tìm thấy đơn hàng {id}." });
        }

        var response = new OrderResponse
        {
            OrderId = order.Id,
            CustomerName = order.Customer?.Name ?? "Khách vãng lai",
            Phone = order.Customer?.Phone,
            TableNumber = order.TableNumber,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            Notes = order.Notes,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            Items = order.OrderDetails.Select(od => new OrderDetailResponse
            {
                MenuItemId = od.MenuItemId,
                Name = od.MenuItem?.Name ?? string.Empty,
                UnitPrice = od.UnitPrice,
                Quantity = od.Quantity,
                Subtotal = od.Subtotal,
                Notes = od.ItemNotes
            }).ToList(),
            StatusHistory = order.StatusHistories.OrderBy(h => h.CreatedAt).Select(h => new OrderStatusHistoryResponse
            {
                FromStatus = h.FromStatus,
                ToStatus = h.ToStatus,
                ChangedBy = h.ChangedBy,
                Notes = h.Notes,
                Timestamp = h.CreatedAt
            }).ToList()
        };

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetOrders([FromQuery] string? status = null)
    {
        var query = _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.MenuItem)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status.ToLower() == status.ToLower());
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                orderId = o.Id,
                customerName = o.Customer != null ? o.Customer.Name : "Khách vãng lai",
                tableNumber = o.TableNumber,
                totalAmount = o.TotalAmount,
                status = o.Status,
                notes = o.Notes,
                itemCount = o.OrderDetails.Count,
                createdAt = o.CreatedAt,
                updatedAt = o.UpdatedAt,
                items = o.OrderDetails.Select(d => new
                {
                    name = d.MenuItem != null ? d.MenuItem.Name : "",
                    quantity = d.Quantity,
                    notes = d.ItemNotes
                })
            })
            .ToListAsync();

        return Ok(orders);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new { message = $"Không tìm thấy đơn hàng {id}." });
        }

        var previousStatus = order.Status;
        order.Status = request.Status;
        order.UpdatedAt = DateTime.UtcNow;

        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = request.Status,
            ChangedBy = request.ChangedBy,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        if (Enum.TryParse<OrderStatus>(previousStatus, out var prevEnum) &&
            Enum.TryParse<OrderStatus>(request.Status, out var newEnum))
        {
            _producer.PublishOrderStatusChanged(new OrderStatusChangedEvent
            {
                CorrelationId = order.Id,
                Data = new OrderStatusChangedData
                {
                    OrderId = order.Id,
                    CustomerName = order.Customer?.Name ?? "",
                    TableNumber = order.TableNumber,
                    PreviousStatus = prevEnum,
                    NewStatus = newEnum,
                    Message = request.Notes ?? $"Đơn hàng chuyển sang trạng thái {request.Status}",
                    ChangedBy = request.ChangedBy,
                    UpdatedAt = order.UpdatedAt
                }
            });
        }

        return Ok(new
        {
            orderId = order.Id,
            previousStatus,
            newStatus = order.Status,
            updatedAt = order.UpdatedAt
        });
    }

    private static OrderResponse MapToResponse(
        Order order,
        string customerName,
        string? phone,
        List<OrderDetail> details,
        Dictionary<int, MenuItem> menuItems)
    {
        return new OrderResponse
        {
            OrderId = order.Id,
            CustomerName = customerName,
            Phone = phone,
            TableNumber = order.TableNumber,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            Notes = order.Notes,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            Items = details.Select(d => new OrderDetailResponse
            {
                MenuItemId = d.MenuItemId,
                Name = menuItems[d.MenuItemId].Name,
                UnitPrice = d.UnitPrice,
                Quantity = d.Quantity,
                Subtotal = d.Subtotal,
                Notes = d.ItemNotes
            }).ToList(),
            StatusHistory = new List<OrderStatusHistoryResponse>
            {
                new()
                {
                    FromStatus = null,
                    ToStatus = order.Status,
                    ChangedBy = "OrderApi",
                    Notes = "Đơn hàng mới tạo",
                    Timestamp = order.CreatedAt
                }
            }
        };
    }
}
