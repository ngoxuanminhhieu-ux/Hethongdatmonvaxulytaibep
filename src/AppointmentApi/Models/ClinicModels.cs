using System.ComponentModel.DataAnnotations;

namespace AppointmentApi.Models;

public class DoctorResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
}

public class CreateDoctorRequest
{
    [Required(ErrorMessage = "Tên bác sĩ không được để trống")]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Chuyên khoa không được để trống")]
    [MaxLength(100)]
    public string Specialty { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số phòng khám không được để trống")]
    [MaxLength(20)]
    public string RoomNumber { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }
}

public class TimeSlotResponse
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public string SlotDate { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int MaxCapacity { get; set; }
    public int CurrentBookings { get; set; }
    public int AvailableSpots => Math.Max(0, MaxCapacity - CurrentBookings);
    public bool IsAvailable { get; set; }
}

public class CreateTimeSlotRequest
{
    [Required]
    public int DoctorId { get; set; }

    [Required]
    public DateTime SlotDate { get; set; }

    [Required]
    [MaxLength(10)]
    public string StartTime { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string EndTime { get; set; } = string.Empty;

    [Range(1, 50)]
    public int MaxCapacity { get; set; } = 5;
}

public class CreateAppointmentRequest
{
    [Required(ErrorMessage = "Họ tên bệnh nhân không được để trống")]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại không được để trống")]
    [MaxLength(20)]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? IdentityNumber { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(10)]
    public string? Gender { get; set; }

    [Required(ErrorMessage = "Bác sĩ khám là bắt buộc")]
    [Range(1, int.MaxValue, ErrorMessage = "Mã bác sĩ không hợp lệ")]
    public int DoctorId { get; set; }

    [Required(ErrorMessage = "Khung giờ khám là bắt buộc")]
    [Range(1, int.MaxValue, ErrorMessage = "Mã khung giờ không hợp lệ")]
    public int TimeSlotId { get; set; }

    public string? Symptoms { get; set; }
    public string? Notes { get; set; }
}

public class AppointmentResponse
{
    public Guid AppointmentId { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? IdentityNumber { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public int TimeSlotId { get; set; }
    public string SlotDate { get; set; } = string.Empty;
    public string SlotTime { get; set; } = string.Empty;
    public int? QueueNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Symptoms { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<QueueLogResponse> History { get; set; } = new();
}

public class QueueLogResponse
{
    public int QueueNumber { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime Timestamp { get; set; }
}

public class QueueStatusResponse
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public int? CurrentServingNumber { get; set; }
    public string? CurrentPatientName { get; set; }
    public Guid? CurrentAppointmentId { get; set; }
    public int TotalWaiting { get; set; }
    public int TotalCompleted { get; set; }
    public List<QueueItemResponse> WaitingList { get; set; } = new();
}

public class QueueItemResponse
{
    public Guid AppointmentId { get; set; }
    public int QueueNumber { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SlotTime { get; set; } = string.Empty;
    public DateTime BookingTime { get; set; }
}

public class NextQueueRequest
{
    [Required]
    public string Action { get; set; } = "CallNext"; // CallNext, Complete, NoShow

    [Required]
    public string ChangedBy { get; set; } = "Doctor";

    public string? Notes { get; set; }
}
