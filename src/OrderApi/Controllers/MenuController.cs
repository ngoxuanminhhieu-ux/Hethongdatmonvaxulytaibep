using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderApi.Data;
using OrderApi.Data.Entities;
using OrderApi.Models;

namespace OrderApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MenuController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<MenuController> _logger;

    public MenuController(AppDbContext context, ILogger<MenuController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách món ăn trong thực đơn, hỗ trợ lọc theo danh mục, từ khóa tìm kiếm và trạng thái phục vụ.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MenuItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<MenuItemResponse>>> GetMenuItems(
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] bool includeUnavailable = false)
    {
        _logger.LogInformation("Lấy danh mục món ăn. Category: {Category}, Search: {Search}, IncludeUnavailable: {IncludeUnavailable}",
            category ?? "Tất cả", search ?? "None", includeUnavailable);

        var query = _context.MenuItems.AsQueryable();

        if (!includeUnavailable)
        {
            query = query.Where(m => m.IsAvailable);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim();
            var catLower = cat.ToLower();
            query = query.Where(m => m.Category == cat || m.Category.ToLower() == catLower);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var sLower = s.ToLower();
            query = query.Where(m => m.Name.Contains(s) ||
                                     m.Name.ToLower().Contains(sLower) ||
                                     (m.Description != null && (m.Description.Contains(s) || m.Description.ToLower().Contains(sLower))));
        }

        var items = await query
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
            .Select(m => new MenuItemResponse
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                Price = m.Price,
                ImageUrl = m.ImageUrl,
                Category = m.Category,
                IsAvailable = m.IsAvailable,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// Lấy chi tiết một món ăn theo ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MenuItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemResponse>> GetMenuItemById(int id)
    {
        var item = await _context.MenuItems.FindAsync(id);
        if (item == null)
        {
            return NotFound(new { message = $"Không tìm thấy món ăn với ID = {id}." });
        }

        return Ok(new MenuItemResponse
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            Price = item.Price,
            ImageUrl = item.ImageUrl,
            Category = item.Category,
            IsAvailable = item.IsAvailable,
            CreatedAt = item.CreatedAt
        });
    }

    /// <summary>
    /// Thêm món ăn mới vào thực đơn (Dành cho Quản trị viên/Bếp).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MenuItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MenuItemResponse>> CreateMenuItem([FromBody] CreateMenuItemRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Kiểm tra xem tên món ăn đã tồn tại chưa
        var nameExists = await _context.MenuItems
            .AnyAsync(m => m.Name.ToLower() == request.Name.Trim().ToLower());

        if (nameExists)
        {
            return BadRequest(new { message = $"Món ăn mang tên '{request.Name}' đã tồn tại trong thực đơn." });
        }

        var menuItem = new MenuItem
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            ImageUrl = request.ImageUrl?.Trim(),
            Category = request.Category.Trim(),
            IsAvailable = request.IsAvailable,
            CreatedAt = DateTime.UtcNow
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Đã thêm món ăn mới: ID={Id}, Tên={Name}, Giá={Price}", menuItem.Id, menuItem.Name, menuItem.Price);

        var response = new MenuItemResponse
        {
            Id = menuItem.Id,
            Name = menuItem.Name,
            Description = menuItem.Description,
            Price = menuItem.Price,
            ImageUrl = menuItem.ImageUrl,
            Category = menuItem.Category,
            IsAvailable = menuItem.IsAvailable,
            CreatedAt = menuItem.CreatedAt
        };

        return CreatedAtAction(nameof(GetMenuItemById), new { id = menuItem.Id }, response);
    }

    /// <summary>
    /// Cập nhật thông tin món ăn (giá, tên, mô tả, trạng thái phục vụ).
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(MenuItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemResponse>> UpdateMenuItem(int id, [FromBody] UpdateMenuItemRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var menuItem = await _context.MenuItems.FindAsync(id);
        if (menuItem == null)
        {
            return NotFound(new { message = $"Không tìm thấy món ăn với ID = {id}." });
        }

        // Kiểm tra trùng tên với món khác
        var duplicateName = await _context.MenuItems
            .AnyAsync(m => m.Id != id && m.Name.ToLower() == request.Name.Trim().ToLower());

        if (duplicateName)
        {
            return BadRequest(new { message = $"Đã có món ăn khác mang tên '{request.Name}'." });
        }

        menuItem.Name = request.Name.Trim();
        menuItem.Description = request.Description?.Trim();
        menuItem.Price = request.Price;
        menuItem.ImageUrl = request.ImageUrl?.Trim();
        menuItem.Category = request.Category.Trim();
        menuItem.IsAvailable = request.IsAvailable;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Đã cập nhật món ăn ID={Id}: Tên={Name}, Giá={Price}, IsAvailable={Available}",
            menuItem.Id, menuItem.Name, menuItem.Price, menuItem.IsAvailable);

        return Ok(new MenuItemResponse
        {
            Id = menuItem.Id,
            Name = menuItem.Name,
            Description = menuItem.Description,
            Price = menuItem.Price,
            ImageUrl = menuItem.ImageUrl,
            Category = menuItem.Category,
            IsAvailable = menuItem.IsAvailable,
            CreatedAt = menuItem.CreatedAt
        });
    }

    /// <summary>
    /// Xóa hoặc ngừng phục vụ món ăn (bảo toàn tính toàn vẹn tham chiếu CSDL phân tán).
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMenuItem(int id)
    {
        var menuItem = await _context.MenuItems.FindAsync(id);
        if (menuItem == null)
        {
            return NotFound(new { message = $"Không tìm thấy món ăn với ID = {id}." });
        }

        // Kiểm tra xem món ăn đã từng được đặt trong đơn hàng nào chưa
        var isUsedInOrders = await _context.OrderDetails.AnyAsync(od => od.MenuItemId == id);
        if (isUsedInOrders)
        {
            // Bảo toàn tính toàn vẹn tham chiếu: chuyển sang ngừng phục vụ (Soft Delete)
            menuItem.IsAvailable = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Món ăn ID={Id} đã tồn tại trong lịch sử đơn hàng, chuyển trạng thái sang ngưng phục vụ (IsAvailable = false).", id);
            return Ok(new
            {
                message = $"Món '{menuItem.Name}' đã có trong lịch sử đơn hàng nên được chuyển sang trạng thái ngưng phục vụ (IsAvailable = false) để đảm bảo toàn vẹn dữ liệu.",
                isAvailable = false
            });
        }

        // Nếu chưa từng có đơn hàng nào dùng món này, xóa vĩnh viễn khỏi CSDL
        _context.MenuItems.Remove(menuItem);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Đã xóa vĩnh viễn món ăn ID={Id} khỏi thực đơn.", id);
        return Ok(new { message = $"Đã xóa thành công món ăn '{menuItem.Name}' khỏi thực đơn." });
    }

    /// <summary>
    /// Bật/Tắt nhanh trạng thái khả dụng của món ăn (Còn hàng / Tạm hết hàng).
    /// </summary>
    [HttpPatch("{id:int}/toggle-availability")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleAvailability(int id)
    {
        var item = await _context.MenuItems.FindAsync(id);
        if (item == null)
        {
            return NotFound(new { message = $"Không tìm thấy món ăn với ID = {id}." });
        }

        item.IsAvailable = !item.IsAvailable;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Món ăn ID={Id} đổi trạng thái IsAvailable thành {Status}", id, item.IsAvailable);
        return Ok(new
        {
            id = item.Id,
            name = item.Name,
            isAvailable = item.IsAvailable,
            message = item.IsAvailable ? "Món ăn đã được mở phục vụ trở lại." : "Món ăn đã được đánh dấu tạm hết."
        });
    }
}
