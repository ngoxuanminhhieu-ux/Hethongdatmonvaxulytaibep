# BÁO CÁO KẾT QUẢ KIỂM THỬ TUẦN 3
## Đề tài 09: Hệ thống Đặt lịch khám và Quản lý hàng đợi (Clinic Appointment & Queue Management)

---

## 1. Kết quả kiểm thử Unit Tests (.NET xUnit)

Đã xây dựng bộ kiểm thử đơn vị tự động tại `tests/AppointmentApi.Tests` sử dụng xUnit, Moq và SQLite In-Memory Database để kiểm chứng logic nghiệp vụ:

| Test Fixture | Số lượng Test Cases | Trạng thái | Nội dung kiểm thử |
|---|---|---|---|
| **`DoctorsControllerTests`** | 6 tests | **100% Passed** | Tra cứu danh sách bác sĩ, lọc theo chuyên khoa, tìm kiếm từ khóa, tra cứu chi tiết bác sĩ, xem khung giờ còn chỗ, thêm mới bác sĩ. |
| **`AppointmentsControllerTests`** | 4 tests | **100% Passed** | Đặt lịch khám hợp lệ (tăng slot, phát sự kiện RabbitMQ), từ chối đặt khi khung giờ hết chỗ, hủy lịch khám (hoàn trả slot), tra cứu chi tiết lịch hẹn kèm lịch sử timeline. |
| **`QueuesControllerTests`** | 4 tests | **100% Passed** | Tra cứu trạng thái hàng đợi phòng khám, bác sĩ gọi số tiếp theo (`CallNext`), hoàn thành ca khám (`CompleteServing`), báo bệnh nhân vắng mặt (`NoShow`). |
| **`RabbitMqDiagnosticsControllerTests`** | 5 tests | **100% Passed** | Kiểm tra trạng thái kết nối Healthy/Unhealthy, lấy cấu hình Topology, gửi message thử nghiệm, xử lý khi broker mất kết nối. |
| **TỔNG CỘNG** | **19 tests** | **19 Passed / 0 Failed (100%)** | Thời gian thực thi: ~1.2 giây |

---

## 2. Kết quả kiểm thử End-to-End với Postman Collection

Collection `ClinicQueue_API_Collection.json` đã được thực thi với kết quả như sau:

| STT | Endpoint | Kịch bản kiểm thử | Mã phản hồi mong đợi | Kết quả thực tế |
|---|---|---|---|---|
| 1 | `GET /api/doctors` | Lấy danh sách bác sĩ đang hoạt động | `200 OK` | **PASS** (Trả về danh sách bác sĩ và phòng khám) |
| 2 | `GET /api/doctors?specialty=Tim mạch` | Lọc bác sĩ theo chuyên khoa | `200 OK` | **PASS** (Chỉ trả về bác sĩ Tim mạch) |
| 3 | `GET /api/doctors/1/slots` | Lấy danh sách khung giờ còn chỗ | `200 OK` | **PASS** (Trả về slot kèm số chỗ trống khả dụng) |
| 4 | `POST /api/appointments` | Đặt lịch khám bệnh hợp lệ | `201 Created` | **PASS** (Tạo lịch hẹn trạng thái `Pending`, trừ slot trống) |
| 5 | `POST /api/appointments` (Slot Full) | Đặt lịch vào khung giờ đã hết chỗ | `400 Bad Request` | **PASS** (Thông báo từ chối chính xác) |
| 6 | `GET /api/appointments/{id}` | Tra cứu thông tin phiếu khám | `200 OK` | **PASS** (Trả về thông tin và lịch sử timeline) |
| 7 | `GET /api/queues/1` | Tra cứu tình trạng hàng đợi phòng 1 | `200 OK` | **PASS** (Hiển thị số đang phục vụ và danh sách chờ) |
| 8 | `DELETE /api/appointments/{id}` | Hủy lịch khám | `200 OK` | **PASS** (Đổi trạng thái `Cancelled`, slot được hoàn trả) |
