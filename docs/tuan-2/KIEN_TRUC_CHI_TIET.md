# BÁO CÁO THIẾT KẾ KIẾN TRÚC CHI TIẾT CÁC SERVICE
## Đề tài 09: Hệ thống Đặt lịch khám và Quản lý hàng đợi (Clinic Appointment & Queue Management)

---

## 1. Phân chia trách nhiệm các Service (Separation of Concerns)

| Service | Công nghệ | Trách nhiệm chính |
|---|---|---|
| **Appointment API** | ASP.NET Core 8 Web API | Tiếp nhận HTTP REST từ Client, xác thực dữ liệu, quản lý bác sĩ và khung giờ, kiểm tra slot trống trong ACID transaction, lưu lịch hẹn ở trạng thái `Pending`, đưa message vào RabbitMQ. |
| **Queue Worker** | .NET 8 Worker Service | Background Consumer độc lập tiêu thụ message `appointment.requested`. Thực hiện thuật toán sinh số thứ tự tự động theo ngày/bác sĩ, cập nhật lịch hẹn sang `Confirmed`, ghi log kiểm toán và phát sự kiện `queue.number.assigned`. |
| **Notification Service** | ASP.NET Core 8 SignalR | Quản lý kết nối thời gian thực WebSockets/SignalR với client. Tiêu thụ các sự kiện hàng đợi từ RabbitMQ và broadcast thông báo đến đúng nhóm phòng khám (`doctor-queue-{id}`). |
| **PostgreSQL Database** | PostgreSQL 16 | Lưu trữ dữ liệu tập trung đảm bảo tính ACID, toàn vẹn khóa ngoại và ràng buộc duy nhất. |
| **RabbitMQ Broker** | RabbitMQ 3.13-management | Đảm bảo tính bất đồng bộ, đệm lưu lượng yêu cầu đặt khám khi cao điểm, phân phối sự kiện đa hướng (Pub/Sub). |
| **Client Frontend** | HTML5, CSS3, JS, SignalR | Cung cấp giao diện tương tác cho Bệnh nhân (`index.html`) và màn hình phòng khám cho Bác sĩ (`doctor.html`). |

---

## 2. Luồng giao tiếp toàn hệ thống (End-to-End Sequence Diagram)

```mermaid
sequenceDiagram
    autonumber
    actor Patient as Bệnh nhân (Client)
    actor Doctor as Bác sĩ (Doctor UI)
    participant API as Appointment API
    participant DB as PostgreSQL DB
    participant MQ as RabbitMQ Broker
    participant Worker as Queue Worker
    participant SignalR as Notification Service

    Note over Patient,API: BƯỚC 1: ĐẶT LỊCH KHÁM
    Patient->>API: POST /api/appointments (Chọn Bác sĩ, Khung giờ, Thông tin)
    API->>DB: Kiểm tra slot còn chỗ trống (CurrentBookings < MaxCapacity)
    alt Khung giờ đã hết chỗ
        API-->>Patient: 400 Bad Request ("Khung giờ đã hết chỗ")
    else Khung giờ còn chỗ
        API->>DB: Ghi nhận Appointment (Status: Pending) & Tăng CurrentBookings
        API->>MQ: Publish 'AppointmentRequestedEvent' (Persistent mode)
        API-->>Patient: 201 Created (AppointmentId, Status: Pending)
    end

    Note over MQ,Worker: BƯỚC 2: CẤP SỐ THỨ TỰ BẤT ĐỒNG BỘ
    MQ->>Worker: Consume 'appointment.requested'
    Worker->>DB: Lấy MAX(queue_number) của Bác sĩ trong ngày + 1
    Worker->>DB: Cập nhật Appointment (QueueNumber, Status: Confirmed) & Ghi QueueLog
    Worker->>MQ: BasicAck & Publish 'QueueNumberAssignedEvent'

    Note over MQ,SignalR: BƯỚC 3: THÔNG BÁO REALTIME SỐ THỨ TỰ
    MQ->>SignalR: Consume 'queue.number.assigned'
    SignalR-->>Patient: Push SignalR 'ReceiveAppointmentStatus' (Hiển thị số thứ tự phiếu khám)

    Note over Doctor,API: BƯỚC 4: BÁC SĨ GỌI SỐ VÀO KHÁM
    Doctor->>API: POST /api/queues/{doctorId}/next
    API->>DB: Cập nhật ca trước thành Completed, ca kế tiếp thành Serving
    API->>MQ: Publish 'QueueServingChangedEvent' (Action: CallNext)
    API-->>Doctor: 200 OK (Số đang phục vụ mới)

    MQ->>SignalR: Consume 'queue.serving.changed'
    SignalR-->>Doctor: Push SignalR 'ReceiveQueueUpdate' (Cập nhật màn hình bác sĩ)
    SignalR-->>Patient: Push SignalR 'ReceiveQueueUpdate' (Màn hình bệnh nhân đổi số lượt khám)
```
