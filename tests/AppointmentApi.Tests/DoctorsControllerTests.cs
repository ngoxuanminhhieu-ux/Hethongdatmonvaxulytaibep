using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using AppointmentApi.Controllers;
using AppointmentApi.Data.Entities;
using AppointmentApi.Models;
using Xunit;

namespace AppointmentApi.Tests;

public class DoctorsControllerTests : IDisposable
{
    private readonly Data.AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Mock<ILogger<DoctorsController>> _loggerMock;
    private readonly DoctorsController _controller;

    public DoctorsControllerTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateSqliteInMemoryDbContext();
        _loggerMock = new Mock<ILogger<DoctorsController>>();
        _controller = new DoctorsController(_context, _loggerMock.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var doctor1 = new Doctor
        {
            Id = 1,
            Name = "BS. CKII Nguyễn Văn A",
            Specialty = "Tim mạch",
            RoomNumber = "P.201",
            Phone = "0901234567",
            IsActive = true
        };

        var doctor2 = new Doctor
        {
            Id = 2,
            Name = "ThS. BS Trần Thị B",
            Specialty = "Nhi khoa",
            RoomNumber = "P.105",
            Phone = "0912345678",
            IsActive = true
        };

        var doctor3 = new Doctor
        {
            Id = 3,
            Name = "BS. Lê Văn C",
            Specialty = "Răng Hàm Mặt",
            RoomNumber = "P.302",
            Phone = "0987654321",
            IsActive = false // Inactive
        };

        _context.Doctors.AddRange(doctor1, doctor2, doctor3);

        var slot1 = new TimeSlot
        {
            Id = 1,
            DoctorId = 1,
            SlotDate = DateTime.UtcNow.Date,
            StartTime = "08:00",
            EndTime = "09:00",
            MaxCapacity = 5,
            CurrentBookings = 2,
            IsAvailable = true
        };

        var slot2 = new TimeSlot
        {
            Id = 2,
            DoctorId = 1,
            SlotDate = DateTime.UtcNow.Date,
            StartTime = "09:00",
            EndTime = "10:00",
            MaxCapacity = 5,
            CurrentBookings = 5,
            IsAvailable = false // Full
        };

        _context.TimeSlots.AddRange(slot1, slot2);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetDoctors_ReturnsOnlyActiveDoctors()
    {
        // Act
        var result = await _controller.GetDoctors();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var doctors = Assert.IsAssignableFrom<IEnumerable<DoctorResponse>>(okResult.Value);
        Assert.Equal(2, doctors.Count());
        Assert.DoesNotContain(doctors, d => d.Id == 3);
    }

    [Fact]
    public async Task GetDoctors_FilterBySpecialty_ReturnsMatchingDoctors()
    {
        // Act
        var result = await _controller.GetDoctors(specialty: "Tim mạch");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var doctors = Assert.IsAssignableFrom<IEnumerable<DoctorResponse>>(okResult.Value);
        var list = doctors.ToList();
        Assert.Single(list);
        Assert.Equal("Tim mạch", list[0].Specialty);
        Assert.Equal(1, list[0].Id);
    }

    [Fact]
    public async Task GetDoctorById_WhenExists_ReturnsDoctor()
    {
        // Act
        var result = await _controller.GetDoctorById(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var doctor = Assert.IsType<DoctorResponse>(okResult.Value);
        Assert.Equal("BS. CKII Nguyễn Văn A", doctor.Name);
    }

    [Fact]
    public async Task GetDoctorById_WhenNotFound_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetDoctorById(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetDoctorSlots_ReturnsAllSlotsForDoctor()
    {
        // Act
        var result = await _controller.GetDoctorSlots(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var slots = Assert.IsAssignableFrom<IEnumerable<TimeSlotResponse>>(okResult.Value);
        Assert.Equal(2, slots.Count());
    }

    [Fact]
    public async Task CreateDoctor_ValidData_AddsAndReturnsDoctor()
    {
        // Arrange
        var request = new CreateDoctorRequest
        {
            Name = "BS. Phạm Văn D",
            Specialty = "Da liễu",
            RoomNumber = "P.401",
            Phone = "0933333333"
        };

        // Act
        var result = await _controller.CreateDoctor(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var doctor = Assert.IsType<DoctorResponse>(createdResult.Value);
        Assert.Equal("BS. Phạm Văn D", doctor.Name);
        Assert.True(doctor.Id > 0);
    }
}
