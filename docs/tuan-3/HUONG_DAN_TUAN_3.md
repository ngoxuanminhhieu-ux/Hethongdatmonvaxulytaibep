# HƯỚNG DẪN TRIỂN KHAI VÀ BÁO CÁO CÔNG VIỆC TUẦN 3
## Đề tài 09: Hệ thống Đặt lịch khám và Quản lý hàng đợi (Clinic Appointment & Queue Management)
**Mục tiêu tuần 3:** Xây dựng Appointment API hoàn chỉnh (Kết nối CSDL PostgreSQL, kiểm tra slot trống trong Transaction ACID, CRUD bác sĩ và khung giờ, đặt lịch khám, hủy lịch và kiểm thử với Postman).

---

## 1. Các công việc đã hoàn thành trong Tuần 3

### 1.1. Chức năng Quản lý Bác sĩ & Khung giờ khám (Doctor & TimeSlot Management)
Đã triển khai đầy đủ các endpoint tại `src/AppointmentApi/Controllers/DoctorsController.cs`:
- **`GET /api/doctors`**: Truy vấn danh sách bác sĩ đang hoạt động, hỗ trợ:
  - `specialty`: Lọc theo chuyên khoa (vd: `Tim mạch`, `Nhi khoa`, `Nội tổng quát`).
  - `search`: Tìm kiếm từ khóa theo tên bác sĩ, số phòng hoặc chuyên khoa.
- **`GET /api/doctors/{id}`**: Lấy chi tiết thông tin một bác sĩ.
- **`GET /api/doctors/{id}/slots`**: Lấy danh sách khung giờ khám của bác sĩ theo ngày (`date`), hiển thị số lượng chỗ còn trống (`availableSpots = maxCapacity - currentBookings`).
- **`POST /api/doctors`**: Thêm mới bác sĩ/phòng khám với validation: Tên bác sĩ không để trống, chuyên khoa bắt buộc, số phòng khám bắt buộc.

---

### 1.2. Chức năng Đặt lịch khám và Quản lý Hàng đợi (Appointment & Queue)
Đã triển khai các endpoint nghiệp vụ tại `src/AppointmentApi/Controllers/AppointmentsController.cs` và `QueuesController.cs`:
- **`POST /api/appointments`**:
  - **Kiểm tra dữ liệu đầu vào (Validation):**
    - Họ tên bệnh nhân không để trống.
    - Số điện thoại hợp lệ.
    - Bác sĩ và Khung giờ phải tồn tại trong CSDL.
    - Khung giờ phải còn chỗ trống (`current_bookings < max_capacity`). Nếu khung giờ đã hết chỗ $\to$ Từ chối và trả về lỗi 400 kèm thông báo rõ ràng.
  - **Bảo toàn giao dịch ACID:** Toàn bộ bước tạo/cập nhật hồ sơ bệnh nhân, tăng `current_bookings` của khung giờ, lưu lịch hẹn ở trạng thái `Pending` được bọc trong DB Transaction.
  - **Khởi tạo vết lịch sử:** Tự động ghi bản ghi đầu tiên vào `queue_logs` với trạng thái `Pending` do `Patient` tạo.
  - **Phát sự kiện bất đồng bộ:** Sau khi Commit DB thành công, phát sự kiện `AppointmentRequestedEvent` vào RabbitMQ Exchange `clinic.appointment.exchange` với routing key `appointment.requested`.
- **`DELETE /api/appointments/{id}`**:
  - Cho phép hủy lịch khám nếu lịch đang ở trạng thái `Pending` hoặc `Confirmed`.
  - Giảm `current_bookings` của khung giờ để trả lại chỗ trống cho bệnh nhân khác.
  - Cập nhật trạng thái thành `Cancelled` và ghi log kiểm toán.
- **`GET /api/appointments/{id}`**:
  - Trả về chi tiết lịch hẹn, thông tin bác sĩ, phòng khám, số thứ tự (nếu đã cấp) và toàn bộ lịch sử trạng thái (Timeline) từ `queue_logs`.
- **`GET /api/queues/{doctorId}`**:
  - Tra cứu trạng thái hàng đợi hiện tại của phòng khám: số thứ tự đang khám (`currentServingNumber`), tên bệnh nhân đang khám, và danh sách bệnh nhân đang chờ (`waitingList`).

---

## 2. Hướng dẫn chạy và kiểm thử

1. **Khởi chạy hạ tầng qua Docker Compose:**
   ```bash
   docker compose up -d
   ```
2. **Khởi chạy Appointment API:**
   ```bash
   dotnet run --project src/AppointmentApi/AppointmentApi.csproj
   ```
3. **Mở Swagger UI:**
   Truy cập `http://localhost:5001/swagger` để kiểm tra danh sách API trực quan.
4. **Kiểm thử bằng Postman:**
   Import file `postman/ClinicQueue_API_Collection.json` vào Postman và thực hiện kiểm thử tự động toàn bộ collection.
