# THIẾT KẾ CƠ SỞ DỮ LIỆU VÀ SƠ ĐỒ THỰC THỂ QUAN HỆ (ERD)
## Đề tài 04: Hệ thống Đặt món và Xử lý tại Bếp

---

## 1. Sơ đồ Thực thể Quan hệ (Entity-Relationship Diagram - ERD)

```mermaid
erDiagram
    CUSTOMERS ||--o{ ORDERS : "places"
    ORDERS ||--|{ ORDER_DETAILS : "contains"
    MENU_ITEMS ||--o{ ORDER_DETAILS : "referenced_in"
    ORDERS ||--|{ ORDER_STATUS_HISTORIES : "tracks"

    CUSTOMERS {
        int id PK "Khóa chính tự tăng"
        string name "Họ tên khách hàng"
        string phone "Số điện thoại liên lạc"
        string table_number "Số bàn ăn"
        datetime created_at "Thời gian tạo bản ghi"
    }

    MENU_ITEMS {
        int id PK "Khóa chính tự tăng"
        string name "Tên món ăn / đồ uống"
        string description "Mô tả chi tiết nguyên liệu, vị giác"
        decimal price "Đơn giá (VND)"
        string image_url "Đường dẫn ảnh minh họa"
        string category "Danh mục (Món chính, Tráng miệng, Nước)"
        boolean is_available "Trạng thái còn món hay hết"
        datetime created_at "Thời gian thêm món"
    }

    ORDERS {
        uuid id PK "Khóa chính định danh duy nhất (UUID/GUID)"
        int customer_id FK "Liên kết bảng customers"
        string table_number "Số bàn phục vụ"
        decimal total_amount "Tổng giá trị đơn hàng (VND)"
        string status "Trạng thái: Pending, Preparing, Ready, Cancelled"
        string notes "Ghi chú chung của đơn"
        datetime created_at "Thời điểm tạo đơn"
        datetime updated_at "Thời điểm cập nhật trạng thái gần nhất"
    }

    ORDER_DETAILS {
        int id PK "Khóa chính tự tăng"
        uuid order_id FK "Khóa ngoại liên kết bảng orders"
        int menu_item_id FK "Khóa ngoại liên kết bảng menu_items"
        decimal unit_price "Đơn giá tại thời điểm đặt"
        int quantity "Số lượng món đặt (>= 1)"
        decimal subtotal "Thành tiền (unit_price * quantity)"
        string item_notes "Ghi chú riêng cho món (ít đá, không ớt)"
    }

    ORDER_STATUS_HISTORIES {
        int id PK "Khóa chính tự tăng"
        uuid order_id FK "Khóa ngoại liên kết bảng orders"
        string from_status "Trạng thái trước khi đổi"
        string to_status "Trạng thái mới được chuyển sang"
        string changed_by "Tác nhân thay đổi (OrderApi, KitchenStaff, Worker)"
        string notes "Lý do thay đổi hoặc ghi chú lỗi"
        datetime created_at "Thời điểm ghi nhận lịch sử"
    }
```

---

## 2. Từ điển Dữ liệu Chi tiết (Data Dictionary)

### 2.1. Bảng `customers` (Khách hàng)
Lưu trữ thông tin khách hàng đặt bàn và gọi món.

| Tên cột | Kiểu dữ liệu | Ràng buộc | Mô tả |
|---|---|---|---|
| `id` | `SERIAL` | `PRIMARY KEY` | Định danh khách hàng tự tăng |
| `name` | `VARCHAR(100)` | `NOT NULL` | Tên người đặt món |
| `phone` | `VARCHAR(20)` | `NULL` | Số điện thoại |
| `table_number` | `VARCHAR(20)` | `NOT NULL` | Số bàn ngồi ăn (Bàn 01, Bàn VIP,...) |
| `created_at` | `TIMESTAMP WITH TIME ZONE` | `DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo |

---

### 2.2. Bảng `menu_items` (Thực đơn món ăn)
Lưu trữ danh sách các món ăn, đồ uống phục vụ tại quán.

| Tên cột | Kiểu dữ liệu | Ràng buộc | Mô tả |
|---|---|---|---|
| `id` | `SERIAL` | `PRIMARY KEY` | Mã định danh món ăn |
| `name` | `VARCHAR(150)` | `NOT NULL` | Tên món ăn |
| `description` | `TEXT` | `NULL` | Mô tả chi tiết món |
| `price` | `NUMERIC(12, 2)` | `NOT NULL, CHECK (price >= 0)` | Đơn giá món ăn |
| `image_url` | `VARCHAR(500)` | `NULL` | Link hình ảnh đại diện |
| `category` | `VARCHAR(50)` | `NOT NULL` | Danh mục (Ví dụ: `Món chính`, `Đồ uống`, `Tráng miệng`) |
| `is_available` | `BOOLEAN` | `DEFAULT TRUE` | `true`: còn hàng, `false`: tạm hết hàng |
| `created_at` | `TIMESTAMP WITH TIME ZONE` | `DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo món |

---

### 2.3. Bảng `orders` (Đơn đặt hàng)
Lưu trữ thông tin tổng quan của từng đơn hàng. Sử dụng `UUID` làm khóa chính để đảm bảo tính phân tán duy nhất trên toàn hệ thống mà không lo xung đột khi scale.

| Tên cột | Kiểu dữ liệu | Ràng buộc | Mô tả |
|---|---|---|---|
| `id` | `UUID` | `PRIMARY KEY` | Định danh đơn hàng (Global Unique ID) |
| `customer_id` | `INT` | `REFERENCES customers(id)` | Liên kết khách hàng |
| `table_number` | `VARCHAR(20)` | `NOT NULL` | Số bàn của đơn |
| `total_amount` | `NUMERIC(12, 2)` | `NOT NULL, CHECK (total_amount >= 0)` | Tổng giá trị toàn đơn hàng |
| `status` | `VARCHAR(20)` | `NOT NULL, CHECK (status IN ('Pending', 'Preparing', 'Ready', 'Cancelled'))` | Trạng thái xử lý |
| `notes` | `TEXT` | `NULL` | Ghi chú chung của khách |
| `created_at` | `TIMESTAMP WITH TIME ZONE` | `DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo đơn |
| `updated_at` | `TIMESTAMP WITH TIME ZONE` | `DEFAULT CURRENT_TIMESTAMP` | Thời điểm trạng thái thay đổi |

---

### 2.4. Bảng `order_details` (Chi tiết đơn hàng)
Lưu từng món ăn trong đơn, số lượng, và bảo toàn giá trị tại thời điểm mua (`unit_price`).

| Tên cột | Kiểu dữ liệu | Ràng buộc | Mô tả |
|---|---|---|---|
| `id` | `SERIAL` | `PRIMARY KEY` | Định danh chi tiết đơn |
| `order_id` | `UUID` | `NOT NULL, REFERENCES orders(id) ON DELETE CASCADE` | Mã đơn hàng tương ứng |
| `menu_item_id` | `INT` | `NOT NULL, REFERENCES menu_items(id)` | Mã món ăn được chọn |
| `unit_price` | `NUMERIC(12, 2)` | `NOT NULL, CHECK (unit_price >= 0)` | Đơn giá tại thời điểm đặt |
| `quantity` | `INT` | `NOT NULL, CHECK (quantity > 0)` | Số lượng đặt |
| `subtotal` | `NUMERIC(12, 2)` | `NOT NULL, CHECK (subtotal >= 0)` | Thành tiền (`unit_price * quantity`) |
| `item_notes` | `VARCHAR(255)` | `NULL` | Ghi chú món (VD: Không hành, ít ngọt) |

---

### 2.5. Bảng `order_status_histories` (Lịch sử trạng thái đơn)
Lưu vết toàn bộ quá trình dịch chuyển trạng thái của đơn phục vụ audit và hiển thị timeline trên giao diện khách hàng.

| Tên cột | Kiểu dữ liệu | Ràng buộc | Mô tả |
|---|---|---|---|
| `id` | `SERIAL` | `PRIMARY KEY` | Định danh bản ghi lịch sử |
| `order_id` | `UUID` | `NOT NULL, REFERENCES orders(id) ON DELETE CASCADE` | Mã đơn hàng |
| `from_status` | `VARCHAR(20)` | `NULL` | Trạng thái trước đó |
| `to_status` | `VARCHAR(20)` | `NOT NULL` | Trạng thái chuyển tới |
| `changed_by` | `VARCHAR(50)` | `NOT NULL` | Tác nhân thực hiện (`OrderApi`, `KitchenWorker`, `Chef`) |
| `notes` | `TEXT` | `NULL` | Ghi chú kèm theo (VD: Bếp bắt đầu làm) |
| `created_at` | `TIMESTAMP WITH TIME ZONE` | `DEFAULT CURRENT_TIMESTAMP` | Thời điểm diễn ra sự kiện |

---

## 3. Các Chỉ mục (Indexes) Tối ưu Truy vấn Phân tán

1. `CREATE INDEX idx_orders_status ON orders(status);`  
   $\to$ Tối ưu cho Kitchen Worker và Dashboard khi lấy danh sách đơn `Pending` hoặc `Preparing`.
2. `CREATE INDEX idx_orders_customer_id ON orders(customer_id);`  
   $\to$ Tối ưu khi khách hàng tra cứu các đơn mình đã đặt.
3. `CREATE INDEX idx_order_details_order_id ON order_details(order_id);`  
   $\to$ Tối ưu khi join chi tiết món ăn trong một đơn hàng.
4. `CREATE INDEX idx_order_status_histories_order_id ON order_status_histories(order_id);`  
   $\to$ Tối ưu khi truy vấn timeline lịch sử trạng thái của một đơn hàng.
