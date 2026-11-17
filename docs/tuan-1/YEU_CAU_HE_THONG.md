# TÀI LIỆU PHÂN TÍCH YÊU CẦU HỆ THỐNG
## Đề tài 04: Hệ thống Đặt món và Xử lý tại Bếp (Food Ordering & Kitchen Processing System)
**Môn học:** Phát triển ứng dụng phân tán (Distributed Application Development)  
**Thời gian thực hiện:** 9 Tuần  
**Kiến trúc cốt lõi:** Microservices / Event-Driven Architecture với Message Broker (RabbitMQ)

---

## 1. Giới thiệu tổng quan và Bối cảnh bài toán

Trong các nhà hàng và chuỗi dịch vụ ăn uống hiện đại, luồng đặt món từ khách hàng và luồng chuẩn bị món của bộ phận bếp đòi hỏi sự đồng bộ và phản hồi nhanh chóng nhưng không được gây nghẽn hệ thống.

Nếu sử dụng kiến trúc nguyên khối (Monolith) truyền thống với lời gọi hàm đồng bộ:
- Khi lượng khách đặt món tăng đột biến vào giờ cao điểm, Web server quá tải, dẫn đến khách hàng bị treo trang hoặc mất đơn hàng.
- Khi bộ phận bếp gặp sự cố hoặc xử lý chậm, hệ thống đặt món bị ảnh hưởng trực tiếp (tight coupling).
- Khách hàng không nhận được phản hồi tức thời về quá trình chế biến món ăn nếu không liên tục tải lại trang (polling gây lãng phí tài nguyên mạng).

**Giải pháp phân tán:**
Áp dụng **Kiến trúc Hướng sự kiện (Event-Driven Architecture - EDA)** kết hợp **Hàng đợi thông điệp (Message Broker - RabbitMQ)**:
- **Tách rời (Decoupling):** Order API chỉ tiếp nhận, xác thực đơn và phát sự kiện vào RabbitMQ rồi phản hồi ngay cho khách hàng (non-blocking).
- **Hấp thụ tải (Load Leveling/Buffering):** Hàng đợi RabbitMQ lưu trữ các đơn đặt hàng, cho phép Kitchen Worker lấy đơn về xử lý theo công suất thực tế của bếp mà không làm sập API tiếp nhận.
- **Phát sự kiện trạng thái (State Events):** Khi bếp cập nhật trạng thái chế biến (Preparing, Ready, Cancelled), sự kiện được gửi ngược lại vào RabbitMQ để Notification Service phân phối đến đúng khách hàng thông qua kết nối thời gian thực (SignalR / WebSockets).

---

## 2. Phạm vi dự án (Project Scope)

### 2.1. Trong phạm vi (In-Scope)
1. **Khách hàng (Client - Customer):**
   - Xem thực đơn món ăn kèm giá, hình ảnh, mô tả và trạng thái còn món.
   - Chọn món, thêm vào giỏ hàng và gửi yêu cầu đặt món (POST /orders).
   - Nhận mã đơn hàng (Order ID) để theo dõi trạng thái.
   - Nhận thông báo thời gian thực (Push Notification qua SignalR/WebSocket) khi trạng thái đơn đổi từ `Pending` $\to$ `Preparing` $\to$ `Ready` (hoặc `Cancelled`).
2. **Bộ phận bếp (Client - Kitchen Staff):**
   - Màn hình Kitchen Display System (KDS) hiển thị danh sách đơn hàng cần làm theo thứ tự thời gian (FIFO hoặc mức độ ưu tiên).
   - Nút thao tác chuyển trạng thái: Tiếp nhận nấu (`Preparing`), Hoàn thành (`Ready`), hoặc Hủy món (`Cancelled` - kèm lý do hết nguyên liệu).
3. **Các Service phân tán:**
   - **Order API (REST API):** Cung cấp API Menu, Order, tiếp nhận đơn, ghi nhận CSDL và đẩy event `OrderCreated` lên RabbitMQ.
   - **Kitchen Worker (Background Consumer):** Lắng nghe queue `order.process.queue`, tiếp nhận đơn hàng, xử lý nghiệp vụ bếp và bắn event `OrderStatusChanged`.
   - **Notification Service (Realtime Pusher):** Lắng nghe queue `order.notification.queue`, điều hướng và phát thông báo WebSocket/SignalR tới đúng Client hoặc Group đơn hàng tương ứng.
   - **Database (PostgreSQL):** Lưu trữ tập trung dữ liệu Khách hàng, Thực đơn, Đơn hàng, Chi tiết đơn hàng và Lịch sử thay đổi trạng thái đơn.
   - **RabbitMQ Broker:** Cấu hình Exchange (Topic/Direct), Queues, Bindings, Routing Keys, cơ chế Dead Letter Queue (DLQ).

### 2.2. Ngoài phạm vi (Out-of-Scope)
- Tích hợp cổng thanh toán trực tuyến thực tế (VNPay, Momo, Stripe) - giả định đơn hàng tự động xác nhận thanh toán khi tạo hoặc thanh toán tiền mặt (COD).
- Thuật toán định vị GPS theo dõi tài xế giao hàng (hệ thống tập trung vào nghiệp vụ đặt món và chế biến tại bếp/quầy).
- Hệ thống quản lý kho vận chuyên sâu (quản lý nguyên vật liệu theo gram/ml).

---

## 3. Xác định Stakeholders (Các bên liên quan)

| Stakeholder | Vai trò & Trách nhiệm | Kỳ vọng đối với hệ thống |
|---|---|---|
| **Khách hàng (Customer)** | Người xem thực đơn, đặt món và theo dõi quá trình làm món | Thao tác nhanh, không giật lag khi đông khách, biết chính xác khi nào món ăn sẵn sàng |
| **Nhân viên bếp (Kitchen Staff / Chef)** | Tiếp nhận thông tin món cần nấu, cập nhật tiến độ chế biến | Nhận đơn tức thì theo thứ tự, giao diện trực quan, thao tác 1 chạm để chuyển trạng thái |
| **Quản trị viên / Nhà phát triển (Developer / Tech Lead)** | Vận hành, bảo trì và kiểm thử các service phân tán | Các service chạy độc lập, có logs chi tiết, dễ mở rộng quy mô (scale), có cơ chế chịu lỗi |
| **Giảng viên hướng dẫn / Chấm thi** | Đánh giá kiến thức phân tán của sinh viên | Thể hiện rõ giao tiếp đồng bộ (REST), bất đồng bộ (RabbitMQ), realtime (SignalR), Docker hóa và cơ chế xử lý lỗi/idempotency |

---

## 4. Yêu cầu chức năng (Functional Requirements - FR)

- **FR-01: Quản lý và xem thực đơn (Menu Browsing):**
  - Hệ thống cung cấp danh mục món ăn (ID, tên món, giá tiền, mô tả, hình ảnh minh họa, danh mục, trạng thái `is_available`).
  - Khách hàng có thể truy vấn danh sách món ăn còn phục vụ thông qua API `GET /api/menu`.
- **FR-02: Tạo đơn đặt món (Order Placement):**
  - Khách hàng chọn một hoặc nhiều món, chỉ định số lượng, ghi chú món ăn (ví dụ: không cay, ít đường) và thông tin bàn/khách hàng.
  - Hệ thống kiểm tra tính hợp lệ của dữ liệu đầu vào (món ăn có tồn tại không, số lượng > 0, thông tin khách hàng).
  - Hệ thống lưu trữ đơn hàng vào CSDL với trạng thái ban đầu là `Pending`.
  - Hệ thống phát sự kiện `OrderCreated` vào RabbitMQ Exchange.
- **FR-03: Xử lý đơn hàng bất đồng bộ tại Bếp (Kitchen Processing):**
  - Kitchen Worker tự động tiêu thụ (consume) thông điệp từ hàng đợi `order.process.queue`.
  - Đơn hàng xuất hiện trên màn hình quản lý của bếp theo thứ tự thời gian tạo.
- **FR-04: Cập nhật trạng thái đơn hàng (Order Status Transition):**
  - Bếp chuyển trạng thái đơn hàng theo luồng nghiệp vụ hợp lệ:
    - `Pending` $\to$ `Preparing` (Bếp bắt đầu chế biến)
    - `Preparing` $\to$ `Ready` (Món ăn đã nấu xong, sẵn sàng phục vụ)
    - `Pending` / `Preparing` $\to$ `Cancelled` (Hủy đơn do hết món hoặc sự cố)
  - Mọi sự thay đổi trạng thái phải được ghi nhận vào bảng `order_status_histories` để phục vụ audit/truy vết.
  - Phát sự kiện `OrderStatusChanged` vào RabbitMQ sau mỗi lần cập nhật.
- **FR-05: Thông báo trạng thái thời gian thực (Realtime Notification):**
  - Notification Service tiếp nhận sự kiện `OrderStatusChanged` từ RabbitMQ.
  - Đẩy thông điệp qua WebSocket/SignalR tới đúng phiên làm việc của khách hàng đang theo dõi đơn hàng đó (theo `OrderId` hoặc `CustomerId`).
  - Client cập nhật tiến trình trên giao diện mà không cần reload trang.
- **FR-06: Tra cứu thông tin đơn hàng (Order Query):**
  - Khách hàng và bếp có thể tra cứu thông tin chi tiết đơn hàng theo `GET /api/orders/{id}` kèm toàn bộ lịch sử trạng thái.
  - Màn hình bếp có thể lấy toàn bộ danh sách đơn đang chờ/đang làm qua `GET /api/orders`.

---

## 5. Yêu cầu phi chức năng (Non-Functional Requirements - NFR)

### NFR cho Hệ thống Phân tán (Distributed Systems Specific)
- **NFR-01: Loose Coupling (Tách rời các thành phần):**
  - Order API không gọi trực tiếp Kitchen Worker. Nếu Kitchen Worker tạm thời ngắt kết nối hoặc gặp sự cố, Order API vẫn tiếp nhận đơn bình thường và đưa vào RabbitMQ. Khi Worker phục hồi, đơn hàng sẽ tiếp tục được xử lý.
- **NFR-02: Asynchronous Messaging & Buffering:**
  - Sử dụng giao thức AMQP 0-9-1 (RabbitMQ). Thời gian phản hồi tạo đơn của Order API không bị ảnh hưởng bởi tốc độ nấu món của bếp.
- **NFR-03: Eventual Consistency (Nhất quán cuối cùng):**
  - Trạng thái giữa CSDL của Order API và trạng thái hiển thị trên màn hình bếp/client đạt được sự nhất quán trong vòng dưới 1 giây sau khi sự kiện được phát đi.
- **NFR-04: Idempotency (Xử lý trùng lặp thông điệp):**
  - Message broker có thể gửi lại message khi có sự cố mạng (At-least-once delivery). Consumer (Kitchen Worker & Notification Service) phải kiểm tra `MessageId` hoặc trạng thái hiện tại của đơn hàng để không xử lý trùng lặp 2 lần một đơn.
- **NFR-05: Chịu lỗi và Dead Letter Queue (Fault Tolerance & DLQ):**
  - Khi một thông điệp bị lỗi định dạng hoặc consumer xử lý thất bại nhiều lần (retry count vượt ngưỡng), thông điệp phải được chuyển vào `order.dlq` để kiểm tra và xử lý sau, không làm tắc nghẽn toàn bộ hàng đợi chính.
- **NFR-06: Realtime Latency (Độ trễ thời gian thực):**
  - Thời gian từ khi Bếp nhấn chuyển trạng thái đến khi màn hình Khách hàng nhận được thông báo không quá 500ms trong điều kiện mạng nội bộ.
- **NFR-07: Containerization & Portability:**
  - Toàn bộ cơ sở hạ tầng (PostgreSQL, RabbitMQ) và các service có thể khởi động đồng nhất bằng `docker-compose up` chỉ với một câu lệnh.
- **NFR-08: Structured Logging & Traceability:**
  - Mỗi đơn hàng gắn liền với một `CorrelationId` xuyên suốt từ Client $\to$ Order API $\to$ RabbitMQ Message Header $\to$ Kitchen Worker $\to$ Notification Service $\to$ Client UI để dễ dàng debug và vẽ luồng truy vết phân tán.
