const API_BASE_URL = "http://localhost:5001/api";
const NOTIFICATION_HUB_URL = "http://localhost:5003/hubs/order-notification";

let orders = [];
let signalrConnection = null;

document.addEventListener("DOMContentLoaded", () => {
    loadOrders();
    initKitchenSignalR();
});

// 1. Tải danh sách đơn hàng từ Order API
async function loadOrders() {
    try {
        const res = await fetch(`${API_BASE_URL}/orders`);
        if (!res.ok) throw new Error("Không thể tải danh sách đơn hàng.");
        orders = await res.json();
        renderBoard(orders);
    } catch (err) {
        console.warn("Lỗi tải đơn hàng:", err.message);
    }
}

// 2. Render các đơn lên bảng KDS
function renderBoard(orderList) {
    const colPending = document.getElementById("column-pending");
    const colPreparing = document.getElementById("column-preparing");
    const colReady = document.getElementById("column-ready");

    colPending.innerHTML = "";
    colPreparing.innerHTML = "";
    colReady.innerHTML = "";

    let countPending = 0;
    let countPreparing = 0;
    let countReady = 0;

    orderList.forEach(order => {
        const status = order.status.toLowerCase();
        const card = createOrderCard(order);

        if (status === "pending") {
            colPending.appendChild(card);
            countPending++;
        } else if (status === "preparing") {
            colPreparing.appendChild(card);
            countPreparing++;
        } else if (status === "ready") {
            colReady.appendChild(card);
            countReady++;
        }
    });

    document.getElementById("count-pending").textContent = countPending;
    document.getElementById("count-preparing").textContent = countPreparing;
    document.getElementById("count-ready").textContent = countReady;
}

function createOrderCard(order) {
    const card = document.createElement("div");
    card.className = `order-card ${order.status.toLowerCase()}`;

    const itemsHtml = order.items.map(i => `
        <div><b>${i.quantity}x</b> ${i.name} ${i.notes ? `<i style="color:#64748b;">(${i.notes})</i>` : ''}</div>
    `).join("");

    let actionButtons = "";
    if (order.status.toLowerCase() === "pending") {
        actionButtons = `
            <button class="btn btn-primary" style="flex: 1;" onclick="updateStatus('${order.orderId}', 'Preparing')">🔥 Bắt đầu nấu</button>
            <button class="btn btn-outline" style="color: #ef4444;" onclick="updateStatus('${order.orderId}', 'Cancelled')">Hủy</button>
        `;
    } else if (order.status.toLowerCase() === "preparing") {
        actionButtons = `
            <button class="btn btn-success" style="width: 100%;" onclick="updateStatus('${order.orderId}', 'Ready')">✅ Đã nấu xong</button>
        `;
    } else if (order.status.toLowerCase() === "ready") {
        actionButtons = `
            <span style="font-size: 0.8rem; color: #15803d; font-weight: 600;">✨ Sẵn sàng phục vụ</span>
        `;
    }

    card.innerHTML = `
        <div class="order-card-header">
            <span>🏷️ ${order.tableNumber}</span>
            <span style="color: #64748b; font-size: 0.8rem;">${new Date(order.createdAt).toLocaleTimeString()}</span>
        </div>
        <div style="font-size: 0.9rem; margin-bottom: 0.4rem;">
            Khách: <b>${order.customerName}</b>
        </div>
        ${order.notes ? `<div style="font-size: 0.8rem; color: #b45309; background: #fef3c7; padding: 0.3rem 0.5rem; border-radius: 4px; margin-bottom: 0.4rem;">📝 ${order.notes}</div>` : ''}
        <div class="order-card-items">
            ${itemsHtml}
        </div>
        <div class="order-actions">
            ${actionButtons}
        </div>
    `;

    return card;
}

// 3. Cập nhật trạng thái qua API (PUT /api/orders/{id}/status)
window.updateStatus = async function(orderId, newStatus) {
    try {
        const res = await fetch(`${API_BASE_URL}/orders/${orderId}/status`, {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                status: newStatus,
                changedBy: "Chef KDS",
                notes: `Bếp chuyển trạng thái sang ${newStatus}`
            })
        });

        if (!res.ok) throw new Error("Cập nhật trạng thái thất bại.");
        await loadOrders();
    } catch (err) {
        alert("Lỗi: " + err.message);
    }
};

// 4. Kết nối Realtime SignalR cho màn hình bếp
function initKitchenSignalR() {
    if (typeof signalR === "undefined") return;

    signalrConnection = new signalR.HubConnectionBuilder()
        .withUrl(NOTIFICATION_HUB_URL)
        .withAutomaticReconnect()
        .build();

    signalrConnection.on("ReceiveKitchenUpdate", (data) => {
        console.log(">>> [KITCHEN REALTIME EVENT]", data);
        // Tự động tải lại danh sách đơn khi có sự kiện mới từ RabbitMQ
        loadOrders();
    });

    signalrConnection.start()
        .then(() => {
            console.log("Kitchen connected to SignalR Hub");
            signalrConnection.invoke("JoinKitchenDashboard");
        })
        .catch(err => {
            console.warn("SignalR Kitchen Warning:", err.message);
            document.getElementById("realtime-status-dot").style.background = "#94a3b8";
            document.getElementById("realtime-status-text").textContent = "Polling Mode";
            document.getElementById("realtime-status-text").style.color = "#94a3b8";
        });
}
