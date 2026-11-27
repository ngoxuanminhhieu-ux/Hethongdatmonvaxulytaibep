using Shared.Enums;

namespace Shared.Contracts;

public class QueueNumberAssignedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; }
    public string EventType { get; set; } = nameof(QueueNumberAssignedEvent);
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string SourceService { get; set; } = "QueueWorker";
    public QueueNumberAssignedData Data { get; set; } = new();
}

public class QueueNumberAssignedData
{
    public Guid AppointmentId { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public int QueueNumber { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;
    public string Message { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
