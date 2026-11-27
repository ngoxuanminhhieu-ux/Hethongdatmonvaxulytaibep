using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppointmentApi.Data;
using AppointmentApi.Data.Entities;
using AppointmentApi.Models;

namespace AppointmentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DoctorsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<DoctorsController> _logger;

    public DoctorsController(AppDbContext context, ILogger<DoctorsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách bác sĩ / phòng khám, hỗ trợ lọc theo chuyên khoa hoặc từ khóa tìm kiếm.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DoctorResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DoctorResponse>>> GetDoctors(
        [FromQuery] string? specialty = null,
        [FromQuery] string? search = null)
    {
        var query = _context.Doctors.Where(d => d.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(specialty))
        {
            var spec = specialty.Trim();
            var specLower = spec.ToLower();
            query = query.Where(d => d.Specialty == spec || d.Specialty.ToLower() == specLower);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var sLower = s.ToLower();
            query = query.Where(d => d.Name.Contains(s) || d.Name.ToLower().Contains(sLower) ||
                                     d.Specialty.Contains(s) || d.Specialty.ToLower().Contains(sLower) ||
                                     d.RoomNumber.Contains(s));
        }

        var doctors = await query
            .OrderBy(d => d.RoomNumber)
            .ThenBy(d => d.Name)
            .Select(d => new DoctorResponse
            {
                Id = d.Id,
                Name = d.Name,
                Specialty = d.Specialty,
                RoomNumber = d.RoomNumber,
                Phone = d.Phone,
                IsActive = d.IsActive
            })
            .ToListAsync();

        return Ok(doctors);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một bác sĩ / phòng khám theo ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DoctorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DoctorResponse>> GetDoctorById(int id)
    {
        var doctor = await _context.Doctors.FindAsync(id);
        if (doctor == null)
        {
            return NotFound(new { message = $"Không tìm thấy bác sĩ với ID = {id}." });
        }

        return Ok(new DoctorResponse
        {
            Id = doctor.Id,
            Name = doctor.Name,
            Specialty = doctor.Specialty,
            RoomNumber = doctor.RoomNumber,
            Phone = doctor.Phone,
            IsActive = doctor.IsActive
        });
    }

    /// <summary>
    /// Tra cứu các khung giờ khám còn chỗ của bác sĩ theo ngày (Mặc định: Ngày hôm nay).
    /// </summary>
    [HttpGet("{id:int}/slots")]
    [ProducesResponseType(typeof(IEnumerable<TimeSlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TimeSlotResponse>>> GetDoctorSlots(
        int id,
        [FromQuery] DateTime? date = null)
    {
        var doctorExists = await _context.Doctors.AnyAsync(d => d.Id == id);
        if (!doctorExists)
        {
            return NotFound(new { message = $"Không tìm thấy bác sĩ với ID = {id}." });
        }

        var targetDate = (date ?? DateTime.Today).Date;

        var slots = await _context.TimeSlots
            .Where(t => t.DoctorId == id && t.SlotDate.Date == targetDate)
            .OrderBy(t => t.StartTime)
            .Select(t => new TimeSlotResponse
            {
                Id = t.Id,
                DoctorId = t.DoctorId,
                SlotDate = t.SlotDate.ToString("yyyy-MM-dd"),
                StartTime = t.StartTime,
                EndTime = t.EndTime,
                MaxCapacity = t.MaxCapacity,
                CurrentBookings = t.CurrentBookings,
                IsAvailable = t.IsAvailable && (t.CurrentBookings < t.MaxCapacity)
            })
            .ToListAsync();

        return Ok(slots);
    }

    /// <summary>
    /// Thêm bác sĩ mới vào hệ thống (Quản trị viên).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DoctorResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DoctorResponse>> CreateDoctor([FromBody] CreateDoctorRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var doctor = new Doctor
        {
            Name = request.Name.Trim(),
            Specialty = request.Specialty.Trim(),
            RoomNumber = request.RoomNumber.Trim(),
            Phone = request.Phone?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Doctors.Add(doctor);
        await _context.SaveChangesAsync();

        var response = new DoctorResponse
        {
            Id = doctor.Id,
            Name = doctor.Name,
            Specialty = doctor.Specialty,
            RoomNumber = doctor.RoomNumber,
            Phone = doctor.Phone,
            IsActive = doctor.IsActive
        };

        return CreatedAtAction(nameof(GetDoctorById), new { id = doctor.Id }, response);
    }
}
