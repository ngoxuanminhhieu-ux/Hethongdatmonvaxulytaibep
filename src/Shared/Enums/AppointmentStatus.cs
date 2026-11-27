namespace Shared.Enums;

public enum AppointmentStatus
{
    Pending,     // Chờ cấp số thứ tự qua RabbitMQ
    Confirmed,   // Đã cấp số thứ tự, đang xếp hàng chờ khám
    Serving,     // Đang trong phòng khám được bác sĩ phục vụ
    Completed,   // Đã hoàn thành khám bệnh
    Cancelled,   // Bệnh nhân hoặc hệ thống hủy lịch
    NoShow       // Bệnh nhân vắng mặt khi được gọi số
}
