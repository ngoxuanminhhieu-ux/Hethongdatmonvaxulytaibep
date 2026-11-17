# THIẾT KẾ RABBITMQ VÀ CẤU TRÚC MESSAGE
## Đề tài 04: Hệ thống Đặt món và Xử lý tại Bếp (Food Ordering & Kitchen Processing)

---

## 1. Sơ đồ RabbitMQ Topology

```mermaid
flowchart TD
    ProducerAPI["Order API (Producer)"]
    KitchenWorkerProd["Kitchen Worker (Producer)"]

    subgraph RabbitMQ_Broker["CỤM MESSAGE BROKER (RABBITMQ)"]
        MainExchange{{"Topic Exchange:<br/>food.order.exchange"}}
        DLXExchange{{"Direct Exchange:<br/>food.order.dlx"}}
        
        QueueKitchen[("Queue: order.process.queue<br/>• Durable: true<br/>• x-dead-letter-exchange: food.order.dlx")]
        QueueNotify[("Queue: order.notification.queue<br/>• Durable: true")]
        QueueDLQ[("Queue: order.dlq<br/>(Dead Letter Queue)")]
    end

    ConsumerKitchen["Kitchen Worker (Consumer)"]
    ConsumerNotify["Notification Service (Consumer)"]

    %% Publishing
    ProducerAPI -->|Publish with key: 'order.created'| MainExchange
    KitchenWorkerProd -->|Publish with key: 'order.status.changed'| MainExchange

    %% Routing
    MainExchange -->|Binding key: 'order.created'| QueueKitchen
    MainExchange -->|Binding key: 'order.*'| QueueNotify
    
    %% Dead letter routing
    QueueKitchen -.->|Khi nack requeue=false hoặc retry thất bại| DLXExchange
    DLXExchange -->|Binding key: 'order.failed'| QueueDLQ

    %% Consuming
    QueueKitchen -->|Consume (Prefetch=1, Manual Ack)| ConsumerKitchen
    QueueNotify -->|Consume (Broadcast realtime)| ConsumerNotify
```

---

## 2. Đặc tả các thành phần Topology

| Thành phần | Tên | Loại (Type) | Cấu hình (Properties) | Chức năng |
|---|---|---|---|---|
| **Main Exchange** | `food.order.exchange` | `topic` | `durable: true`, `autoDelete: false` | Định tuyến các sự kiện đơn hàng dựa trên routing key phân cấp (`order.<action>`). |
| **Dead Letter Exchange (DLX)** | `food.order.dlx` | `direct` | `durable: true`, `autoDelete: false` | Hứng các thông điệp bị xử lý lỗi hoặc vượt quá số lần retry. |
| **Queue Bếp** | `order.process.queue` | Quorum / Classic | `durable: true`<br/>`x-dead-letter-exchange: food.order.dlx`<br/>`x-dead-letter-routing-key: order.failed` | Tiếp nhận đơn đặt món để bếp xử lý. |
| **Queue Thông báo** | `order.notification.queue` | Classic | `durable: true` | Tiếp nhận mọi sự kiện đơn hàng để đẩy realtime cho người dùng. |
| **Queue Chết (DLQ)** | `order.dlq` | Classic | `durable: true` | Lưu vết các thông điệp hỏng để quản trị viên kiểm tra và xử lý sự cố. |

---

## 3. Cấu trúc Message Contracts (JSON Schemas)

Mọi message trao đổi qua RabbitMQ đều bao gồm 2 phần:
1. **Metadata Header:** Chứa thông tin phục vụ truy vết phân tán (`EventId`, `CorrelationId`, `Timestamp`, `EventType`).
2. **Payload:** Dữ liệu nghiệp vụ chi tiết của sự kiện.

### 3.1. Sự kiện `OrderCreatedEvent`
- **Routing Key:** `order.created`
- **Exchange:** `food.order.exchange`
- **Thời điểm phát:** Ngay sau khi Order API lưu đơn thành công vào CSDL.
- **Cấu trúc JSON:**
```json
{
  "eventId": "c71a3948-289e-4c7b-9c76-5fa7b2ec1623",
  "correlationId": "b8d6f512-4217-4952-b8d9-291124458bc2",
  "eventType": "OrderCreated",
  "timestamp": "2026-11-17T20:45:00.125Z",
  "sourceService": "OrderApi",
  "data": {
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
        "notes": "1 tô không hành"
      },
      {
        "menuItemId": 6,
        "name": "Cà Phê Sữa Đá Sài Gòn",
        "unitPrice": 25000.00,
        "quantity": 1,
        "notes": "Ít ngọt, nhiều đá"
      }
    ]
  }
}
```

---

### 3.2. Sự kiện `OrderStatusChangedEvent`
- **Routing Key:** `order.status.changed`
- **Exchange:** `food.order.exchange`
- **Thời điểm phát:** Khi trạng thái đơn hàng được cập nhật (`Pending` $\to$ `Preparing` $\to$ `Ready` hoặc `Cancelled`).
- **Cấu trúc JSON:**
```json
{
  "eventId": "e92f418a-6b5d-481e-84ad-3081e74f7621",
  "correlationId": "b8d6f512-4217-4952-b8d9-291124458bc2",
  "eventType": "OrderStatusChanged",
  "timestamp": "2026-11-17T20:46:15.300Z",
  "sourceService": "KitchenWorker",
  "data": {
    "orderId": "b8d6f512-4217-4952-b8d9-291124458bc2",
    "customerName": "Nguyễn Văn An",
    "tableNumber": "Bàn 01",
    "previousStatus": "Pending",
    "newStatus": "Preparing",
    "message": "Bếp đã tiếp nhận đơn và đang bắt đầu chế biến",
    "changedBy": "KitchenWorker",
    "updatedAt": "2026-11-17T20:46:15.000Z"
  }
}
```

---

## 4. Các cơ chế đảm bảo chất lượng hệ thống phân tán

### 4.1. Độ bền vững của thông điệp (Message Durability)
- Cờ `durable: true` trên Exchange và Queue đảm bảo metadata của hàng đợi không bị mất khi RabbitMQ restart.
- Cờ `IBasicProperties.DeliveryMode = 2` (Persistent) khi Producer publish message đảm bảo thân message được ghi xuống đĩa (disk storage).

### 4.2. Cơ chế xác nhận thủ công (Manual Acknowledgement)
- Consumer không sử dụng chế độ `autoAck: true`.
- Consumer chỉ gửi `channel.BasicAck(deliveryTag, multiple: false)` sau khi:
  1. Đã ghi nhận thay đổi vào CSDL thành công.
  2. Không có ngoại lệ logic nào xảy ra.
- Nếu service bị crash trong khi đang xử lý đơn, RabbitMQ sẽ tự động hoàn trả message vào queue khi phát hiện kết nối TCP bị ngắt, đảm bảo không bao giờ thất thoát đơn hàng của khách.

### 4.3. Phân tải công bằng (Fair Dispatching - Prefetch Count)
- Thiết lập `channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false)`.
- RabbitMQ chỉ gửi đơn mới cho worker khi worker đó đã xử lý xong và gửi Ack cho đơn trước. Điều này ngăn chặn tình trạng một worker bị nghẽn trong khi worker khác đang rảnh rỗi.

### 4.4. Cơ chế Idempotency (Chống xử lý trùng lặp)
- Khi nhận `OrderCreatedEvent`, Kitchen Worker kiểm tra xem `OrderId` này đã tồn tại trong danh sách đơn đang nấu hoặc đã hoàn thành chưa.
- Nếu đơn hàng đã ở trạng thái `Preparing` hoặc `Ready`, worker chỉ gửi Ack và bỏ qua bước ghi đè, ngăn ngừa tác động phụ (side effects) khi nhận message bị retry.
