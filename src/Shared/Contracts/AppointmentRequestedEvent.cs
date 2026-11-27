using Shared.Enums;

namespace Shared.Contracts;

public class AppointmentRequestedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; }
    public string EventType { get; set; } = nameof(AppointmentRequestedEvent);
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string SourceService { get; set; } = "AppointmentApi";
    public AppointmentRequestedData Data { get; set; } = new();
}

public class AppointmentRequestedData
{
    public Guid AppointmentId { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public int TimeSlotId { get; set; }
    public string SlotDate { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string? Symptoms { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
