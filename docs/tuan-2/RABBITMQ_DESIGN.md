# THIẾT KẾ TOPOLOGY VÀ MESSAGE CONTRACT RABBITMQ
## Đề tài 09: Hệ thống Đặt lịch khám và Quản lý hàng đợi (Clinic Appointment & Queue Management)

---

## 1. RabbitMQ Topology Design

```mermaid
flowchart LR
    Producer["Appointment API & Queue Worker<br/>(Publisher)"]
    Exchange["Topic Exchange<br/>'clinic.appointment.exchange'"]
    
    DLX["Direct Exchange (DLX)<br/>'clinic.appointment.dlx'"]
    QueueDLQ["Queue: 'queue.dlq'"]

    QueueProcess["Queue: 'queue.process.queue'<br/>(Durable: true, x-dead-letter-exchange)"]
    QueueNotify["Queue: 'queue.notification.queue'<br/>(Durable: true)"]

    Worker["Queue Worker<br/>(Consumer Cấp số)"]
    Notify["Notification Service<br/>(SignalR Broadcaster)"]

    Producer -->|Publish: appointment.requested| Exchange
    Producer -->|Publish: queue.serving.changed| Exchange

    Exchange -->|Binding: appointment.requested| QueueProcess
    Exchange -->|Binding: queue.*, appointment.*| QueueNotify

    QueueProcess -->|Consume & Ack| Worker
    Worker -->|Publish: queue.number.assigned| Exchange
    QueueProcess -.->|Nack (requeue=false)| DLX
    DLX -->|Binding: queue.failed| QueueDLQ

    QueueNotify -->|Consume| Notify
```

---

## 2. Thông số kỹ thuật của Exchange và Queues

### 2.1. Exchanges
1. **Main Exchange:**
   - Name: `clinic.appointment.exchange`
   - Type: `topic`
   - Durable: `true`
   - AutoDelete: `false`
2. **Dead Letter Exchange (DLX):**
   - Name: `clinic.appointment.dlx`
   - Type: `direct`
   - Durable: `true`
   - AutoDelete: `false`

### 2.2. Queues
1. **Queue xử lý cấp số thứ tự (`queue.process.queue`):**
   - Durable: `true`
   - Exclusive: `false`, AutoDelete: `false`
   - Arguments:
     - `x-dead-letter-exchange`: `clinic.appointment.dlx`
     - `x-dead-letter-routing-key`: `queue.failed`
   - Bound to: `clinic.appointment.exchange` với Routing Key `appointment.requested`.

2. **Queue gửi thông báo realtime (`queue.notification.queue`):**
   - Durable: `true`
   - Bound to: `clinic.appointment.exchange` với Routing Keys `queue.*` và `appointment.*`.

3. **Queue thư chết (`queue.dlq`):**
   - Durable: `true`
   - Bound to: `clinic.appointment.dlx` với Routing Key `queue.failed`.

---

## 3. Cấu trúc Message Contracts (JSON Schema)

### 3.1. `AppointmentRequestedEvent` (Routing Key: `appointment.requested`)
```json
{
  "eventId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "correlationId": "7f8b91a2-3c4d-5e6f-7a8b-9c0d1e2f3a4b",
  "eventType": "AppointmentRequestedEvent",
  "timestamp": "2026-11-27T14:30:00Z",
  "sourceService": "AppointmentApi",
  "data": {
    "appointmentId": "7f8b91a2-3c4d-5e6f-7a8b-9c0d1e2f3a4b",
    "patientId": 10,
    "patientName": "Nguyễn Văn Bệnh Nhân",
    "phone": "0988888888",
    "doctorId": 1,
    "doctorName": "BS. CKII Nguyễn Văn A",
    "roomNumber": "P.201",
    "timeSlotId": 1,
    "slotDate": "2026-11-28",
    "startTime": "08:00",
    "endTime": "09:00",
    "symptoms": "Đau tức ngực trái khi gắng sức",
    "createdAt": "2026-11-27T14:30:00Z"
  }
}
```

### 3.2. `QueueNumberAssignedEvent` (Routing Key: `queue.number.assigned`)
```json
{
  "eventId": "b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e",
  "correlationId": "7f8b91a2-3c4d-5e6f-7a8b-9c0d1e2f3a4b",
  "eventType": "QueueNumberAssignedEvent",
  "timestamp": "2026-11-27T14:30:01Z",
  "sourceService": "QueueWorker",
  "data": {
    "appointmentId": "7f8b91a2-3c4d-5e6f-7a8b-9c0d1e2f3a4b",
    "doctorId": 1,
    "doctorName": "BS. CKII Nguyễn Văn A",
    "roomNumber": "P.201",
    "patientId": 10,
    "patientName": "Nguyễn Văn Bệnh Nhân",
    "queueNumber": 3,
    "status": "Confirmed",
    "slotTime": "08:00 - 09:00",
    "assignedAt": "2026-11-27T14:30:01Z"
  }
}
```

### 3.3. `QueueServingChangedEvent` (Routing Key: `queue.serving.changed`)
```json
{
  "eventId": "c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f",
  "correlationId": "7f8b91a2-3c4d-5e6f-7a8b-9c0d1e2f3a4b",
  "eventType": "QueueServingChangedEvent",
  "timestamp": "2026-11-27T14:35:00Z",
  "sourceService": "AppointmentApi",
  "data": {
    "doctorId": 1,
    "doctorName": "BS. CKII Nguyễn Văn A",
    "roomNumber": "P.201",
    "currentServingNumber": 3,
    "currentPatientName": "Nguyễn Văn Bệnh Nhân",
    "currentAppointmentId": "7f8b91a2-3c4d-5e6f-7a8b-9c0d1e2f3a4b",
    "waitingCount": 2,
    "action": "CallNext",
    "message": "Mời số #03 - Bệnh nhân Nguyễn Văn Bệnh Nhân vào P.201 (BS. CKII Nguyễn Văn A)",
    "changedBy": "Doctor",
    "updatedAt": "2026-11-27T14:35:00Z"
  }
}
```
