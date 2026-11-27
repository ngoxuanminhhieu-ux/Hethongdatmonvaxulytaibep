using Microsoft.EntityFrameworkCore;
using AppointmentApi.Data.Entities;

namespace AppointmentApi.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context, ILogger logger)
    {
        try
        {
            logger.LogInformation("Kiểm tra và khởi tạo CSDL Phòng khám ban đầu...");

            await context.Database.EnsureCreatedAsync();

            if (!await context.Doctors.AnyAsync())
            {
                logger.LogInformation("Nạp danh sách bác sĩ và phòng khám mẫu...");

                var doctors = new List<Doctor>
                {
                    new() { Name = "BS. CKII Nguyễn Văn Hùng", Specialty = "Tim mạch", RoomNumber = "Phòng 101", Phone = "0901112233", IsActive = true, CreatedAt = DateTime.UtcNow },
                    new() { Name = "ThS. BS Trần Mai Lan", Specialty = "Nhi khoa", RoomNumber = "Phòng 102", Phone = "0902223344", IsActive = true, CreatedAt = DateTime.UtcNow },
                    new() { Name = "BS. CKI Lê Quang Vinh", Specialty = "Tai Mũi Họng", RoomNumber = "Phòng 201", Phone = "0903334455", IsActive = true, CreatedAt = DateTime.UtcNow },
                    new() { Name = "BS. Phạm Thị Hương", Specialty = "Da liễu", RoomNumber = "Phòng 202", Phone = "0904445566", IsActive = true, CreatedAt = DateTime.UtcNow },
                    new() { Name = "ThS. BS Hoàng Tuấn Anh", Specialty = "Cơ Xương Khớp", RoomNumber = "Phòng 301", Phone = "0905556677", IsActive = true, CreatedAt = DateTime.UtcNow }
                };

                await context.Doctors.AddRangeAsync(doctors);
                await context.SaveChangesAsync();

                // Tạo khung giờ khám cho các bác sĩ trong 3 ngày tới
                var slots = new List<TimeSlot>();
                var baseDate = DateTime.Today;

                foreach (var doc in doctors)
                {
                    for (int day = 0; day < 3; day++)
                    {
                        var slotDate = baseDate.AddDays(day);
                        slots.Add(new TimeSlot { DoctorId = doc.Id, SlotDate = slotDate, StartTime = "08:00", EndTime = "09:00", MaxCapacity = 5, CurrentBookings = 0, IsAvailable = true });
                        slots.Add(new TimeSlot { DoctorId = doc.Id, SlotDate = slotDate, StartTime = "09:00", EndTime = "10:00", MaxCapacity = 5, CurrentBookings = 0, IsAvailable = true });
                        slots.Add(new TimeSlot { DoctorId = doc.Id, SlotDate = slotDate, StartTime = "10:00", EndTime = "11:00", MaxCapacity = 5, CurrentBookings = 0, IsAvailable = true });
                        slots.Add(new TimeSlot { DoctorId = doc.Id, SlotDate = slotDate, StartTime = "13:30", EndTime = "14:30", MaxCapacity = 5, CurrentBookings = 0, IsAvailable = true });
                        slots.Add(new TimeSlot { DoctorId = doc.Id, SlotDate = slotDate, StartTime = "14:30", EndTime = "15:30", MaxCapacity = 5, CurrentBookings = 0, IsAvailable = true });
                    }
                }

                await context.TimeSlots.AddRangeAsync(slots);
                await context.SaveChangesAsync();
                logger.LogInformation("Đã khởi tạo thành công {DocCount} bác sĩ và {SlotCount} khung giờ khám.", doctors.Count, slots.Count);
            }

            if (!await context.Patients.AnyAsync())
            {
                var patients = new List<Patient>
                {
                    new() { FullName = "Nguyễn Văn An", Phone = "0901234567", IdentityNumber = "079090001234", Gender = "Nam", CreatedAt = DateTime.UtcNow },
                    new() { FullName = "Trần Thị Bình", Phone = "0912345678", IdentityNumber = "079192005678", Gender = "Nữ", CreatedAt = DateTime.UtcNow },
                    new() { FullName = "Lê Hoàng Nam", Phone = "0987654321", IdentityNumber = "079201009988", Gender = "Nam", CreatedAt = DateTime.UtcNow }
                };

                await context.Patients.AddRangeAsync(patients);
                await context.SaveChangesAsync();
                logger.LogInformation("Đã nạp thành công {Count} bệnh nhân mẫu.", patients.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Chưa thể kết nối CSDL PostgreSQL để khởi tạo seed data.");
        }
    }
}
