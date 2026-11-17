# TÀI LIỆU HỢP ĐỒNG API (API CONTRACT SPECIFICATION)
## Đề tài 04: Hệ thống Đặt món và Xử lý tại Bếp
**Base URL:** `http://localhost:5001`  
**Content-Type:** `application/json`

---

## 1. Danh sách các API Endpoints

| Phương thức | Đường dẫn API | Mục đích | Người gọi |
|---|---|---|---|
| `GET` | `/api/menu` | Lấy danh sách các món ăn khả dụng | Khách hàng |
| `POST` | `/api/orders` | Tiếp nhận đơn đặt món mới | Khách hàng |
| `GET` | `/api/orders/{id}` | Lấy chi tiết đơn hàng & lịch sử trạng thái | Khách hàng / Bếp |
| `GET` | `/api/orders` | Lấy danh sách tất cả các đơn hàng | Bếp / Quản trị |
| `PUT` | `/api/orders/{id}/status` | Cập nhật trạng thái đơn hàng thủ công | Bếp |

---

## 2. Chi tiết từng API Endpoint

### 2.1. GET `/api/menu`
Lấy danh sách các món ăn đang phục vụ tại nhà hàng.

- **Query Parameters:** (Tùy chọn)
  - `category` (string, optional): Lọc theo danh mục (vd: `Món chính`, `Đồ uống`).
- **Phản hồi thành công (HTTP 200 OK):**
```json
[
  {
    "id": 1,
    "name": "Phở Bò Tái Nạm",
    "description": "Bánh phở tươi, thịt bò tái nạm mềm thơm cùng nước dùng hầm xương 24h",
    "price": 65000.00,
    "imageUrl": "https://images.unsplash.com/photo-1582878826629-29b7ad1cdc43?w=500",
    "category": "Món chính",
    "isAvailable": true
  },
  {
    "id": 6,
    "name": "Cà Phê Sữa Đá Sài Gòn",
    "description": "Cà phê nguyên chất pha phin truyền thống hòa quyện cùng sữa đặc béo ngậy",
    "price": 25000.00,
    "imageUrl": "https://images.unsplash.com/photo-1517701550927-30cf4ba1dba5?w=500",
    "category": "Đồ uống",
    "isAvailable": true
  }
]
```

---

### 2.2. POST `/api/orders`
Tạo một đơn đặt món mới. Hệ thống sẽ validate dữ liệu, tính tổng tiền, lưu vào CSDL và phát sự kiện `OrderCreatedEvent` vào RabbitMQ.

- **Headers:** `Content-Type: application/json`
- **Request Body:**
```json
{
  "customerName": "Nguyễn Văn An",
  "phone": "0901234567",
  "tableNumber": "Bàn 01",
  "notes": "Món phở làm ít hành, nhiều tiêu",
  "items": [
    {
      "menuItemId": 1,
      "quantity": 2,
      "notes": "1 tô không hành"
    },
    {
      "menuItemId": 6,
      "quantity": 1,
      "notes": "Ít ngọt, nhiều đá"
    }
  ]
}
```

- **Quy tắc kiểm tra (Validation Rules):**
  - `customerName`: Không được để trống, độ dài tối đa 100 ký tự.
  - `tableNumber`: Không được để trống.
  - `items`: Danh sách món phải có ít nhất 1 phần tử.
  - Mỗi phần tử trong `items`: `menuItemId` phải tồn tại trong CSDL và `quantity` phải là số nguyên dương $\ge 1$.

- **Phản hồi thành công (HTTP 201 Created):**
```json
{
  "orderId": "b8d6f512-4217-4952-b8d9-291124458bc2",
  "customerName": "Nguyễn Văn An",
  "tableNumber": "Bàn 01",
  "totalAmount": 155000.00,
  "status": "Pending",
  "notes": "Món phở làm ít hành, nhiều tiêu",
  "createdAt": "2026-11-17T20:45:00.000Z",
  "items": [
    {
      "menuItemId": 1,
      "name": "Phở Bò Tái Nạm",
      "unitPrice": 65000.00,
      "quantity": 2,
      "subtotal": 130000.00,
      "notes": "1 tô không hành"
    },
    {
      "menuItemId": 6,
      "name": "Cà Phê Sữa Đá Sài Gòn",
      "unitPrice": 25000.00,
      "quantity": 1,
      "subtotal": 25000.00,
      "notes": "Ít ngọt, nhiều đá"
    }
  ]
}
```

- **Phản hồi lỗi (HTTP 400 Bad Request):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Dữ liệu đơn hàng không hợp lệ",
  "status": 400,
  "errors": {
    "Items": ["Món ăn với ID 999 không tồn tại trong thực đơn."],
    "TableNumber": ["Số bàn không được để trống."]
  }
}
```

---

### 2.3. GET `/api/orders/{id}`
Lấy chi tiết đơn hàng kèm toàn bộ lịch sử biến đổi trạng thái (Timeline).

- **Path Parameter:**
  - `id` (UUID): Mã đơn hàng cần tra cứu.
- **Phản hồi thành công (HTTP 200 OK):**
```json
{
  "orderId": "b8d6f512-4217-4952-b8d9-291124458bc2",
  "customerName": "Nguyễn Văn An",
  "tableNumber": "Bàn 01",
  "totalAmount": 155000.00,
  "status": "Preparing",
  "notes": "Món phở làm ít hành, nhiều tiêu",
  "createdAt": "2026-11-17T20:45:00.000Z",
  "updatedAt": "2026-11-17T20:46:15.000Z",
  "items": [
    {
      "menuItemId": 1,
      "name": "Phở Bò Tái Nạm",
      "unitPrice": 65000.00,
      "quantity": 2,
      "subtotal": 130000.00,
      "notes": "1 tô không hành"
    },
    {
      "menuItemId": 6,
      "name": "Cà Phê Sữa Đá Sài Gòn",
      "unitPrice": 25000.00,
      "quantity": 1,
      "subtotal": 25000.00,
      "notes": "Ít ngọt, nhiều đá"
    }
  ],
  "statusHistory": [
    {
      "fromStatus": null,
      "toStatus": "Pending",
      "changedBy": "OrderApi",
      "notes": "Đơn hàng mới tạo",
      "timestamp": "2026-11-17T20:45:00.000Z"
    },
    {
      "fromStatus": "Pending",
      "toStatus": "Preparing",
      "changedBy": "KitchenWorker",
      "notes": "Bếp đã tiếp nhận và bắt đầu chế biến",
      "timestamp": "2026-11-17T20:46:15.000Z"
    }
  ]
}
```

- **Phản hồi lỗi (HTTP 404 Not Found):**
```json
{
  "title": "Không tìm thấy đơn hàng",
  "status": 404,
  "detail": "Đơn hàng với mã b8d6f512-4217-4952-b8d9-291124458bc2 không tồn tại."
}
```

---

### 2.4. GET `/api/orders`
Lấy danh sách các đơn hàng hiện có trong hệ thống (dùng cho màn hình bếp KDS).

- **Query Parameters:** (Tùy chọn)
  - `status` (string, optional): Lọc theo trạng thái (`Pending`, `Preparing`, `Ready`, `Cancelled`).
- **Phản hồi thành công (HTTP 200 OK):** Mảng các đối tượng tóm tắt đơn hàng sắp xếp theo thời gian tạo (cũ nhất lên trước để bếp làm trước - FIFO).

---

### 2.5. PUT `/api/orders/{id}/status`
Cập nhật thủ công trạng thái của đơn hàng từ màn hình bếp (khi đầu bếp thao tác bấm nút).

- **Request Body:**
```json
{
  "status": "Ready",
  "changedBy": "Chef Minh",
  "notes": "Đã nấu xong đầy đủ các món"
}
```
- **Phản hồi thành công (HTTP 200 OK):**
```json
{
  "orderId": "b8d6f512-4217-4952-b8d9-291124458bc2",
  "newStatus": "Ready",
  "updatedAt": "2026-11-17T20:50:00.000Z"
}
```
