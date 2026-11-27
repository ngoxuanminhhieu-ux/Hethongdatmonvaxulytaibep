using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppointmentApi.Data.Entities;

[Table("queue_logs")]
public class QueueLog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("doctor_id")]
    public int DoctorId { get; set; }

    [ForeignKey(nameof(DoctorId))]
    public Doctor? Doctor { get; set; }

    [Column("appointment_id")]
    public Guid? AppointmentId { get; set; }

    [ForeignKey(nameof(AppointmentId))]
    public Appointment? Appointment { get; set; }

    [Column("queue_number")]
    public int QueueNumber { get; set; }

    [MaxLength(20)]
    [Column("from_status")]
    public string? FromStatus { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("to_status")]
    public string ToStatus { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("action")]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("changed_by")]
    public string ChangedBy { get; set; } = string.Empty;

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
