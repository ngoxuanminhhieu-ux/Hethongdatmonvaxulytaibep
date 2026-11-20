using System.ComponentModel.DataAnnotations;

namespace OrderApi.Models;

public class CreateMenuItemRequest
{
    [Required(ErrorMessage = "Tên món ăn không được để trống")]
    [MaxLength(150, ErrorMessage = "Tên món ăn tối đa 150 ký tự")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Đơn giá không được để trống")]
    [Range(0, 100000000, ErrorMessage = "Đơn giá phải lớn hơn hoặc bằng 0")]
    public decimal Price { get; set; }

    [MaxLength(500, ErrorMessage = "URL hình ảnh tối đa 500 ký tự")]
    public string? ImageUrl { get; set; }

    [Required(ErrorMessage = "Danh mục không được để trống")]
    [MaxLength(50, ErrorMessage = "Danh mục tối đa 50 ký tự")]
    public string Category { get; set; } = string.Empty;

    public bool IsAvailable { get; set; } = true;
}

public class UpdateMenuItemRequest
{
    [Required(ErrorMessage = "Tên món ăn không được để trống")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Đơn giá không được để trống")]
    [Range(0, 100000000)]
    public decimal Price { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    public bool IsAvailable { get; set; } = true;
}

public class MenuItemResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
}
