# HƯỚNG DẪN TRIỂN KHAI VÀ BÁO CÁO CÔNG VIỆC TUẦN 3
## Đề tài 04: Hệ thống Đặt món và Xử lý tại Bếp (Food Ordering & Kitchen Processing)
**Mục tiêu tuần 3:** Xây dựng Order API hoàn chỉnh và Chức năng quản lý món ăn (Kết nối CSDL, Transaction ACID, Validation chặt chẽ, Postman Collection & Automated Tests).

---

## 1. Các công việc đã hoàn thành trong Tuần 3

### 1.1. Chức năng Quản lý Thực đơn (Menu Management)
Đã triển khai đầy đủ các endpoint CRUD cho thực đơn tại `src/OrderApi/Controllers/MenuController.cs`:
- **`GET /api/menu`**: Truy vấn danh sách món ăn, hỗ trợ bộ lọc:
  - `category`: Lọc theo loại món (`Món chính`, `Đồ uống`, `Tráng miệng`, `Khai vị`).
  - `search`: Tìm kiếm từ khóa theo tên món ăn hoặc mô tả.
  - `includeUnavailable`: `false` (mặc định dành cho khách chỉ xem món còn hàng) hoặc `true` (dành cho quản trị viên/bếp quản lý).
- **`GET /api/menu/{id}`**: Lấy chi tiết một món ăn, trả về `404 Not Found` nếu không tìm thấy.
- **`POST /api/menu`**: Thêm món ăn mới với validation: Tên không để trống, Giá $\ge$ 0, Danh mục bắt buộc, kiểm tra trùng tên.
- **`PUT /api/menu/{id}`**: Cập nhật thông tin món ăn (giá, tên, mô tả, danh mục, hình ảnh).
- **`DELETE /api/menu/{id}`**: Cơ chế xóa thông minh bảo toàn toàn vẹn tham chiếu CSDL phân tán:
  - Nếu món ăn chưa từng được đặt $\to$ Xóa vĩnh viễn khỏi CSDL.
  - Nếu món ăn đã có trong lịch sử `order_details` $\to$ Tự động chuyển `is_available = false` (Soft Delete) để bảo toàn dữ liệu lịch sử đơn của khách hàng.
- **`PATCH /api/menu/{id}/toggle-availability`**: Chuyển đổi nhanh trạng thái Còn hàng / Tạm hết của món ăn.

---

### 1.2. Chức năng Đặt món và Xử lý Đơn hàng (Orders Management)
Đã triển khai các endpoint nghiệp vụ tại `src/OrderApi/Controllers/OrdersController.cs`:
- **`POST /api/orders`**:
  - **Kiểm tra dữ liệu đầu vào (Validation):**
    - Khách hàng phải có tên và số bàn.
    - Đơn hàng phải có ít nhất 1 món.
    - Tất cả `menuItemId` phải tồn tại trong CSDL.
    - Các món được chọn phải đang sẵn sàng phục vụ (`is_available = true`), nếu món tạm hết sẽ từ chối với thông báo rõ ràng.
    - Số lượng mỗi món phải từ 1 đến 100.
  - **Bảo toàn giao dịch ACID:** Toàn bộ bước ghi khách hàng, đơn hàng, chi tiết đơn và lịch sử trạng thái ban đầu được bọc trong `using var transaction = await _context.Database.BeginTransactionAsync()`. Nếu có bất kỳ lỗi nào, hệ thống tự động Rollback toàn bộ.
  - **Tính tiền Server-side:** Giá món ăn lấy trực tiếp từ CSDL tại thời điểm tạo đơn, ngăn chặn việc Client can thiệp giả mạo đơn giá.
  - **Khởi tạo vết lịch sử:** Tự động ghi bản ghi đầu tiên vào `order_status_histories` với trạng thái `Pending` do `OrderApi` tạo.
  - **Phát sự kiện bất đồng bộ:** Sau khi Commit DB thành công, phát sự kiện `OrderCreatedEvent` vào RabbitMQ Topic Exchange `food.order.exchange`.
- **`GET /api/orders/{id}`**:
  - Trả về chi tiết đơn hàng, danh sách món kèm số lượng/thành tiền, và **toàn bộ lịch sử dịch chuyển trạng thái (Timeline)** sắp xếp theo thời gian tăng dần.
- **`GET /api/orders`**:
  - Hỗ trợ phân trang: `page` (trang số mấy), `pageSize` (kích thước trang).
  - Hỗ trợ lọc theo trạng thái: `Pending`, `Preparing`, `Ready`, `Cancelled`.
  - Hỗ trợ lọc theo số bàn: `tableNumber`.
  - Hỗ trợ tìm kiếm theo tên khách hàng.
  - Hỗ trợ sắp xếp: `sortBy=createdAt|totalAmount|tableNumber`, `sortOrder=asc|desc`.
- **`PUT /api/orders/{id}/status`**:
  - Cho phép Bếp cập nhật trạng thái đơn (`Pending` $\to$ `Preparing` $\to$ `Ready`).
  - Ràng buộc nghiệp vụ: Không thể chuyển từ `Ready` quay ngược về `Pending`; không thể cập nhật đơn đã `Cancelled`.
  - Tự động ghi nhận lịch sử vào `order_status_histories` kèm tên người thao tác (`changedBy`).
  - Phát sự kiện `OrderStatusChangedEvent` vào RabbitMQ.
- **`POST /api/orders/{id}/cancel`**:
  - Cho phép hủy đơn hàng với lý do cụ thể nếu đơn chưa hoàn thành (`Ready`).

---

### 1.3. Khởi tạo CSDL tự động (DbInitializer)
- Đã bổ sung `src/OrderApi/Data/DbInitializer.cs`:
  - Tự động gọi `EnsureCreatedAsync()` khi service khởi động.
  - Tự động nạp danh mục 8 món ăn truyền thống Việt Nam và khách hàng mẫu nếu bảng CSDL trống, giúp hệ thống luôn sẵn sàng dữ liệu để test ngay.

### 1.4. Xử lý ngoại lệ toàn cục (Exception Handling Middleware)
- Đã bổ sung `src/OrderApi/Middleware/ExceptionHandlingMiddleware.cs`:
  - Bắt toàn bộ lỗi Exception chưa được xử lý, ghi log chi tiết.
  - Chuẩn hóa định dạng lỗi trả về theo chuẩn quốc tế RFC 7807 (`application/problem+json`) kèm `traceId` và thời gian.

---

## 2. Kết quả Kiểm thử Tự động (Automated Unit & Integration Tests)
Đã xây dựng bộ test tự động tại `tests/OrderApi.Tests` sử dụng xUnit, Moq và SQLite in-memory:
- **18 / 18 test cases đều vượt qua (100% Passed):**
  - 9 test cases cho `MenuController`: Lấy món khả dụng, Lọc theo danh mục, Thêm món hợp lệ, Chặn trùng tên, Cập nhật món, Xóa mềm món khi đã có đơn hàng, Bật tắt trạng thái còn hàng.
  - 9 test cases cho `OrdersController`: Tính tiền chính xác server-side, Chặn món không tồn tại, Chặn món đã hết hàng, Ghi nhận lịch sử Pending ban đầu, Lấy đơn kèm timeline, Chuyển trạng thái sang Preparing, Chặn chuyển trạng thái sai quy tắc, Hủy đơn thành công, Chặn hủy đơn đã nấu xong.

Lệnh thực thi kiểm thử:
```powershell
dotnet test FoodOrderSystem.sln
```
Kết quả:
```
Passed!  - Failed: 0, Passed: 18, Skipped: 0, Total: 18, Duration: 1 s - OrderApi.Tests.dll (net8.0)
```

---

## 3. Hướng dẫn Kiểm thử với Postman Collection

File Postman Collection được lưu tại:
- `docs/tuan-3/postman/Order_API_Collection.json`
- `postman/Order_API_Collection.json`

### Các bước kiểm thử qua Postman:
1. Mở Postman $\to$ Chọn **Import** $\to$ Chọn file `postman/Order_API_Collection.json`.
2. Biến môi trường mặc định: `baseUrl = http://localhost:5001`.
3. Chạy từng request theo thứ tự hoặc dùng **Run Collection** để chạy toàn bộ:
   - Request tạo đơn `POST /api/orders` sẽ tự động lưu `orderId` vào biến môi trường để các request sau (`GET /api/orders/{id}`, `PUT /api/orders/{id}/status`) tự động sử dụng mà không cần copy paste thủ công!
