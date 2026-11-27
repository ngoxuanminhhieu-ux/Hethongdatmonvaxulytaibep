# HỆ THỐNG ĐẶT LỊCH KHÁM VÀ QUẢN LÝ HÀNG ĐỢI (CLINIC APPOINTMENT & QUEUE MANAGEMENT SYSTEM)
> **Môn học:** Phát triển ứng dụng phân tán (Distributed Application Development - PTUDPT-DA)  
> **Đề tài:** 09  
> **Thời gian:** 9 Tuần  
> **Kiến trúc:** Microservices / Event-Driven Architecture (RabbitMQ + PostgreSQL + ASP.NET Core 8 + SignalR)

---

## 1. Giới thiệu tổng quan

Hệ thống cho phép người dùng đặt lịch khám theo bác sĩ và khung giờ còn trống, nhận số thứ tự và theo dõi tiến độ hàng đợi theo thời gian thực.
Yêu cầu cấp số thứ tự được xử lý **bất đồng bộ qua RabbitMQ** để chống nghẽn và chống tranh chấp số. Khi lượt khám thay đổi, các client liên quan (bệnh nhân và màn hình phòng khám) nhận cập nhật thời gian thực bằng **SignalR**.

### Các chức năng chính:
- **Tra cứu bác sĩ / phòng khám và khung giờ:** Xem danh sách bác sĩ theo chuyên khoa và kiểm tra các khung giờ khám còn chỗ trống (`is_available = true`, `current_bookings < max_capacity`).
- **Đặt hoặc hủy lịch khám:** Tiếp nhận thông tin bệnh nhân, trừ slot trong Transaction CSDL, đưa yêu cầu cấp số thứ tự vào RabbitMQ (`appointment.requested`). Hỗ trợ hủy lịch khám và hoàn trả slot trống.
- **Xử lý cấp số thứ tự bất đồng bộ:** `Queue Worker` nhận yêu cầu từ queue, sinh số thứ tự liên tục tăng dần theo bác sĩ/ngày khám, cập nhật trạng thái `Confirmed` và lưu vết kiểm toán vào `queue_logs`.
- **Điều phối hàng đợi phòng khám:** Bác sĩ gọi số tiếp theo (`Call Next`), hoàn thành ca khám (`Complete Serving`), báo bệnh nhân vắng mặt (`No Show`).
- **Thông báo thời gian thực:** `Notification Service` lắng nghe sự kiện từ RabbitMQ và đẩy thông báo tức thời đến Client qua SignalR WebSockets.

---

## 2. Sơ đồ kiến trúc phân tán

```
Patient Client --------> Appointment API --------> PostgreSQL Database
(index.html)        (ASP.NET Core :5001)             (Port :5432)
                             |
                             v (Publish: appointment.requested)
                        RabbitMQ Topic Exchange: 'clinic.appointment.exchange'
                             |
                             v (Consume: queue.process.queue)
                        Queue Worker ------------> PostgreSQL Database
                     (.NET Worker Service)     (Assigns STT & Confirmed)
                             |
                             v (Publish: queue.number.assigned, queue.serving.changed)
                        RabbitMQ Broker
                             |
                             v (Consume: queue.notification.queue)
                    Notification Service --------> SignalR WebSocket --------> Client
                     (SignalR Hub :5003)         (ReceiveQueueUpdate)    (Patient & Doctor UI)
```

---

## 3. Cấu trúc thư mục dự án

```
PTUDPT-DA/
├── .gitignore                          # Cấu hình bỏ qua file build & tạm
├── README.md                           # Hướng dẫn tổng quan & khởi chạy hệ thống
├── docker-compose.yml                  # Containerization cho PostgreSQL & RabbitMQ
├── ClinicAppointmentSystem.sln         # Solution file .NET (5 projects)
├── database/
│   └── init.sql                        # Script DDL tạo bảng CSDL & dữ liệu mẫu (Seed Data)
├── docs/
│   ├── KE_HOACH_9_TUAN.md              # Kế hoạch chi tiết 9 tuần quản lý dự án
│   ├── tuan-1/
│   │   ├── YEU_CAU_HE_THONG.md         # Phân tích Functional & Non-Functional Requirements
│   │   ├── USE_CASE_DIAGRAM.md         # Biểu đồ Use Case và đặc tả kịch bản chính
│   │   └── ARCHITECTURE_OVERVIEW.md    # Sơ đồ kiến trúc phân tán tổng quan & công nghệ
│   ├── tuan-2/
│   │   ├── KIEN_TRUC_CHI_TIET.md       # Thiết kế chi tiết từng service & phân định ranh giới
│   │   ├── THIET_KE_CSDL_ERD.md        # Sơ đồ ERD, từ điển dữ liệu 5 bảng & Indexes
│   │   ├── API_CONTRACT.md             # Đặc tả chuẩn REST API (Request/Response)
│   │   └── RABBITMQ_DESIGN.md          # Topology RabbitMQ: Exchanges, Queues, DLQ, Routing Keys
│   ├── tuan-3/
│   │   ├── HUONG_DAN_TUAN_3.md         # Báo cáo kỹ thuật triển khai API tuần 3
│   │   ├── KET_QUA_KIEM_THU_TUAN_3.md  # Báo cáo kết quả kiểm thử tự động
│   │   └── postman/                    # Collection Postman tuần 3
│   └── tuan-4/
│       ├── TICH_HOP_RABBITMQ_CHUYEN_SAU.md  # Tài liệu tích hợp RabbitMQ kiên cố
│       ├── KIEM_CHUNG_THONG_DIEP_WEB_MANAGEMENT.md # Kiểm chứng qua RabbitMQ UI
│       └── postman/                    # Collection Postman tuần 4
├── postman/
│   └── ClinicQueue_API_Collection.json # Bộ test Postman tự động toàn diện
├── src/
│   ├── Shared/                         # Thư viện dùng chung (Contracts, Events, Enums)
│   │   ├── Contracts/                  # AppointmentRequestedEvent, QueueNumberAssignedEvent, ...
│   │   └── Enums/                      # AppointmentStatus (Pending, Confirmed, Serving, ...)
│   ├── AppointmentApi/                 # Web API tiếp nhận đặt lịch & điều phối
│   │   ├── Controllers/                # Doctors, Appointments, Queues, RabbitMqDiagnostics
│   │   ├── Data/                       # AppDbContext, Entities (Doctor, TimeSlot, Patient, ...)
│   │   ├── Models/                     # DTO Requests & Responses
│   │   └── Services/                   # RabbitMqPersistentConnection, Producer, TopologyInitializer
│   ├── QueueWorker/                    # Background Service cấp số thứ tự tự động
│   │   └── Worker.cs                   # Consumer lắng nghe queue.process.queue & cấp số
│   ├── NotificationService/            # Realtime Service SignalR Hub
│   │   ├── Hubs/                       # QueueNotificationHub (Groups theo doctorId)
│   │   └── Services/                   # Consumer lắng nghe queue.notification.queue
│   └── Client/                         # Giao diện người dùng Web Frontend
│       ├── index.html                  # Giao diện Bệnh nhân (Đặt khám & Phiếu STT realtime)
│       ├── doctor.html                 # Giao diện Màn hình Bác sĩ (Gọi số, Hoàn thành, Vắng mặt)
│       ├── css/style.css               # Thiết kế giao diện hiện đại
│       └── js/                         # Logic tương tác REST API & SignalR Client
└── tests/
    └── AppointmentApi.Tests/           # Bộ kiểm thử tự động xUnit (19/19 tests passed)
        ├── DoctorsControllerTests.cs
        ├── AppointmentsControllerTests.cs
        ├── QueuesControllerTests.cs
        └── RabbitMqDiagnosticsControllerTests.cs
```

---

## 4. Danh sách API tối thiểu (Topic 09)

| Method | Endpoint | Mô tả |
|---|---|---|
| `GET` | `/api/doctors` | Lấy danh sách bác sĩ / phòng khám (hỗ trợ lọc `specialty`, `search`). |
| `GET` | `/api/doctors/{id}/slots` | Lấy danh sách khung giờ còn chỗ theo bác sĩ. |
| `POST` | `/api/appointments` | Đặt lịch khám mới, kiểm tra slot trống, đưa yêu cầu cấp số vào RabbitMQ. |
| `DELETE` | `/api/appointments/{id}` | Hủy lịch khám, giải phóng chỗ trống của khung giờ. |
| `GET` | `/api/appointments/{id}` | Tra cứu chi tiết lịch hẹn, số thứ tự và lịch sử kiểm toán timeline. |
| `GET` | `/api/queues/{doctorId}` | Tra cứu trạng thái hàng đợi phòng khám (số đang phục vụ, danh sách chờ). |
| `POST` | `/api/queues/{doctorId}/next` | Bác sĩ gọi số thứ tự tiếp theo vào phòng khám (phát sự kiện realtime). |
| `POST` | `/api/queues/{doctorId}/complete` | Bác sĩ hoàn thành ca khám hiện tại. |
| `POST` | `/api/queues/{doctorId}/noshow` | Báo bệnh nhân vắng mặt. |
| `GET` | `/api/rabbitmq/health` | Kiểm tra tình trạng kết nối tới RabbitMQ Broker. |
| `GET` | `/api/rabbitmq/topology` | Kiểm tra cấu hình Exchanges, Queues và Bindings. |
| `POST` | `/api/rabbitmq/test-publish` | Gửi message kiểm tra kết nối RabbitMQ. |

---

## 5. Hướng dẫn khởi chạy hệ thống

### 5.1. Khởi động CSDL & Message Broker (Docker Compose)
```bash
docker compose up -d
```
- **PostgreSQL:** `localhost:5432` (Database: `clinic_queue_db`, User: `postgres`, Pass: `postgres123`)
- **RabbitMQ Broker:** `localhost:5672`
- **RabbitMQ Management UI:** `http://localhost:15672` (Tài khoản: `guest` / `guest`)

### 5.2. Chạy Appointment API (Port 5001)
```bash
dotnet run --project src/AppointmentApi/AppointmentApi.csproj
```
- **Swagger UI:** `http://localhost:5001/swagger`

### 5.3. Chạy Queue Worker (Background Service)
Mở cửa sổ terminal mới:
```bash
dotnet run --project src/QueueWorker/QueueWorker.csproj
```

### 5.4. Chạy Notification Service (Port 5003)
Mở cửa sổ terminal mới:
```bash
dotnet run --project src/NotificationService/NotificationService.csproj
```
- **SignalR Hub Endpoint:** `http://localhost:5003/hubs/queue-notification`

### 5.5. Trải nghiệm Giao diện Web Client
Mở trực tiếp bằng trình duyệt hoặc dùng Live Server (VS Code) / Python HTTP server:
- **Giao diện Bệnh nhân:** `src/Client/index.html`
- **Giao diện Màn hình Phòng khám Bác sĩ:** `src/Client/doctor.html`

---

## 6. Kiểm thử tự động (Automated Testing)

### 6.1. Chạy Unit Tests (.NET xUnit)
```bash
dotnet test ClinicAppointmentSystem.sln
```
**Kết quả:** 19/19 test cases thành công (100% Passed) bao gồm kiểm thử ACID Transaction, Slot Validation, Cấp số thứ tự, RabbitMQ Event Publishing và Exception Handling.

### 6.2. Chạy Postman Collection
Import file [ClinicQueue_API_Collection.json](file:///d:/PTUDPT-DA/postman/ClinicQueue_API_Collection.json) vào Postman và chạy với Collection Runner.
