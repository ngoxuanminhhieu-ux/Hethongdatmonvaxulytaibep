using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppointmentApi.Data.Entities;

[Table("doctors")]
public class Doctor
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("specialty")]
    public string Specialty { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("room_number")]
    public string RoomNumber { get; set; } = string.Empty;

    [MaxLength(20)]
    [Column("phone")]
    public string? Phone { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<TimeSlot> TimeSlots { get; set; } = new();
    public List<Appointment> Appointments { get; set; } = new();
}
