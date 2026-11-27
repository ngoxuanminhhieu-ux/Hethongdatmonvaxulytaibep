# BÁO CÁO KIỂM CHỨNG THÔNG ĐIỆP QUA RABBITMQ WEB MANAGEMENT
## Đề tài 09: Hệ thống Đặt lịch khám và Quản lý hàng đợi (Clinic Appointment & Queue Management)

---

## 1. Giới thiệu giao diện RabbitMQ Web Management

RabbitMQ Management UI được tích hợp sẵn qua cổng `http://localhost:15672` (Tài khoản mặc định: `guest` / `guest`).

Giao diện quản lý cung cấp các công cụ trực quan để:
- Giám sát lưu lượng thông điệp (Publish rate, Deliver/Ack rate).
- Kiểm tra danh sách Exchange, Queues và các ràng buộc Bindings.
- Xem số lượng message đang sẵn sàng (`Ready`), đang chờ xử lý (`Unacked`) trong từng Queue.
- Trích xuất nội dung thông điệp mẫu (Get Messages) để đối soát cấu trúc JSON.

---

## 2. Kiểm chứng Topology đã khởi tạo tự động

Khi Appointment API khởi động, `RabbitMqTopologyInitializer` tự động khai báo đầy đủ trên RabbitMQ:

### 2.1. Exchanges (Tab `Exchanges`)
| Name | Type | Features | Bindings |
|---|---|---|---|
| `clinic.appointment.exchange` | `topic` | `D` (Durable) | Gắn tới `queue.process.queue` (`appointment.requested`) và `queue.notification.queue` (`queue.*`, `appointment.*`) |
| `clinic.appointment.dlx` | `direct` | `D` (Durable) | Gắn tới `queue.dlq` (`queue.failed`) |

### 2.2. Queues (Tab `Queues`)
| Name | Features | Arguments | Consumers |
|---|---|---|---|
| `queue.process.queue` | `D` (Durable) | `x-dead-letter-exchange: clinic.appointment.dlx`<br/>`x-dead-letter-routing-key: queue.failed` | Queue Worker |
| `queue.notification.queue` | `D` (Durable) | - | Notification Service |
| `queue.dlq` | `D` (Durable) | - | DLQ Auditor |

---

## 3. Kiểm chứng quá trình đẩy thông điệp khi Đặt lịch khám

### 3.1. Kịch bản thực hiện
1. Gọi API `POST /api/appointments` để tạo một lịch khám mới cho bệnh nhân "Nguyễn Văn Bệnh Nhân".
2. Truy cập vào RabbitMQ Web Management tại `http://localhost:15672/#/queues/%2F/queue.process.queue`.
3. Kiểm tra thông số hàng đợi:
   - Cột **Ready**: Tăng lên `1`.
   - Biểu đồ **Message Rates**: Xuất hiện xung nhịp Publish ~ 1 msg/s.
4. Mở rộng mục **Get messages**, bấm nút **Get Message(s)** để xem nội dung message.

### 3.2. Kết quả kiểm chứng nội dung Message
- **Payload:**
```json
{
  "eventId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "correlationId": "7f8b91a2-3c4d-5e6f-7a8b-9c0d1e2f3a4b",
  "eventType": "AppointmentRequestedEvent",
  "timestamp": "2026-11-27T14:30:00.123456Z",
  "sourceService": "AppointmentApi",
  "data": {
    "appointmentId": "7f8b91a2-3c4d-5e6f-7a8b-9c0d1e2f3a4b",
    "patientId": 10,
    "patientName": "Nguyễn Văn Bệnh Nhân",
    "phone": "0988888888",
    "doctorId": 1,
    "doctorName": "BS. CKII Nguyễn Văn A",
    "roomNumber": "P.201",
    "timeSlotId": 1,
    "slotDate": "2026-11-28",
    "startTime": "08:00",
    "endTime": "09:00",
    "symptoms": "Đau tức ngực trái khi gắng sức",
    "createdAt": "2026-11-27T14:30:00.123456Z"
  }
}
```
- **Properties:**
  - `delivery_mode: 2` (Persistent) $\to$ Message được lưu bền vững trên đĩa.
  - `content_type: application/json`
  - `correlation_id: 7f8b91a2-3c4d-5e6f-7a8b-9c0d1e2f3a4b`
  - `message_id: a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d`

---

## 4. Kiểm chứng Dead Letter Queue (DLQ)

Khi gửi một thông điệp lỗi hoặc cố tình kích hoạt `basicNack` với `requeue = false`:
1. Thông điệp bị loại khỏi `queue.process.queue`.
2. RabbitMQ tự động đẩy message sang Dead Letter Exchange `clinic.appointment.dlx` với routing key `queue.failed`.
3. Message xuất hiện an toàn trong `queue.dlq`.
4. Queue chính tiếp tục hoạt động bình thường mà không bị nghẽn (No blocking).
