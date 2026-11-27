const API_BASE_URL = "http://localhost:5001/api";
const NOTIFICATION_HUB_URL = "http://localhost:5003/hubs/queue-notification";

let doctorsList = [];
let selectedDoctorId = null;
let selectedSlotId = null;
let currentAppointmentId = null;
let signalrConnection = null;

document.addEventListener("DOMContentLoaded", () => {
    fetchDoctors();
    initSignalR();
});

// 1. Tải danh sách Bác sĩ
async function fetchDoctors() {
    try {
        const res = await fetch(`${API_BASE_URL}/doctors`);
        if (!res.ok) throw new Error("Không thể tải danh sách bác sĩ.");
        doctorsList = await res.json();

        const select = document.getElementById("doctorSelect");
        select.innerHTML = '<option value="">-- Chọn Bác sĩ & Chuyên khoa --</option>';

        doctorsList.forEach(doc => {
            const opt = document.createElement("option");
            opt.value = doc.id;
            opt.textContent = `${doc.name} - ${doc.specialty} (${doc.roomNumber})`;
            select.appendChild(opt);
        });

        // Mặc định chọn bác sĩ đầu tiên nếu có
        if (doctorsList.length > 0) {
            select.value = doctorsList[0].id;
            onDoctorChanged();
        }
    } catch (err) {
        console.warn("Lỗi tải danh sách bác sĩ:", err.message);
    }
}

// 2. Khi đổi Bác sĩ -> Tải khung giờ khám
async function onDoctorChanged() {
    const docId = document.getElementById("doctorSelect").value;
    if (!docId) return;

    selectedDoctorId = parseInt(docId);
    selectedSlotId = null;

    const doctor = doctorsList.find(d => d.id === selectedDoctorId);
    if (doctor) {
        document.getElementById("doctor-info-box").style.display = "block";
        document.getElementById("info-room").textContent = doctor.roomNumber;
        document.getElementById("info-specialty").textContent = doctor.specialty;
    }

    // Tải slots
    await fetchSlots(selectedDoctorId);

    // Tham gia nhóm SignalR của bác sĩ đó để theo dõi lượt khám
    if (signalrConnection && signalrConnection.state === signalR.HubConnectionState.Connected) {
        signalrConnection.invoke("JoinDoctorQueueGroup", selectedDoctorId.toString());
    }

    // Tải trạng thái hàng đợi phòng khám hiện tại
    fetchQueueStatus(selectedDoctorId);
}

async function fetchSlots(doctorId) {
    const container = document.getElementById("slots-container");
    container.innerHTML = '<p style="color: #94a3b8; font-size: 0.85rem;">Đang tải khung giờ...</p>';

    try {
        const res = await fetch(`${API_BASE_URL}/doctors/${doctorId}/slots`);
        if (!res.ok) throw new Error("Không thể tải khung giờ.");
        const slots = await res.json();

        if (slots.length === 0) {
            container.innerHTML = '<p style="color: #ef4444; font-size: 0.85rem;">Bác sĩ này hiện chưa có khung giờ khám hôm nay.</p>';
            return;
        }

        container.innerHTML = "";
        slots.forEach(slot => {
            const pill = document.createElement("div");
            pill.className = `slot-pill ${slot.isAvailable ? '' : 'disabled'}`;
            pill.innerHTML = `
                <div class="slot-time">${slot.startTime} - ${slot.endTime}</div>
                <div class="slot-avail">${slot.isAvailable ? `Còn ${slot.availableSpots} chỗ` : 'Hết chỗ'}</div>
            `;

            if (slot.isAvailable) {
                pill.onclick = () => {
                    document.querySelectorAll(".slot-pill").forEach(p => p.classList.remove("active"));
                    pill.classList.add("active");
                    selectedSlotId = slot.id;
                };
            }
            container.appendChild(pill);
        });
    } catch (err) {
        container.innerHTML = `<p style="color: #ef4444; font-size: 0.85rem;">${err.message}</p>`;
    }
}

// 3. Đặt lịch khám
async function submitAppointment() {
    if (!selectedDoctorId) {
        alert("Vui lòng chọn bác sĩ!");
        return;
    }
    if (!selectedSlotId) {
        alert("Vui lòng chọn khung giờ khám!");
        return;
    }

    const fullName = document.getElementById("fullName").value.trim();
    const phone = document.getElementById("phone").value.trim();
    const identityNumber = document.getElementById("identityNumber").value.trim();
    const gender = document.getElementById("gender").value;
    const symptoms = document.getElementById("symptoms").value.trim();
    const btn = document.getElementById("btn-book");

    if (!fullName || !phone) {
        alert("Vui lòng nhập họ tên và số điện thoại!");
        return;
    }

    const payload = {
        fullName,
        phone,
        identityNumber,
        gender,
        doctorId: selectedDoctorId,
        timeSlotId: selectedSlotId,
        symptoms
    };

    try {
        btn.disabled = true;
        btn.textContent = "⏳ Đang gửi yêu cầu cấp số vào RabbitMQ...";

        const res = await fetch(`${API_BASE_URL}/appointments`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        if (!res.ok) {
            const errData = await res.json();
            throw new Error(errData.message || "Đặt lịch thất bại.");
        }

        const appt = await res.json();
        currentAppointmentId = appt.appointmentId;

        displayTicket(appt);

        // Tham gia nhóm SignalR để nhận số thứ tự ngay khi Queue Worker cấp số
        if (signalrConnection && signalrConnection.state === signalR.HubConnectionState.Connected) {
            signalrConnection.invoke("JoinAppointmentGroup", currentAppointmentId);
        }

        alert("🎉 Đặt lịch thành công! Yêu cầu cấp số thứ tự đã được đưa vào RabbitMQ.");
    } catch (err) {
        alert("Lỗi: " + err.message);
    } finally {
        btn.disabled = false;
        btn.textContent = "🚀 Xác Nhận Đặt Lịch & Cấp Số Thứ Tự";
    }
}

function displayTicket(appt) {
    document.getElementById("ticket-card").style.display = "block";
    document.getElementById("ticket-number-display").textContent = appt.queueNumber ? `#${appt.queueNumber.toString().padStart(2, '0')}` : "⏳";
    document.getElementById("ticket-status-badge").textContent = appt.status;
    document.getElementById("ticket-status-badge").className = `badge badge-${appt.status.toLowerCase()}`;
    document.getElementById("ticket-doctor-name").textContent = appt.doctorName;
    document.getElementById("ticket-room-name").textContent = `${appt.roomNumber} (${appt.specialty})`;
    document.getElementById("ticket-patient").textContent = appt.patientName;
    document.getElementById("ticket-time").textContent = `${appt.slotTime} (${appt.slotDate})`;
    document.getElementById("ticket-appt-id").textContent = appt.appointmentId.substring(0, 8) + "...";
}

// 4. Hủy lịch hẹn
async function cancelCurrentAppointment() {
    if (!currentAppointmentId) return;
    if (!confirm("Bạn có chắc chắn muốn hủy lịch khám này? Khung giờ sẽ được giải phóng.")) return;

    try {
        const res = await fetch(`${API_BASE_URL}/appointments/${currentAppointmentId}`, {
            method: "DELETE"
        });

        if (!res.ok) throw new Error("Hủy lịch khám thất bại.");
        alert("Đã hủy lịch khám thành công.");
        document.getElementById("ticket-card").style.display = "none";
        currentAppointmentId = null;
        onDoctorChanged();
    } catch (err) {
        alert("Lỗi: " + err.message);
    }
}

// 5. Tra cứu hàng đợi hiện tại
async function fetchQueueStatus(doctorId) {
    try {
        const res = await fetch(`${API_BASE_URL}/queues/${doctorId}`);
        if (!res.ok) return;
        const data = await res.json();

        document.getElementById("current-serving-display").textContent = data.currentServingNumber ? `#${data.currentServingNumber.toString().padStart(2, '0')}` : "-";
        document.getElementById("current-serving-patient").textContent = data.currentPatientName ? `Bệnh nhân: ${data.currentPatientName}` : "(Chưa có lượt nào)";
        document.getElementById("waiting-count-display").textContent = data.totalWaiting;
    } catch (e) {
        console.warn("Lỗi tải queue:", e);
    }
}

// 6. SignalR Realtime Listener
function initSignalR() {
    if (typeof signalR === "undefined") return;

    signalrConnection = new signalR.HubConnectionBuilder()
        .withUrl(NOTIFICATION_HUB_URL)
        .withAutomaticReconnect()
        .build();

    // Nhận cập nhật tiến trình hàng đợi của phòng khám
    signalrConnection.on("ReceiveQueueUpdate", (data) => {
        console.log(">>> [REALTIME QUEUE UPDATE]", data);
        if (selectedDoctorId) {
            fetchQueueStatus(selectedDoctorId);
        }
    });

    // Nhận số thứ tự cấp riêng cho lịch hẹn của mình
    signalrConnection.on("ReceiveAppointmentStatus", (data) => {
        console.log(">>> [REALTIME APPOINTMENT STATUS]", data);
        try {
            const parsed = typeof data.rawMessage === "string" ? JSON.parse(data.rawMessage) : data;
            if (parsed.data && parsed.data.queueNumber) {
                document.getElementById("ticket-number-display").textContent = `#${parsed.data.queueNumber.toString().padStart(2, '0')}`;
                document.getElementById("ticket-status-badge").textContent = parsed.data.status || "Confirmed";
                document.getElementById("ticket-status-badge").className = "badge badge-confirmed";
            }
        } catch (e) {
            console.error("Lỗi parse realtime message:", e);
        }
    });

    signalrConnection.start()
        .then(() => {
            console.log("Connected to Clinic Queue Notification SignalR Hub!");
            if (selectedDoctorId) {
                signalrConnection.invoke("JoinDoctorQueueGroup", selectedDoctorId.toString());
            }
        })
        .catch(err => console.warn("SignalR warning:", err.message));
}
