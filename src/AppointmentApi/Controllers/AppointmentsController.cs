using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppointmentApi.Data;
using AppointmentApi.Data.Entities;
using AppointmentApi.Models;
using AppointmentApi.Services;
using Shared.Contracts;
using Shared.Enums;

namespace AppointmentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AppointmentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IRabbitMqProducer _producer;
    private readonly ILogger<AppointmentsController> _logger;

    public AppointmentsController(
        AppDbContext context,
        IRabbitMqProducer producer,
        ILogger<AppointmentsController> logger)
    {
        _context = context;
        _producer = producer;
        _logger = logger;
    }

    /// <summary>
    /// Đặt lịch khám mới: Kiểm tra slot còn chỗ, tạo lịch hẹn và gửi yêu cầu cấp số thứ tự vào RabbitMQ.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AppointmentResponse>> CreateAppointment([FromBody] CreateAppointmentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // 1. Kiểm tra Bác sĩ
        var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == request.DoctorId && d.IsActive);
        if (doctor == null)
        {
            return BadRequest(new { message = $"Bác sĩ với ID = {request.DoctorId} không tồn tại hoặc tạm ngưng hoạt động." });
        }

        // 2. Kiểm tra Khung giờ (TimeSlot)
        var slot = await _context.TimeSlots.FirstOrDefaultAsync(t => t.Id == request.TimeSlotId && t.DoctorId == request.DoctorId);
        if (slot == null)
        {
            return BadRequest(new { message = $"Khung giờ ID = {request.TimeSlotId} không thuộc bác sĩ '{doctor.Name}' hoặc không tồn tại." });
        }

        if (!slot.IsAvailable || slot.CurrentBookings >= slot.MaxCapacity)
        {
            return BadRequest(new { message = $"Khung giờ {slot.StartTime} - {slot.EndTime} ngày {slot.SlotDate:dd/MM/yyyy} đã hết chỗ. Vui lòng chọn khung giờ khác." });
        }

        // 3. Sử dụng Transaction CSDL để đảm bảo tính toàn vẹn ACID
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 3.1. Tìm hoặc tạo mới bệnh nhân
            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.Phone == request.Phone.Trim());

            if (patient == null)
            {
                patient = new Patient
                {
                    FullName = request.FullName.Trim(),
                    Phone = request.Phone.Trim(),
                    IdentityNumber = request.IdentityNumber?.Trim(),
                    DateOfBirth = request.DateOfBirth,
                    Gender = request.Gender?.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Cập nhật tên mới nếu thay đổi
                patient.FullName = request.FullName.Trim();
                if (!string.IsNullOrEmpty(request.IdentityNumber)) patient.IdentityNumber = request.IdentityNumber.Trim();
                await _context.SaveChangesAsync();
            }

            // 3.2. Tạo lịch hẹn (Trạng thái ban đầu: Pending - Chờ cấp số thứ tự qua RabbitMQ)
            var appointmentId = Guid.NewGuid();
            var appointment = new Appointment
            {
                Id = appointmentId,
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                TimeSlotId = slot.Id,
                QueueNumber = null, // Sẽ được Queue Worker cấp bất đồng bộ
                Status = AppointmentStatus.Pending.ToString(),
                Symptoms = request.Symptoms?.Trim(),
                Notes = request.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // 3.3. Tăng số lượng đặt chỗ trong slot
            slot.CurrentBookings += 1;
            if (slot.CurrentBookings >= slot.MaxCapacity)
            {
                slot.IsAvailable = false;
            }

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("Đã lưu lịch hẹn {AppointmentId} của bệnh nhân '{PatientName}' với Bác sĩ '{DoctorName}'",
                appointment.Id, patient.FullName, doctor.Name);

            // 4. Phát sự kiện AppointmentRequestedEvent vào RabbitMQ để Queue Worker cấp số thứ tự
            var requestedEvent = new AppointmentRequestedEvent
            {
                CorrelationId = appointment.Id,
                Timestamp = DateTime.UtcNow,
                SourceService = "AppointmentApi",
                Data = new AppointmentRequestedData
                {
                    AppointmentId = appointment.Id,
                    PatientId = patient.Id,
                    PatientName = patient.FullName,
                    Phone = patient.Phone,
                    DoctorId = doctor.Id,
                    DoctorName = doctor.Name,
                    RoomNumber = doctor.RoomNumber,
                    TimeSlotId = slot.Id,
                    SlotDate = slot.SlotDate.ToString("yyyy-MM-dd"),
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    Symptoms = appointment.Symptoms,
                    CreatedAt = appointment.CreatedAt
                }
            };

            _producer.PublishAppointmentRequested(requestedEvent);

            var response = new AppointmentResponse
            {
                AppointmentId = appointment.Id,
                PatientId = patient.Id,
                PatientName = patient.FullName,
                Phone = patient.Phone,
                IdentityNumber = patient.IdentityNumber,
                DoctorId = doctor.Id,
                DoctorName = doctor.Name,
                Specialty = doctor.Specialty,
                RoomNumber = doctor.RoomNumber,
                TimeSlotId = slot.Id,
                SlotDate = slot.SlotDate.ToString("yyyy-MM-dd"),
                SlotTime = $"{slot.StartTime} - {slot.EndTime}",
                QueueNumber = appointment.QueueNumber,
                Status = appointment.Status,
                Symptoms = appointment.Symptoms,
                Notes = appointment.Notes,
                CreatedAt = appointment.CreatedAt,
                UpdatedAt = appointment.UpdatedAt
            };

            return CreatedAtAction(nameof(GetAppointmentById), new { id = appointment.Id }, response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Lỗi xảy ra khi đặt lịch khám, đã Rollback.");
            throw;
        }
    }

    /// <summary>
    /// Tra cứu chi tiết lịch hẹn theo ID kèm số thứ tự khám và nhật ký hàng đợi.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentResponse>> GetAppointmentById(Guid id)
    {
        var appointment = await _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Include(a => a.TimeSlot)
            .Include(a => a.QueueLogs)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment == null)
        {
            return NotFound(new { message = $"Không tìm thấy lịch hẹn với mã ID = {id}." });
        }

        var response = new AppointmentResponse
        {
            AppointmentId = appointment.Id,
            PatientId = appointment.PatientId,
            PatientName = appointment.Patient?.FullName ?? "Bệnh nhân",
            Phone = appointment.Patient?.Phone ?? "",
            IdentityNumber = appointment.Patient?.IdentityNumber,
            DoctorId = appointment.DoctorId,
            DoctorName = appointment.Doctor?.Name ?? "",
            Specialty = appointment.Doctor?.Specialty ?? "",
            RoomNumber = appointment.Doctor?.RoomNumber ?? "",
            TimeSlotId = appointment.TimeSlotId,
            SlotDate = appointment.TimeSlot?.SlotDate.ToString("yyyy-MM-dd") ?? "",
            SlotTime = $"{appointment.TimeSlot?.StartTime} - {appointment.TimeSlot?.EndTime}",
            QueueNumber = appointment.QueueNumber,
            Status = appointment.Status,
            Symptoms = appointment.Symptoms,
            Notes = appointment.Notes,
            CreatedAt = appointment.CreatedAt,
            UpdatedAt = appointment.UpdatedAt,
            History = appointment.QueueLogs
                .OrderBy(l => l.CreatedAt)
                .Select(l => new QueueLogResponse
                {
                    QueueNumber = l.QueueNumber,
                    FromStatus = l.FromStatus,
                    ToStatus = l.ToStatus,
                    Action = l.Action,
                    ChangedBy = l.ChangedBy,
                    Notes = l.Notes,
                    Timestamp = l.CreatedAt
                }).ToList()
        };

        return Ok(response);
    }

    /// <summary>
    /// Hủy lịch khám: Giải phóng slot khám, cập nhật trạng thái Cancelled và phát sự kiện cập nhật hàng đợi.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAppointment(Guid id, [FromQuery] string? reason = null)
    {
        var appointment = await _context.Appointments
            .Include(a => a.Doctor)
            .Include(a => a.TimeSlot)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment == null)
        {
            return NotFound(new { message = $"Không tìm thấy lịch hẹn với mã ID = {id}." });
        }

        if (appointment.Status == AppointmentStatus.Completed.ToString())
        {
            return BadRequest(new { message = "Lịch khám này đã hoàn thành, không thể hủy." });
        }

        if (appointment.Status == AppointmentStatus.Cancelled.ToString())
        {
            return BadRequest(new { message = "Lịch khám này đã bị hủy trước đó." });
        }

        var prevStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Cancelled.ToString();
        appointment.UpdatedAt = DateTime.UtcNow;

        // Giải phóng slot
        if (appointment.TimeSlot != null)
        {
            appointment.TimeSlot.CurrentBookings = Math.Max(0, appointment.TimeSlot.CurrentBookings - 1);
            appointment.TimeSlot.IsAvailable = true;
        }

        // Ghi log hàng đợi
        _context.QueueLogs.Add(new QueueLog
        {
            DoctorId = appointment.DoctorId,
            AppointmentId = appointment.Id,
            QueueNumber = appointment.QueueNumber ?? 0,
            FromStatus = prevStatus,
            ToStatus = AppointmentStatus.Cancelled.ToString(),
            Action = "CancelAppointment",
            ChangedBy = "Patient/System",
            Notes = reason ?? "Bệnh nhân yêu cầu hủy lịch",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation("Lịch hẹn {Id} đã được hủy thành công.", id);

        // Phát sự kiện cập nhật hàng đợi
        var remainingWaiting = await _context.Appointments
            .CountAsync(a => a.DoctorId == appointment.DoctorId &&
                             a.Status == AppointmentStatus.Confirmed.ToString());

        _producer.PublishQueueServingChanged(new QueueServingChangedEvent
        {
            CorrelationId = appointment.Id,
            Data = new QueueServingChangedData
            {
                DoctorId = appointment.DoctorId,
                DoctorName = appointment.Doctor?.Name ?? "",
                RoomNumber = appointment.Doctor?.RoomNumber ?? "",
                CurrentAppointmentId = null,
                CurrentServingNumber = null,
                WaitingCount = remainingWaiting,
                Action = "CancelAppointment",
                Message = $"Lịch khám mã {appointment.Id.ToString()[..8]} đã bị hủy.",
                ChangedBy = "Patient/System",
                UpdatedAt = DateTime.UtcNow
            }
        });

        return Ok(new
        {
            appointmentId = appointment.Id,
            status = AppointmentStatus.Cancelled.ToString(),
            message = "Hủy lịch khám thành công và đã giải phóng khung giờ."
        });
    }

    /// <summary>
    /// Lấy danh sách lịch khám (lọc theo Bác sĩ, Trạng thái, Ngày khám).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppointmentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppointmentResponse>>> GetAppointments(
        [FromQuery] int? doctorId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? date = null)
    {
        var query = _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Include(a => a.TimeSlot)
            .AsQueryable();

        if (doctorId.HasValue)
        {
            query = query.Where(a => a.DoctorId == doctorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status.ToLower() == status.Trim().ToLower());
        }

        if (date.HasValue)
        {
            var targetDate = date.Value.Date;
            query = query.Where(a => a.TimeSlot != null && a.TimeSlot.SlotDate.Date == targetDate);
        }

        var list = await query
            .OrderBy(a => a.QueueNumber ?? 9999)
            .ThenBy(a => a.CreatedAt)
            .Select(a => new AppointmentResponse
            {
                AppointmentId = a.Id,
                PatientId = a.PatientId,
                PatientName = a.Patient != null ? a.Patient.FullName : "Bệnh nhân",
                Phone = a.Patient != null ? a.Patient.Phone : "",
                IdentityNumber = a.Patient != null ? a.Patient.IdentityNumber : null,
                DoctorId = a.DoctorId,
                DoctorName = a.Doctor != null ? a.Doctor.Name : "",
                Specialty = a.Doctor != null ? a.Doctor.Specialty : "",
                RoomNumber = a.Doctor != null ? a.Doctor.RoomNumber : "",
                TimeSlotId = a.TimeSlotId,
                SlotDate = a.TimeSlot != null ? a.TimeSlot.SlotDate.ToString("yyyy-MM-dd") : "",
                SlotTime = a.TimeSlot != null ? $"{a.TimeSlot.StartTime} - {a.TimeSlot.EndTime}" : "",
                QueueNumber = a.QueueNumber,
                Status = a.Status,
                Symptoms = a.Symptoms,
                Notes = a.Notes,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            })
            .ToListAsync();

        return Ok(list);
    }
}
