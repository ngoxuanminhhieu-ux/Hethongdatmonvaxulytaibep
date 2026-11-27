const API_BASE_URL = "http://localhost:5001/api";
const NOTIFICATION_HUB_URL = "http://localhost:5003/hubs/queue-notification";

let doctorsList = [];
let selectedDoctorId = null;
let signalrConnection = null;

document.addEventListener("DOMContentLoaded", () => {
    fetchDoctors();
    initSignalR();
});

// 1. Tải danh sách bác sĩ
async function fetchDoctors() {
    try {
        const res = await fetch(`${API_BASE_URL}/doctors`);
        if (!res.ok) throw new Error("Không thể tải danh sách bác sĩ.");
        doctorsList = await res.json();

        const select = document.getElementById("doctorSelect");
        select.innerHTML = '<option value="">-- Chọn Bác sĩ / Phòng khám --</option>';

        doctorsList.forEach(doc => {
            const opt = document.createElement("option");
            opt.value = doc.id;
            opt.textContent = `${doc.name} - ${doc.specialty} (${doc.roomNumber})`;
            select.appendChild(opt);
        });

        if (doctorsList.length > 0) {
            select.value = doctorsList[0].id;
            onDoctorChanged();
        }
    } catch (err) {
        console.warn("Lỗi tải bác sĩ:", err.message);
    }
}

function onDoctorChanged() {
    const docId = document.getElementById("doctorSelect").value;
    if (!docId) return;

    selectedDoctorId = parseInt(docId);
    const doctor = doctorsList.find(d => d.id === selectedDoctorId);
    if (doctor) {
        document.getElementById("serving-room-name").textContent = `${doctor.roomNumber} - ${doctor.specialty}`;
    }

    loadQueueStatus();

    if (signalrConnection && signalrConnection.state === signalR.HubConnectionState.Connected) {
        signalrConnection.invoke("JoinDoctorQueueGroup", selectedDoctorId.toString());
    }
}

// 2. Tải trạng thái hàng đợi
async function loadQueueStatus() {
    if (!selectedDoctorId) return;

    try {
        const res = await fetch(`${API_BASE_URL}/queues/${selectedDoctorId}`);
        if (!res.ok) throw new Error("Không thể tải trạng thái hàng đợi.");
        const data = await res.json();

        renderQueue(data);
    } catch (err) {
        console.warn("Lỗi tải queue:", err.message);
    }
}

function renderQueue(data) {
    document.getElementById("serving-number-big").textContent = data.currentServingNumber ? `#${data.currentServingNumber.toString().padStart(2, '0')}` : "-";
    document.getElementById("serving-patient-name").textContent = data.currentPatientName ? `Bệnh nhân: ${data.currentPatientName}` : "Chưa có lượt nào";
    document.getElementById("badge-waiting-count").textContent = `${data.totalWaiting} đang chờ`;

    const tbody = document.getElementById("waiting-table-body");
    tbody.innerHTML = "";

    if (data.waitingList.length === 0) {
        tbody.innerHTML = `<tr><td colspan="4" style="text-align: center; color: #94a3b8; padding: 2rem;">Hiện không còn bệnh nhân nào trong hàng đợi.</td></tr>`;
        return;
    }

    data.waitingList.forEach(item => {
        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td><b style="font-size: 1.1rem; color: #0284c7;">#${item.queueNumber.toString().padStart(2, '0')}</b></td>
            <td><b>${item.patientName}</b></td>
            <td>${item.slotTime}</td>
            <td><span class="badge badge-confirmed">Chờ khám</span></td>
        `;
        tbody.appendChild(tr);
    });
}

// 3. Thao tác bác sĩ
async function callNext() {
    if (!selectedDoctorId) return;

    try {
        const res = await fetch(`${API_BASE_URL}/queues/${selectedDoctorId}/next`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ action: "CallNext", changedBy: "Doctor" })
        });

        if (!res.ok) throw new Error("Thao tác thất bại.");
        await loadQueueStatus();
    } catch (err) {
        alert("Lỗi: " + err.message);
    }
}

async function completeServing() {
    if (!selectedDoctorId) return;

    try {
        const res = await fetch(`${API_BASE_URL}/queues/${selectedDoctorId}/complete`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ action: "Complete", changedBy: "Doctor" })
        });

        if (!res.ok) {
            const errData = await res.json();
            throw new Error(errData.message || "Thao tác thất bại.");
        }
        await loadQueueStatus();
    } catch (err) {
        alert("Thông báo: " + err.message);
    }
}

async function markNoShow() {
    if (!selectedDoctorId) return;

    try {
        const res = await fetch(`${API_BASE_URL}/queues/${selectedDoctorId}/noshow`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ action: "NoShow", changedBy: "Doctor" })
        });

        if (!res.ok) {
            const errData = await res.json();
            throw new Error(errData.message || "Thao tác thất bại.");
        }
        await loadQueueStatus();
    } catch (err) {
        alert("Thông báo: " + err.message);
    }
}

// 4. SignalR Realtime Listener
function initSignalR() {
    if (typeof signalR === "undefined") return;

    signalrConnection = new signalR.HubConnectionBuilder()
        .withUrl(NOTIFICATION_HUB_URL)
        .withAutomaticReconnect()
        .build();

    signalrConnection.on("ReceiveQueueUpdate", (data) => {
        console.log(">>> [DOCTOR SCREEN REALTIME EVENT]", data);
        loadQueueStatus();
    });

    signalrConnection.start()
        .then(() => {
            console.log("Doctor screen connected to SignalR Hub!");
            if (selectedDoctorId) {
                signalrConnection.invoke("JoinDoctorQueueGroup", selectedDoctorId.toString());
            }
        })
        .catch(err => {
            console.warn("SignalR Warning:", err.message);
            document.getElementById("realtime-dot").style.background = "#94a3b8";
            document.getElementById("realtime-text").textContent = "Polling Mode";
        });
}
