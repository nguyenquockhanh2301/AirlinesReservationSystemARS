-- Fresh Database Creation Script for ARS
-- Creates database, all tables (including ASP.NET Identity), and populates with flight data through end of 2025

-- Drop and create database
DROP DATABASE IF EXISTS arsdatabase;
CREATE DATABASE arsdatabase CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE arsdatabase;

-- ASP.NET Identity Tables
CREATE TABLE AspNetRoles (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(256),
    NormalizedName VARCHAR(256),
    ConcurrencyStamp LONGTEXT
);

CREATE TABLE AspNetUsers (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    UserName VARCHAR(256),
    NormalizedUserName VARCHAR(256),
    Email VARCHAR(256),
    NormalizedEmail VARCHAR(256),
    EmailConfirmed TINYINT(1) NOT NULL,
    PasswordHash LONGTEXT,
    SecurityStamp LONGTEXT,
    ConcurrencyStamp LONGTEXT,
    PhoneNumber LONGTEXT,
    PhoneNumberConfirmed TINYINT(1) NOT NULL,
    TwoFactorEnabled TINYINT(1) NOT NULL,
    LockoutEnd DATETIME(6),
    LockoutEnabled TINYINT(1) NOT NULL,
    AccessFailedCount INT NOT NULL,
    FirstName VARCHAR(100) NOT NULL,
    LastName VARCHAR(100) NOT NULL,
    Phone VARCHAR(20),
    Address VARCHAR(500),
    Gender CHAR(1) NOT NULL,
    Age INT,
    CreditCardNumber VARCHAR(20),
    SkyMiles INT NOT NULL DEFAULT 0
);

CREATE TABLE AspNetUserRoles (
    UserId INT NOT NULL,
    RoleId INT NOT NULL,
    PRIMARY KEY (UserId, RoleId),
    FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id) ON DELETE CASCADE
);

CREATE TABLE AspNetUserClaims (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    ClaimType LONGTEXT,
    ClaimValue LONGTEXT,
    FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);

CREATE TABLE AspNetUserLogins (
    LoginProvider VARCHAR(255) NOT NULL,
    ProviderKey VARCHAR(255) NOT NULL,
    ProviderDisplayName LONGTEXT,
    UserId INT NOT NULL,
    PRIMARY KEY (LoginProvider, ProviderKey),
    FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);

CREATE TABLE AspNetUserTokens (
    UserId INT NOT NULL,
    LoginProvider VARCHAR(255) NOT NULL,
    Name VARCHAR(255) NOT NULL,
    Value LONGTEXT,
    PRIMARY KEY (UserId, LoginProvider, Name),
    FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);

CREATE TABLE AspNetRoleClaims (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    RoleId INT NOT NULL,
    ClaimType LONGTEXT,
    ClaimValue LONGTEXT,
    FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id) ON DELETE CASCADE
);

-- Create PricingPolicies table
CREATE TABLE PricingPolicies (
    PricingPolicyID INT AUTO_INCREMENT PRIMARY KEY,
    PolicyName VARCHAR(100) NOT NULL,
    Description VARCHAR(500),
    DiscountPercentage DECIMAL(5,2)
);

-- Create Cities table
CREATE TABLE Cities (
    CityID INT AUTO_INCREMENT PRIMARY KEY,
    CityName VARCHAR(100) NOT NULL,
    Country VARCHAR(100) NOT NULL,
    AirportCode VARCHAR(10) NOT NULL UNIQUE
);

-- Create SeatLayouts table
CREATE TABLE SeatLayouts (
    SeatLayoutId INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    MetadataJson LONGTEXT
);

-- Create Flights table
CREATE TABLE Flights (
    FlightID INT AUTO_INCREMENT PRIMARY KEY,
    FlightNumber VARCHAR(20) NOT NULL UNIQUE,
    OriginCityID INT NOT NULL,
    DestinationCityID INT NOT NULL,
    DepartureTime DATETIME NOT NULL,
    ArrivalTime DATETIME NOT NULL,
    Duration INT NOT NULL,
    AircraftType VARCHAR(50),
    TotalSeats INT NOT NULL,
    BaseFare DECIMAL(10,2) NOT NULL,
    SeatLayoutId INT,
    PolicyID INT,
    FOREIGN KEY (OriginCityID) REFERENCES Cities(CityID),
    FOREIGN KEY (DestinationCityID) REFERENCES Cities(CityID),
    FOREIGN KEY (SeatLayoutId) REFERENCES SeatLayouts(SeatLayoutId),
    FOREIGN KEY (PolicyID) REFERENCES PricingPolicies(PricingPolicyID)
);

-- Create Seats table
CREATE TABLE Seats (
    SeatId INT AUTO_INCREMENT PRIMARY KEY,
    SeatLayoutId INT NOT NULL,
    RowNumber INT NOT NULL,
    `Column` VARCHAR(5) NOT NULL,
    Label VARCHAR(10) NOT NULL,
    CabinClass INT NOT NULL DEFAULT 2,
    IsExitRow TINYINT(1) NOT NULL DEFAULT 0,
    IsPremium TINYINT(1) NOT NULL DEFAULT 0,
    PriceModifier DECIMAL(10,2),
    FOREIGN KEY (SeatLayoutId) REFERENCES SeatLayouts(SeatLayoutId),
    UNIQUE KEY UK_Seat_Layout (SeatLayoutId, RowNumber, `Column`)
);

-- Create Schedules table
CREATE TABLE Schedules (
    ScheduleID INT AUTO_INCREMENT PRIMARY KEY,
    FlightID INT NOT NULL,
    Date DATE NOT NULL,
    Status VARCHAR(50) NOT NULL DEFAULT 'Scheduled',
    CityID INT,
    FOREIGN KEY (FlightID) REFERENCES Flights(FlightID),
    FOREIGN KEY (CityID) REFERENCES Cities(CityID)
);

-- Create FlightSeats table (FK to Reservations added later due to circular dependency)
CREATE TABLE FlightSeats (
    FlightSeatId INT AUTO_INCREMENT PRIMARY KEY,
    ScheduleId INT NOT NULL,
    SeatId INT NOT NULL,
    Status INT NOT NULL DEFAULT 0,
    ReservedByReservationID INT,
    Price DECIMAL(10,2),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME,
    FOREIGN KEY (ScheduleId) REFERENCES Schedules(ScheduleID),
    FOREIGN KEY (SeatId) REFERENCES Seats(SeatId),
    UNIQUE KEY UK_FlightSeat (ScheduleId, SeatId)
);

-- Create Reservations table
CREATE TABLE Reservations (
    ReservationID INT AUTO_INCREMENT PRIMARY KEY,
    UserID INT NOT NULL,
    FlightID INT,
    ScheduleID INT,
    BookingDate DATE NOT NULL,
    TravelDate DATE NOT NULL,
    Status VARCHAR(50) NOT NULL DEFAULT 'Confirmed',
    NumAdults INT NOT NULL DEFAULT 1,
    NumChildren INT NOT NULL DEFAULT 0,
    NumSeniors INT NOT NULL DEFAULT 0,
    Class VARCHAR(50) NOT NULL DEFAULT 'Economy',
    ConfirmationNumber VARCHAR(50) NOT NULL,
    BlockingNumber VARCHAR(50),
    SeatId INT,
    FlightSeatId INT,
    SeatLabel VARCHAR(10),
    FOREIGN KEY (UserID) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    FOREIGN KEY (FlightID) REFERENCES Flights(FlightID) ON DELETE RESTRICT,
    FOREIGN KEY (ScheduleID) REFERENCES Schedules(ScheduleID) ON DELETE RESTRICT,
    FOREIGN KEY (SeatId) REFERENCES Seats(SeatId),
    FOREIGN KEY (FlightSeatId) REFERENCES FlightSeats(FlightSeatId)
);

-- Create ReservationLegs table
CREATE TABLE ReservationLegs (
    ReservationLegID INT AUTO_INCREMENT PRIMARY KEY,
    ReservationID INT NOT NULL,
    FlightID INT NOT NULL,
    ScheduleID INT NOT NULL,
    TravelDate DATE NOT NULL,
    LegOrder INT NOT NULL,
    SeatId INT,
    FlightSeatId INT,
    SeatLabel VARCHAR(10),
    FOREIGN KEY (ReservationID) REFERENCES Reservations(ReservationID),
    FOREIGN KEY (FlightID) REFERENCES Flights(FlightID),
    FOREIGN KEY (ScheduleID) REFERENCES Schedules(ScheduleID),
    FOREIGN KEY (SeatId) REFERENCES Seats(SeatId),
    FOREIGN KEY (FlightSeatId) REFERENCES FlightSeats(FlightSeatId)
);

-- Create Payments table
CREATE TABLE Payments (
    PaymentID INT AUTO_INCREMENT PRIMARY KEY,
    ReservationID INT NOT NULL,
    Amount DECIMAL(10,2) NOT NULL,
    PaymentDate DATETIME NOT NULL,
    PaymentMethod VARCHAR(50) NOT NULL,
    TransactionStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    TransactionRefNo VARCHAR(100),
    FOREIGN KEY (ReservationID) REFERENCES Reservations(ReservationID)
);

-- Create Refunds table
CREATE TABLE Refunds (
    RefundID INT AUTO_INCREMENT PRIMARY KEY,
    ReservationID INT NOT NULL,
    RefundAmount DECIMAL(10,2) NOT NULL,
    RefundDate DATETIME NOT NULL,
    RefundPercentage DECIMAL(5,2) NOT NULL,
    FOREIGN KEY (ReservationID) REFERENCES Reservations(ReservationID)
);

-- Add circular FK constraint after all tables are created
ALTER TABLE FlightSeats 
ADD CONSTRAINT FK_FlightSeats_Reservations 
FOREIGN KEY (ReservedByReservationID) REFERENCES Reservations(ReservationID);

-- Insert Cities
INSERT INTO Cities (CityName, Country, AirportCode) VALUES
('Hong Kong', 'Hong Kong', 'HKG'),
('Singapore', 'Singapore', 'SIN'),
('Taipei', 'Taiwan', 'TPE'),
('Tokyo', 'Japan', 'NRT'),
('Bangkok', 'Thailand', 'BKK'),
('Manila', 'Philippines', 'MNL'),
('Seoul', 'South Korea', 'ICN'),
('Shanghai', 'China', 'PVG');

-- Get city IDs
SET @hkgId = (SELECT CityID FROM Cities WHERE AirportCode = 'HKG');
SET @sinId = (SELECT CityID FROM Cities WHERE AirportCode = 'SIN');
SET @tpeId = (SELECT CityID FROM Cities WHERE AirportCode = 'TPE');
SET @nrtId = (SELECT CityID FROM Cities WHERE AirportCode = 'NRT');
SET @bkkId = (SELECT CityID FROM Cities WHERE AirportCode = 'BKK');
SET @mnlId = (SELECT CityID FROM Cities WHERE AirportCode = 'MNL');
SET @icnId = (SELECT CityID FROM Cities WHERE AirportCode = 'ICN');
SET @pvgId = (SELECT CityID FROM Cities WHERE AirportCode = 'PVG');

-- Insert default seat layout
INSERT INTO SeatLayouts (Name, MetadataJson)
VALUES ('Standard 180-seat', NULL);

SET @defaultLayoutId = LAST_INSERT_ID();

-- Insert seats for the default layout (30 rows x 6 seats = 180 seats)
-- CabinClass: 0=First, 1=Business, 2=Economy
INSERT INTO Seats (SeatLayoutId, RowNumber, `Column`, Label, CabinClass, IsExitRow, IsPremium, PriceModifier)
SELECT @defaultLayoutId, r.RowNumber, s.SeatLetter,
    CONCAT(r.RowNumber, s.SeatLetter) as Label,
    CASE 
        WHEN r.RowNumber <= 5 THEN 1  -- Business
        ELSE 2  -- Economy
    END as CabinClass,
    0 as IsExitRow,
    0 as IsPremium,
    NULL as PriceModifier
FROM (
    SELECT 1 as RowNumber UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION
    SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9 UNION SELECT 10 UNION
    SELECT 11 UNION SELECT 12 UNION SELECT 13 UNION SELECT 14 UNION SELECT 15 UNION
    SELECT 16 UNION SELECT 17 UNION SELECT 18 UNION SELECT 19 UNION SELECT 20 UNION
    SELECT 21 UNION SELECT 22 UNION SELECT 23 UNION SELECT 24 UNION SELECT 25 UNION
    SELECT 26 UNION SELECT 27 UNION SELECT 28 UNION SELECT 29 UNION SELECT 30
) r
CROSS JOIN (
    SELECT 'A' as SeatLetter UNION SELECT 'B' UNION SELECT 'C' UNION 
    SELECT 'D' UNION SELECT 'E' UNION SELECT 'F'
) s;

-- Insert Flights
INSERT INTO Flights (FlightNumber, OriginCityID, DestinationCityID, DepartureTime, ArrivalTime, Duration, AircraftType, TotalSeats, BaseFare, SeatLayoutId)
VALUES
-- Hong Kong <-> Singapore (4 daily flights each way)
('AR101', @hkgId, @sinId, '2025-01-01 06:30:00', '2025-01-01 10:15:00', 225, 'Airbus A320', 180, 450.00, @defaultLayoutId),
('AR102', @sinId, @hkgId, '2025-01-01 11:30:00', '2025-01-01 15:15:00', 225, 'Airbus A320', 180, 450.00, @defaultLayoutId),
('AR103', @hkgId, @sinId, '2025-01-01 12:00:00', '2025-01-01 15:45:00', 225, 'Boeing 737', 180, 480.00, @defaultLayoutId),
('AR104', @sinId, @hkgId, '2025-01-01 16:30:00', '2025-01-01 20:15:00', 225, 'Boeing 737', 180, 480.00, @defaultLayoutId),
('AR105', @hkgId, @sinId, '2025-01-01 16:00:00', '2025-01-01 19:45:00', 225, 'Airbus A321', 180, 500.00, @defaultLayoutId),
('AR106', @sinId, @hkgId, '2025-01-01 20:30:00', '2025-01-01 00:15:00', 225, 'Airbus A321', 180, 500.00, @defaultLayoutId),
('AR107', @hkgId, @sinId, '2025-01-01 20:00:00', '2025-01-01 23:45:00', 225, 'Boeing 787', 180, 550.00, @defaultLayoutId),
('AR108', @sinId, @hkgId, '2025-01-01 07:00:00', '2025-01-01 10:45:00', 225, 'Boeing 787', 180, 550.00, @defaultLayoutId),

-- Hong Kong <-> Taipei (3 daily flights each way)
('AR201', @hkgId, @tpeId, '2025-01-01 08:00:00', '2025-01-01 09:50:00', 110, 'Airbus A320', 180, 280.00, @defaultLayoutId),
('AR202', @tpeId, @hkgId, '2025-01-01 10:30:00', '2025-01-01 12:20:00', 110, 'Airbus A320', 180, 280.00, @defaultLayoutId),
('AR203', @hkgId, @tpeId, '2025-01-01 14:00:00', '2025-01-01 15:50:00', 110, 'Boeing 737', 180, 300.00, @defaultLayoutId),
('AR204', @tpeId, @hkgId, '2025-01-01 16:30:00', '2025-01-01 18:20:00', 110, 'Boeing 737', 180, 300.00, @defaultLayoutId),
('AR205', @hkgId, @tpeId, '2025-01-01 19:00:00', '2025-01-01 20:50:00', 110, 'Airbus A321', 180, 320.00, @defaultLayoutId),
('AR206', @tpeId, @hkgId, '2025-01-01 21:30:00', '2025-01-01 23:20:00', 110, 'Airbus A321', 180, 320.00, @defaultLayoutId),

-- Hong Kong <-> Tokyo (2 daily flights each way)
('AR301', @hkgId, @nrtId, '2025-01-01 09:00:00', '2025-01-01 14:30:00', 330, 'Boeing 787', 180, 650.00, @defaultLayoutId),
('AR302', @nrtId, @hkgId, '2025-01-01 15:30:00', '2025-01-01 20:00:00', 270, 'Boeing 787', 180, 650.00, @defaultLayoutId),
('AR303', @hkgId, @nrtId, '2025-01-01 18:00:00', '2025-01-01 23:30:00', 330, 'Airbus A350', 180, 700.00, @defaultLayoutId),
('AR304', @nrtId, @hkgId, '2025-01-01 10:00:00', '2025-01-01 14:30:00', 270, 'Airbus A350', 180, 700.00, @defaultLayoutId),

-- Singapore <-> Bangkok (3 daily flights each way)
('AR401', @sinId, @bkkId, '2025-01-01 07:00:00', '2025-01-01 09:30:00', 150, 'Airbus A320', 180, 350.00, @defaultLayoutId),
('AR402', @bkkId, @sinId, '2025-01-01 10:30:00', '2025-01-01 13:00:00', 150, 'Airbus A320', 180, 350.00, @defaultLayoutId),
('AR403', @sinId, @bkkId, '2025-01-01 13:00:00', '2025-01-01 15:30:00', 150, 'Boeing 737', 180, 370.00, @defaultLayoutId),
('AR404', @bkkId, @sinId, '2025-01-01 16:30:00', '2025-01-01 19:00:00', 150, 'Boeing 737', 180, 370.00, @defaultLayoutId),
('AR405', @sinId, @bkkId, '2025-01-01 19:00:00', '2025-01-01 21:30:00', 150, 'Airbus A321', 180, 390.00, @defaultLayoutId),
('AR406', @bkkId, @sinId, '2025-01-01 22:30:00', '2025-01-01 01:00:00', 150, 'Airbus A321', 180, 390.00, @defaultLayoutId),

-- Singapore <-> Tokyo (2 daily flights each way)
('AR501', @sinId, @nrtId, '2025-01-01 10:00:00', '2025-01-01 17:30:00', 450, 'Boeing 787', 180, 800.00, @defaultLayoutId),
('AR502', @nrtId, @sinId, '2025-01-01 18:30:00', '2025-01-01 01:00:00', 390, 'Boeing 787', 180, 800.00, @defaultLayoutId),
('AR503', @sinId, @nrtId, '2025-01-01 22:00:00', '2025-01-01 05:30:00', 450, 'Airbus A350', 180, 850.00, @defaultLayoutId),
('AR504', @nrtId, @sinId, '2025-01-01 11:00:00', '2025-01-01 17:30:00', 390, 'Airbus A350', 180, 850.00, @defaultLayoutId),

-- Bangkok <-> Manila (2 daily flights each way)
('AR601', @bkkId, @mnlId, '2025-01-01 08:00:00', '2025-01-01 12:30:00', 270, 'Airbus A320', 180, 400.00, @defaultLayoutId),
('AR602', @mnlId, @bkkId, '2025-01-01 13:30:00', '2025-01-01 18:00:00', 270, 'Airbus A320', 180, 400.00, @defaultLayoutId),
('AR603', @bkkId, @mnlId, '2025-01-01 17:00:00', '2025-01-01 21:30:00', 270, 'Boeing 737', 180, 420.00, @defaultLayoutId),
('AR604', @mnlId, @bkkId, '2025-01-01 22:30:00', '2025-01-01 03:00:00', 270, 'Boeing 737', 180, 420.00, @defaultLayoutId),

-- Seoul <-> Tokyo (2 daily flights each way)
('AR701', @icnId, @nrtId, '2025-01-01 09:00:00', '2025-01-01 11:30:00', 150, 'Airbus A320', 180, 450.00, @defaultLayoutId),
('AR702', @nrtId, @icnId, '2025-01-01 12:30:00', '2025-01-01 15:00:00', 150, 'Airbus A320', 180, 450.00, @defaultLayoutId),
('AR703', @icnId, @nrtId, '2025-01-01 16:00:00', '2025-01-01 18:30:00', 150, 'Boeing 737', 180, 470.00, @defaultLayoutId),
('AR704', @nrtId, @icnId, '2025-01-01 19:30:00', '2025-01-01 22:00:00', 150, 'Boeing 737', 180, 470.00, @defaultLayoutId),

-- Shanghai <-> Hong Kong (3 daily flights each way)
('AR801', @pvgId, @hkgId, '2025-01-01 07:00:00', '2025-01-01 10:00:00', 180, 'Airbus A320', 180, 400.00, @defaultLayoutId),
('AR802', @hkgId, @pvgId, '2025-01-01 11:00:00', '2025-01-01 14:00:00', 180, 'Airbus A320', 180, 400.00, @defaultLayoutId),
('AR803', @pvgId, @hkgId, '2025-01-01 14:00:00', '2025-01-01 17:00:00', 180, 'Boeing 737', 180, 420.00, @defaultLayoutId),
('AR804', @hkgId, @pvgId, '2025-01-01 18:00:00', '2025-01-01 21:00:00', 180, 'Boeing 737', 180, 420.00, @defaultLayoutId),
('AR805', @pvgId, @hkgId, '2025-01-01 19:00:00', '2025-01-01 22:00:00', 180, 'Airbus A321', 180, 440.00, @defaultLayoutId),
('AR806', @hkgId, @pvgId, '2025-01-01 23:00:00', '2025-01-01 02:00:00', 180, 'Airbus A321', 180, 440.00, @defaultLayoutId),

-- Shanghai <-> Singapore (2 daily flights each way)
('AR901', @pvgId, @sinId, '2025-01-01 08:00:00', '2025-01-01 13:30:00', 330, 'Boeing 787', 180, 600.00, @defaultLayoutId),
('AR902', @sinId, @pvgId, '2025-01-01 14:30:00', '2025-01-01 20:00:00', 330, 'Boeing 787', 180, 600.00, @defaultLayoutId),
('AR903', @pvgId, @sinId, '2025-01-01 20:00:00', '2025-01-01 01:30:00', 330, 'Airbus A350', 180, 650.00, @defaultLayoutId),
('AR904', @sinId, @pvgId, '2025-01-01 09:00:00', '2025-01-01 14:30:00', 330, 'Airbus A350', 180, 650.00, @defaultLayoutId);

-- Generate Schedules (daily from Nov 28, 2025 to Dec 31, 2025 = 34 days)
DELIMITER $$

CREATE PROCEDURE GenerateSchedules()
BEGIN
    DECLARE done INT DEFAULT FALSE;
    DECLARE flight_id INT;
    DECLARE flight_cursor CURSOR FOR SELECT FlightID FROM Flights;
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;
    
    OPEN flight_cursor;
    
    read_loop: LOOP
        FETCH flight_cursor INTO flight_id;
        IF done THEN
            LEAVE read_loop;
        END IF;
        
        -- Generate daily schedules for 34 days
        INSERT INTO Schedules (FlightID, Date, Status, CityID)
        SELECT 
            flight_id,
            DATE_ADD('2025-11-28', INTERVAL n.num DAY) as Date,
            'Scheduled' as Status,
            NULL as CityID
        FROM (
            SELECT 0 as num UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION
            SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9 UNION
            SELECT 10 UNION SELECT 11 UNION SELECT 12 UNION SELECT 13 UNION SELECT 14 UNION
            SELECT 15 UNION SELECT 16 UNION SELECT 17 UNION SELECT 18 UNION SELECT 19 UNION
            SELECT 20 UNION SELECT 21 UNION SELECT 22 UNION SELECT 23 UNION SELECT 24 UNION
            SELECT 25 UNION SELECT 26 UNION SELECT 27 UNION SELECT 28 UNION SELECT 29 UNION
            SELECT 30 UNION SELECT 31 UNION SELECT 32 UNION SELECT 33
        ) n;
        
    END LOOP;
    
    CLOSE flight_cursor;
END$$

DELIMITER ;

CALL GenerateSchedules();
DROP PROCEDURE GenerateSchedules;

-- Summary
SELECT 
    CONCAT(oc.AirportCode, ' <-> ', dc.AirportCode) as Route,
    COUNT(*) as 'Flights per Day',
    COUNT(*) * 34 as 'Total Schedules'
FROM Flights f
JOIN Cities oc ON f.OriginCityID = oc.CityID
JOIN Cities dc ON f.DestinationCityID = dc.CityID
GROUP BY oc.AirportCode, dc.AirportCode
ORDER BY Route;

SELECT 
    'Total Flights' as Summary,
    COUNT(*) as Count
FROM Flights
UNION ALL
SELECT 
    'Total Schedules' as Summary,
    COUNT(*) as Count
FROM Schedules;
