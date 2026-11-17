# THIẾT KẾ KIẾN TRÚC CHI TIẾT CÁC SERVICE
## Đề tài 04: Hệ thống Đặt món và Xử lý tại Bếp (Food Ordering & Kitchen Processing)

---

## 1. Phân định ranh giới và trách nhiệm các Service (Service Boundaries)

Hệ thống được tổ chức thành các service có tính gắn kết cao (High Cohesion) và ghép nối lỏng (Loose Coupling):

```
+-----------------------------------------------------------------------------------+
|                                  CLIENT LAYER                                     |
|  +-------------------------------------+   +------------------------------------+ |
|  |  Customer Web App (Port 8080/Static)|   |  Kitchen Display System (Port 8080)| |
|  +-------------------------------------+   +------------------------------------+ |
+-----------------------------------------------------------------------------------+
           |                                                      ^
    (1) REST HTTP                                          (4) SignalR WebSockets
           v                                                      |
+------------------------------------+               +------------------------------+
|             ORDER API              |               |     NOTIFICATION SERVICE     |
|         (ASP.NET Core 8)           |               |       (ASP.NET Core 8)       |
|            Port 5001               |               |          Port 5003           |
|------------------------------------|               |------------------------------|
| • Quản lý Menu (GET /menu)         |               | • SignalR Hub (/hubs/order)  |
| • Nhận đơn (POST /orders)          |               | • Quản lý nhóm theo OrderId  |
| • Truy vấn đơn (GET /orders/{id})  |               | • Consume 'order.notification'|
| • Lưu DB & Publish sự kiện         |               | • Push thông báo cho Client  |
+------------------------------------+               +------------------------------+
         |                       \                               ^
         | Write Data             \ (2) Publish                   | Consume
         v                         v                              |
+-------------------+      +--------------------------------------------------------+
|    POSTGRESQL     |      |                     RABBITMQ BROKER                    |
|    Port 5432      |      |                       Port 5672                        |
|-------------------|      |                 Web Management: 15672                  |
| • customers       |      |--------------------------------------------------------|
| • menu_items      |      | Exchange: food.order.exchange (Topic)                  |
| • orders          |      | • Queue 1: order.process.queue (Key: order.created)    |
| • order_details   |      | • Queue 2: order.notification.queue (Key: order.*)     |
| • status_histories|      | • Queue DLQ: order.dlq (Dead Letter Queue)             |
+-------------------+      +--------------------------------------------------------+
         ^                                                 |
         | Read/Update Status                              v Consume (3)
         |                                   +------------------------------+
         +-----------------------------------|        KITCHEN WORKER        |
                                             |   (.NET Background Service)  |
                                             |------------------------------|
                                             | • Lắng nghe đơn đặt món mới  |
                                             | • Chuyển trạng thái bếp      |
                                             | • Ghi nhận lịch sử trạng thái|
                                             | • Publish OrderStatusChanged |
                                             +------------------------------+
```

---

## 2. Chi tiết từng Service

### 2.1. Order API (Port: 5001)
- **Nền tảng:** ASP.NET Core 8 Web API.
- **Vai trò:** Cổng tiếp nhận nghiệp vụ chính từ phía khách hàng và nhân viên.
- **Trách nhiệm:**
  1. Cung cấp API xem thực đơn (`GET /api/menu`).
  2. Tiếp nhận đơn hàng mới (`POST /api/orders`), kiểm tra dữ liệu đầu vào (Validation).
  3. Tính toán tổng tiền dựa trên giá gốc từ bảng `menu_items` trong CSDL.
  4. Mở Transaction CSDL: Ghi đơn hàng vào bảng `orders`, ghi từng món vào `order_details`, ghi trạng thái ban đầu `Pending` vào `order_status_histories`.
  5. Khi Transaction thành công, kết nối tới RabbitMQ và publish sự kiện `OrderCreatedEvent` với chế độ lưu trữ bền vững (Persistent Delivery Mode = 2).
  6. Trả về mã HTTP `201 Created` kèm `OrderId` và thông tin đơn hàng cho Client.
- **Tính chịu lỗi:** Nếu RabbitMQ tạm thời gián đoạn, API ghi log lỗi và vẫn đảm bảo đơn hàng đã được lưu an toàn trong CSDL PostgreSQL.

---

### 2.2. Kitchen Worker (Background Consumer)
- **Nền tảng:** .NET 8 Worker Service (`IHostedService` / `BackgroundService`).
- **Vai trò:** Bộ xử lý nghiệp vụ bất đồng bộ tại bếp.
- **Trách nhiệm:**
  1. Duy trì kết nối lâu dài tới RabbitMQ Broker.
  2. Khai báo và lắng nghe hàng đợi `order.process.queue` gắn với binding `order.created`.
  3. Cấu hình `BasicQos(prefetchSize: 0, prefetchCount: 1, global: false)`: Mỗi worker chỉ nhận 1 đơn tại một thời điểm, tránh việc 1 worker quá tải trong khi các worker khác rảnh rỗi (Fair Dispatching).
  4. Khi nhận được message:
     - Deserialize payload thành `OrderCreatedEvent`.
     - Kiểm tra tính hợp lệ và Idempotency (nếu đơn hàng đã được xử lý thì bỏ qua để tránh trùng lặp).
     - Cập nhật trạng thái trong CSDL sang `Preparing` (Đang chế biến) và sau đó sang `Ready` (Đã nấu xong).
     - Ghi nhận lịch sử vào `order_status_histories`.
     - Gửi `BasicAck` xác nhận đã xử lý thành công.
     - Publish sự kiện `OrderStatusChangedEvent` lên Exchange `food.order.exchange` với routing key `order.status.changed`.
  5. Nếu xử lý thất bại do lỗi không thể phục hồi: gửi `BasicNack(requeue: false)` để RabbitMQ đẩy message vào Dead Letter Queue (`order.dlq`).

---

### 2.3. Notification Service (Port: 5003)
- **Nền tảng:** ASP.NET Core 8 Web API tích hợp SignalR Hub (`OrderNotificationHub`).
- **Vai trò:** Cầu nối thời gian thực (Realtime Bridge) giữa kiến trúc bất đồng bộ RabbitMQ và giao diện người dùng.
- **Trách nhiệm:**
  1. Quản lý các kết nối WebSocket / Long Polling từ phía Client.
  2. Cung cấp Hub method `JoinOrderGroup(string orderId)` và `LeaveOrderGroup(string orderId)` để Client đăng ký theo dõi trạng thái của đúng đơn hàng của mình.
  3. Chạy một Background Service nội bộ lắng nghe queue `order.notification.queue` với routing key `order.*` (bắt cả `order.created` và `order.status.changed`).
  4. Khi nhận được sự kiện:
     - Trích xuất `OrderId`, `NewStatus`, `Message`, `Timestamp`.
     - Gọi `_hubContext.Clients.Group($"order-{orderId}").SendAsync("ReceiveOrderStatusUpdate", payload)`.
     - Đồng thời broadcast lên group `kitchen-dashboard` để màn hình bếp tự động cập nhật mà không cần người dùng thao tác F5.

---

### 2.4. Database Layer (PostgreSQL - Port: 5432)
- **Cơ sở dữ liệu:** PostgreSQL 16.
- **Vai trò:** Lưu trữ tập trung trạng thái thực tế (Single Source of Truth) của toàn bộ hệ thống.
- **Nguyên tắc thiết kế:**
  - Chuẩn hóa CSDL (3NF).
  - Ràng buộc toàn vẹn khóa ngoại (Foreign Keys) với cơ chế `ON DELETE CASCADE / RESTRICT` phù hợp.
  - Sử dụng Index cho các cột thường xuyên tìm kiếm (`order_id`, `customer_id`, `status`).

---

### 2.5. Message Broker (RabbitMQ - AMQP Port: 5672, Web UI: 15672)
- **Giao thức:** AMQP 0-9-1.
- **Vai trò:** Đảm bảo truyền thông bất đồng bộ tin cậy, phân tải và cách ly lỗi giữa Order API, Kitchen Worker và Notification Service.
- **Exchange:** Topic Exchange mang tên `food.order.exchange`.
- **Đặc tính:** Durable (bền vững, không mất khi broker khởi động lại).

---

## 3. Bản đồ Cổng mạng (Network & Port Mapping)

| Service | Host Port | Container Port | Giao thức | Mục đích |
|---|---|---|---|---|
| **Order API** | `5001` | `80` hoặc native | HTTP/REST | Tiếp nhận đặt món, xem menu |
| **Notification Service** | `5003` | `80` hoặc native | HTTP/SignalR | Kết nối WebSocket realtime |
| **Kitchen Worker** | - (chạy nền) | - | AMQP | Tiêu thụ message từ hàng đợi |
| **PostgreSQL** | `5432` | `5432` | TCP/SQL | CSDL quan hệ chính |
| **RabbitMQ AMQP** | `5672` | `5672` | AMQP | Truyền thông điệp giữa các service |
| **RabbitMQ Management** | `15672` | `15672` | HTTP | Giao diện quản trị Web UI |
| **Client Frontend** | `8080` / Trình duyệt | - | HTTP | Giao diện người dùng |
