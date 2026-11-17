using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderApi.Data;
using OrderApi.Data.Entities;

namespace OrderApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<MenuController> _logger;

    public MenuController(AppDbContext context, ILogger<MenuController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MenuItem>>> GetMenuItems([FromQuery] string? category = null)
    {
        _logger.LogInformation("Lấy danh sách thực đơn với bộ lọc category: {Category}", category ?? "Tất cả");

        var query = _context.MenuItems.Where(m => m.IsAvailable);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(m => m.Category == category);
        }

        var items = await query.OrderBy(m => m.Category).ThenBy(m => m.Name).ToListAsync();
        return Ok(items);
    }
}
