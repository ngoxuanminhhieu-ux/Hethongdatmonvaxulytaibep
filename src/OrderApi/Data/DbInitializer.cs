using Microsoft.EntityFrameworkCore;
using OrderApi.Data.Entities;

namespace OrderApi.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context, ILogger logger)
    {
        try
        {
            logger.LogInformation("Kiểm tra và khởi tạo dữ liệu CSDL ban đầu...");

            // Tự động kiểm tra tạo bảng nếu chưa có
            await context.Database.EnsureCreatedAsync();

            // Nạp dữ liệu món ăn mẫu nếu bảng menu_items chưa có dữ liệu
            if (!await context.MenuItems.AnyAsync())
            {
                logger.LogInformation("Bảng menu_items trống, tiến hành nạp danh mục thực đơn mẫu...");

                var defaultItems = new List<MenuItem>
                {
                    new()
                    {
                        Name = "Phở Bò Tái Nạm",
                        Description = "Bánh phở tươi, thịt bò tái nạm mềm thơm cùng nước dùng hầm xương 24h đậm đà gia truyền",
                        Price = 65000,
                        ImageUrl = "https://images.unsplash.com/photo-1582878826629-29b7ad1cdc43?w=500",
                        Category = "Món chính",
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "Cơm Tấm Sườn Bì Chả",
                        Description = "Cơm tấm thơm nóng dẻo, sườn nướng mật ong đậm đà, bì thơm và chả trứng hấp dẫn",
                        Price = 60000,
                        ImageUrl = "https://images.unsplash.com/photo-1544025162-d76694265947?w=500",
                        Category = "Món chính",
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "Bún Chả Hà Nội",
                        Description = "Bún tươi, chả miếng nướng than hoa thơm lừng kèm bát nước mắm chua ngọt và rau sống",
                        Price = 55000,
                        ImageUrl = "https://images.unsplash.com/photo-1569058242253-92a9c755a0ec?w=500",
                        Category = "Món chính",
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "Bánh Mì Thịt Nướng Đặc Biệt",
                        Description = "Bánh mì giòn rụm, thịt nướng than hoa, pa-tê béo ngậy, đồ chua và sốt đặc biệt",
                        Price = 35000,
                        ImageUrl = "https://images.unsplash.com/photo-1626804475297-41608ea09aeb?w=500",
                        Category = "Món chính",
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "Khoai Tây Chiên Giòn",
                        Description = "Khoai tây cắt thanh chiên giòn rụm chấm kèm sốt tương cà hoặc sốt phô mai thơm ngậy",
                        Price = 30000,
                        ImageUrl = "https://images.unsplash.com/photo-1576107232684-1279f3908594?w=500",
                        Category = "Khai vị",
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "Cà Phê Sữa Đá Sài Gòn",
                        Description = "Cà phê nguyên chất pha phin truyền thống hòa quyện cùng sữa đặc béo ngậy và đá lạnh",
                        Price = 25000,
                        ImageUrl = "https://images.unsplash.com/photo-1517701550927-30cf4ba1dba5?w=500",
                        Category = "Đồ uống",
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "Trà Đào Cam Sả",
                        Description = "Trà thanh mát kết hợp vị cam thơm dịu, sả cay nhẹ và miếng đào giòn ngọt",
                        Price = 35000,
                        ImageUrl = "https://images.unsplash.com/photo-1556881286-fc6915169721?w=500",
                        Category = "Đồ uống",
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new()
                    {
                        Name = "Bánh Flan Caramel",
                        Description = "Bánh flan mềm mịn, thơm mùi trứng sữa cùng sốt caramel đắng nhẹ thơm ngon",
                        Price = 20000,
                        ImageUrl = "https://images.unsplash.com/photo-1528975604071-b4dc52a2d18c?w=500",
                        Category = "Tráng miệng",
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow
                    }
                };

                await context.MenuItems.AddRangeAsync(defaultItems);
                await context.SaveChangesAsync();
                logger.LogInformation("Đã khởi tạo thành công {Count} món ăn mẫu vào CSDL.", defaultItems.Count);
            }

            // Nạp khách hàng mẫu nếu chưa có
            if (!await context.Customers.AnyAsync())
            {
                var defaultCustomers = new List<Customer>
                {
                    new() { Name = "Nguyễn Văn An", Phone = "0901234567", TableNumber = "Bàn 01", CreatedAt = DateTime.UtcNow },
                    new() { Name = "Trần Thị Bình", Phone = "0912345678", TableNumber = "Bàn 02", CreatedAt = DateTime.UtcNow },
                    new() { Name = "Lê Hoàng Nam", Phone = "0987654321", TableNumber = "Bàn 05", CreatedAt = DateTime.UtcNow }
                };

                await context.Customers.AddRangeAsync(defaultCustomers);
                await context.SaveChangesAsync();
                logger.LogInformation("Đã khởi tạo thành công {Count} khách hàng mẫu vào CSDL.", defaultCustomers.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Chưa thể kết nối CSDL PostgreSQL để khởi tạo seed data (CSDL có thể chưa chạy hoặc chưa sẵn sàng). Sẽ thử lại khi có request.");
        }
    }
}
