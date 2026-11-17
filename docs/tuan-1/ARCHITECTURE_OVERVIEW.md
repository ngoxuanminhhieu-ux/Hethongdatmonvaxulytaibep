# SƠ ĐỒ VÀ BÁO CÁO KIẾN TRÚC TỔNG QUAN HỆ THỐNG
## Đề tài 04: Hệ thống Đặt món và Xử lý tại Bếp (Food Ordering & Kitchen Processing)

---

## 1. Sơ đồ kiến trúc tổng quan (System Architecture Diagram)

Hệ thống được thiết kế theo mô hình **Kiến trúc Hướng sự kiện phân tán (Distributed Event-Driven Architecture)** gồm 5 khối thành phần độc lập:

```mermaid
flowchart TD
    subgraph Clients["TẦNG CLIENT (GIAO DIỆN NGƯỜI DÙNG)"]
        CustomerUI["Khách hàng UI<br/>(Đặt món & Theo dõi)"]
        KitchenUI["Màn hình Bếp KDS<br/>(Quản lý & Cập nhật đơn)"]
    end

    subgraph ApiGateway["TẦNG TIẾP NHẬN YÊU CẦU"]
        OrderApi["Order API (ASP.NET Core Web API)<br/>• GET /api/menu<br/>• POST /api/orders<br/>• GET /api/orders/{id}<br/>• Xác thực dữ liệu & Tính toán"]
    end

    subgraph DatabaseLayer["TẦNG LƯU TRỮ TẬP TRUNG"]
        PostgresDB[("PostgreSQL Database<br/>• customers<br/>• menu_items<br/>• orders<br/>• order_details<br/>• order_status_histories")]
    end

    subgraph MessageBroker["TẦNG TRUYỀN THÔNG ĐIỆP BẤT ĐỒNG BỘ"]
        Exchange["RabbitMQ Topic Exchange<br/>('food.order.exchange')"]
        QueueKitchen["Queue: 'order.process.queue'<br/>(Routing Key: 'order.created')"]
        QueueNotify["Queue: 'order.notification.queue'<br/>(Routing Key: 'order.*')"]
        QueueDLQ["Queue: 'order.dlq'<br/>(Dead Letter Queue)"]
    end

    subgraph Workers["TẦNG XỬ LÝ NGHIỆP VỤ BẾP"]
        KitchenWorker["Kitchen Worker (.NET Background Service)<br/>• Lắng nghe đơn đặt món<br/>• Xử lý theo thứ tự<br/>• Cập nhật trạng thái chế biến"]
    end

    subgraph RealtimeLayer["TẦNG THÔNG BÁO THỜI GIAN THỰC"]
        NotifyService["Notification Service (SignalR Hub)<br/>• Lắng nghe sự kiện từ RabbitMQ<br/>• Điều hướng nhóm theo OrderId<br/>• Đẩy thông báo Push Realtime"]
    end

    %% Client calls
    CustomerUI -->|1. REST HTTP: Xem Menu & Đặt món| OrderApi
    KitchenUI -->|Tra cứu & Cập nhật trạng thái| OrderApi

    %% Order API persistence & publish
    OrderApi -->|2. Ghi nhận đơn hàng & lịch sử| PostgresDB
    OrderApi -->|3. Publish: OrderCreatedEvent| Exchange

    %% RabbitMQ routing
    Exchange -->|Binding: order.created| QueueKitchen
    Exchange -->|Binding: order.*| QueueNotify
    QueueKitchen -.->|Lỗi quá số lần retry| QueueDLQ

    %% Kitchen processing
    QueueKitchen -->|4. Consume đơn hàng| KitchenWorker
    KitchenWorker -->|5. Cập nhật trạng thái chế biến| PostgresDB
    KitchenWorker -->|6. Publish: OrderStatusChangedEvent| Exchange

    %% Realtime push
    QueueNotify -->|7. Consume sự kiện| NotifyService
    NotifyService -->|8. Push WebSocket / SignalR| CustomerUI
    NotifyService -->|Đồng bộ màn hình| KitchenUI
```

---

## 2. Luồng giao tiếp đầu-cuối (End-to-End Sequence Diagram)

Sơ đồ trình bày chi tiết sự kết hợp giữa **giao tiếp đồng bộ (Synchronous HTTP REST)** và **giao tiếp bất đồng bộ (Asynchronous AMQP/RabbitMQ)**:

```mermaid
sequenceDiagram
    autonumber
    actor C as Khách hàng (Client UI)
    participant API as Order API
    participant DB as PostgreSQL DB
    participant MQ as RabbitMQ Broker
    participant KW as Kitchen Worker
    participant NS as Notification Service (SignalR)

    Note over C, API: GIAO TIẾP ĐỒNG BỘ (SYNCHRONOUS)
    C->>API: POST /api/orders (Thông tin món, bàn, khách)
    API->>API: Kiểm tra dữ liệu hợp lệ (Validation)
    API->>DB: Bắt đầu Transaction: INSERT order & order_details
    DB-->>API: Lưu thành công (Status = Pending)
    
    Note over API, MQ: GIAO TIẾP BẤT ĐỒNG BỘ (ASYNCHRONOUS)
    API->>MQ: Publish 'OrderCreatedEvent' (Exchange: food.order.exchange, Key: order.created)
    API-->>C: Phản hồi HTTP 201 Created (Kèm OrderId)
    C->>NS: Kết nối SignalR Hub & JoinGroup("order-{id}")

    Note over MQ, KW: BẾP TIẾP NHẬN ĐƠN HÀNG QUA QUEUE
    MQ->>KW: Consume message từ 'order.process.queue'
    KW->>KW: Kiểm tra Idempotency (Tránh trùng lặp)
    KW-->>MQ: Gửi BasicAck xác nhận đã nhận đơn
    KW->>DB: Cập nhật status = 'Preparing' & ghi lịch sử
    KW->>MQ: Publish 'OrderStatusChangedEvent' (Key: order.status.changed)

    Note over MQ, NS: PHÁT SỰ KIỆN REALTIME CHO KHÁCH HÀNG
    MQ->>NS: Consume message từ 'order.notification.queue'
    NS->>C: Push SignalR: "Đơn hàng của bạn đang được chế biến" (Preparing)
    
    Note over KW, C: KHI MÓN ĂN HOÀN THÀNH
    KW->>DB: Cập nhật status = 'Ready' & ghi lịch sử
    KW->>MQ: Publish 'OrderStatusChangedEvent' (Status: Ready)
    MQ->>NS: Consume message
    NS->>C: Push SignalR: "Món ăn đã nấu xong! Mời bạn thưởng thức" (Ready)
```

---

## 3. Lý do lựa chọn bộ công nghệ

| Thành phần | Công nghệ lựa chọn | Lý do và Lợi ích kỹ thuật |
|---|---|---|
| **Backend Framework** | **ASP.NET Core 8 Web API (C#)** | - Hiệu năng cao (top benchmark thế giới).<br/>- Hệ sinh thái C# mạnh mẽ, Dependency Injection tích hợp sẵn.<br/>- Hỗ trợ SignalR nguyên bản cho realtime.<br/>- Chuẩn hóa theo chương trình giảng dạy PTUDPT tại các trường đại học. |
| **Message Broker** | **RabbitMQ (v3.13-management)** | - Hỗ trợ chuẩn giao thức AMQP 0-9-1.<br/>- Đảm bảo độ tin cậy tin nhắn (Message Durability, Acknowledgement, DLQ).<br/>- Có giao diện Web Management trực quan tại port 15672, hỗ trợ kiểm tra queue, rate, message payload một cách dễ dàng trong buổi bảo vệ. |
| **Cơ sở dữ liệu** | **PostgreSQL 16** | - Hệ quản trị CSDL quan hệ mã nguồn mở mạnh mẽ nhất hiện nay.<br/>- Hỗ trợ giao dịch ACID nghiêm ngặt (đặc biệt quan trọng khi tính tiền đơn hàng).<br/>- Tích hợp hoàn hảo với Entity Framework Core (`Npgsql.EntityFrameworkCore.PostgreSQL`). |
| **Truyền thông Realtime**| **ASP.NET Core SignalR** | - Tự động đàm phán phương thức truyền tải tốt nhất (ưu tiên WebSockets, fallback SSE/Long Polling).<br/>- Hỗ trợ định tuyến nhóm (`Groups.AddToGroupAsync`), cho phép chỉ gửi thông báo trạng thái đến đúng phiên khách hàng đặt đơn đó. |
| **Frontend Client** | **HTML5 / CSS3 / Modern JS (ES6+)** | - Nhẹ, không phụ thuộc vào chuỗi build phức tạp, chạy trực tiếp trên trình duyệt.<br/>- Dễ dàng tích hợp thư viện `@microsoft/signalr` qua CDN.<br/>- Cung cấp 2 giao diện riêng biệt: Khách hàng đặt món và Màn hình bếp KDS. |
| **Containerization** | **Docker & Docker Compose** | - Đóng gói môi trường đồng nhất.<br/>- Khởi động toàn bộ CSDL, Message Queue chỉ bằng lệnh `docker compose up -d`. |
