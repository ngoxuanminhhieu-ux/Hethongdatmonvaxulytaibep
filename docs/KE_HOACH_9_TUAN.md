# KẾ HOẠCH TRIỂN KHAI DỰ ÁN 9 TUẦN
## Đề tài 04: Hệ thống Đặt món và Xử lý tại Bếp (Food Ordering & Kitchen Processing System)
**Môn học:** Phát triển ứng dụng phân tán  
**Phương pháp quản lý:** Agile/Scrum theo tuần, phân rã mục tiêu tăng dần (Iterative & Incremental)

---

### BẢNG TIẾN ĐỘ VÀ KẾ HOẠCH CHI TIẾT THEO TUẦN

#### TUẦN 1: Khảo sát, phân tích yêu cầu và lập kế hoạch
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Xác định phạm vi bài toán, phân tích toàn bộ yêu cầu chức năng/phi chức năng, mô hình hóa Use Case, thiết kế kiến trúc phân tán sơ bộ và thiết lập cấu trúc mã nguồn Git. |
| **Công việc** | 1. Họp nhóm/nghiên cứu đề tài 04: phân tích nghiệp vụ F&B đặt món tại bàn.<br/>2. Liệt kê các Stakeholders và lập danh sách Functional/Non-functional Requirements.<br/>3. Xây dựng biểu đồ Use Case và viết đặc tả ca sử dụng chính.<br/>4. Vẽ sơ đồ kiến trúc tổng quan hệ thống phân tán.<br/>5. Lựa chọn và biện luận bộ công nghệ (.NET 8, PostgreSQL, RabbitMQ, SignalR).<br/>6. Khởi tạo Git repository, cấu hình `.gitignore`, `README.md` và khung tài liệu dự án. |
| **Đầu vào** | Đề cương môn học PTUDPT, mô tả đề tài 04 từ giảng viên. |
| **Đầu ra** | - Bộ tài liệu phân tích yêu cầu (`docs/tuan-1/YEU_CAU_HE_THONG.md`).<br/>- Sơ đồ Use Case và đặc tả (`docs/tuan-1/USE_CASE_DIAGRAM.md`).<br/>- Sơ đồ kiến trúc tổng quan (`docs/tuan-1/ARCHITECTURE_OVERVIEW.md`).<br/>- Kế hoạch 9 tuần (`docs/KE_HOACH_9_TUAN.md`).<br/>- Repository Git đã khởi tạo. |
| **Người phụ trách**| Tech Lead / Senior Architect & Toàn đội ngũ phát triển. |
| **Công nghệ** | Git, Markdown, Mermaid, VS Code / Visual Studio. |
| **Tiêu chí nghiệm thu** | - Tài liệu bao quát đầy đủ phạm vi đề bài.<br/>- Sơ đồ kiến trúc thể hiện rõ luồng phân tán (Client $\to$ API $\to$ RabbitMQ $\to$ Kitchen Worker $\to$ RabbitMQ $\to$ Notification $\to$ Client).<br/>- Repo Git sạch, cấu trúc rõ ràng. |
| **Rủi ro** | Xác định sai phạm vi hoặc chọn công nghệ quá phức tạp dẫn đến trễ tiến độ các tuần sau. *(Giảm thiểu: Chọn stack .NET 8 + RabbitMQ + PostgreSQL chuẩn mực).* |

---

#### TUẦN 2: Thiết kế kiến trúc chi tiết, Cơ sở dữ liệu và Khởi tạo Project Skeleton
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Thiết kế CSDL quan hệ chi tiết (ERD), thiết kế hợp đồng API (API Contracts), thiết kế cấu trúc message và topology RabbitMQ, khởi tạo solution và chạy thử nghiệm hạ tầng container. |
| **Công việc** | 1. Thiết kế ERD với 5 bảng cốt lõi: `Customer`, `MenuItem`, `Order`, `OrderDetail`, `OrderStatusHistory`.<br/>2. Viết file DDL khởi tạo schema và seed data mẫu (`database/init.sql`).<br/>3. Thiết kế đặc tả REST API (`GET /menu`, `POST /orders`, `GET /orders/{id}`, `GET /orders`).<br/>4. Thiết kế Schema sự kiện RabbitMQ (`OrderCreatedEvent`, `OrderStatusChangedEvent`).<br/>5. Thiết kế Exchange, Queue, Routing Key và Dead Letter Queue (DLQ).<br/>6. Tạo Solution .NET gồm `OrderApi`, `KitchenWorker`, `NotificationService`, `Shared`.<br/>7. Viết file `docker-compose.yml` cho PostgreSQL và RabbitMQ Management. |
| **Đầu vào** | Tài liệu phân tích yêu cầu và kiến trúc tổng quan Tuần 1. |
| **Đầu ra** | - Sơ đồ ERD và script CSDL (`docs/tuan-2/THIET_KE_CSDL_ERD.md`, `database/init.sql`).<br/>- Tài liệu hợp đồng API (`docs/tuan-2/API_CONTRACT.md`).<br/>- Tài liệu RabbitMQ Topology & Message Contract (`docs/tuan-2/RABBITMQ_DESIGN.md`).<br/>- Solution C# biên dịch thành công (`dotnet build` = 0 errors).<br/>- Docker Compose khởi chạy PostgreSQL và RabbitMQ thành công. |
| **Người phụ trách**| Database Architect & Backend Lead. |
| **Công nghệ** | .NET 8, C#, PostgreSQL 16, RabbitMQ 3.13-management, Docker Compose. |
| **Tiêu chí nghiệm thu** | - Chạy `docker compose up -d` khởi động được PostgreSQL (port 5432) và RabbitMQ Management (port 15672).<br/>- Chạy `dotnet build` toàn bộ solution không có lỗi cú pháp hay thiếu package. |
| **Rủi ro** | Thiếu trường dữ liệu quan trọng trong CSDL ảnh hưởng đến các tuần sau. *(Giảm thiểu: Kiểm tra kỹ tính toàn vẹn và lịch sử đơn hàng).* |

---

#### TUẦN 3: Xây dựng Order API và Quản lý thực đơn
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Hiện thực hóa Order API kết nối CSDL PostgreSQL, hoàn thành các endpoint nghiệp vụ cơ bản và kiểm thử tự động với Postman. |
| **Công việc** | 1. Cấu hình DbContext, Entity Framework Core / Dapper kết nối PostgreSQL.<br/>2. Hiện thực `GET /api/menu` (truy vấn danh sách món ăn khả dụng).<br/>3. Hiện thực `POST /api/orders` (tiếp nhận đơn hàng, tính tổng tiền, validate, lưu đơn vào DB trong Transaction).<br/>4. Hiện thực `GET /api/orders/{id}` và `GET /api/orders`.<br/>5. Xử lý Exception Handling Middleware và Data Validation.<br/>6. Xuất bản và kiểm thử qua Swagger UI & Postman Collection. |
| **Đầu vào** | CSDL đã dựng, Entity model và API Contract từ Tuần 2. |
| **Đầu ra** | - Order API chạy độc lập tại `http://localhost:5001`.<br/>- Dữ liệu đơn hàng ghi nhận thành công vào bảng `orders` và `order_details`.<br/>- File Postman Collection `Order_API_Collection.json` kèm kết quả test. |
| **Người phụ trách**| Backend Developer (Order API Team). |
| **Công nghệ** | ASP.NET Core Web API, EF Core / Npgsql, Postman, Swagger. |
| **Tiêu chí nghiệm thu** | Gọi `POST /api/orders` từ Postman trả về mã `201 Created`, CSDL lưu đúng các dòng chi tiết và tổng tiền chính xác. |
| **Rủi ro** | Lỗi Transaction rollback khi một món trong đơn không hợp lệ. |

---

#### TUẦN 4: Tích hợp RabbitMQ vào Order API
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Tích hợp thành công RabbitMQ Producer vào Order API, xuất bản sự kiện `OrderCreatedEvent` một cách tin cậy ngay khi đơn hàng được tạo. |
| **Công việc** | 1. Cài đặt và cấu hình thư viện `RabbitMQ.Client` trong Order API.<br/>2. Hiện thực RabbitMQ Connection Factory và cơ chế Singleton/Resilient connection.<br/>3. Khởi tạo Topology: Exchange `food.order.exchange` loại Topic, Queue `order.process.queue`.<br/>4. Trong luồng `POST /api/orders`, sau khi Commit DB thành công, publish event `OrderCreatedEvent` kèm CorrelationId.<br/>5. Xử lý ngoại lệ nếu broker mất kết nối (ghi log cảnh báo, graceful degradation).<br/>6. Kiểm chứng message xuất hiện trên RabbitMQ Management Console. |
| **Đầu vào** | Order API hoàn thiện từ Tuần 3, cụm RabbitMQ container từ Tuần 2. |
| **Đầu ra** | - Order API có khả năng publish message lên RabbitMQ.<br/>- Bằng chứng (ảnh chụp/log) message nằm trong queue `order.process.queue`.<br/>- Tài liệu mô tả cơ chế truyền tin Producer và message attributes (Persistent delivery mode). |
| **Người phụ trách**| Distributed Systems Engineer. |
| **Công nghệ** | RabbitMQ.Client, AMQP 0-9-1, System.Text.Json. |
| **Tiêu chí nghiệm thu** | Tạo 1 đơn hàng qua API, vào Web Management `http://localhost:15672` thấy số lượng Ready Message trong queue `order.process.queue` tăng lên 1 và nội dung JSON khớp với đơn hàng. |
| **Rủi ro** | Kết nối RabbitMQ bị ngắt quãng do network glitch. |

---

#### TUẦN 5: Xây dựng Kitchen Worker (Background Consumer)
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Xây dựng Kitchen Worker độc lập, tiêu thụ đơn hàng từ queue, cập nhật trạng thái chế biến và phát sự kiện `OrderStatusChangedEvent`. |
| **Công việc** | 1. Hiện thực `BackgroundService` trong project `KitchenWorker`.<br/>2. Lắng nghe queue `order.process.queue` với cờ `autoAck = false` (Manual Acknowledgement).<br/>3. Kiểm tra Idempotency: tránh xử lý lại đơn đã được xử lý.<br/>4. Cập nhật trạng thái đơn trong CSDL (`Pending` $\to$ `Preparing` $\to$ `Ready`).<br/>5. Ghi nhận lịch sử vào bảng `order_status_histories`.<br/>6. Publish sự kiện `OrderStatusChangedEvent` lên Exchange với routing key `order.status.changed`.<br/>7. Cấu hình xử lý lỗi và đẩy message lỗi sang Dead Letter Queue (DLQ). |
| **Đầu vào** | Message trong queue từ Order API (Tuần 4). |
| **Đầu ra** | - Kitchen Worker chạy độc lập, tự động bốc đơn từ queue và xử lý.<br/>- Trạng thái đơn hàng trong DB tự động chuyển đổi có kiểm soát.<br/>- Log chi tiết quá trình consume, process và ack message. |
| **Người phụ trách**| Backend Developer (Worker Team). |
| **Công nghệ** | .NET 8 Worker Service, RabbitMQ Consumer, PostgreSQL. |
| **Tiêu chí nghiệm thu** | Chạy đồng thời Order API và Kitchen Worker: khi tạo đơn qua API, Kitchen Worker lập tức nhận được message, log ra màn hình, DB cập nhật trạng thái thành `Preparing` rồi `Ready`. |
| **Rủi ro** | Worker crash giữa chừng làm mất message. *(Khắc phục: Dùng Manual Ack, chỉ gửi Ack sau khi DB đã commit).* |

---

#### TUẦN 6: Xây dựng Notification Service và Cơ chế Realtime
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Xây dựng Notification Service độc lập, lắng nghe sự kiện thay đổi trạng thái từ RabbitMQ và đẩy thông báo tức thì tới Client qua SignalR. |
| **Công việc** | 1. Thiết lập SignalR Hub (`OrderNotificationHub`) trong project `NotificationService`.<br/>2. Quản lý kết nối Client theo Group: `JoinOrderGroup(orderId)`.<br/>3. Tạo background consumer lắng nghe queue `order.notification.queue`.<br/>4. Khi nhận được `OrderStatusChangedEvent`, phân giải thông điệp và gọi `Clients.Group($"order-{orderId}").SendAsync(...)`.<br/>5. Viết trang web HTML test đơn giản kết nối SignalR để nhận push message. |
| **Đầu vào** | Sự kiện `OrderStatusChangedEvent` từ Kitchen Worker (Tuần 5). |
| **Đầu ra** | - Notification Service chạy tại `http://localhost:5003`.<br/>- SignalR Hub hoạt động và quản lý kết nối realtime.<br/>- Client nhận được thông báo ngay lập tức khi Bếp đổi trạng thái. |
| **Người phụ trách**| Realtime & Integration Developer. |
| **Công nghệ** | ASP.NET Core SignalR, WebSockets, RabbitMQ Consumer. |
| **Tiêu chí nghiệm thu** | Mở trình duyệt kết nối tới hub với OrderId bất kỳ, kích hoạt Kitchen Worker đổi trạng thái đơn, trình duyệt nhận ngay thông báo JSON/alert với độ trễ < 500ms. |
| **Rủi ro** | Client mất kết nối mạng và không nhận được thông báo. *(Khắc phục: Cơ chế auto-reconnect của SignalR client).* |

---

#### TUẦN 7: Xây dựng Giao diện Người dùng và Tích hợp Toàn hệ thống
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Hoàn thiện giao diện Khách hàng (Menu, Giỏ hàng, Đặt món, Theo dõi đơn) và Màn hình Bếp (KDS), tích hợp toàn diện với các backend service. |
| **Công việc** | 1. Xây dựng giao diện Khách hàng: xem menu, giỏ hàng, đặt món, màn hình theo dõi đơn.<br/>2. Xây dựng giao diện Bếp KDS: bảng Kanban/danh sách đơn, nút chuyển trạng thái `Preparing`, `Ready`, `Cancelled`.<br/>3. Tích hợp thư viện `@microsoft/signalr` vào client để nhận thông báo thời gian thực.<br/>4. Tích hợp gọi REST API (`OrderApi`) cho cả 2 giao diện.<br/>5. Kiểm thử toàn bộ luồng trải nghiệm người dùng (UX) trên trình duyệt. |
| **Đầu vào** | Order API, Kitchen Worker và Notification Service đã hoạt động. |
| **Đầu ra** | - Giao diện Web Khách hàng (`src/Client/index.html`).<br/>- Giao diện Màn hình Bếp (`src/Client/kitchen.html`).<br/>- Hệ thống chạy thông suốt từ giao diện khách đến giao diện bếp. |
| **Người phụ trách**| Frontend Developer & Fullstack Lead. |
| **Công nghệ** | HTML5, CSS3/Tailwind, JavaScript ES6, SignalR Client. |
| **Tiêu chí nghiệm thu** | Đặt món từ màn hình Khách hàng $\to$ Đơn lập tức hiện lên màn hình Bếp $\to$ Bếp bấm "Bắt đầu nấu" $\to$ Màn hình Khách hàng lập tức đổi trạng thái sang "Đang nấu" mà không cần F5. |
| **Rủi ro** | Lỗi CORS khi gọi giữa Client và các API port khác nhau. *(Khắc phục: Cấu hình CORS Policy cho phép origin của Client).* |

---

#### TUẦN 8: Kiểm thử Phân tán, Tối ưu và Docker hóa toàn bộ
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Đóng gói toàn bộ các service thành Docker container, kiểm thử chịu lỗi (fault tolerance), xử lý message trùng lặp và kiểm tra tải đồng thời. |
| **Công việc** | 1. Viết `Dockerfile` cho `OrderApi`, `KitchenWorker`, `NotificationService`, `Client`.<br/>2. Hoàn thiện `docker-compose.yml` chạy đồng thời cả 5 thành phần cùng Database và RabbitMQ.<br/>3. Kiểm thử kịch bản phân tán 1: Tắt Kitchen Worker, tạo đơn qua API, kiểm tra message được giữ an toàn trong RabbitMQ; bật lại Worker, kiểm tra đơn được xử lý tuần tự.<br/>4. Kiểm thử kịch bản phân tán 2: Gửi thông điệp trùng lặp (Idempotency test).<br/>5. Kiểm thử kịch bản phân tán 3: Đưa message sai cấu trúc vào kiểm tra Dead Letter Queue (DLQ).<br/>6. Lập bảng báo cáo kết quả kiểm thử phân tán. |
| **Đầu vào** | Toàn bộ source code của các service và frontend. |
| **Đầu ra** | - Bộ Dockerfile và docker-compose hoàn chỉnh.<br/>- Báo cáo kết quả kiểm thử phân tán (`docs/tuan-8/BAO_CAO_KIEM_THU.md`).<br/>- Toàn bộ hệ thống khởi động chỉ với `docker compose up --build`. |
| **Người phụ trách**| DevOps / QA Engineer & Tech Lead. |
| **Công nghệ** | Docker, Docker Compose, Postman Runner / k6 / Apache Bench. |
| **Tiêu chí nghiệm thu** | Chạy `docker compose up` trên một máy tính mới, toàn bộ hệ thống khởi động và hoạt động trơn tru không lỗi kết nối. |
| **Rủi ro** | Thứ tự khởi động service bị lỗi (service khởi động trước khi DB hoặc RabbitMQ kịp sẵn sàng). *(Khắc phục: Dùng healthcheck và depends_on condition).* |

---

#### TUẦN 9: Hoàn thiện Dự án, Đóng gói Báo cáo và Chuẩn bị Bảo vệ
| Nội dung | Chi tiết |
|---|---|
| **Mục tiêu tuần** | Rà soát toàn bộ hệ thống, viết tài liệu hướng dẫn hoàn chỉnh, xây dựng slide thuyết trình và chuẩn bị kịch bản demo bảo vệ đề tài. |
| **Công việc** | 1. Rà soát chuẩn hóa mã nguồn, xóa bỏ console log dư thừa, cấu hình biến môi trường.<br/>2. Viết tài liệu `README.md` hướng dẫn cài đặt từng bước chi tiết.<br/>3. Viết Báo cáo tổng kết đồ án môn học (Word/PDF).<br/>4. Thiết kế Slide thuyết trình kiến trúc và kết quả đạt được.<br/>5. Xây dựng Kịch bản Demo trực tiếp (kèm trường hợp chứng minh tính chịu lỗi của RabbitMQ).<br/>6. Tập dượt thuyết trình và phản biện câu hỏi phân tán của hội đồng chấm. |
| **Đầu vào** | Sản phẩm hoàn chỉnh từ Tuần 1 đến Tuần 8. |
| **Đầu ra** | - Source code hoàn chỉnh trên GitHub.<br/>- File Báo cáo đồ án môn học.<br/>- File Slide thuyết trình (PowerPoint/PDF).<br/>- File Kịch bản Demo chi tiết từng bước. |
| **Người phụ trách**| Toàn bộ thành viên nhóm. |
| **Công nghệ** | Git, Markdown, PowerPoint, Word. |
| **Tiêu chí nghiệm thu** | Demo mượt mà không có lỗi kỹ thuật phát sinh; trả lời tự tin mọi câu hỏi về kiến trúc hướng sự kiện, RabbitMQ topology, tính nhất quán và xử lý lỗi phân tán. |
| **Rủi ro** | Demo trực tiếp gặp lỗi mạng hoặc sự cố bất ngờ. *(Khắc phục: Chuẩn bị video quay sẵn sơ cua).* |
