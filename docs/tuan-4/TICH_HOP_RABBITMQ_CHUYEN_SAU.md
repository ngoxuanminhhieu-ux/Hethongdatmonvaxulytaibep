# TÀI LIỆU KỸ THUẬT: TÍCH HỢP RABBITMQ VÀO ORDER API CHUYÊN SÂU
## Đề tài 04: Hệ thống Đặt món và Xử lý tại Bếp (Food Ordering & Kitchen Processing)
**Mục tiêu tuần 4:** Xây dựng cơ chế giao tiếp Message Broker bất đồng bộ chuẩn doanh nghiệp với độ tin cậy cao: Persistent Connection, Publisher Confirms, Message Durability, Exponential Backoff Retry, Dead Letter Queue và Diagnostics API.

---

## 1. Tổng quan Kiến trúc Messaging trong Order API

Trong các hệ thống phân tán chịu tải cao, việc Order API gọi hàm đồng bộ hoặc tạo mới kết nối TCP mỗi lần gửi thông điệp là một **Anti-pattern** (gây cạn kiệt socket, chậm trễ phản hồi và mất thông điệp khi broker quá tải).

Hệ thống đã được nâng cấp theo chuẩn doanh nghiệp (Enterprise Messaging Architecture):

```
[ Client Request ]
       |
       v
[ Order API (Controller) ]
       |
       v (Save to DB in ACID Transaction)
[ PostgreSQL Database ]
       |
       v (Trigger Event)
[ RabbitMqProducer (Singleton) ]
       |
       |-- Uses: IRabbitMqPersistentConnection (Long-lived TCP connection, auto-recovery)
       |-- Enables: channel.ConfirmSelect() (Publisher Confirms)
       |-- Sets: DeliveryMode = 2 (Persistent to disk)
       |-- Attaches: MessageId, CorrelationId, AppId, Custom Headers
       |-- Retries: Exponential Backoff (3 attempts: 500ms, 1000ms, 1500ms)
       v
[ RabbitMQ Topic Exchange: 'food.order.exchange' ]
       |
       +------------------------------------+
       | (Routing Key: 'order.created')     | (Routing Key: 'order.*')
       v                                    v
[ Queue: 'order.process.queue' ]    [ Queue: 'order.notification.queue' ]
 (with Dead Letter to DLX)           (Broadcast to SignalR)
       | (When failed & nacked)
       v
[ DLX: 'food.order.dlx' ] -> [ Queue: 'order.dlq' ]
```

---

## 2. Các kỹ thuật phân tán cốt lõi được áp dụng

### 2.1. Quản lý Kết nối Bền bỉ (Persistent Connection Management)
- **Vấn đề:** Bắt tay 3 bước TCP + AMQP Handshake tiêu tốn tài nguyên mạng và CPU nếu tạo mới trên mỗi request HTTP.
- **Giải pháp:** Hiện thực `IRabbitMqPersistentConnection` (lớp `RabbitMqPersistentConnection` đăng ký Singleton trong DI container):
  - Duy trì duy nhất một kết nối TCP sống lâu dài (Long-lived connection).
  - Tự động phát hiện và kết nối lại khi có sự cố mạng:
    ```csharp
    AutomaticRecoveryEnabled = true;
    TopologyRecoveryEnabled = true;
    NetworkRecoveryInterval = TimeSpan.FromSeconds(5);
    ```
  - Lắng nghe và ghi log các sự kiện vòng đời kết nối: `ConnectionShutdown`, `CallbackException`, `ConnectionBlocked`, `ConnectionUnblocked`.

### 2.2. Cơ chế Xác nhận Xuất bản (Publisher Confirms)
- **Vấn đề:** Mặc định trong AMQP, hàm `BasicPublish` là "Fire-and-Forget" (gửi đi và không chờ xác nhận). Nếu broker bị crash trước khi ghi đĩa hoặc exchange không định tuyến được, message có thể bị mất mà Order API không hề hay biết.
- **Giải pháp:** Sử dụng **Publisher Confirms** (phương thức được khuyến nghị thay thế cho Transactions vì hiệu năng cao hơn gấp 100 lần):
  ```csharp
  channel.ConfirmSelect();
  channel.BasicPublish(...);
  bool confirmed = channel.WaitForConfirms(TimeSpan.FromSeconds(5), out bool timedOut);
  ```
  Broker RabbitMQ chỉ phản hồi ACK khi message đã được ghi xuống đĩa hoặc chuyển giao thành công vào tất cả các queue đích.

### 2.3. Độ bền vững của Thông điệp (Message Durability)
Đảm bảo thông điệp không bị mất ngay cả khi khởi động lại (restart) toàn bộ container RabbitMQ:
1. **Durable Exchange:** Khai báo với cờ `durable: true`.
2. **Durable Queues:** Khai báo `queueDeclare` với `durable: true`.
3. **Persistent Message Delivery Mode:** Thiết lập `IBasicProperties.DeliveryMode = 2` (Persistent) để ép broker ghi message xuống ổ đĩa (Disk Storage).

### 2.4. Siêu dữ liệu Thông điệp (AMQP Metadata Headers & Traceability)
Mỗi message xuất bản đều được gắn đầy đủ metadata chuẩn AMQP:
- `MessageId`: UUID duy nhất của thông điệp để consumer kiểm tra Idempotency.
- `CorrelationId`: Mã `OrderId` xuyên suốt toàn bộ luồng nghiệp vụ.
- `AppId`: `"OrderApi"` định danh service nguồn.
- `Timestamp`: Thời điểm tạo message theo Unix Epoch.
- `ContentType`: `"application/json"`.
- `ContentEncoding`: `"utf-8"`.
- `Headers`: Dictionary chứa các thông tin tùy biến:
  - `x-source-service`: `"OrderApi"`
  - `x-publish-attempt`: Số thứ tự lần thử gửi
  - `x-client-timestamp`: Thời gian ISO 8601

### 2.5. Cơ chế Thử lại Luỹ thừa (Exponential Backoff Retries)
Khi mạng nội bộ bị ngắt quãng ngắn hoặc broker tạm thời bị nghẽn (Connection Blocked):
- Producer tự động thử lại tối đa 3 lần với khoảng thời gian chờ tăng dần:
  $$\text{Delay} = 500\text{ms} \times \text{attempt}$$
- Nếu sau 3 lần vẫn không thể gửi, hệ thống ghi log lỗi có cấu trúc (`LogError`) và đảm bảo đơn hàng đã được bảo toàn trong CSDL PostgreSQL.

### 2.6. Tự động Khởi tạo Topology khi khởi động (Topology Auto-Initializer)
- Lớp `RabbitMqTopologyInitializer` triển khai `IHostedService` tự động chạy khi Order API khởi động.
- Tự động khai báo:
  - `food.order.exchange` (Topic)
  - `food.order.dlx` (Direct - Dead Letter Exchange)
  - `order.process.queue` (Queue bếp với tham số `x-dead-letter-exchange`)
  - `order.notification.queue` (Queue thông báo)
  - `order.dlq` (Dead Letter Queue)
  - Thiết lập bindings tương ứng.
$\to$ Giúp hệ thống tự vận hành mà không cần người dùng phải vào Web UI tạo queue thủ công.

---

## 3. Các Endpoint Chẩn đoán (Diagnostics API)

Order API cung cấp controller `RabbitMqDiagnosticsController` tại `/api/rabbitmq`:

| Endpoint | Phương thức | Mô tả | Ứng dụng |
|---|---|---|---|
| `/api/rabbitmq/health` | `GET` | Kiểm tra kết nối broker, hostname, port, vhost | Dùng cho Health Check / Docker healthcheck |
| `/api/rabbitmq/topology` | `GET` | Xem chi tiết cấu hình Exchanges, Queues, DLQ | Dùng cho báo cáo kỹ thuật & bảo vệ đồ án |
| `/api/rabbitmq/test-publish` | `POST` | Gửi message test vào broker với routing key tùy chọn | Dùng để demo kiểm chứng thông điệp qua Web UI |
