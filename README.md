# HỆ THỐNG ĐẶT MÓN VÀ XỬ LÝ TẠI BẾP (FOOD ORDERING & KITCHEN PROCESSING SYSTEM)
> **Môn học:** Phát triển ứng dụng phân tán (Distributed Application Development - PTUDPT-DA)  
> **Đề tài:** 04  
> **Thời gian:** 9 Tuần  
> **Kiến trúc:** Microservices / Event-Driven Architecture (RabbitMQ + PostgreSQL + ASP.NET Core 8 + SignalR)

---

## 1. Giới thiệu tổng quan

Dự án xây dựng một hệ thống phân tán thực tế phục vụ quy trình gọi món tại bàn của khách hàng và chế biến tại bếp của nhà hàng:
- **Khách hàng:** Xem menu, chọn món, gửi đơn đặt hàng và theo dõi tiến trình làm món theo thời gian thực (Realtime).
- **Nhà bếp (Kitchen Display System):** Nhận đơn hàng theo thứ tự qua hàng đợi thông điệp RabbitMQ, chế biến món và phát sự kiện trạng thái (`Pending` $\to$ `Preparing` $\to$ `Ready`).
- **Notification Service:** Cầu nối nhận sự kiện bất đồng bộ từ RabbitMQ và đẩy thông báo tức thời về thiết bị khách hàng qua SignalR (WebSockets).

---

## 2. Sơ đồ kiến trúc phân tán

```
[ Khách hàng Web UI ]                       [ Màn hình Bếp KDS ]
        |                                            ^
  (1) POST /orders                            (4) Realtime Push
        v                                            |
[   Order API       ]                       [ Notification Service ]
(ASP.NET Core 5001)                         (SignalR Hub 5003)
   |            \                                    ^
(2)| Ghi CSDL    \ (3) Publish 'order.created'       | (6) Consume
   v              v                                  |
[ PostgreSQL ]   [ RabbitMQ Broker: Topic Exchange 'food.order.exchange' ]
                      |
                      | (5) Consume 'order.created'
                      v
             [ Kitchen Worker ] ---- (Cập nhật DB & Publish 'order.status.changed')
```

---

## 3. Cấu trúc thư mục dự án

```
PTUDPT-DA/
├── .gitignore                      # Cấu hình bỏ qua file build & tạm
├── README.md                       # Hướng dẫn tổng quan & khởi chạy
├── docker-compose.yml              # Containerization cho PostgreSQL & RabbitMQ
├── FoodOrderSystem.sln             # Solution file .NET
├── database/
│   └── init.sql                    # Script DDL tạo bảng CSDL & dữ liệu mẫu (Seed Data)
├── docs/
│   ├── KE_HOACH_9_TUAN.md          # Kế hoạch chi tiết 9 tuần theo bảng chuẩn quản lý
│   ├── tuan-1/
│   │   ├── YEU_CAU_HE_THONG.md     # Phân tích Functional & Non-Functional Requirements
│   │   ├── USE_CASE_DIAGRAM.md     # Biểu đồ Use Case và đặc tả kịch bản
│   │   └── ARCHITECTURE_OVERVIEW.md# Sơ đồ kiến trúc phân tán tổng quan & công nghệ
│   └── tuan-2/
│       ├── KIEN_TRUC_CHI_TIET.md   # Thiết kế chi tiết từng service & phân định ranh giới
│       ├── THIET_KE_CSDL_ERD.md    # Sơ đồ ERD, từ điển dữ liệu 5 bảng & Index
│       ├── API_CONTRACT.md         # Đặc tả chuẩn REST API (Request/Response)
│       └── RABBITMQ_DESIGN.md      # Topology RabbitMQ: Exchanges, Queues, DLQ, Routing Keys
└── src/
    ├── Shared/                     # Thư viện dùng chung (Contracts, Events, Enums)
    ├── OrderApi/                   # REST API tiếp nhận đơn & thực đơn (Port 5001)
    ├── KitchenWorker/              # Background Worker lắng nghe queue & làm món
    ├── NotificationService/        # Realtime SignalR Hub phát thông báo (Port 5003)
    └── Client/                     # Frontend Web: Màn hình khách & Màn hình bếp KDS
```

---

## 4. Hướng dẫn cài đặt và Khởi chạy (Dành cho Tuần 1 & 2)

### Bước 1: Khởi động Hạ tầng Container (PostgreSQL & RabbitMQ)
Đảm bảo đã mở Docker Desktop, tại thư mục gốc chạy:
```powershell
docker compose up -d
```
Kiểm tra các dịch vụ đang chạy:
- **PostgreSQL Database:** `localhost:5432` (User: `postgres`, Pass: `postgres`, DB: `food_order_db`)
- **RabbitMQ Web Management:** `http://localhost:15672` (User: `guest`, Pass: `guest`)

### Bước 2: Biên dịch toàn bộ Solution .NET
```powershell
dotnet build FoodOrderSystem.sln
```
*Kết quả:* Biên dịch thành công 4 project (`Shared`, `OrderApi`, `KitchenWorker`, `NotificationService`) với 0 Warning và 0 Error.

### Bước 3: Khởi chạy các Service (Mở riêng từng Terminal)

**Terminal 1 - Khởi chạy Order API:**
```powershell
dotnet run --project src/OrderApi/OrderApi.csproj --urls "http://localhost:5001"
```
*Truy cập Swagger UI:* `http://localhost:5001` để test các API `/api/menu`, `/api/orders`.

**Terminal 2 - Khởi chạy Notification Service:**
```powershell
dotnet run --project src/NotificationService/NotificationService.csproj --urls "http://localhost:5003"
```
*Truy cập Endpoint:* `http://localhost:5003`

**Terminal 3 - Khởi chạy Kitchen Worker:**
```powershell
dotnet run --project src/KitchenWorker/KitchenWorker.csproj
```

**Terminal 4 - Mở giao diện Web Client:**
Mở trực tiếp file `src/Client/index.html` (Khách hàng) và `src/Client/kitchen.html` (Màn hình bếp) trên trình duyệt hoặc chạy qua Live Server:
```powershell
npx serve src/Client -p 8080
```
Truy cập: `http://localhost:8080`
