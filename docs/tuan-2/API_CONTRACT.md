# TÀI LIỆU HỢP ĐỒNG API (API CONTRACT SPECIFICATION)
## Đề tài 09: Hệ thống Đặt lịch khám và Quản lý hàng đợi (Clinic Appointment & Queue Management)

**Base URL:** `http://localhost:5001/api`  
**Content-Type:** `application/json`

---

## 1. Danh sách Endpoints tối thiểu

### 1.1. Tra cứu danh sách Bác sĩ / Phòng khám
- **Method:** `GET`
- **Endpoint:** `/doctors`
- **Query Parameters:**
  - `specialty` (string, optional): Lọc theo chuyên khoa (vd: `Tim mạch`, `Nhi khoa`).
  - `search` (string, optional): Từ khóa tìm theo tên bác sĩ, phòng hoặc chuyên khoa.
- **Response 200 OK:**
```json
[
  {
    "id": 1,
    "name": "BS. CKII Nguyễn Văn A",
    "specialty": "Tim mạch",
    "roomNumber": "P.201",
    "phone": "0901234567",
    "isActive": true
  }
]
```

---

### 1.2. Tra cứu khung giờ khám còn chỗ
- **Method:** `GET`
- **Endpoint:** `/doctors/{id}/slots`
- **Query Parameters:**
  - `date` (string, format `YYYY-MM-DD`, optional): Ngày khám cần tra cứu.
- **Response 200 OK:**
```json
[
  {
    "id": 1,
    "doctorId": 1,
    "slotDate": "2026-11-28",
    "startTime": "08:00",
    "endTime": "09:00",
    "maxCapacity": 5,
    "currentBookings": 2,
    "availableSpots": 3,
    "isAvailable": true
  }
]
```

---

### 1.3. Đặt lịch khám mới (Tạo yêu cầu cấp số thứ tự)
- **Method:** `POST`
- **Endpoint:** `/appointments`
- **Request Body:**
```json
{
  "fullName": "Nguyễn Văn Bệnh Nhân",
  "phone": "0988888888",
  "identityNumber": "001200001234",
  "dateOfBirth": "1990-05-15",
  "gender": "Nam",
  "doctorId": 1,
  "timeSlotId": 1,
  "symptoms": "Đau tức ngực trái khi vận động mạnh",
  "notes": "Có tiền sử huyết áp cao"
}
```
- **Response 201 Created:**
```json
{
  "appointmentId": "7f8b91a2-3c4d-5e6f-7a8b-9c0d1e2f3a4b",
  "patientId": 10,
  "patientName": "Nguyễn Văn Bệnh Nhân",
  "phone": "0988888888",
  "doctorId": 1,
  "doctorName": "BS. CKII Nguyễn Văn A",
  "specialty": "Tim mạch",
  "roomNumber": "P.201",
  "timeSlotId": 1,
  "slotDate": "2026-11-28",
  "slotTime": "08:00 - 09:00",
  "queueNumber": null,
  "status": "Pending",
  "symptoms": "Đau tức ngực trái khi vận động mạnh",
  "createdAt": "2026-11-27T14:30:00Z"
}
```

---

### 1.4. Tra cứu chi tiết lịch hẹn & tiến độ
- **Method:** `GET`
- **Endpoint:** `/appointments/{id}`
- **Response 200 OK:**
```json
{
  "appointmentId": "7f8b91a2-3c4d-5e6f-7a8b-9c0d1e2f3a4b",
  "patientName": "Nguyễn Văn Bệnh Nhân",
  "doctorName": "BS. CKII Nguyễn Văn A",
  "roomNumber": "P.201",
  "queueNumber": 3,
  "status": "Confirmed",
  "history": [
    {
      "queueNumber": 0,
      "fromStatus": null,
      "toStatus": "Pending",
      "action": "CreateAppointment",
      "changedBy": "Patient",
      "timestamp": "2026-11-27T14:30:00Z"
    },
    {
      "queueNumber": 3,
      "fromStatus": "Pending",
      "toStatus": "Confirmed",
      "action": "AssignQueueNumber",
      "changedBy": "QueueWorker",
      "timestamp": "2026-11-27T14:30:01Z"
    }
  ]
}
```

---

### 1.5. Hủy lịch khám
- **Method:** `DELETE`
- **Endpoint:** `/appointments/{id}`
- **Query Parameters:**
  - `reason` (string, optional): Lý do hủy lịch.
- **Response 200 OK:**
```json
{
  "message": "Đã hủy lịch khám thành công và giải phóng khung giờ.",
  "appointmentId": "7f8b91a2-3c4d-5e6f-7a8b-9c0d1e2f3a4b",
  "status": "Cancelled"
}
```

---

### 1.6. Tra cứu trạng thái hàng đợi phòng khám
- **Method:** `GET`
- **Endpoint:** `/queues/{doctorId}`
- **Response 200 OK:**
```json
{
  "doctorId": 1,
  "doctorName": "BS. CKII Nguyễn Văn A",
  "specialty": "Tim mạch",
  "roomNumber": "P.201",
  "currentServingNumber": 2,
  "currentPatientName": "Trần Thị Lan",
  "currentAppointmentId": "1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d",
  "totalWaiting": 3,
  "waitingList": [
    {
      "appointmentId": "7f8b91a2-3c4d-5e6f-7a8b-9c0d1e2f3a4b",
      "queueNumber": 3,
      "patientName": "Nguyễn Văn Bệnh Nhân",
      "slotTime": "08:00 - 09:00",
      "status": "Confirmed"
    }
  ]
}
```

---

### 1.7. Điều phối hàng đợi phòng khám (Dành cho Bác sĩ)
- **`POST /queues/{doctorId}/next`:** Gọi số thứ tự tiếp theo vào phòng khám.
- **`POST /queues/{doctorId}/complete`:** Hoàn thành ca khám hiện tại.
- **`POST /queues/{doctorId}/noshow`:** Báo bệnh nhân vắng mặt.
