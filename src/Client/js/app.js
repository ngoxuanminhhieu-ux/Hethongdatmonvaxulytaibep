const API_BASE_URL = "http://localhost:5001/api";
const NOTIFICATION_HUB_URL = "http://localhost:5003/hubs/order-notification";

let cart = [];
let menuList = [];
let signalrConnection = null;
let currentTrackingOrderId = null;

// Khởi chạy khi load trang
document.addEventListener("DOMContentLoaded", () => {
    fetchMenu();
    initSignalR();

    document.getElementById("btn-submit-order").addEventListener("click", submitOrder);
});

// 1. Tải thực đơn từ Order API
async function fetchMenu() {
    const loadingElem = document.getElementById("menu-loading");
    const container = document.getElementById("menu-container");

    try {
        const res = await fetch(`${API_BASE_URL}/menu`);
        if (!res.ok) throw new Error("Không thể tải menu từ API.");
        menuList = await res.json();

        loadingElem.textContent = `(${menuList.length} món)`;
        renderMenu(menuList);
    } catch (err) {
        console.warn("Lỗi kết nối API:", err.message);
        loadingElem.textContent = "(API chưa chạy - Hiển thị dữ liệu mẫu)";
        // Dữ liệu mẫu dự phòng để xem giao diện trước
        menuList = [
            { id: 1, name: "Phở Bò Tái Nạm", price: 65000, description: "Bánh phở tươi, thịt bò tái nạm mềm thơm", imageUrl: "https://images.unsplash.com/photo-1582878826629-29b7ad1cdc43?w=500" },
            { id: 2, name: "Cơm Tấm Sườn Bì Chả", price: 60000, description: "Cơm tấm nóng dẻo, sườn nướng mật ong", imageUrl: "https://images.unsplash.com/photo-1544025162-d76694265947?w=500" },
            { id: 6, name: "Cà Phê Sữa Đá Sài Gòn", price: 25000, description: "Cà phê pha phin truyền thống thơm béo", imageUrl: "https://images.unsplash.com/photo-1517701550927-30cf4ba1dba5?w=500" },
            { id: 7, name: "Trà Đào Cam Sả", price: 35000, description: "Trà thanh mát kết hợp vị cam thơm dịu", imageUrl: "https://images.unsplash.com/photo-1556881286-fc6915169721?w=500" }
        ];
        renderMenu(menuList);
    }
}

function renderMenu(items) {
    const container = document.getElementById("menu-container");
    container.innerHTML = "";

    items.forEach(item => {
        const card = document.createElement("div");
        card.className = "menu-card";
        card.innerHTML = `
            <img src="${item.imageUrl || 'https://via.placeholder.com/300x200?text=Food'}" alt="${item.name}">
            <div class="menu-card-body">
                <h3>${item.name}</h3>
                <p>${item.description || ''}</p>
                <div class="menu-card-price">${formatCurrency(item.price)}</div>
                <button class="btn btn-primary" onclick="addToCart(${item.id})">➕ Thêm vào giỏ</button>
            </div>
        `;
        container.appendChild(card);
    });
}

// 2. Quản lý giỏ hàng
window.addToCart = function(menuItemId) {
    const item = menuList.find(m => m.id === menuItemId);
    if (!item) return;

    const existing = cart.find(c => c.menuItemId === menuItemId);
    if (existing) {
        existing.quantity += 1;
    } else {
        cart.push({
            menuItemId: item.id,
            name: item.name,
            price: item.price,
            quantity: 1,
            notes: ""
        });
    }

    renderCart();
};

window.changeQuantity = function(menuItemId, delta) {
    const item = cart.find(c => c.menuItemId === menuItemId);
    if (!item) return;

    item.quantity += delta;
    if (item.quantity <= 0) {
        cart = cart.filter(c => c.menuItemId !== menuItemId);
    }
    renderCart();
};

function renderCart() {
    const container = document.getElementById("cart-items-container");
    const totalElem = document.getElementById("cart-total-price");
    const submitBtn = document.getElementById("btn-submit-order");

    if (cart.length === 0) {
        container.innerHTML = `<p style="color: #94a3b8; text-align: center; padding: 1.5rem 0;">Chưa có món ăn nào trong giỏ.</p>`;
        totalElem.textContent = "0 đ";
        submitBtn.disabled = true;
        return;
    }

    submitBtn.disabled = false;
    container.innerHTML = "";
    let total = 0;

    cart.forEach(item => {
        const subtotal = item.price * item.quantity;
        total += subtotal;

        const row = document.createElement("div");
        row.className = "cart-item";
        row.innerHTML = `
            <div class="cart-item-info">
                <h4>${item.name}</h4>
                <span>${formatCurrency(item.price)} x ${item.quantity} = <b>${formatCurrency(subtotal)}</b></span>
            </div>
            <div>
                <button class="btn btn-outline" style="padding: 0.2rem 0.6rem;" onclick="changeQuantity(${item.menuItemId}, -1)">-</button>
                <span style="margin: 0 0.4rem; font-weight: 600;">${item.quantity}</span>
                <button class="btn btn-outline" style="padding: 0.2rem 0.6rem;" onclick="changeQuantity(${item.menuItemId}, 1)">+</button>
            </div>
        `;
        container.appendChild(row);
    });

    totalElem.textContent = formatCurrency(total);
}

// 3. Gửi đơn đặt món (POST /api/orders)
async function submitOrder() {
    const tableNumber = document.getElementById("tableNumber").value.trim();
    const customerName = document.getElementById("customerName").value.trim();
    const phone = document.getElementById("phone").value.trim();
    const notes = document.getElementById("orderNotes").value.trim();
    const submitBtn = document.getElementById("btn-submit-order");

    if (!tableNumber || !customerName) {
        alert("Vui lòng nhập số bàn và họ tên của bạn!");
        return;
    }

    if (cart.length === 0) {
        alert("Giỏ hàng đang trống!");
        return;
    }

    const payload = {
        customerName,
        phone,
        tableNumber,
        notes,
        items: cart.map(c => ({
            menuItemId: c.menuItemId,
            quantity: c.quantity,
            notes: c.notes
        }))
    };

    try {
        submitBtn.disabled = true;
        submitBtn.textContent = "⏳ Đang gửi đơn...";

        const res = await fetch(`${API_BASE_URL}/orders`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        if (!res.ok) {
            const errData = await res.json();
            throw new Error(errData.message || "Tạo đơn thất bại.");
        }

        const createdOrder = await res.json();
        console.log("Đơn hàng tạo thành công:", createdOrder);

        // Reset giỏ hàng
        cart = [];
        renderCart();

        // Bắt đầu theo dõi đơn hàng
        startTrackingOrder(createdOrder.orderId);
        alert(`🎉 Đặt món thành công! Mã đơn: ${createdOrder.orderId}`);
    } catch (err) {
        alert("Lỗi: " + err.message);
    } finally {
        submitBtn.disabled = false;
        submitBtn.textContent = "🚀 Gửi Đơn Đặt Món";
    }
}

// 4. Theo dõi Realtime với SignalR
function initSignalR() {
    if (typeof signalR === "undefined") {
        console.warn("Thư viện SignalR chưa sẵn sàng.");
        return;
    }

    signalrConnection = new signalR.HubConnectionBuilder()
        .withUrl(NOTIFICATION_HUB_URL)
        .withAutomaticReconnect()
        .build();

    signalrConnection.on("ReceiveOrderStatusUpdate", (data) => {
        console.log(">>> [REALTIME NOTIFICATION]", data);
        try {
            const parsed = typeof data.rawMessage === "string" ? JSON.parse(data.rawMessage) : data;
            const newStatus = parsed.data ? parsed.data.newStatus : (parsed.newStatus || "Unknown");
            updateTrackingUI(newStatus, parsed.data ? parsed.data.message : "Trạng thái đơn hàng thay đổi!");
        } catch (e) {
            console.error("Lỗi parse message realtime:", e);
        }
    });

    signalrConnection.start()
        .then(() => console.log("SignalR Connected to Notification Service!"))
        .catch(err => console.warn("SignalR Connection Warning:", err.message));
}

function startTrackingOrder(orderId) {
    currentTrackingOrderId = orderId;
    document.getElementById("order-tracking-card").style.display = "block";
    document.getElementById("tracking-order-id").textContent = orderId;

    updateTrackingUI("Pending", "Đơn hàng đã được ghi nhận vào RabbitMQ và đang chờ bếp nhận...");

    if (signalrConnection && signalrConnection.state === signalR.HubConnectionState.Connected) {
        signalrConnection.invoke("JoinOrderGroup", orderId)
            .then(() => console.log(`Joined SignalR group for order ${orderId}`))
            .catch(err => console.error("Error joining group:", err));
    }
}

function updateTrackingUI(status, message) {
    const badge = document.getElementById("realtime-status-badge");
    const alertBox = document.getElementById("realtime-alert-box");

    const stepPending = document.getElementById("step-pending");
    const stepPreparing = document.getElementById("step-preparing");
    const stepReady = document.getElementById("step-ready");

    badge.textContent = status;
    badge.className = `badge badge-${status.toLowerCase()}`;
    alertBox.textContent = message || `Trạng thái hiện tại: ${status}`;

    stepPending.className = "timeline-item";
    stepPreparing.className = "timeline-item";
    stepReady.className = "timeline-item";

    if (status === "Pending") {
        stepPending.classList.add("active");
    } else if (status === "Preparing") {
        stepPending.classList.add("active");
        stepPreparing.classList.add("active");
    } else if (status === "Ready") {
        stepPending.classList.add("active");
        stepPreparing.classList.add("active");
        stepReady.classList.add("active");
    }
}

function formatCurrency(amount) {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
}
