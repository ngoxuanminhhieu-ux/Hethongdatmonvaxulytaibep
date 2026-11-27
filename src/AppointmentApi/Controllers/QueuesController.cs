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
public class QueuesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IRabbitMqProducer _producer;
    private readonly ILogger<QueuesController> _logger;

    public QueuesController(
        AppDbContext context,
        IRabbitMqProducer producer,
        ILogger<QueuesController> logger)
    {
        _context = context;
        _producer = producer;
        _logger = logger;
    }

    /// <summary>
    /// Tra cứu trạng thái hàng đợi hiện tại của một phòng khám / bác sĩ (Số đang phục vụ, danh sách đang chờ).
    /// </summary>
    [HttpGet("{doctorId:int}")]
    [ProducesResponseType(typeof(QueueStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QueueStatusResponse>> GetQueueStatus(int doctorId)
    {
        var doctor = await _context.Doctors.FindAsync(doctorId);
        if (doctor == null)
        {
            return NotFound(new { message = $"Không tìm thấy bác sĩ với ID = {doctorId}." });
        }

        // Bệnh nhân đang được phục vụ (Serving)
        var currentServing = await _context.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.DoctorId == doctorId &&
                                      a.Status == AppointmentStatus.Serving.ToString());

        // Danh sách bệnh nhân đang chờ khám (Confirmed)
        var waitingList = await _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.TimeSlot)
            .Where(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Confirmed.ToString())
            .OrderBy(a => a.QueueNumber)
            .Select(a => new QueueItemResponse
            {
                AppointmentId = a.Id,
                QueueNumber = a.QueueNumber ?? 0,
                PatientName = a.Patient != null ? a.Patient.FullName : "Bệnh nhân",
                Status = a.Status,
                SlotTime = a.TimeSlot != null ? $"{a.TimeSlot.StartTime} - {a.TimeSlot.EndTime}" : "",
                BookingTime = a.CreatedAt
            })
            .ToListAsync();

        var completedCount = await _context.Appointments
            .CountAsync(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Completed.ToString());

        return Ok(new QueueStatusResponse
        {
            DoctorId = doctor.Id,
            DoctorName = doctor.Name,
            Specialty = doctor.Specialty,
            RoomNumber = doctor.RoomNumber,
            CurrentServingNumber = currentServing?.QueueNumber,
            CurrentPatientName = currentServing?.Patient?.FullName,
            CurrentAppointmentId = currentServing?.Id,
            TotalWaiting = waitingList.Count,
            TotalCompleted = completedCount,
            WaitingList = waitingList
        });
    }

    /// <summary>
    /// Bác sĩ gọi số thứ tự tiếp theo vào phòng khám (Chuyển lượt phục vụ).
    /// </summary>
    [HttpPost("{doctorId:int}/next")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CallNext(int doctorId, [FromBody] NextQueueRequest? request = null)
    {
        var doctor = await _context.Doctors.FindAsync(doctorId);
        if (doctor == null)
        {
            return NotFound(new { message = $"Không tìm thấy bác sĩ với ID = {doctorId}." });
        }

        // 1. Nếu có người đang khám dở, tự động chuyển người đó sang Completed
        var currentServing = await _context.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Serving.ToString());

        if (currentServing != null)
        {
            currentServing.Status = AppointmentStatus.Completed.ToString();
            currentServing.UpdatedAt = DateTime.UtcNow;

            _context.QueueLogs.Add(new QueueLog
            {
                DoctorId = doctorId,
                AppointmentId = currentServing.Id,
                QueueNumber = currentServing.QueueNumber ?? 0,
                FromStatus = AppointmentStatus.Serving.ToString(),
                ToStatus = AppointmentStatus.Completed.ToString(),
                Action = "CompleteServing",
                ChangedBy = request?.ChangedBy ?? "Doctor",
                Notes = "Hoàn thành ca khám khi gọi số tiếp theo",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 2. Tìm bệnh nhân kế tiếp có số thứ tự nhỏ nhất đang ở trạng thái Confirmed
        var nextPatient = await _context.Appointments
            .Include(a => a.Patient)
            .Where(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Confirmed.ToString())
            .OrderBy(a => a.QueueNumber)
            .FirstOrDefaultAsync();

        if (nextPatient == null)
        {
            await _context.SaveChangesAsync();
            return Ok(new
            {
                message = "Hiện không còn bệnh nhân nào trong hàng đợi chờ khám.",
                currentServingNumber = (int?)null
            });
        }

        nextPatient.Status = AppointmentStatus.Serving.ToString();
        nextPatient.UpdatedAt = DateTime.UtcNow;

        _context.QueueLogs.Add(new QueueLog
        {
            DoctorId = doctorId,
            AppointmentId = nextPatient.Id,
            QueueNumber = nextPatient.QueueNumber ?? 0,
            FromStatus = AppointmentStatus.Confirmed.ToString(),
            ToStatus = AppointmentStatus.Serving.ToString(),
            Action = "CallNext",
            ChangedBy = request?.ChangedBy ?? "Doctor",
            Notes = request?.Notes ?? "Bác sĩ gọi số vào phòng khám",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var waitingCount = await _context.Appointments
            .CountAsync(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Confirmed.ToString());

        _logger.LogInformation("Phòng {Room} - Bác sĩ {Doc}: Gọi số #{Num} ({Patient})",
            doctor.RoomNumber, doctor.Name, nextPatient.QueueNumber, nextPatient.Patient?.FullName);

        // 3. Phát sự kiện realtime lên RabbitMQ
        _producer.PublishQueueServingChanged(new QueueServingChangedEvent
        {
            CorrelationId = nextPatient.Id,
            Data = new QueueServingChangedData
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Name,
                RoomNumber = doctor.RoomNumber,
                CurrentServingNumber = nextPatient.QueueNumber,
                CurrentPatientName = nextPatient.Patient?.FullName,
                CurrentAppointmentId = nextPatient.Id,
                WaitingCount = waitingCount,
                Action = "CallNext",
                Message = $"Mời số #{nextPatient.QueueNumber:D2} - Bệnh nhân {nextPatient.Patient?.FullName} vào {doctor.RoomNumber} ({doctor.Name})",
                ChangedBy = request?.ChangedBy ?? "Doctor",
                UpdatedAt = DateTime.UtcNow
            }
        });

        return Ok(new
        {
            message = $"Đã gọi số #{nextPatient.QueueNumber} vào phòng khám.",
            currentServingNumber = nextPatient.QueueNumber,
            patientName = nextPatient.Patient?.FullName,
            roomNumber = doctor.RoomNumber,
            waitingRemaining = waitingCount
        });
    }

    /// <summary>
    /// Bác sĩ hoàn thành ca khám hiện tại.
    /// </summary>
    [HttpPost("{doctorId:int}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteServing(int doctorId, [FromBody] NextQueueRequest? request = null)
    {
        var doctor = await _context.Doctors.FindAsync(doctorId);
        if (doctor == null)
        {
            return NotFound(new { message = $"Không tìm thấy bác sĩ với ID = {doctorId}." });
        }

        var currentServing = await _context.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Serving.ToString());

        if (currentServing == null)
        {
            return BadRequest(new { message = "Hiện không có ca khám nào đang phục vụ để hoàn thành." });
        }

        currentServing.Status = AppointmentStatus.Completed.ToString();
        currentServing.UpdatedAt = DateTime.UtcNow;

        _context.QueueLogs.Add(new QueueLog
        {
            DoctorId = doctorId,
            AppointmentId = currentServing.Id,
            QueueNumber = currentServing.QueueNumber ?? 0,
            FromStatus = AppointmentStatus.Serving.ToString(),
            ToStatus = AppointmentStatus.Completed.ToString(),
            Action = "CompleteServing",
            ChangedBy = request?.ChangedBy ?? "Doctor",
            Notes = request?.Notes ?? "Bác sĩ hoàn thành khám bệnh",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var waitingCount = await _context.Appointments
            .CountAsync(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Confirmed.ToString());

        _producer.PublishQueueServingChanged(new QueueServingChangedEvent
        {
            CorrelationId = currentServing.Id,
            Data = new QueueServingChangedData
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Name,
                RoomNumber = doctor.RoomNumber,
                CurrentServingNumber = null,
                CurrentPatientName = null,
                CurrentAppointmentId = null,
                WaitingCount = waitingCount,
                Action = "CompleteServing",
                Message = $"Ca khám số #{currentServing.QueueNumber} đã hoàn thành.",
                ChangedBy = request?.ChangedBy ?? "Doctor",
                UpdatedAt = DateTime.UtcNow
            }
        });

        return Ok(new
        {
            message = $"Ca khám số #{currentServing.QueueNumber} của bệnh nhân '{currentServing.Patient?.FullName}' đã hoàn thành.",
            waitingRemaining = waitingCount
        });
    }

    /// <summary>
    /// Bác sĩ báo bệnh nhân vắng mặt (NoShow).
    /// </summary>
    [HttpPost("{doctorId:int}/noshow")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkNoShow(int doctorId, [FromBody] NextQueueRequest? request = null)
    {
        var doctor = await _context.Doctors.FindAsync(doctorId);
        if (doctor == null)
        {
            return NotFound(new { message = $"Không tìm thấy bác sĩ với ID = {doctorId}." });
        }

        var currentServing = await _context.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Serving.ToString());

        if (currentServing == null)
        {
            return BadRequest(new { message = "Hiện không có ca khám nào đang phục vụ để báo vắng mặt." });
        }

        currentServing.Status = AppointmentStatus.NoShow.ToString();
        currentServing.UpdatedAt = DateTime.UtcNow;

        _context.QueueLogs.Add(new QueueLog
        {
            DoctorId = doctorId,
            AppointmentId = currentServing.Id,
            QueueNumber = currentServing.QueueNumber ?? 0,
            FromStatus = AppointmentStatus.Serving.ToString(),
            ToStatus = AppointmentStatus.NoShow.ToString(),
            Action = "NoShow",
            ChangedBy = request?.ChangedBy ?? "Doctor",
            Notes = request?.Notes ?? "Bệnh nhân không có mặt khi được gọi",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var waitingCount = await _context.Appointments
            .CountAsync(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Confirmed.ToString());

        _producer.PublishQueueServingChanged(new QueueServingChangedEvent
        {
            CorrelationId = currentServing.Id,
            Data = new QueueServingChangedData
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Name,
                RoomNumber = doctor.RoomNumber,
                CurrentServingNumber = null,
                CurrentPatientName = null,
                CurrentAppointmentId = null,
                WaitingCount = waitingCount,
                Action = "NoShow",
                Message = $"Số #{currentServing.QueueNumber} vắng mặt khi được gọi.",
                ChangedBy = request?.ChangedBy ?? "Doctor",
                UpdatedAt = DateTime.UtcNow
            }
        });

        return Ok(new
        {
            message = $"Đã đánh dấu số #{currentServing.QueueNumber} vắng mặt.",
            waitingRemaining = waitingCount
        });
    }
}
