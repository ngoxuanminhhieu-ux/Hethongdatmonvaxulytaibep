using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppointmentApi.Data.Entities;

[Table("time_slots")]
public class TimeSlot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("doctor_id")]
    public int DoctorId { get; set; }

    [ForeignKey(nameof(DoctorId))]
    public Doctor? Doctor { get; set; }

    [Column("slot_date")]
    public DateTime SlotDate { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("start_time")]
    public string StartTime { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    [Column("end_time")]
    public string EndTime { get; set; } = string.Empty;

    [Column("max_capacity")]
    public int MaxCapacity { get; set; } = 5;

    [Column("current_bookings")]
    public int CurrentBookings { get; set; } = 0;

    [Column("is_available")]
    public bool IsAvailable { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Appointment> Appointments { get; set; } = new();
}
