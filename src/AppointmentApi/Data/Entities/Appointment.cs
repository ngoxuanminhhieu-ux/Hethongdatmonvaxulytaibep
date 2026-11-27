using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Enums;

namespace AppointmentApi.Data.Entities;

[Table("appointments")]
public class Appointment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("patient_id")]
    public int PatientId { get; set; }

    [ForeignKey(nameof(PatientId))]
    public Patient? Patient { get; set; }

    [Required]
    [Column("doctor_id")]
    public int DoctorId { get; set; }

    [ForeignKey(nameof(DoctorId))]
    public Doctor? Doctor { get; set; }

    [Required]
    [Column("time_slot_id")]
    public int TimeSlotId { get; set; }

    [ForeignKey(nameof(TimeSlotId))]
    public TimeSlot? TimeSlot { get; set; }

    [Column("queue_number")]
    public int? QueueNumber { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = AppointmentStatus.Pending.ToString();

    [Column("symptoms")]
    public string? Symptoms { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<QueueLog> QueueLogs { get; set; } = new();
}
