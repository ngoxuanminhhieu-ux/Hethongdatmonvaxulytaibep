-- ============================================================================
-- ĐỀ TÀI 04: HỆ THỐNG ĐẶT MÓN VÀ XỬ LÝ TẠI BẾP (FOOD ORDER & KITCHEN SYSTEM)
-- Script khởi tạo CSDL PostgreSQL và dữ liệu mẫu (Seed Data)
-- ============================================================================

-- Bật extension tạo UUID
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Xóa bảng cũ nếu tồn tại (theo thứ tự quan hệ phụ thuộc)
DROP TABLE IF EXISTS order_status_histories CASCADE;
DROP TABLE IF EXISTS order_details CASCADE;
DROP TABLE IF EXISTS orders CASCADE;
DROP TABLE IF EXISTS menu_items CASCADE;
DROP TABLE IF EXISTS customers CASCADE;

-- 1. BẢNG KHÁCH HÀNG (customers)
CREATE TABLE customers (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    phone VARCHAR(20),
    table_number VARCHAR(20) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 2. BẢNG THỰC ĐƠN (menu_items)
CREATE TABLE menu_items (
    id SERIAL PRIMARY KEY,
    name VARCHAR(150) NOT NULL,
    description TEXT,
    price NUMERIC(12, 2) NOT NULL CHECK (price >= 0),
    image_url VARCHAR(500),
    category VARCHAR(50) NOT NULL,
    is_available BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 3. BẢNG ĐƠN ĐẶT HÀNG (orders)
CREATE TABLE orders (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id INT REFERENCES customers(id) ON DELETE SET NULL,
    table_number VARCHAR(20) NOT NULL,
    total_amount NUMERIC(12, 2) NOT NULL DEFAULT 0.00 CHECK (total_amount >= 0),
    status VARCHAR(20) NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending', 'Preparing', 'Ready', 'Cancelled')),
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 4. BẢNG CHI TIẾT ĐƠN HÀNG (order_details)
CREATE TABLE order_details (
    id SERIAL PRIMARY KEY,
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    menu_item_id INT NOT NULL REFERENCES menu_items(id),
    unit_price NUMERIC(12, 2) NOT NULL CHECK (unit_price >= 0),
    quantity INT NOT NULL CHECK (quantity > 0),
    subtotal NUMERIC(12, 2) NOT NULL CHECK (subtotal >= 0),
    item_notes VARCHAR(255)
);

-- 5. BẢNG LỊCH SỬ THAY ĐỔI TRẠNG THÁI (order_status_histories)
CREATE TABLE order_status_histories (
    id SERIAL PRIMARY KEY,
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    from_status VARCHAR(20),
    to_status VARCHAR(20) NOT NULL,
    changed_by VARCHAR(50) NOT NULL,
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- CÁC CHỈ MỤC TỐI ƯU TRUY VẤN
CREATE INDEX idx_orders_status ON orders(status);
CREATE INDEX idx_orders_customer_id ON orders(customer_id);
CREATE INDEX idx_order_details_order_id ON order_details(order_id);
CREATE INDEX idx_order_status_histories_order_id ON order_status_histories(order_id);

-- ============================================================================
-- DỮ LIỆU MẪU (SEED DATA)
-- ============================================================================

-- Khách hàng mẫu
INSERT INTO customers (name, phone, table_number) VALUES
('Nguyễn Văn An', '0901234567', 'Bàn 01'),
('Trần Thị Bình', '0912345678', 'Bàn 02'),
('Lê Hoàng Nam', '0987654321', 'Bàn 05');

-- Danh mục món ăn mẫu
INSERT INTO menu_items (name, description, price, image_url, category, is_available) VALUES
('Phở Bò Tái Nạm', 'Bánh phở tươi, thịt bò tái nạm mềm thơm cùng nước dùng hầm xương 24h đậm đà gia truyền', 65000.00, 'https://images.unsplash.com/photo-1582878826629-29b7ad1cdc43?w=500', 'Món chính', true),
('Cơm Tấm Sườn Bì Chả', 'Cơm tấm thơm nóng dẻo, sườn nướng mật ong đậm đà, bì thơm và chả trứng hấp dẫn', 60000.00, 'https://images.unsplash.com/photo-1544025162-d76694265947?w=500', 'Món chính', true),
('Bún Chả Hà Nội', 'Bún tươi, chả miếng nướng than hoa thơm lừng kèm bát nước mắm chua ngọt và rau sống', 55000.00, 'https://images.unsplash.com/photo-1569058242253-92a9c755a0ec?w=500', 'Món chính', true),
('Bánh Mì Thịt Nướng Đặc Biệt', 'Bánh mì giòn rụm, thịt nướng than hoa, pa-tê béo ngậy, đồ chua và sốt đặc biệt', 35000.00, 'https://images.unsplash.com/photo-1626804475297-41608ea09aeb?w=500', 'Món chính', true),
('Khoai Tây Chiên Giòn', 'Khoai tây cắt thanh chiên giòn rụm chấm kèm sốt tương cà hoặc sốt phô mai thơm ngậy', 30000.00, 'https://images.unsplash.com/photo-1576107232684-1279f3908594?w=500', 'Khai vị', true),
('Cà Phê Sữa Đá Sài Gòn', 'Cà phê nguyên chất pha phin truyền thống hòa quyện cùng sữa đặc béo ngậy và đá lạnh', 25000.00, 'https://images.unsplash.com/photo-1517701550927-30cf4ba1dba5?w=500', 'Đồ uống', true),
('Trà Đào Cam Sả', 'Trà thanh mát kết hợp vị cam thơm dịu, sả cay nhẹ và miếng đào giòn ngọt', 35000.00, 'https://images.unsplash.com/photo-1556881286-fc6915169721?w=500', 'Đồ uống', true),
('Bánh Flan Caramel', 'Bánh flan mềm mịn, thơm mùi trứng sữa cùng sốt caramel đắng nhẹ thơm ngon', 20000.00, 'https://images.unsplash.com/photo-1528975604071-b4dc52a2d18c?w=500', 'Tráng miệng', true);
