using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using AppointmentApi.Controllers;
using AppointmentApi.Data.Entities;
using AppointmentApi.Models;
using AppointmentApi.Services;
using Shared.Contracts;
using Shared.Enums;
using Xunit;

namespace AppointmentApi.Tests;

public class AppointmentsControllerTests : IDisposable
{
    private readonly Data.AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Mock<IRabbitMqProducer> _producerMock;
    private readonly Mock<ILogger<AppointmentsController>> _loggerMock;
    private readonly AppointmentsController _controller;

    public AppointmentsControllerTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateSqliteInMemoryDbContext();
        _producerMock = new Mock<IRabbitMqProducer>();
        _loggerMock = new Mock<ILogger<AppointmentsController>>();
        _controller = new AppointmentsController(_context, _producerMock.Object, _loggerMock.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var doctor = new Doctor
        {
            Id = 1,
            Name = "BS. CKII Nguyễn Văn A",
            Specialty = "Tim mạch",
            RoomNumber = "P.201",
            Phone = "0901234567",
            IsActive = true
        };
        _context.Doctors.Add(doctor);

        var slot = new TimeSlot
        {
            Id = 1,
            DoctorId = 1,
            SlotDate = DateTime.UtcNow.Date,
            StartTime = "08:00",
            EndTime = "09:00",
            MaxCapacity = 5,
            CurrentBookings = 0,
            IsAvailable = true
        };
        _context.TimeSlots.Add(slot);

        var fullSlot = new TimeSlot
        {
            Id = 2,
            DoctorId = 1,
            SlotDate = DateTime.UtcNow.Date,
            StartTime = "09:00",
            EndTime = "10:00",
            MaxCapacity = 2,
            CurrentBookings = 2,
            IsAvailable = false
        };
        _context.TimeSlots.Add(fullSlot);

        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateAppointment_ValidRequest_CreatesAppointmentAndPublishesEvent()
    {
        // Arrange
        _producerMock.Setup(p => p.PublishAppointmentRequested(It.IsAny<AppointmentRequestedEvent>()))
                     .Returns(true);

        var request = new CreateAppointmentRequest
        {
            FullName = "Nguyễn Văn Bệnh Nhân",
            Phone = "0988888888",
            IdentityNumber = "001200001234",
            DoctorId = 1,
            TimeSlotId = 1,
            Symptoms = "Đau tức ngực trái khi gắng sức",
            Notes = "Tiền sử cao huyết áp"
        };

        // Act
        var result = await _controller.CreateAppointment(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var apptResponse = Assert.IsType<AppointmentResponse>(createdResult.Value);

        Assert.Equal("Nguyễn Văn Bệnh Nhân", apptResponse.PatientName);
        Assert.Equal(AppointmentStatus.Pending.ToString(), apptResponse.Status);

        // Verify slot current bookings incremented
        var slot = await _context.TimeSlots.FindAsync(1);
        Assert.NotNull(slot);
        Assert.Equal(1, slot.CurrentBookings);

        // Verify RabbitMQ publish was called
        _producerMock.Verify(p => p.PublishAppointmentRequested(It.Is<AppointmentRequestedEvent>(e =>
            e.Data.AppointmentId == apptResponse.AppointmentId &&
            e.Data.DoctorId == 1 &&
            e.Data.PatientName == "Nguyễn Văn Bệnh Nhân"
        )), Times.Once);
    }

    [Fact]
    public async Task CreateAppointment_WhenSlotIsFull_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateAppointmentRequest
        {
            FullName = "Trần Thị C",
            Phone = "0977777777",
            DoctorId = 1,
            TimeSlotId = 2, // Full slot
            Symptoms = "Khám sức khỏe tổng quát"
        };

        // Act
        var result = await _controller.CreateAppointment(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task CancelAppointment_WhenPendingOrConfirmed_CancelsAndReleasesSlot()
    {
        // Arrange
        var patient = new Patient
        {
            FullName = "Lê Văn D",
            Phone = "0966666666"
        };
        _context.Patients.Add(patient);
        _context.SaveChanges();

        var apptId = Guid.NewGuid();
        var appointment = new Appointment
        {
            Id = apptId,
            PatientId = patient.Id,
            DoctorId = 1,
            TimeSlotId = 1,
            QueueNumber = 1,
            Status = AppointmentStatus.Confirmed.ToString()
        };
        _context.Appointments.Add(appointment);

        // Update slot booking count
        var slot = await _context.TimeSlots.FindAsync(1);
        slot!.CurrentBookings = 1;
        _context.SaveChanges();

        // Act
        var result = await _controller.CancelAppointment(apptId, "Bệnh nhân bận việc đột xuất");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updatedAppt = await _context.Appointments.FindAsync(apptId);
        Assert.NotNull(updatedAppt);
        Assert.Equal(AppointmentStatus.Cancelled.ToString(), updatedAppt.Status);

        // Slot current bookings should be decremented
        Assert.Equal(0, slot.CurrentBookings);

        // Event should be published
        _producerMock.Verify(p => p.PublishQueueServingChanged(It.IsAny<QueueServingChangedEvent>()), Times.Once);
    }

    [Fact]
    public async Task GetAppointmentById_WhenExists_ReturnsDetailsWithHistory()
    {
        // Arrange
        var patient = new Patient
        {
            FullName = "Hoàng Văn E",
            Phone = "0955555555"
        };
        _context.Patients.Add(patient);
        _context.SaveChanges();

        var apptId = Guid.NewGuid();
        var appointment = new Appointment
        {
            Id = apptId,
            PatientId = patient.Id,
            DoctorId = 1,
            TimeSlotId = 1,
            QueueNumber = 5,
            Status = AppointmentStatus.Confirmed.ToString(),
            Symptoms = "Ho kéo dài"
        };
        _context.Appointments.Add(appointment);
        _context.SaveChanges();

        // Act
        var result = await _controller.GetAppointmentById(apptId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var res = Assert.IsType<AppointmentResponse>(okResult.Value);
        Assert.Equal(apptId, res.AppointmentId);
        Assert.Equal("Hoàng Văn E", res.PatientName);
        Assert.Equal(5, res.QueueNumber);
    }
}
