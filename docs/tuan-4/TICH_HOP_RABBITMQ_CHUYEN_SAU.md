# TÀI LIỆU KỸ THUẬT: TÍCH HỢP RABBITMQ VÀO APPOINTMENT API CHUYÊN SÂU
## Đề tài 09: Hệ thống Đặt lịch khám và Quản lý hàng đợi (Clinic Appointment & Queue Management)
**Mục tiêu tuần 4:** Xây dựng cơ chế giao tiếp Message Broker bất đồng bộ chuẩn doanh nghiệp với độ tin cậy cao: Persistent Connection, Publisher Confirms, Message Durability, Exponential Backoff Retry, Dead Letter Queue và Diagnostics API.

---

## 1. Tổng quan Kiến trúc Messaging trong Appointment API

Trong các hệ thống phân tán chịu tải cao, việc API thực hiện cấp số thứ tự đồng bộ hoặc kết nối trực tiếp trong luồng HTTP là một **Anti-pattern** (gây cạn kiệt socket, chậm trễ phản hồi khi có nhiều bệnh nhân đăng ký cùng lúc và dễ dẫn đến trùng số thứ tự).

Hệ thống đã được thiết kế theo chuẩn Enterprise Messaging Architecture:

```
[ Patient Request ]
       |
       v
[ Appointment API (Controller) ]
       |
       v (Save to DB in ACID Transaction: Status = Pending)
[ PostgreSQL Database ]
       |
       v (Trigger Event)
[ RabbitMqProducer (Singleton) ]
       |
       |-- Uses: IRabbitMqPersistentConnection (Long-lived TCP connection, auto-recovery)
       |-- Enables: channel.ConfirmSelect() (Publisher Confirms)
       |-- Sets: DeliveryMode = 2 (Persistent to disk)
       |-- Attaches: MessageId, CorrelationId, AppId, Timestamp
       |-- Retries: Exponential Backoff (3 attempts: 500ms, 1000ms, 1500ms)
       v
[ RabbitMQ Topic Exchange: 'clinic.appointment.exchange' ]
       |
       +------------------------------------+
       | (Routing Key: 'appointment.requested') | (Routing Key: 'queue.*', 'appointment.*')
       v                                    v
[ Queue: 'queue.process.queue' ]    [ Queue: 'queue.notification.queue' ]
 (with Dead Letter to DLX)           (Broadcast to SignalR)
       | (When failed & nacked)
       v
[ DLX: 'clinic.appointment.dlx' ] -> [ Queue: 'queue.dlq' ]
```

---

## 2. Các thành phần kỹ thuật cốt lõi đã triển khai

### 2.1. Quản lý kết nối bền bỉ (`RabbitMqPersistentConnection`)
- Triển khai interface `IRabbitMqPersistentConnection` dưới dạng **Singleton**.
- Quản lý một TCP Connection duy nhất cho toàn bộ vòng đời ứng dụng.
- Tự động bắt sự kiện `ConnectionShutdown`, `CallbackException`, `ConnectionBlocked` để tự phục hồi kết nối với thuật toán Retry Exponential Backoff (Polly).

### 2.2. Khởi tạo Topology tự động khi khởi động (`RabbitMqTopologyInitializer`)
- Chạy như một `IHostedService` khi ứng dụng khởi động.
- Tự động khai báo:
  - Main Topic Exchange: `clinic.appointment.exchange` (durable).
  - Dead Letter Direct Exchange: `clinic.appointment.dlx` (durable).
  - Queue `queue.process.queue` liên kết với DLX qua argument `x-dead-letter-exchange`.
  - Queue `queue.notification.queue` và `queue.dlq`.
  - Ràng buộc (Bindings) với các Routing Keys tương ứng.

### 2.3. Nhà sản xuất thông điệp an toàn (`RabbitMqProducer`)
- Bật tính năng **Publisher Confirms** (`channel.ConfirmSelect()`).
- Thiết lập thuộc tính `DeliveryMode = 2` (Persistent) đảm bảo dữ liệu ghi xuống đĩa của broker.
- Đính kèm Metadata: `CorrelationId` (ID lịch hẹn), `MessageId`, `Timestamp`, `Type`.
- Đợi xác nhận ghi thành công từ RabbitMQ với timeout an toàn (`WaitForConfirmsOrDie(TimeSpan.FromSeconds(5))`).

### 2.4. Endpoints chẩn đoán và giám sát (`RabbitMqDiagnosticsController`)
- **`GET /api/rabbitmq/health`**: Trả về tình trạng kết nối broker (Host, Port, VirtualHost, Trạng thái Healthy/Unhealthy).
- **`GET /api/rabbitmq/topology`**: Trả về danh sách chi tiết các Exchanges, Queues và Bindings được cấu hình.
- **`POST /api/rabbitmq/test-publish`**: Cho phép bắn thử nghiệm một message vào broker để kiểm tra kết nối ngay trên Swagger hoặc Postman.
