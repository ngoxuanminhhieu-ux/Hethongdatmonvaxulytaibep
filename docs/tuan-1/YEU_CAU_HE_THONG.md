# TÀI LIỆU YÊU CẦU HỆ THỐNG (SYSTEM REQUIREMENTS)
## Đề tài 09: Hệ thống Đặt lịch khám và Quản lý hàng đợi (Clinic Appointment & Queue Management System)

---

## 1. Mục tiêu và phạm vi bài toán

### 1.1. Mục tiêu
Xây dựng một hệ thống phân tán phục vụ phòng khám/bệnh viện cho phép bệnh nhân đăng ký lịch khám, nhận số thứ tự tự động và theo dõi tiến độ hàng đợi theo thời gian thực. Hệ thống giúp giảm thiểu thời gian chờ đợi trực tiếp tại phòng khám, tự động hóa khâu cấp số thông qua Message Queue và cập nhật trạng thái lượt khám qua SignalR.

### 1.2. Phạm vi hệ thống
- Quản lý danh mục bác sĩ, chuyên khoa, phòng khám và các khung giờ khám bệnh (Time Slots).
- Tiếp nhận yêu cầu đặt lịch khám và kiểm soát công suất tối đa của từng khung giờ.
- Xử lý cấp số thứ tự bất đồng bộ qua RabbitMQ (tránh tranh chấp, tắc nghẽn).
- Điều phối hàng đợi tại phòng khám: gọi số tiếp theo, hoàn thành ca khám, báo vắng mặt.
- Thông báo thời gian thực trạng thái hàng đợi và lượt khám đến người dùng qua WebSockets/SignalR.

---

## 2. Yêu cầu chức năng (Functional Requirements - FR)

### FR-01: Tra cứu Bác sĩ & Khung giờ khám
- Hệ thống phải cung cấp danh sách bác sĩ đang hoạt động, lọc theo chuyên khoa (Nội tổng quát, Tim mạch, Nhi khoa, v.v.).
- Hiển thị các khung giờ khám còn chỗ trống (`is_available = true` và `current_bookings < max_capacity`).

### FR-02: Đặt lịch khám bệnh
- Người dùng nhập thông tin: Họ tên, Số điện thoại, CCCD, Bác sĩ, Khung giờ, Triệu chứng.
- Hệ thống kiểm tra tính hợp lệ và slot còn chỗ trong một Transaction.
- Hệ thống tạo lịch hẹn ở trạng thái `Pending` và đưa sự kiện yêu cầu cấp số vào RabbitMQ.

### FR-03: Cấp số thứ tự khám bất đồng bộ
- Queue Worker tiêu thụ thông điệp từ RabbitMQ.
- Sinh số thứ tự tuần tự tăng dần theo từng Bác sĩ / Phòng khám trong ngày khám.
- Chuyển trạng thái lịch hẹn từ `Pending` $\to$ `Confirmed`.
- Lưu vết kiểm toán sự thay đổi trạng thái vào `queue_logs`.

### FR-04: Điều phối hàng đợi phòng khám
- Cho phép Bác sĩ xem trạng thái hàng đợi hiện tại: số đang phục vụ và danh sách bệnh nhân đang chờ.
- Bác sĩ thực hiện các hành động:
  - **Gọi số tiếp theo (`Call Next`):** Chuyển bệnh nhân kế tiếp sang `Serving`, tự động hoàn thành ca khám trước đó.
  - **Hoàn thành ca khám (`Complete Serving`):** Đánh dấu ca khám là `Completed`.
  - **Báo bệnh nhân vắng mặt (`No Show`):** Đánh dấu ca khám là `NoShow`.

### FR-05: Thông báo Realtime qua SignalR
- Khi Queue Worker cấp số thành công, phát sự kiện realtime để màn hình bệnh nhân hiển thị số thứ tự phiếu khám.
- Khi Bác sĩ gọi số hoặc đổi trạng thái lượt khám, SignalR Hub phát sóng tức thì đến các client đăng ký theo dõi phòng khám đó.

### FR-06: Hủy lịch khám
- Bệnh nhân có thể hủy lịch khám nếu lịch đang ở trạng thái `Pending` hoặc `Confirmed`.
- Hệ thống hoàn trả 1 chỗ trống (`current_bookings -= 1`) cho khung giờ tương ứng và ghi log hủy.

---

## 3. Yêu cầu phi chức năng (Non-Functional Requirements - NFR)

### NFR-01: Tính phân tán và tách rời dịch vụ (Loose Coupling)
- Hệ thống phân chia thành các service riêng biệt: `AppointmentApi`, `QueueWorker`, `NotificationService`, `Database` và `RabbitMQ Broker`.
- Sự cố tại Worker hay Notification Service không làm sập chức năng tiếp nhận lịch hẹn của Appointment API.

### NFR-02: Độ bền vững và an toàn thông điệp (Message Durability)
- Toàn bộ Exchange và Queue trên RabbitMQ được thiết lập `durable: true`.
- Tin nhắn gửi lên RabbitMQ được đánh dấu `delivery_mode: 2` (Persistent) để không mất tin khi broker khởi động lại.
- Cơ chế Publisher Confirms đảm bảo thông điệp đã được broker lưu trữ thành công trước khi phản hồi Client.

### NFR-03: Tính nhất quán và toàn vẹn dữ liệu (Data Integrity)
- Quá trình đặt lịch và cập nhật số lượng slot phải nằm trong CSDL Transaction.
- Đảm bảo số thứ tự khám là duy nhất và tăng dần liên tục theo từng bác sĩ trong ngày.

### NFR-04: Khả năng chịu lỗi và Dead Letter Queue (Resilience & DLQ)
- Các kết nối đến RabbitMQ được trang bị chính sách Retry Exponential Backoff với Polly.
- Các message lỗi không thể xử lý được chuyển hướng vào `queue.dlq` thông qua Dead Letter Exchange, ngăn ngừa poison message làm tắc nghẽn queue chính.

### NFR-05: Hiệu năng thời gian thực (Realtime Latency)
- Độ trễ gửi nhận thông báo qua SignalR khi bác sĩ bấm gọi số đến khi hiển thị trên màn hình bệnh nhân dưới 500ms trong điều kiện mạng bình thường.
