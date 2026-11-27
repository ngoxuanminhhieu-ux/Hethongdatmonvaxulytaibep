-- ============================================================================
-- ĐỀ TÀI 09: HỆ THỐNG ĐẶT LỊCH KHÁM VÀ QUẢN LÝ HÀNG ĐỢI
-- (CLINIC APPOINTMENT & QUEUE MANAGEMENT SYSTEM)
-- Script khởi tạo CSDL PostgreSQL và dữ liệu mẫu (Seed Data)
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Xóa bảng cũ theo thứ tự quan hệ
DROP TABLE IF EXISTS queue_logs CASCADE;
DROP TABLE IF EXISTS appointments CASCADE;
DROP TABLE IF EXISTS patients CASCADE;
DROP TABLE IF EXISTS time_slots CASCADE;
DROP TABLE IF EXISTS doctors CASCADE;

-- 1. BẢNG BÁC SĨ / PHÒNG KHÁM (doctors)
CREATE TABLE doctors (
    id SERIAL PRIMARY KEY,
    name VARCHAR(120) NOT NULL,
    specialty VARCHAR(100) NOT NULL,
    room_number VARCHAR(20) NOT NULL,
    phone VARCHAR(20),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 2. BẢNG KHUNG GIỜ KHÁM (time_slots)
CREATE TABLE time_slots (
    id SERIAL PRIMARY KEY,
    doctor_id INT NOT NULL REFERENCES doctors(id) ON DELETE CASCADE,
    slot_date DATE NOT NULL,
    start_time VARCHAR(10) NOT NULL,
    end_time VARCHAR(10) NOT NULL,
    max_capacity INT NOT NULL DEFAULT 5,
    current_bookings INT NOT NULL DEFAULT 0,
    is_available BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 3. BẢNG BỆNH NHÂN (patients)
CREATE TABLE patients (
    id SERIAL PRIMARY KEY,
    full_name VARCHAR(120) NOT NULL,
    phone VARCHAR(20) NOT NULL,
    identity_number VARCHAR(30),
    date_of_birth DATE,
    gender VARCHAR(10),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 4. BẢNG LỊCH HẸN & SỐ THỨ TỰ KHÁM (appointments)
CREATE TABLE appointments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    patient_id INT NOT NULL REFERENCES patients(id) ON DELETE CASCADE,
    doctor_id INT NOT NULL REFERENCES doctors(id) ON DELETE RESTRICT,
    time_slot_id INT NOT NULL REFERENCES time_slots(id) ON DELETE RESTRICT,
    queue_number INT,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending', 'Confirmed', 'Serving', 'Completed', 'Cancelled', 'NoShow')),
    symptoms TEXT,
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 5. BẢNG NHẬT KÝ HÀNG ĐỢI (queue_logs)
CREATE TABLE queue_logs (
    id SERIAL PRIMARY KEY,
    doctor_id INT NOT NULL REFERENCES doctors(id) ON DELETE CASCADE,
    appointment_id UUID REFERENCES appointments(id) ON DELETE SET NULL,
    queue_number INT NOT NULL,
    from_status VARCHAR(20),
    to_status VARCHAR(20) NOT NULL,
    action VARCHAR(50) NOT NULL,
    changed_by VARCHAR(50) NOT NULL,
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- CHỈ MỤC TỐI ƯU TRUY VẤN PHÂN TÁN
CREATE INDEX idx_appointments_doctor_status ON appointments(doctor_id, status);
CREATE INDEX idx_appointments_queue_number ON appointments(doctor_id, queue_number);
CREATE INDEX idx_time_slots_doctor_date ON time_slots(doctor_id, slot_date);
CREATE INDEX idx_queue_logs_doctor_id ON queue_logs(doctor_id);

-- ============================================================================
-- DỮ LIỆU MẪU (SEED DATA)
-- ============================================================================

-- Bác sĩ mẫu
INSERT INTO doctors (name, specialty, room_number, phone, is_active) VALUES
('BS. CKII Nguyễn Văn Hùng', 'Tim mạch', 'Phòng 101', '0901112233', true),
('ThS. BS Trần Mai Lan', 'Nhi khoa', 'Phòng 102', '0902223344', true),
('BS. CKI Lê Quang Vinh', 'Tai Mũi Họng', 'Phòng 201', '0903334455', true),
('BS. Phạm Thị Hương', 'Da liễu', 'Phòng 202', '0904445566', true),
('ThS. BS Hoàng Tuấn Anh', 'Cơ Xương Khớp', 'Phòng 301', '0905556677', true);

-- Khung giờ khám mẫu cho ngày hôm nay và các ngày tới
INSERT INTO time_slots (doctor_id, slot_date, start_time, end_time, max_capacity, current_bookings, is_available) VALUES
-- BS Hùng (Tim mạch)
(1, CURRENT_DATE, '08:00', '09:00', 5, 0, true),
(1, CURRENT_DATE, '09:00', '10:00', 5, 0, true),
(1, CURRENT_DATE, '10:00', '11:00', 5, 0, true),
(1, CURRENT_DATE, '13:30', '14:30', 5, 0, true),
(1, CURRENT_DATE, '14:30', '15:30', 5, 0, true),
(1, CURRENT_DATE + INTERVAL '1 day', '08:00', '09:00', 5, 0, true),
(1, CURRENT_DATE + INTERVAL '1 day', '09:00', '10:00', 5, 0, true),

-- ThS.BS Lan (Nhi khoa)
(2, CURRENT_DATE, '08:00', '09:00', 5, 0, true),
(2, CURRENT_DATE, '09:00', '10:00', 5, 0, true),
(2, CURRENT_DATE, '10:00', '11:00', 5, 0, true),
(2, CURRENT_DATE, '13:30', '14:30', 5, 0, true),

-- BS Vinh (Tai Mũi Họng)
(3, CURRENT_DATE, '08:00', '09:00', 5, 0, true),
(3, CURRENT_DATE, '09:00', '10:00', 5, 0, true),
(3, CURRENT_DATE, '13:30', '14:30', 5, 0, true),

-- BS Hương (Da liễu)
(4, CURRENT_DATE, '08:30', '09:30', 5, 0, true),
(4, CURRENT_DATE, '09:30', '10:30', 5, 0, true),

-- BS Tuấn Anh (Cơ Xương Khớp)
(5, CURRENT_DATE, '08:00', '09:00', 5, 0, true),
(5, CURRENT_DATE, '09:00', '10:00', 5, 0, true);

-- Bệnh nhân mẫu
INSERT INTO patients (full_name, phone, identity_number, date_of_birth, gender) VALUES
('Nguyễn Văn An', '0901234567', '079090001234', '1990-05-15', 'Nam'),
('Trần Thị Bình', '0912345678', '079192005678', '1985-08-20', 'Nữ'),
('Lê Hoàng Nam', '0987654321', '079201009988', '2001-11-02', 'Nam');
