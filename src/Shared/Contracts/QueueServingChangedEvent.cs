namespace Shared.Contracts;

public class QueueServingChangedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; }
    public string EventType { get; set; } = nameof(QueueServingChangedEvent);
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string SourceService { get; set; } = "AppointmentApi";
    public QueueServingChangedData Data { get; set; } = new();
}

public class QueueServingChangedData
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public int? CurrentServingNumber { get; set; }
    public string? CurrentPatientName { get; set; }
    public Guid? CurrentAppointmentId { get; set; }
    public int WaitingCount { get; set; }
    public string Action { get; set; } = string.Empty; // CallNext, CompleteServing, Cancel, NoShow
    public string Message { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
