-- SQL Script to seed InventoryItems table with 20 sample items
-- Run this script in your SQL Server database to populate data for testing.

INSERT INTO InventoryItems (Id, Description, Supplier, Category, Location, JhbQuantity, CptQuantity, QuantityOnHand, ReorderPoint, UnitOfMeasure, Sku, AverageCost, Price, TrackLowStock, IsStockItem)
VALUES
(NEWID(), 'Portland Cement 42.5N', 'Builders Warehouse', 'Material', 'Warehouse A', 50, 30, 80, 20, 'bag', 'MAT-CEM-001', 95.50, 135.00, 1, 1),
(NEWID(), 'Red Clay Bricks (Pallet)', 'Cashbuild', 'Material', 'Yard 1', 10, 5, 15, 5, 'pallet', 'MAT-BRK-050', 1200.00, 1650.00, 1, 1),
(NEWID(), 'Safety Helmet (White)', 'Safety First', 'Safety', 'Office Store', 15, 20, 35, 10, 'ea', 'SAF-HLM-WHT', 45.00, 85.00, 1, 0),
(NEWID(), 'Safety Vest (Reflective)', 'Safety First', 'Safety', 'Office Store', 40, 40, 80, 20, 'ea', 'SAF-VST-ORG', 35.00, 65.00, 1, 0),
(NEWID(), 'Angle Grinder 115mm', 'Makro', 'Tool', 'Secure Cage', 3, 2, 5, 2, 'ea', 'TOL-AGR-115', 850.00, 1200.00, 1, 1),
(NEWID(), 'Extension Cord 20m', 'Voltex', 'Electrical', 'Warehouse B', 8, 4, 12, 5, 'ea', 'ELE-EXT-020', 250.00, 400.00, 1, 1),
(NEWID(), 'Copper Pipe 15mm (5m)', 'Plumblink', 'Plumbing', 'Rack 3', 30, 20, 50, 15, 'length', 'PLU-COP-015', 180.00, 280.00, 1, 1),
(NEWID(), 'PVC Elbow 110mm', 'Plumblink', 'Plumbing', 'Rack 3', 60, 40, 100, 30, 'ea', 'PLU-PVC-110', 45.00, 75.00, 1, 1),
(NEWID(), 'Circuit Breaker 20A', 'Voltex', 'Electrical', 'Shelf E2', 25, 15, 40, 10, 'ea', 'ELE-CBK-020', 65.00, 110.00, 1, 1),
(NEWID(), 'LED Downlight 5W', 'Voltex', 'Electrical', 'Shelf E3', 100, 50, 150, 40, 'ea', 'ELE-LED-005', 35.00, 60.00, 1, 1),
(NEWID(), 'Spade (Round Nose)', 'Builders Warehouse', 'Tool', 'Yard Store', 12, 8, 20, 5, 'ea', 'TOL-SPD-RND', 120.00, 180.00, 1, 1),
(NEWID(), 'Wheelbarrow (Heavy Duty)', 'Builders Warehouse', 'Tool', 'Yard Store', 5, 3, 8, 3, 'ea', 'TOL-WHL-HDY', 650.00, 950.00, 1, 1),
(NEWID(), 'Pine Timber 38x38 (3m)', 'Cashbuild', 'Material', 'Warehouse A', 200, 100, 300, 50, 'length', 'MAT-TIM-038', 45.00, 70.00, 1, 1),
(NEWID(), 'Plaster Primer 20L', 'Dulux', 'Paint', 'Warehouse B', 10, 5, 15, 5, 'drum', 'PNT-PRI-020', 890.00, 1350.00, 1, 1),
(NEWID(), 'Roof Paint Charcoal 20L', 'Dulux', 'Paint', 'Warehouse B', 8, 4, 12, 4, 'drum', 'PNT-ROF-CHR', 1100.00, 1600.00, 1, 1),
(NEWID(), 'Drill Bit Set (Masonry)', 'Makro', 'Tool', 'Secure Cage', 10, 10, 20, 5, 'set', 'TOL-DRL-MAS', 150.00, 250.00, 1, 1),
(NEWID(), 'Hammer 500g', 'Makro', 'Tool', 'Rack 1', 15, 10, 25, 5, 'ea', 'TOL-HAM-500', 95.00, 150.00, 1, 1),
(NEWID(), 'Tape Measure 5m', 'Makro', 'Tool', 'Rack 1', 20, 15, 35, 10, 'ea', 'TOL-TAP-005', 45.00, 80.00, 1, 0),
(NEWID(), 'Work Gloves (Leather)', 'Safety First', 'Safety', 'Office Store', 50, 30, 80, 20, 'pair', 'SAF-GLV-LTH', 30.00, 55.00, 1, 0),
(NEWID(), 'Safety Boots Size 9', 'Safety First', 'Safety', 'Office Store', 5, 5, 10, 3, 'pair', 'SAF-BOT-009', 450.00, 750.00, 1, 0);

-- Query to verify insertion
-- SELECT * FROM InventoryItems;

-- Seed Suppliers
INSERT INTO Suppliers (Id, Name, Address, City, PostalCode, Phone, ContactPerson, Email, VatNumber, BankName, BankAccountNumber, BranchCode, SupplierAccountNumber)
VALUES
(NEWID(), 'Builders Warehouse', '14 North Reef Road', 'Germiston', '1401', '011-822-1234', 'John Smith', 'orders@builders.co.za', '4010123456', 'FNB', '62012345678', '250655', 'OCC-WH-001'),
(NEWID(), 'Cashbuild', '54 Main Street', 'Johannesburg', '2001', '011-334-5678', 'Jane Doe', 'sales@cashbuild.co.za', '4050987654', 'Standard Bank', '012345678', '000205', 'OCC-CB-050'),
(NEWID(), 'Voltex', '77 Planet Avenue', 'Cape Town', '7441', '021-551-9988', 'Mike Jones', 'ct@voltex.co.za', '4090112233', 'Nedbank', '1234567890', '198765', 'OCC-VX-77'),
(NEWID(), 'Plumblink', '12 Waterway Close', 'Bellville', '7530', '021-948-4433', 'Sarah Wilson', 'bellville@plumblink.co.za', '4120334455', 'Absa', '4055667788', '632005', 'OCC-PL-12'),
(NEWID(), 'Safety First', '6 Protective Way', 'Midrand', '1685', '011-315-4400', 'Gary Guard', 'info@safetyfirst.co.za', '4150667788', 'FNB', '62112233445', '250117', 'OCC-SF-06');

-- Seed Customers
INSERT INTO Customers (Id, Name, Header, Email, Phone, Address)
VALUES
(NEWID(), 'Standard Bank Ltd', 'Banking Sector', 'infrastructure@standardbank.co.za', '011-636-9111', '5 Simmonds St, Johannesburg, 2001'),
(NEWID(), 'Growthpoint Properties', 'Real Estate', 'facilities@growthpoint.co.za', '011-944-6000', 'The Place, 1 Sandton Dr, Sandton, 2196'),
(NEWID(), 'Redefine Properties', 'Real Estate', 'projects@redefine.co.za', '011-283-0000', 'Rosebank Towers, 15 Biermann Ave, Rosebank, 2196'),
(NEWID(), 'Old Mutual', 'Insurance', 'maintenance@oldmutual.com', '021-509-9111', 'Mutualpark, Jan Smuts Dr, Pinelands, 7405');

-- Seed Employees
INSERT INTO Employees (Id, FirstName, LastName, EmployeeNumber, Role, RateType, HourlyRate, EmploymentType, EmploymentDate, Branch, Status, Email, Phone, IdNumber, IdType, DoB, TaxNumber, AnnualLeaveBalance, SickLeaveBalance, LeaveBalance, ShiftStartTime, ShiftEndTime)
VALUES
(NEWID(), 'Neil', 'Ketting', 'EMP001', 21, 1, 0, 0, GETDATE(), 'Johannesburg', 0, 'neil@occ.co.za', '082-123-4455', '8501015001081', 0, '1985-01-01', '1234567890', 15.0, 30.0, 15.0, '07:00:00', '16:45:00'),
(NEWID(), 'Helga', 'Ketting', 'EMP002', 0, 1, 0, 0, GETDATE(), 'Johannesburg', 0, 'helga@occ.co.za', '083-456-7788', '8801015001082', 0, '1988-01-01', '0987654321', 15.0, 30.0, 15.0, '08:00:00', '17:00:00'),
(NEWID(), 'Barend', 'Botha', 'EMP003', 18, 0, 150.0, 0, GETDATE(), 'Johannesburg', 0, 'barend@occ.co.za', '071-112-2334', '7505055001083', 0, '1975-05-05', '1122334455', 12.0, 20.0, 12.0, '07:00:00', '16:00:00'),
(NEWID(), 'Sipho', 'Zulu', 'EMP004', 1, 0, 65.0, 0, GETDATE(), 'Johannesburg', 0, 'sipho@occ.co.za', '061-998-8776', '9208155001084', 0, '1992-08-15', '2233445566', 10.0, 15.0, 10.0, '07:00:00', '16:00:00'),
(NEWID(), 'Petrus', 'Mokoena', 'EMP005', 2, 0, 85.0, 1, GETDATE(), 'Cape Town', 0, 'petrus@occ.co.za', '072-555-6677', '8004205001085', 0, '1980-04-20', '3344556677', 5.0, 10.0, 5.0, '07:30:00', '17:00:00');
