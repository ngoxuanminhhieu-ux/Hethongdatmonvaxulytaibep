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

public class QueuesControllerTests : IDisposable
{
    private readonly Data.AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Mock<IRabbitMqProducer> _producerMock;
    private readonly Mock<ILogger<QueuesController>> _loggerMock;
    private readonly QueuesController _controller;

    public QueuesControllerTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateSqliteInMemoryDbContext();
        _producerMock = new Mock<IRabbitMqProducer>();
        _loggerMock = new Mock<ILogger<QueuesController>>();
        _controller = new QueuesController(_context, _producerMock.Object, _loggerMock.Object);

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
            MaxCapacity = 10,
            CurrentBookings = 2,
            IsAvailable = true
        };
        _context.TimeSlots.Add(slot);

        var patient1 = new Patient { Id = 1, FullName = "Bệnh nhân 1", Phone = "0900000001" };
        var patient2 = new Patient { Id = 2, FullName = "Bệnh nhân 2", Phone = "0900000002" };
        _context.Patients.AddRange(patient1, patient2);

        var appt1 = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = 1,
            DoctorId = 1,
            TimeSlotId = 1,
            QueueNumber = 1,
            Status = AppointmentStatus.Serving.ToString()
        };

        var appt2 = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = 2,
            DoctorId = 1,
            TimeSlotId = 1,
            QueueNumber = 2,
            Status = AppointmentStatus.Confirmed.ToString()
        };

        _context.Appointments.AddRange(appt1, appt2);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetQueueStatus_ReturnsCurrentServingAndWaitingList()
    {
        // Act
        var result = await _controller.GetQueueStatus(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var status = Assert.IsType<QueueStatusResponse>(okResult.Value);

        Assert.Equal(1, status.DoctorId);
        Assert.Equal(1, status.CurrentServingNumber);
        Assert.Equal("Bệnh nhân 1", status.CurrentPatientName);
        Assert.Equal(1, status.TotalWaiting);
        Assert.Single(status.WaitingList);
        Assert.Equal(2, status.WaitingList[0].QueueNumber);
    }

    [Fact]
    public async Task CallNext_WhenWaitingPatientExists_AdvancesQueueAndPublishesEvent()
    {
        // Act
        var result = await _controller.CallNext(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        Assert.Equal(2, doc.RootElement.GetProperty("currentServingNumber").GetInt32());
        Assert.Equal("Bệnh nhân 2", doc.RootElement.GetProperty("patientName").GetString());
        Assert.Equal(0, doc.RootElement.GetProperty("waitingRemaining").GetInt32());

        // Verify previous patient was marked Completed
        var prevAppt = _context.Appointments.First(a => a.QueueNumber == 1);
        Assert.Equal(AppointmentStatus.Completed.ToString(), prevAppt.Status);

        // Verify new patient is Serving
        var currAppt = _context.Appointments.First(a => a.QueueNumber == 2);
        Assert.Equal(AppointmentStatus.Serving.ToString(), currAppt.Status);

        // Verify RabbitMQ event published
        _producerMock.Verify(p => p.PublishQueueServingChanged(It.Is<QueueServingChangedEvent>(e =>
            e.Data.DoctorId == 1 &&
            e.Data.CurrentServingNumber == 2
        )), Times.Once);
    }

    [Fact]
    public async Task CompleteServing_WhenServingExists_MarksCompletedAndClearsServing()
    {
        // Act
        var result = await _controller.CompleteServing(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var prevAppt = _context.Appointments.First(a => a.QueueNumber == 1);
        Assert.Equal(AppointmentStatus.Completed.ToString(), prevAppt.Status);

        // Verify RabbitMQ event published
        _producerMock.Verify(p => p.PublishQueueServingChanged(It.Is<QueueServingChangedEvent>(e =>
            e.Data.DoctorId == 1 &&
            e.Data.CurrentServingNumber == null &&
            e.Data.Action == "CompleteServing"
        )), Times.Once);
    }

    [Fact]
    public async Task MarkNoShow_WhenServingExists_MarksNoShow()
    {
        // Act
        var result = await _controller.MarkNoShow(1, new NextQueueRequest { Notes = "Bệnh nhân không có mặt sau 3 lần gọi" });

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var prevAppt = _context.Appointments.First(a => a.QueueNumber == 1);
        Assert.Equal(AppointmentStatus.NoShow.ToString(), prevAppt.Status);

        // Verify RabbitMQ event published
        _producerMock.Verify(p => p.PublishQueueServingChanged(It.Is<QueueServingChangedEvent>(e =>
            e.Data.DoctorId == 1 &&
            e.Data.Action == "NoShow"
        )), Times.Once);
    }
}
