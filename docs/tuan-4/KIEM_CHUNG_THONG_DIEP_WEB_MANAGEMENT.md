# HƯỚNG DẪN KIỂM CHỨNG THÔNG ĐIỆP QUA RABBITMQ WEB MANAGEMENT CONSOLE
## Đề tài 04: Hệ thống Đặt món và Xử lý tại Bếp (Food Ordering & Kitchen Processing)
**Mục tiêu:** Hướng dẫn từng bước kiểm chứng các thông điệp được Order API đẩy vào RabbitMQ thông qua giao diện Web Management tại `http://localhost:15672`.

---

## 1. Chuẩn bị Môi trường

1. **Khởi động RabbitMQ:**
   ```powershell
   docker compose up -d rabbitmq
   ```
2. **Khởi động Order API:**
   ```powershell
   dotnet run --project src/OrderApi/OrderApi.csproj --urls "http://localhost:5001"
   ```
3. **Mở trình duyệt truy cập RabbitMQ Management Console:**
   - **URL:** [http://localhost:15672](http://localhost:15672)
   - **Tài khoản:** `guest`
   - **Mật khẩu:** `guest`

---

## 2. Các bước Kiểm chứng Chi tiết

### Bước 1: Kiểm chứng Kết nối Bền bỉ (Connections & Channels)
1. Trên thanh điều hướng của Web UI, bấm vào tab **Connections**.
2. **Quan sát:**
   - Bạn sẽ thấy một connection từ `OrderApi` đang ở trạng thái `running`.
   - Cột **SSL/TLS**: plain text (TCP).
   - Cột **Channels**: có ít nhất 1 channel đang hoạt động.
3. Bấm vào tab **Channels**:
   - Quan sát thấy channel đang mở, cờ `C` (Confirms) hiển thị xác nhận chế độ **Publisher Confirms** đã được kích hoạt.

```
+---------------------------------------------------------------------------------+
| RabbitMQ Management - Connections                                               |
| User: guest | Virtual host: /                                                   |
+---------------------------------------------------------------------------------+
| Client connection | State   | Channels | Network latency | SSL/TLS |            |
| 172.18.0.1:54210  | running | 1        | 0.2ms           | No      |            |
+---------------------------------------------------------------------------------+
```

---

### Bước 2: Kiểm chứng Topology (Exchanges & Bindings)
1. Bấm vào tab **Exchanges**:
   - Kiểm tra thấy sàn giao dịch **`food.order.exchange`**:
     - Type: `topic`
     - Features: `D` (Durable - Bền vững)
   - Kiểm tra thấy sàn giao dịch lỗi **`food.order.dlx`**:
     - Type: `direct`
     - Features: `D` (Durable)
2. Bấm vào sàn giao dịch `food.order.exchange`:
   - Kéo xuống mục **Bindings**, quan sát thấy:
     - Queue `order.process.queue` liên kết với routing key `order.created`.
     - Queue `order.notification.queue` liên kết với routing key `order.*`.

---

### Bước 3: Kiểm chứng Hàng đợi và Cơ chế Dead Letter (Queues)
1. Bấm vào tab **Queues**:
   - Quan sát danh sách 3 hàng đợi đã được tự động khởi tạo:
     1. **`order.process.queue`**:
        - Features: `D` (Durable), `DLX` (Dead Letter Exchange gắn với `food.order.dlx`).
     2. **`order.notification.queue`**:
        - Features: `D` (Durable).
     3. **`order.dlq`**:
        - Features: `D` (Durable) - Dead Letter Queue.

---

### Bước 4: Kiểm chứng Đẩy thông điệp qua API (Message Ingestion)

#### Cách 1: Gửi qua Endpoint Chẩn đoán
Gửi request `POST /api/rabbitmq/test-publish`:
```powershell
curl -X POST "http://localhost:5001/api/rabbitmq/test-publish" `
     -H "Content-Type: application/json" `
     -d '{"routingKey": "order.created", "payload": {"table": "Bàn 01", "dish": "Phở Bò Tái Nạm"}}'
```
*Phản hồi:*
```json
{
  "success": true,
  "routingKey": "order.created",
  "exchange": "food.order.exchange",
  "message": "Đã publish thành công với Publisher Confirms và DeliveryMode=2 (Persistent)..."
}
```

#### Cách 2: Tạo đơn hàng thực tế
Gửi request `POST /api/orders`:
```powershell
curl -X POST "http://localhost:5001/api/orders" `
     -H "Content-Type: application/json" `
     -d '{"customerName":"Nguyễn Văn An","tableNumber":"Bàn 01","items":[{"menuItemId":1,"quantity":2}]}'
```

---

### Bước 5: Kiểm tra Biểu đồ và Message trong Queue trên Web UI
1. Quay lại tab **Queues** trên [http://localhost:15672](http://localhost:15672).
2. **Quan sát số lượng:**
   - Queue **`order.process.queue`**: Cột **Ready** tăng lên `+1`.
   - Queue **`order.notification.queue`**: Cột **Ready** tăng lên `+1` (vì routing key `order.created` khớp với binding `order.*`).
3. Bấm vào hàng đợi **`order.process.queue`**:
   - Kéo xuống phần **Get messages**:
     - **Messages:** Nhập `1`
     - **Requeue:** Chọn `Yes` (để không làm mất message sau khi đọc)
     - Bấm nút **Get Message(s)**.
4. **Kiểm tra Payload và Headers:**
   - Quan sát thấy đầy đủ thuộc tính bền vững:
     - `delivery_mode: 2` (Persistent)
     - `content_type: application/json`
     - `correlation_id: <OrderId>`
     - `app_id: OrderApi`
     - Header `x-source-service: OrderApi`
   - **Payload JSON:**
     ```json
     {
       "eventId": "...",
       "correlationId": "b8d6f512-...",
       "eventType": "OrderCreatedEvent",
       "sourceService": "OrderApi",
       "data": {
         "orderId": "b8d6f512-...",
         "customerName": "Nguyễn Văn An",
         "tableNumber": "Bàn 01",
         "totalAmount": 130000.0,
         "status": 0,
         "items": [
           { "menuItemId": 1, "name": "Phở Bò Tái Nạm", "unitPrice": 65000.0, "quantity": 2 }
         ]
       }
     }
     ```

---

### Bước 6: Kiểm chứng Tính Bền Bỉ khi Khởi động lại Broker (Durability Test)
1. Đảm bảo trong queue `order.process.queue` đang có ít nhất 1 message ở trạng thái `Ready`.
2. Khởi động lại container RabbitMQ:
   ```powershell
   docker restart food_order_rabbitmq
   ```
3. Sau khi container khởi động lại xong, tải lại trang Web Management:
   - **Kết quả:** Cả 3 queues và toàn bộ số lượng message `Ready` vẫn nguyên vẹn 100%, chứng minh cờ `durable: true` và `delivery_mode: 2` đã hoạt động chính xác!
