-- SQL Script to sync local and live DB with Excel rates and housing status
BEGIN TRANSACTION;

-- Sync Housing for ALLEN MSIMANGA
UPDATE Employees SET LivesInCompanyHousing = 0 WHERE EmployeeNumber = '458';
-- Sync Housing for BLONDY MALEPE
UPDATE Employees SET LivesInCompanyHousing = 0 WHERE EmployeeNumber = '331';
-- Sync Housing for ELVIS NSIMBINI
UPDATE Employees SET LivesInCompanyHousing = 1 WHERE EmployeeNumber = '354';
-- Sync Housing for PATRICK MASILELA
UPDATE Employees SET LivesInCompanyHousing = 1 WHERE EmployeeNumber = '114';
-- Sync Housing for XOLANI MTSHWENI
UPDATE Employees SET LivesInCompanyHousing = 1 WHERE EmployeeNumber = '431';
-- Sync Rate for DUBE HAPPINESS
UPDATE Employees SET HourlyRate = 33.00 WHERE EmployeeNumber = '445';
-- Sync Rate for HERIS MTHOMBENI
UPDATE Employees SET HourlyRate = 30.23 WHERE EmployeeNumber = '460';
-- Sync Rate for KHASHANE FRANS MABETWA
UPDATE Employees SET HourlyRate = 30.23 WHERE EmployeeNumber = '471';
-- Sync Rate for MPHO MAKGOPO
UPDATE Employees SET HourlyRate = 30.23 WHERE EmployeeNumber = '451';
-- Sync Rate for MPHO SITHUGA
UPDATE Employees SET HourlyRate = 30.23 WHERE EmployeeNumber = '385';
-- Sync Rate for SIMBONGILE MPEPHO
UPDATE Employees SET HourlyRate = 30.23 WHERE EmployeeNumber = '492';
-- Sync Rate for SIPHO SIMILO MAZIBUKO
UPDATE Employees SET HourlyRate = 30.23 WHERE EmployeeNumber = '449';
-- Sync Rate for STUART KHOZA
UPDATE Employees SET HourlyRate = 40.00 WHERE EmployeeNumber = '450';
-- Sync Rate for TSULUFELO NCUBE
UPDATE Employees SET HourlyRate = 30.23 WHERE EmployeeNumber = '430';

COMMIT TRANSACTION;
