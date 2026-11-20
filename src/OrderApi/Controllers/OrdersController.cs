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
[Produces("application/json")]
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

    /// <summary>
    /// Tiếp nhận đơn đặt món mới từ khách hàng, kiểm tra hợp lệ, tính tiền và đẩy sự kiện OrderCreated vào RabbitMQ.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderResponse>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // 1. Kiểm tra danh sách món ăn đầu vào
        if (request.Items == null || !request.Items.Any())
        {
            return BadRequest(new { message = "Đơn hàng phải có ít nhất 1 món ăn." });
        }

        var itemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _context.MenuItems
            .Where(m => itemIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        // Kiểm tra món không tồn tại
        var missingIds = itemIds.Except(menuItems.Keys).ToList();
        if (missingIds.Any())
        {
            return BadRequest(new
            {
                message = $"Các món ăn với ID [{string.Join(", ", missingIds)}] không tồn tại trong thực đơn."
            });
        }

        // Kiểm tra món đã hết hàng / tạm ngưng phục vụ
        var unavailableItems = menuItems.Values
            .Where(m => !m.IsAvailable)
            .Select(m => m.Name)
            .ToList();

        if (unavailableItems.Any())
        {
            return BadRequest(new
            {
                message = $"Các món sau hiện đang tạm hết hoặc ngưng phục vụ: [{string.Join(", ", unavailableItems)}]. Vui lòng chọn món khác."
            });
        }

        // 2. Sử dụng Transaction CSDL để đảm bảo tính toàn vẹn ACID
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 2.1. Tìm hoặc tạo mới khách hàng
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Name.ToLower() == request.CustomerName.Trim().ToLower() &&
                                          c.TableNumber.ToLower() == request.TableNumber.Trim().ToLower());

            if (customer == null)
            {
                customer = new Customer
                {
                    Name = request.CustomerName.Trim(),
                    Phone = request.Phone?.Trim(),
                    TableNumber = request.TableNumber.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }
            else if (!string.IsNullOrEmpty(request.Phone) && customer.Phone != request.Phone.Trim())
            {
                // Cập nhật số điện thoại mới nếu có
                customer.Phone = request.Phone.Trim();
                await _context.SaveChangesAsync();
            }

            // 2.2. Khởi tạo đối tượng Order
            var orderId = Guid.NewGuid();
            var order = new Order
            {
                Id = orderId,
                CustomerId = customer.Id,
                TableNumber = request.TableNumber.Trim(),
                Status = OrderStatus.Pending.ToString(),
                Notes = request.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            decimal totalAmount = 0;
            var orderDetails = new List<OrderDetail>();
            var eventItems = new List<OrderItemData>();

            foreach (var reqItem in request.Items)
            {
                var menuItem = menuItems[reqItem.MenuItemId];
                var subtotal = menuItem.Price * reqItem.Quantity;
                totalAmount += subtotal;

                orderDetails.Add(new OrderDetail
                {
                    OrderId = order.Id,
                    MenuItemId = menuItem.Id,
                    UnitPrice = menuItem.Price,
                    Quantity = reqItem.Quantity,
                    Subtotal = subtotal,
                    ItemNotes = reqItem.Notes?.Trim()
                });

                eventItems.Add(new OrderItemData
                {
                    MenuItemId = menuItem.Id,
                    Name = menuItem.Name,
                    UnitPrice = menuItem.Price,
                    Quantity = reqItem.Quantity,
                    Notes = reqItem.Notes?.Trim()
                });
            }

            order.TotalAmount = totalAmount;
            order.OrderDetails = orderDetails;

            // 2.3. Ghi vết lịch sử trạng thái ban đầu: Pending
            order.StatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = null,
                ToStatus = OrderStatus.Pending.ToString(),
                ChangedBy = "OrderApi",
                Notes = "Đơn hàng mới được tạo thành công bởi khách hàng",
                CreatedAt = DateTime.UtcNow
            });

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Commit Transaction CSDL
            await transaction.CommitAsync();

            _logger.LogInformation("Đã lưu đơn hàng {OrderId} vào CSDL PostgreSQL (Tổng tiền: {Total} VND)", order.Id, totalAmount);

            // 3. Gửi sự kiện OrderCreated vào RabbitMQ
            var orderCreatedEvent = new OrderCreatedEvent
            {
                CorrelationId = order.Id,
                Timestamp = DateTime.UtcNow,
                SourceService = "OrderApi",
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
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Lỗi xảy ra trong quá trình tạo đơn hàng, đã Rollback Transaction.");
            throw;
        }
    }

    /// <summary>
    /// Lấy chi tiết đơn hàng theo Order ID kèm toàn bộ danh sách món và lịch sử trạng thái (Timeline).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
            return NotFound(new { message = $"Không tìm thấy đơn hàng với mã ID = {id}." });
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
                Name = od.MenuItem?.Name ?? $"Món #{od.MenuItemId}",
                UnitPrice = od.UnitPrice,
                Quantity = od.Quantity,
                Subtotal = od.Subtotal,
                Notes = od.ItemNotes
            }).ToList(),
            StatusHistory = order.StatusHistories
                .OrderBy(h => h.CreatedAt)
                .Select(h => new OrderStatusHistoryResponse
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

    /// <summary>
    /// Lấy danh sách các đơn hàng, hỗ trợ lọc theo trạng thái, số bàn, tìm kiếm tên khách và phân trang.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<OrderSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<OrderSummaryResponse>>> GetOrders(
        [FromQuery] OrderQueryParameters queryParams)
    {
        var query = _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.MenuItem)
            .AsQueryable();

        // Lọc theo trạng thái
        if (!string.IsNullOrWhiteSpace(queryParams.Status))
        {
            query = query.Where(o => o.Status.ToLower() == queryParams.Status.Trim().ToLower());
        }

        // Lọc theo số bàn
        if (!string.IsNullOrWhiteSpace(queryParams.TableNumber))
        {
            query = query.Where(o => o.TableNumber.ToLower() == queryParams.TableNumber.Trim().ToLower());
        }

        // Tìm kiếm theo tên khách hàng hoặc mã đơn
        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var search = queryParams.Search.Trim().ToLower();
            query = query.Where(o => (o.Customer != null && o.Customer.Name.ToLower().Contains(search)) ||
                                     o.Id.ToString().ToLower().Contains(search) ||
                                     o.TableNumber.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync();

        // Sắp xếp
        query = queryParams.SortBy?.ToLower() switch
        {
            "totalamount" => queryParams.SortOrder?.ToLower() == "asc"
                ? query.OrderBy(o => o.TotalAmount)
                : query.OrderByDescending(o => o.TotalAmount),
            "tablenumber" => queryParams.SortOrder?.ToLower() == "asc"
                ? query.OrderBy(o => o.TableNumber)
                : query.OrderByDescending(o => o.TableNumber),
            _ => queryParams.SortOrder?.ToLower() == "asc"
                ? query.OrderBy(o => o.CreatedAt)
                : query.OrderByDescending(o => o.CreatedAt)
        };

        var page = queryParams.Page <= 0 ? 1 : queryParams.Page;
        var pageSize = queryParams.PageSize <= 0 ? 20 : queryParams.PageSize;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderSummaryResponse
            {
                OrderId = o.Id,
                CustomerName = o.Customer != null ? o.Customer.Name : "Khách vãng lai",
                Phone = o.Customer != null ? o.Customer.Phone : null,
                TableNumber = o.TableNumber,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                Notes = o.Notes,
                ItemCount = o.OrderDetails.Count,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                Items = o.OrderDetails.Select(od => new OrderSummaryItemResponse
                {
                    MenuItemId = od.MenuItemId,
                    Name = od.MenuItem != null ? od.MenuItem.Name : "",
                    Quantity = od.Quantity,
                    Subtotal = od.Subtotal,
                    Notes = od.ItemNotes
                }).ToList()
            })
            .ToListAsync();

        return Ok(new PagedResponse<OrderSummaryResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        });
    }

    /// <summary>
    /// Cập nhật trạng thái đơn hàng (Dành cho Màn hình Bếp KDS hoặc Quản trị viên).
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var order = await _context.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new { message = $"Không tìm thấy đơn hàng với mã ID = {id}." });
        }

        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var newStatusEnum))
        {
            return BadRequest(new
            {
                message = $"Trạng thái '{request.Status}' không hợp lệ. Các trạng thái hợp lệ gồm: Pending, Preparing, Ready, Cancelled."
            });
        }

        var currentStatus = order.Status;

        // Quy tắc nghiệp vụ chuyển trạng thái
        if (currentStatus == OrderStatus.Ready.ToString() && newStatusEnum == OrderStatus.Pending)
        {
            return BadRequest(new { message = "Đơn hàng đã nấu xong (Ready), không thể chuyển ngược lại về Pending." });
        }

        if (currentStatus == OrderStatus.Cancelled.ToString())
        {
            return BadRequest(new { message = "Đơn hàng đã bị hủy, không thể thay đổi trạng thái nữa." });
        }

        order.Status = newStatusEnum.ToString();
        order.UpdatedAt = DateTime.UtcNow;

        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = currentStatus,
            ToStatus = newStatusEnum.ToString(),
            ChangedBy = request.ChangedBy.Trim(),
            Notes = request.Notes?.Trim() ?? $"Chuyển trạng thái sang {newStatusEnum}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation("Đơn hàng {OrderId} đổi trạng thái: {From} -> {To} bởi {Actor}",
            order.Id, currentStatus, newStatusEnum, request.ChangedBy);

        // Gửi sự kiện OrderStatusChanged lên RabbitMQ
        if (Enum.TryParse<OrderStatus>(currentStatus, out var prevStatusEnum))
        {
            _producer.PublishOrderStatusChanged(new OrderStatusChangedEvent
            {
                CorrelationId = order.Id,
                Timestamp = DateTime.UtcNow,
                SourceService = "OrderApi",
                Data = new OrderStatusChangedData
                {
                    OrderId = order.Id,
                    CustomerName = order.Customer?.Name ?? "",
                    TableNumber = order.TableNumber,
                    PreviousStatus = prevStatusEnum,
                    NewStatus = newStatusEnum,
                    Message = request.Notes ?? $"Đơn hàng chuyển sang trạng thái {newStatusEnum}",
                    ChangedBy = request.ChangedBy,
                    UpdatedAt = order.UpdatedAt
                }
            });
        }

        return Ok(new
        {
            orderId = order.Id,
            previousStatus = currentStatus,
            newStatus = order.Status,
            updatedAt = order.UpdatedAt,
            message = "Cập nhật trạng thái thành công."
        });
    }

    /// <summary>
    /// Hủy đơn hàng với lý do cụ thể.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelOrder(Guid id, [FromBody] CancelOrderRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var order = await _context.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new { message = $"Không tìm thấy đơn hàng với mã ID = {id}." });
        }

        if (order.Status == OrderStatus.Ready.ToString())
        {
            return BadRequest(new { message = "Món ăn đã nấu xong, không thể hủy đơn!" });
        }

        if (order.Status == OrderStatus.Cancelled.ToString())
        {
            return BadRequest(new { message = "Đơn hàng này đã bị hủy trước đó." });
        }

        var previousStatus = order.Status;
        order.Status = OrderStatus.Cancelled.ToString();
        order.UpdatedAt = DateTime.UtcNow;

        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = OrderStatus.Cancelled.ToString(),
            ChangedBy = request.CancelledBy.Trim(),
            Notes = $"Hủy đơn: {request.Reason.Trim()}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation("Đơn hàng {OrderId} bị hủy bởi {Actor}. Lý do: {Reason}",
            order.Id, request.CancelledBy, request.Reason);

        if (Enum.TryParse<OrderStatus>(previousStatus, out var prevStatusEnum))
        {
            _producer.PublishOrderStatusChanged(new OrderStatusChangedEvent
            {
                CorrelationId = order.Id,
                Timestamp = DateTime.UtcNow,
                SourceService = "OrderApi",
                Data = new OrderStatusChangedData
                {
                    OrderId = order.Id,
                    CustomerName = order.Customer?.Name ?? "",
                    TableNumber = order.TableNumber,
                    PreviousStatus = prevStatusEnum,
                    NewStatus = OrderStatus.Cancelled,
                    Message = $"Đơn hàng đã bị hủy. Lý do: {request.Reason}",
                    ChangedBy = request.CancelledBy,
                    UpdatedAt = order.UpdatedAt
                }
            });
        }

        return Ok(new
        {
            orderId = order.Id,
            status = OrderStatus.Cancelled.ToString(),
            reason = request.Reason,
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
                Name = menuItems.TryGetValue(d.MenuItemId, out var item) ? item.Name : $"Món #{d.MenuItemId}",
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
                    Notes = "Đơn hàng mới tạo bởi khách hàng",
                    Timestamp = order.CreatedAt
                }
            }
        };
    }
}
