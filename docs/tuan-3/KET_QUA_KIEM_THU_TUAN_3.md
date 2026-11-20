# BÁO CÁO KẾT QUẢ KIỂM THỬ TUẦN 3 (TEST REPORT)
## Đề tài 04: Hệ thống Đặt món và Xử lý tại Bếp
**Thời gian kiểm thử:** Tuần 3  
**Mục tiêu:** Kiểm tra toàn diện chức năng Order API và Quản lý thực đơn

---

## 1. Kết quả kiểm thử Unit & Integration Tests (.NET xUnit)

Tổng số test cases: **18** | Đạt: **18 (100%)** | Thất bại: **0** | Bỏ qua: **0**

| STT | Tên Test Case | Thành phần kiểm tra | Kết quả mong đợi | Thực tế | Trạng thái |
|---|---|---|---|---|---|
| 1 | `GetMenuItems_ReturnsOnlyAvailable_ByDefault` | `MenuController` | Chỉ trả về các món có `isAvailable = true` | Trả về đúng 3 món khả dụng | ✅ PASSED |
| 2 | `GetMenuItems_FiltersByCategory` | `MenuController` | Lọc đúng các món thuộc danh mục chỉ định | Trả về 2 món danh mục 'Đồ uống' | ✅ PASSED |
| 3 | `GetMenuItemById_ExistingId_ReturnsOkWithItem` | `MenuController` | Trả về thông tin món ăn kèm mã 200 OK | Trả về đúng món ID = 1 | ✅ PASSED |
| 4 | `GetMenuItemById_NonExistingId_ReturnsNotFound` | `MenuController` | Trả về mã lỗi 404 Not Found | Trả về 404 Not Found | ✅ PASSED |
| 5 | `CreateMenuItem_ValidRequest_ReturnsCreatedAndSavesToDb` | `MenuController` | Tạo món mới thành công, mã 201 Created | CSDL lưu đúng tên và giá | ✅ PASSED |
| 6 | `CreateMenuItem_DuplicateName_ReturnsBadRequest` | `MenuController` | Chặn tạo món trùng tên, mã 400 Bad Request | Trả về 400 Bad Request | ✅ PASSED |
| 7 | `UpdateMenuItem_ValidRequest_UpdatesItemInDb` | `MenuController` | Cập nhật giá và mô tả món thành công | CSDL cập nhật giá mới 75.000đ | ✅ PASSED |
| 8 | `DeleteMenuItem_WhenUsedInOrders_SoftDeletesItem` | `MenuController` | Không xóa cứng nếu món đã có trong đơn, chuyển `isAvailable = false` | CSDL bảo toàn khóa ngoại, đổi `isAvailable = false` | ✅ PASSED |
| 9 | `ToggleAvailability_TogglesIsAvailableProperty` | `MenuController` | Đổi trạng thái khả dụng true $\leftrightarrow$ false | Chuyển đổi chính xác | ✅ PASSED |
| 10 | `CreateOrder_ValidRequest_CalculatesTotalAmountAccurately` | `OrdersController` | Tính tổng tiền chính xác từ CSDL, trả về 201 Created | Tổng tiền 155.000đ khớp 100%, phát event RabbitMQ | ✅ PASSED |
| 11 | `CreateOrder_WhenMenuItemNotFound_ReturnsBadRequest` | `OrdersController` | Chặn đơn có ID món không tồn tại | Trả về 400 Bad Request, không tạo đơn | ✅ PASSED |
| 12 | `CreateOrder_WhenMenuItemUnavailable_ReturnsBadRequest` | `OrdersController` | Chặn đơn có món tạm hết (`isAvailable = false`) | Trả về 400 Bad Request kèm tên món hết | ✅ PASSED |
| 13 | `CreateOrder_RecordsInitialPendingStatusHistory` | `OrdersController` | Ghi vết lịch sử trạng thái ban đầu `Pending` | Bảng `order_status_histories` có 1 dòng Pending | ✅ PASSED |
| 14 | `GetOrderById_ExistingOrder_ReturnsOrderWithItemsAndTimeline` | `OrdersController` | Trả về thông tin đơn, chi tiết món và timeline lịch sử | Trả về đủ 2 sự kiện lịch sử (Pending $\to$ Preparing) | ✅ PASSED |
| 15 | `UpdateOrderStatus_TransitionsPendingToPreparing_PublishesEvent` | `OrdersController` | Cập nhật trạng thái sang Preparing và phát event | DB đổi trạng thái, RabbitMQ nhận event mới | ✅ PASSED |
| 16 | `UpdateOrderStatus_WhenTransitionReadyToPending_RejectsBadRequest` | `OrdersController` | Chặn chuyển trạng thái ngược từ Ready về Pending | Trả về 400 Bad Request | ✅ PASSED |
| 17 | `CancelOrder_WhenOrderPending_CancelsSuccessfully` | `OrdersController` | Hủy đơn đang Pending thành công, lưu lý do hủy | DB đổi sang Cancelled, phát event | ✅ PASSED |
| 18 | `CancelOrder_WhenOrderReady_RejectsBadRequest` | `OrdersController` | Chặn hủy đơn khi món ăn đã nấu xong (`Ready`) | Trả về 400 Bad Request | ✅ PASSED |

---

## 2. Bằng chứng kiểm thử Postman Collection

Collection gồm 17 API requests bao phủ toàn bộ các kịch bản:
- **Tạo đơn hàng thành công:** Trả về `201 Created`, tự động gán `orderId` vào biến môi trường `currentOrderId`.
- **Kiểm tra ràng buộc dữ liệu:** Các request với số lượng = 0 hoặc ID món không tồn tại đều bị chặn với mã `400 Bad Request` và thông điệp lỗi rõ ràng.
- **Truy vết đơn hàng:** Gọi `GET /api/orders/{{currentOrderId}}` hiển thị mảng `statusHistory` đầy đủ các mốc thời gian và tác nhân thay đổi.
