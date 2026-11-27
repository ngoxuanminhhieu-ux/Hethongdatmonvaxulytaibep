# KẾ HOẠCH TRIỂN KHAI DỰ ÁN 9 TUẦN
## Đề tài 09: Hệ thống Đặt lịch khám và Quản lý hàng đợi (Clinic Appointment & Queue Management System)
**Môn học:** Phát triển ứng dụng phân tán  
**Phương pháp quản lý:** Agile/Scrum theo tuần, phân rã mục tiêu tăng dần (Iterative & Incremental)

---

### BẢNG TIẾN ĐỘ VÀ KẾ HOẠCH CHI TIẾT THEO TUẦN

#### TUẦN 1: Khảo sát, phân tích yêu cầu và lập kế hoạch
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Xác định phạm vi bài toán Đề tài 09, phân tích yêu cầu chức năng/phi chức năng, mô hình hóa Use Case, thiết kế kiến trúc phân tán sơ bộ và thiết lập cấu trúc mã nguồn Git. |
| **Công việc** | 1. Nghiên cứu đề tài 09: nghiệp vụ đặt lịch khám bác sĩ, cấp số thứ tự khám tự động và điều phối hàng đợi phòng khám.<br/>2. Xác định các Stakeholders (Bệnh nhân, Bác sĩ/Nhân viên y tế, Quản trị viên) và danh sách Functional/Non-functional Requirements.<br/>3. Xây dựng biểu đồ Use Case và viết đặc tả ca sử dụng chính.<br/>4. Xây dựng sơ đồ kiến trúc tổng quan hệ thống phân tán.<br/>5. Lựa chọn và biện luận bộ công nghệ (.NET 8, PostgreSQL, RabbitMQ, SignalR).<br/>6. Khởi tạo Git repository, cấu hình `.gitignore`, `README.md` và khung tài liệu dự án. |
| **Đầu vào** | Đề cương môn học PTUDPT, mô tả đề tài 09 từ giảng viên. |
| **Đầu ra** | - Bộ tài liệu phân tích yêu cầu (`docs/tuan-1/YEU_CAU_HE_THONG.md`).<br/>- Sơ đồ Use Case và đặc tả (`docs/tuan-1/USE_CASE_DIAGRAM.md`).<br/>- Sơ đồ kiến trúc tổng quan (`docs/tuan-1/ARCHITECTURE_OVERVIEW.md`).<br/>- Kế hoạch 9 tuần (`docs/KE_HOACH_9_TUAN.md`).<br/>- Repository Git đã khởi tạo. |
| **Người phụ trách**| Tech Lead / Senior Architect & Toàn đội ngũ phát triển. |
| **Công nghệ** | Git, Markdown, Mermaid, VS Code / Visual Studio. |
| **Tiêu chí nghiệm thu** | - Tài liệu bao quát đầy đủ phạm vi đề bài Đề tài 09.<br/>- Sơ đồ kiến trúc thể hiện rõ luồng phân tán (Patient Client $\to$ Appointment API $\to$ RabbitMQ $\to$ Queue Worker $\to$ RabbitMQ $\to$ Notification Service $\to$ SignalR $\to$ Client).<br/>- Repo Git sạch, cấu trúc rõ ràng. |
| **Rủi ro** | Chưa làm rõ cơ chế xử lý tranh chấp số thứ tự hoặc slot khám. *(Giảm thiểu: Áp dụng cơ chế Message Queue tuần tự hóa việc cấp số và ACID Transaction khi kiểm tra slot).* |

---

#### TUẦN 2: Thiết kế kiến trúc chi tiết, Cơ sở dữ liệu và Khởi tạo Project Skeleton
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Thiết kế CSDL quan hệ chi tiết (ERD), thiết kế hợp đồng API (API Contracts), thiết kế cấu trúc message và topology RabbitMQ, khởi tạo solution và chạy thử nghiệm hạ tầng container. |
| **Công việc** | 1. Thiết kế ERD với 5 bảng cốt lõi: `doctors`, `time_slots`, `patients`, `appointments`, `queue_logs`.<br/>2. Viết file DDL khởi tạo schema và seed data mẫu (`database/init.sql`).<br/>3. Thiết kế đặc tả REST API (`GET /doctors`, `GET /doctors/{id}/slots`, `POST /appointments`, `DELETE /appointments/{id}`, `GET /appointments/{id}`, `GET /queues/{doctorId}`).<br/>4. Thiết kế Schema sự kiện RabbitMQ (`AppointmentRequestedEvent`, `QueueNumberAssignedEvent`, `QueueServingChangedEvent`).<br/>5. Thiết kế Exchange (`clinic.appointment.exchange`), Queues (`queue.process.queue`, `queue.notification.queue`), Routing Keys và Dead Letter Queue (DLQ).<br/>6. Tạo Solution .NET gồm `AppointmentApi`, `QueueWorker`, `NotificationService`, `Shared`.<br/>7. Viết file `docker-compose.yml` cho PostgreSQL và RabbitMQ Management. |
| **Đầu vào** | Tài liệu phân tích yêu cầu và kiến trúc tổng quan Tuần 1. |
| **Đầu ra** | - Sơ đồ ERD và script CSDL (`docs/tuan-2/THIET_KE_CSDL_ERD.md`, `database/init.sql`).<br/>- Tài liệu hợp đồng API (`docs/tuan-2/API_CONTRACT.md`).<br/>- Tài liệu RabbitMQ Topology & Message Contract (`docs/tuan-2/RABBITMQ_DESIGN.md`).<br/>- Solution C# biên dịch thành công (`dotnet build` = 0 errors).<br/>- Docker Compose khởi chạy PostgreSQL và RabbitMQ thành công. |
| **Người phụ trách**| Database Architect & Backend Lead. |
| **Công nghệ** | .NET 8, C#, PostgreSQL 16, RabbitMQ 3.13-management, Docker Compose. |
| **Tiêu chí nghiệm thu** | - Chạy `docker compose up -d` khởi động được PostgreSQL (port 5432) và RabbitMQ Management (port 15672).<br/>- Chạy `dotnet build` toàn bộ solution không có lỗi cú pháp hay thiếu package. |
| **Rủi ro** | Thiếu trường dữ liệu khung giờ hoặc trạng thái hàng đợi. *(Giảm thiểu: Bổ sung bảng `time_slots` giới hạn công suất và `queue_logs` lưu vết kiểm toán).* |

---

#### TUẦN 3: Xây dựng Appointment API và Quản lý lịch khám
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Hiện thực hóa Appointment API kết nối CSDL PostgreSQL, hoàn thành các endpoint nghiệp vụ cơ bản và kiểm thử tự động với Postman. |
| **Công việc** | 1. Cấu hình DbContext, Entity Framework Core kết nối PostgreSQL.<br/>2. Hiện thực `GET /api/doctors` và `GET /api/doctors/{id}/slots` (truy vấn bác sĩ và khung giờ còn chỗ).<br/>3. Hiện thực `POST /api/appointments` (kiểm tra slot còn chỗ, tạo bệnh nhân, lưu lịch hẹn ở trạng thái `Pending`).<br/>4. Hiện thực `DELETE /api/appointments/{id}` (hủy lịch khám, hoàn trả số chỗ trống của slot).<br/>5. Hiện thực `GET /api/appointments/{id}` và `GET /api/queues/{doctorId}`.<br/>6. Xử lý Exception Handling Middleware và Data Validation.<br/>7. Xuất bản và kiểm thử qua Swagger UI & Postman Collection. |
| **Đầu vào** | CSDL đã dựng, Entity model và API Contract từ Tuần 2. |
| **Đầu ra** | - Appointment API chạy độc lập tại `http://localhost:5001`.<br/>- Dữ liệu lịch hẹn ghi nhận thành công vào bảng `appointments` và `patients`.<br/>- File Postman Collection `ClinicQueue_API_Collection.json` kèm kết quả test. |
| **Người phụ trách**| Backend Developer (Appointment API Team). |
| **Công nghệ** | ASP.NET Core Web API, EF Core / Npgsql, Postman, Swagger. |
| **Tiêu chí nghiệm thu** | Gọi `POST /api/appointments` từ Postman trả về mã `201 Created`, CSDL cập nhật `current_bookings` của khung giờ chính xác. |
| **Rủi ro** | Lỗi race condition khi nhiều người cùng đặt 1 slot cuối cùng. *(Giảm thiểu: Kiểm tra điều kiện `CurrentBookings < MaxCapacity` trong Transaction).* |

---

#### TUẦN 4: Tích hợp RabbitMQ vào Appointment API
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Thiết lập kết nối bất đồng bộ kiên cố từ Appointment API đến RabbitMQ: Message Durability, Resilient Connection, Publisher Confirms và kiểm chứng qua Web Management. |
| **Công việc** | 1. Hiện thực `RabbitMqPersistentConnection` (tự động reconnect với Polly retry exponential backoff).<br/>2. Khởi tạo Topology RabbitMQ tự động khi API startup (`RabbitMqTopologyInitializer`): Exchange, Queues, DLX, DLQ.<br/>3. Hiện thực `RabbitMqProducer` hỗ trợ Publisher Confirms, Persistent Delivery Mode.<br/>4. Bổ sung endpoint chẩn đoán: `GET /api/rabbitmq/health`, `GET /api/rabbitmq/topology`, `POST /api/rabbitmq/test-publish`.<br/>5. Tích hợp publish sự kiện `AppointmentRequestedEvent` ngay khi tạo lịch hẹn thành công.<br/>6. Kiểm chứng hàng đợi qua RabbitMQ Management UI (`http://localhost:15672`). |
| **Đầu vào** | Appointment API tuần 3, RabbitMQ Docker container. |
| **Đầu ra** | - Appointment API gửi message thành công lên RabbitMQ với routing key `appointment.requested`.<br/>- Message nằm bền vững trong `queue.process.queue`.<br/>- Báo cáo kiểm chứng RabbitMQ Management UI (`docs/tuan-4/KIEM_CHUNG_THONG_DIEP_WEB_MANAGEMENT.md`). |
| **Người phụ trách**| Distributed Systems Engineer & Backend Lead. |
| **Công nghệ** | RabbitMQ.Client 6.8.1, Polly, RabbitMQ Web Management. |
| **Tiêu chí nghiệm thu** | Đặt lịch khám qua API $\to$ Message xuất hiện ngay trong `queue.process.queue` trên RabbitMQ UI với thuộc tính `delivery_mode=2` (Persistent). |
| **Rủi ro** | Mất message khi RabbitMQ khởi động lại. *(Giảm thiểu: Durable exchange, durable queues và delivery_mode=2).* |

---

#### TUẦN 5: Xây dựng Queue Worker và Cơ chế cấp số tự động
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Xây dựng Queue Worker xử lý bất đồng bộ message từ RabbitMQ: sinh số thứ tự tự động theo bác sĩ/ngày, cập nhật trạng thái lịch hẹn, ghi nhật ký và xử lý lỗi vào Dead Letter Queue. |
| **Công việc** | 1. Xây dựng .NET BackgroundService `QueueWorker` độc lập lắng nghe `queue.process.queue`.<br/>2. Lấy số thứ tự lớn nhất hiện tại của bác sĩ trong ngày để sinh số tiếp theo (`MAX(queue_number) + 1`).<br/>3. Cập nhật trạng thái lịch hẹn: `Pending` $\to$ `Confirmed`, gán số thứ tự.<br/>4. Ghi log kiểm toán vào bảng `queue_logs`.<br/>5. Cơ chế Manual Acknowledgment (`basicAck`), Dead Letter Exchange khi gặp lỗi không thể phục hồi (`basicNack` with `requeue=false`).<br/>6. Phát sự kiện `QueueNumberAssignedEvent` lên RabbitMQ (routing key `queue.number.assigned`). |
| **Đầu vào** | Queue `queue.process.queue` có message từ Tuần 4. |
| **Đầu ra** | - Queue Worker xử lý thông điệp tự động không làm nghẽn API.<br/>- Bảng `appointments` cập nhật `queue_number` và trạng thái `Confirmed`.<br/>- Log xử lý chi tiết thể hiện từng bước tiêu thụ message. |
| **Người phụ trách**| Worker Developer. |
| **Công nghệ** | .NET 8 Worker Service, EF Core, RabbitMQ Client. |
| **Tiêu chí nghiệm thu** | Gửi 5 yêu cầu đặt lịch $\to$ Queue Worker sinh đúng thứ tự số 1, 2, 3, 4, 5 cho cùng bác sĩ trong ngày mà không bị trùng lặp số. |
| **Rủi ro** | Trùng số thứ tự khi tải cao. *(Giảm thiểu: Worker xử lý tuần tự theo queue hoặc sử dụng atomic sequence trong DB).* |

---

#### TUẦN 6: Xây dựng Notification Service và Realtime SignalR
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Xây dựng Notification Service độc lập nhận các sự kiện thay đổi hàng đợi từ RabbitMQ và đẩy thông báo thời gian thực về Client qua SignalR Hub. |
| **Công việc** | 1. Khởi tạo dự án `NotificationService` với ASP.NET Core SignalR.<br/>2. Xây dựng `QueueNotificationHub` hỗ trợ client tham gia các nhóm: `doctor-queue-{doctorId}` và `appointment-{appointmentId}`.<br/>3. Tạo Background Consumer lắng nghe queue `queue.notification.queue` (bound với `queue.*` và `appointment.*`).<br/>4. Phát sóng sự kiện realtime: `ReceiveQueueUpdate` (khi lượt khám thay đổi, gọi số mới) và `ReceiveAppointmentStatus` (khi số thứ tự được cấp hoặc lịch bị hủy).<br/>5. Cấu hình CORS và kiểm thử kết nối WebSocket/SignalR. |
| **Đầu vào** | Sự kiện phát từ Queue Worker và Appointment API. |
| **Đầu ra** | - Notification Service chạy tại `http://localhost:5003`.<br/>- SignalR Hub kết nối ổn định, broadcast đúng group mục tiêu.<br/>- Kiểm thử nhận tin realtime từ Postman hoặc SignalR Client. |
| **Người phụ trách**| Realtime Specialist & Fullstack Developer. |
| **Công nghệ** | ASP.NET Core SignalR, WebSocket, RabbitMQ Client. |
| **Tiêu chí nghiệm thu** | Khi bác sĩ gọi số tiếp theo, màn hình bệnh nhân và màn hình phòng khám lập tức hiển thị số mới mà không cần F5/Reload trang. |
| **Rủi ro** | Rò rỉ kết nối WebSocket hoặc gửi nhầm nhóm phòng khám khác. *(Giảm thiểu: Quản lý ConnectionId theo Groups nghiêm ngặt).* |

---

#### TUẦN 7: Xây dựng Giao diện người dùng và Tích hợp toàn hệ thống
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Hoàn thiện giao diện cho 2 đối tượng người dùng chính (Bệnh nhân đặt khám & Bác sĩ điều phối phòng khám), tích hợp thông suốt từ Client đến Backend. |
| **Công việc** | 1. Xây dựng giao diện Bệnh nhân (`index.html`): xem danh sách bác sĩ, chọn khung giờ còn chỗ, đặt lịch, nhận phiếu khám số thứ tự và theo dõi tiến độ hàng đợi.<br/>2. Xây dựng màn hình Bác sĩ / Phòng khám (`doctor.html`): hiển thị số lớn đang phục vụ, danh sách bệnh nhân đang chờ, nút "Gọi số tiếp theo", "Hoàn thành khám", "Báo vắng mặt".<br/>3. Tích hợp thư viện `@microsoft/signalr` trên trình duyệt để lắng nghe sự kiện từ `NotificationService`.<br/>4. Kiểm thử luồng End-to-End: Bệnh nhân đặt lịch $\to$ API $\to$ RabbitMQ $\to$ Worker cấp số $\to$ SignalR cập nhật phiếu $\to$ Bác sĩ bấm Next $\to$ Cả hai màn hình nhảy số tức thì. |
| **Đầu vào** | Tất cả các API, Worker và SignalR Service từ các tuần trước. |
| **Đầu ra** | - Giao diện Web Client hoàn chỉnh, thẩm mỹ, responsive.<br/>- Kịch bản kiểm thử End-to-End hoạt động trơn tru. |
| **Người phụ trách**| Frontend Developer & QA Engineer. |
| **Công nghệ** | HTML5, Modern CSS, Vanilla JS, SignalR Client JavaScript. |
| **Tiêu chí nghiệm thu** | Mở song song 2 tab trình duyệt (1 tab Bệnh nhân, 1 tab Bác sĩ), thao tác gọi khám trên tab Bác sĩ làm tab Bệnh nhân đổi trạng thái ngay lập tức. |
| **Rủi ro** | Lỗi CORS khi Client (port 3000/5500) gọi API (port 5001) và SignalR (port 5003). *(Giảm thiểu: Cấu hình CORS AllowAnyOrigin, AllowCredentials chuẩn).* |

---

#### TUẦN 8: Xử lý ngoại lệ, Giám sát, Tối ưu và Kiểm thử tải
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Nâng cao tính kiên cố của hệ thống phân tán: xử lý lỗi mạng, retry cơ chế Dead Letter Queue, viết bộ unit test toàn diện và kiểm thử chịu tải. |
| **Công việc** | 1. Xây dựng kịch bản kiểm thử Dead Letter Queue (DLQ) khi dữ liệu message bị lỗi cấu trúc.<br/>2. Viết bộ kiểm thử đơn vị (Unit Tests) cho `DoctorsController`, `AppointmentsController`, `QueuesController`, `RabbitMqDiagnosticsController` (19 test cases đạt 100% pass).<br/>3. Tối ưu index CSDL trên các trường `doctor_id`, `status`, `slot_date`, `queue_number`.<br/>4. Kiểm tra khả năng chịu lỗi: ngắt kết nối RabbitMQ hoặc PostgreSQL để kiểm chứng hành vi khôi phục kết nối tự động. |
| **Đầu vào** | Toàn bộ source code hệ thống. |
| **Đầu ra** | - Báo cáo kiểm thử tự động xUnit (19/19 tests Passed).<br/>- Luồng DLQ hoạt động chính xác khi có poison message.<br/>- Chỉ số hiệu năng hệ thống ổn định. |
| **Người phụ trách**| QA Lead & Senior Architect. |
| **Công nghệ** | xUnit, Moq, SQLite In-Memory, Postman Runner. |
| **Tiêu chí nghiệm thu** | `dotnet test` trả về 0 failed; message lỗi không làm sập Worker mà được chuyển vào `queue.dlq`. |
| **Rủi ro** | Worker bị crash liên tục do unhandled exception. *(Giảm thiểu: Bọc try-catch toàn diện và nack chuyển vào DLQ).* |

---

#### TUẦN 9: Đóng gói Docker Compose, Viết báo cáo kỹ thuật và Nghiệm thu dự án
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Đóng gói toàn bộ hệ thống vào Docker Compose để triển khai bằng 1 lệnh duy nhất, hoàn thiện tài liệu hướng dẫn và video/demo nghiệm thu. |
| **Công việc** | 1. Viết Dockerfile cho `AppointmentApi`, `QueueWorker`, `NotificationService`.<br/>2. Cấu hình `docker-compose.yml` liên kết toàn bộ: PostgreSQL, RabbitMQ, 3 Services và Nginx / Static Web.<br/>3. Kiểm tra khởi động toàn hệ thống bằng lệnh `docker compose up -d`.<br/>4. Hoàn thiện tài liệu kiến trúc, báo cáo đề tài 09 và Slide thuyết trình.<br/>5. Rà soát source code, push mã nguồn hoàn chỉnh lên GitHub. |
| **Đầu vào** | Toàn bộ hệ sinh thái dịch vụ và tài liệu đã hoàn thiện. |
| **Đầu ra** | - File `docker-compose.yml` hoàn chỉnh có thể chạy ở bất kỳ máy tính nào có Docker.<br/>- Báo cáo tổng kết dự án môn học PTUDPT.<br/>- Repository GitHub cập nhật đầy đủ mã nguồn và tài liệu. |
| **Người phụ trách**| DevOps Engineer & Tech Lead. |
| **Công nghệ** | Docker, Docker Compose, Git, Markdown. |
| **Tiêu chí nghiệm thu** | Chạy `docker compose up` trên một máy sạch, toàn bộ hệ thống khởi động thành công và thực hiện được toàn bộ luồng khám bệnh. |
| **Rủi ro** | Lỗi phân giải tên DNS hoặc thứ tự phụ thuộc dịch vụ trong Docker. *(Giảm thiểu: Sử dụng healthchecks và `depends_on: condition: service_healthy`).* |
