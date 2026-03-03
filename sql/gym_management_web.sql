-- 1) Create database if missing
IF DB_ID('gym_management_web') IS NULL
BEGIN
    CREATE DATABASE gym_management_web;
END
GO

USE gym_management_web;
GO

-- 2) Core tables (create only if not exists) - order matters for FKs

-- Member
IF OBJECT_ID('dbo.Member','U') IS NULL
BEGIN
CREATE TABLE dbo.Member (
    member_id   INT IDENTITY(1,1) PRIMARY KEY,
    first_name  VARCHAR(50) NOT NULL,
    last_name   VARCHAR(50) NOT NULL,
    phone       VARCHAR(15) NOT NULL,
    email       VARCHAR(100) NULL,
    birth_date  DATE NOT NULL,
    gender      VARCHAR(10) NULL,
    join_date   DATE NULL
);
END
GO

-- MembershipPackage
IF OBJECT_ID('dbo.MembershipPackage','U') IS NULL
BEGIN
CREATE TABLE dbo.MembershipPackage (
    package_id      INT IDENTITY(1,1) PRIMARY KEY,
    package_name    VARCHAR(50) NOT NULL,
    price_monthly   DECIMAL(8,2) NOT NULL,
    duration_months INT NOT NULL
);
END
GO

-- Membership
IF OBJECT_ID('dbo.Membership','U') IS NULL
BEGIN
CREATE TABLE dbo.Membership (
    membership_id INT IDENTITY(1,1) PRIMARY KEY,
    member_id     INT NOT NULL,
    package_id    INT NOT NULL,
    start_date    DATE NOT NULL,
    end_date      DATE NOT NULL,
    status        VARCHAR(20) NULL,
    CONSTRAINT FK_Membership_Member FOREIGN KEY (member_id)  REFERENCES dbo.Member(member_id),
    CONSTRAINT FK_Membership_Package FOREIGN KEY (package_id) REFERENCES dbo.MembershipPackage(package_id)
);
END
GO

-- Payment
IF OBJECT_ID('dbo.Payment','U') IS NULL
BEGIN
CREATE TABLE dbo.Payment (
    payment_id     INT IDENTITY(1,1) PRIMARY KEY,
    membership_id  INT NOT NULL,
    amount         DECIMAL(8,2) NOT NULL,
    payment_date   DATE NOT NULL,
    payment_method VARCHAR(20) NOT NULL,
        CONSTRAINT FK_Payment_Membership FOREIGN KEY (membership_id) REFERENCES dbo.Membership(membership_id)
);
END
GO

-- GymZones
IF OBJECT_ID('dbo.GymZones','U') IS NULL
BEGIN
CREATE TABLE dbo.GymZones
(
    zone_id INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(100) NOT NULL,
    description NVARCHAR(255) NULL,
    capacity INT NULL
);
END
GO

-- Equipment (depends on GymZones)
IF OBJECT_ID('dbo.Equipment','U') IS NULL
BEGIN
CREATE TABLE dbo.Equipment
(
    equipment_id INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(150) NOT NULL,
    equipment_type NVARCHAR(100) NOT NULL,
    zone_id INT NULL,
    status NVARCHAR(20) NOT NULL CONSTRAINT DF_Equipment_Status DEFAULT ('active'),
    purchase_date DATE NULL,
    last_maintenance_date DATETIME NULL,
    usage_count INT NOT NULL CONSTRAINT DF_Equipment_Usage DEFAULT (0),
    CONSTRAINT FK_Equipment_GymZones FOREIGN KEY (zone_id) REFERENCES dbo.GymZones(zone_id) ON DELETE SET NULL
);
END
GO

-- Maintenance (depends on Equipment)
IF OBJECT_ID('dbo.Maintenance','U') IS NULL
BEGIN
CREATE TABLE dbo.Maintenance
(
    maintenance_id INT IDENTITY(1,1) PRIMARY KEY,
    equipment_id INT NOT NULL,
    maintenance_date DATETIME NOT NULL CONSTRAINT DF_Maintenance_Date DEFAULT (GETDATE()),
    performed_by NVARCHAR(100) NULL,
    description NVARCHAR(400) NULL,
    cost DECIMAL(10,2) NULL DEFAULT(0),
    CONSTRAINT FK_Maintenance_Equipment FOREIGN KEY (equipment_id) REFERENCES dbo.Equipment(equipment_id)
);
END
GO

-- Trainer
IF OBJECT_ID('dbo.Trainer','U') IS NULL
BEGIN
CREATE TABLE dbo.Trainer (
    trainer_id INT IDENTITY(1,1) PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    specialty VARCHAR(50) NULL,
    phone VARCHAR(20) NULL
);
END
GO

-- Class (depends on Trainer)
IF OBJECT_ID('dbo.Class','U') IS NULL
BEGIN
CREATE TABLE dbo.Class (
    class_id INT IDENTITY(1,1) PRIMARY KEY,
    trainer_id INT NULL,
    class_name VARCHAR(50) NULL,
    schedule DATETIME NULL,
    room VARCHAR(30) NULL,
    CONSTRAINT FK_Class_Trainer FOREIGN KEY (trainer_id) REFERENCES dbo.Trainer(trainer_id) ON DELETE SET NULL ON UPDATE CASCADE
);
END
GO

-- Class_Registration (depends on Member and Class)
IF OBJECT_ID('dbo.Class_Registration','U') IS NULL
BEGIN
CREATE TABLE dbo.Class_Registration (
    registration_id INT IDENTITY(1,1) PRIMARY KEY,
    member_id INT NOT NULL,
    class_id INT NOT NULL,
    registration_date DATE NULL,
    CONSTRAINT FK_ClassReg_Member FOREIGN KEY (member_id) REFERENCES dbo.Member(member_id) ON DELETE CASCADE,
    CONSTRAINT FK_ClassReg_Class FOREIGN KEY (class_id) REFERENCES dbo.Class(class_id) ON DELETE CASCADE
);
END
GO

-- Admin
IF OBJECT_ID('dbo.Admin', 'U') IS NULL
BEGIN
CREATE TABLE dbo.Admin (
    admin_id INT IDENTITY(1,1) PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    password VARCHAR(100) NOT NULL,
    last_login DATETIME NULL
);
END
GO

-- Reports
IF OBJECT_ID('dbo.Reports','U') IS NULL
BEGIN
CREATE TABLE dbo.Reports (
    report_id INT IDENTITY(1,1) PRIMARY KEY,
    report_name VARCHAR(100) NOT NULL,
    generated_date DATETIME NOT NULL DEFAULT GETDATE(),
    admin_id INT NOT NULL,
    CONSTRAINT FK_Reports_Admin FOREIGN KEY (admin_id)
        REFERENCES dbo.Admin(admin_id)
);
END
GO


-- 3) Stored procedures (replace existing versions)
IF OBJECT_ID('dbo.getEquipmentUsage','P') IS NOT NULL
    DROP PROCEDURE dbo.getEquipmentUsage;
GO
CREATE PROCEDURE dbo.getEquipmentUsage
    @Top INT = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Top)
        e.equipment_id,
        e.name,
        e.equipment_type,
        e.status,
        e.usage_count,
        ISNULL(z.name, '') AS zone_name
    FROM dbo.Equipment e
    LEFT JOIN dbo.GymZones z ON e.zone_id = z.zone_id
    ORDER BY e.usage_count DESC;
END
GO

IF OBJECT_ID('dbo.markEquipmentMaintenance','P') IS NOT NULL
    DROP PROCEDURE dbo.markEquipmentMaintenance;
GO
CREATE PROCEDURE dbo.markEquipmentMaintenance
    @EquipmentId INT,
    @PerformedBy NVARCHAR(100) = NULL,
    @Description NVARCHAR(400) = NULL,
    @Cost DECIMAL(10,2) = 0,
    @NewStatus NVARCHAR(20) = 'maintenance'
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        INSERT INTO dbo.Maintenance (equipment_id, maintenance_date, performed_by, description, cost)
        VALUES (@EquipmentId, GETDATE(), @PerformedBy, @Description, @Cost);

        UPDATE dbo.Equipment
        SET status = @NewStatus,
            last_maintenance_date = GETDATE()
        WHERE equipment_id = @EquipmentId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

IF OBJECT_ID('dbo.getMostActiveMember','P') IS NOT NULL
    DROP PROCEDURE dbo.getMostActiveMember;
GO
CREATE PROCEDURE dbo.getMostActiveMember
AS
BEGIN
    SELECT TOP 1
        m.member_id,
        m.first_name,
        m.last_name,
        COUNT(cr.class_id) AS total_classes
    FROM dbo.Member m
    JOIN dbo.Class_Registration cr ON m.member_id = cr.member_id
    GROUP BY m.member_id, m.first_name, m.last_name
    ORDER BY total_classes DESC;
END
GO

IF OBJECT_ID('dbo.getLeastActiveMember','P') IS NOT NULL
    DROP PROCEDURE dbo.getLeastActiveMember;
GO
CREATE PROCEDURE dbo.getLeastActiveMember
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        m.member_id,
        m.first_name,
        m.last_name,
        COUNT(cr.class_id) AS total_classes
    FROM dbo.Member m
    LEFT JOIN dbo.Class_Registration cr 
        ON m.member_id = cr.member_id
    GROUP BY m.member_id, m.first_name, m.last_name
    ORDER BY total_classes ASC;
END
GO


-- 4) Constraints / indexes (add if missing)
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints WHERE name = 'CHK_Equipment_Status' AND parent_object_id = OBJECT_ID('dbo.Equipment')
)
BEGIN
    ALTER TABLE dbo.Equipment
    ADD CONSTRAINT CHK_Equipment_Status CHECK (status IN ('active','maintenance','broken'));
END
GO

IF OBJECT_ID('dbo.Equipment','U') IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_Equipment_zone_id' AND object_id = OBJECT_ID('dbo.Equipment')
)
CREATE INDEX IX_Equipment_zone_id ON dbo.Equipment(zone_id);
GO

IF OBJECT_ID('dbo.Equipment','U') IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_Equipment_usage_count' AND object_id = OBJECT_ID('dbo.Equipment')
)
CREATE INDEX IX_Equipment_usage_count ON dbo.Equipment(usage_count);
GO

-- 5) Seed sample data (guarded to avoid duplicates)

-- MembershipPackage sample
IF NOT EXISTS (SELECT 1 FROM dbo.MembershipPackage WHERE package_name='Basic')
BEGIN
    INSERT INTO dbo.MembershipPackage (package_name, price_monthly, duration_months) VALUES
    ('Basic', 550.00, 1),
    ('Premium', 950.00, 1),
    ('VIP', 1650.00, 1),
    ('Annual Platinum', 15990.00, 12);
END
GO

-- Member sample (idempotent by phone)
IF NOT EXISTS (SELECT 1 FROM dbo.Member WHERE phone='05323013101')
BEGIN
    INSERT INTO dbo.Member (first_name, last_name, phone, email, birth_date, gender, join_date) VALUES
    ('Burce Nur', 'Kavak',   '05323013101', 'bnkavak@gmail.com',  '2005-04-14', 'Female', '2025-01-15'),
    ('Ece',       'Bayyar',  '05433013102', 'ebayyar@gmail.com',  '2005-07-15', 'Female', '2025-01-20'),
    ('Ibrahim Said','Akinci','05543013103','isakinci@gmail.com', '2002-11-25', 'Male',   '2025-02-01'),
    ('Selin Gul', 'Bayri',   '05353013104', 'sgbayri@gmail.com',  '2004-08-11', 'Female', '2025-02-10');
END
GO

-- Membership sample (guarded by membership for member 1)
IF NOT EXISTS (SELECT 1 FROM dbo.Membership WHERE member_id = 1 AND package_id = (SELECT TOP 1 package_id FROM dbo.MembershipPackage WHERE package_name='VIP'))
BEGIN
    INSERT INTO dbo.Membership (member_id, package_id, start_date, end_date, status) VALUES
    (1, (SELECT TOP 1 package_id FROM dbo.MembershipPackage WHERE package_name='VIP'), '2025-11-01', '2025-11-30', 'Active');
END
GO

-- Payment sample (guarded loosely)
IF NOT EXISTS (SELECT 1 FROM dbo.Payment WHERE amount=1650.00 AND payment_method='Credit Card')
BEGIN
    INSERT INTO dbo.Payment (membership_id, amount, payment_date, payment_method) VALUES
    ((SELECT TOP 1 membership_id FROM dbo.Membership WHERE member_id = 1), 1650.00, '2025-11-01', 'Credit Card');
END
GO

-- Sample GymZones
IF NOT EXISTS (SELECT 1 FROM dbo.GymZones WHERE name='Cardio Zone')
BEGIN
    INSERT INTO dbo.GymZones(name, description, capacity) VALUES
    ('Cardio Zone', 'Treadmills and bikes', 20),
    ('Weight Room', 'Free weights and machines', 30),
    ('Yoga Room', 'Stretching and group classes', 25);
END
GO

-- Sample Equipment
IF NOT EXISTS (SELECT 1 FROM dbo.Equipment WHERE name='Treadmill A')
BEGIN
    INSERT INTO dbo.Equipment (name, equipment_type, zone_id, status, purchase_date, usage_count)
    VALUES
      ('Treadmill A', 'treadmill', (SELECT TOP 1 zone_id FROM dbo.GymZones WHERE name='Cardio Zone'), 'active', '2024-01-15', 120),
      ('Squat Rack 1', 'weight', (SELECT TOP 1 zone_id FROM dbo.GymZones WHERE name='Weight Room'), 'active', '2023-08-01', 80),
      ('Yoga Mat Set', 'mat', (SELECT TOP 1 zone_id FROM dbo.GymZones WHERE name='Yoga Room'), 'active', '2025-02-10', 15);
END
GO

-- Sample Maintenance
IF NOT EXISTS (SELECT 1 FROM dbo.Maintenance WHERE description='Initial check treadmill A')
BEGIN
    INSERT INTO dbo.Maintenance (equipment_id, maintenance_date, performed_by, description, cost)
    VALUES ((SELECT TOP 1 equipment_id FROM dbo.Equipment WHERE name='Treadmill A'), GETDATE(), 'tech1', 'Initial check treadmill A', 0);
END
GO

-- Sample Trainer / Class / Registration
IF NOT EXISTS (SELECT 1 FROM dbo.Trainer WHERE name='Defne Samyeli')
BEGIN
    INSERT INTO dbo.Trainer (name, specialty, phone)
    VALUES ('Defne Samyeli', 'Yoga', '555-1111'),
           ('Ya�mur Ayd�n', 'Pilates', '555-2222'),
           ('Ali Baytar', 'Fitness', '555-3333');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Class WHERE class_name='Morning Yoga')
BEGIN
    INSERT INTO dbo.Class (trainer_id, class_name, schedule, room)
    VALUES ((SELECT TOP 1 trainer_id FROM dbo.Trainer WHERE name='Defne Samyeli'), 'Morning Yoga', '2025-12-15 09:00', 'Yoga Room'),
           ((SELECT TOP 1 trainer_id FROM dbo.Trainer WHERE name='Ya�mur Ayd�n'), 'Flow & Glow', '2025-12-07 10:00', 'Pilates Room'),
           ((SELECT TOP 1 trainer_id FROM dbo.Trainer WHERE name='Ali Baytar'), 'CoreForce', '2025-12-23 20:00', 'Fitness Room');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Class_Registration WHERE member_id=1 AND class_id=(SELECT TOP 1 class_id FROM dbo.Class WHERE class_name='Morning Yoga'))
BEGIN
    INSERT INTO dbo.Class_Registration (member_id, class_id, registration_date)
    VALUES (1, (SELECT TOP 1 class_id FROM dbo.Class WHERE class_name='Morning Yoga'), '2025-12-01'),
           (2, (SELECT TOP 1 class_id FROM dbo.Class WHERE class_name='Flow & Glow'), '2025-12-02'),
           (3, (SELECT TOP 1 class_id FROM dbo.Class WHERE class_name='CoreForce'), '2025-12-03');
END
GO

-- Sample Admin
IF NOT EXISTS (SELECT 1 FROM dbo.Admin WHERE username = 'admin')
BEGIN
    INSERT INTO dbo.Admin (username, password, last_login)
    VALUES ('admin', 'admin123', GETDATE());
END
GO

-- Sample Reports
IF NOT EXISTS (SELECT 1 FROM dbo.Reports WHERE report_name = 'Most Active Member Report')
BEGIN
    INSERT INTO dbo.Reports (report_name, admin_id)
    VALUES
    ('Most Active Member Report', (SELECT TOP 1 admin_id FROM dbo.Admin)),
    ('Equipment Usage Report', (SELECT TOP 1 admin_id FROM dbo.Admin));
END
GO

-- 6) Quick verification selects (optional)
SELECT DB_NAME() AS CurrentDatabase;
SELECT COUNT(*) AS Zones FROM dbo.GymZones;
SELECT COUNT(*) AS EquipmentCount FROM dbo.Equipment;
SELECT COUNT(*) AS MaintenanceCount FROM dbo.Maintenance;
SELECT COUNT(*) AS Members FROM dbo.Member;
GO
