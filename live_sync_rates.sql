-- SQL Script to update live database employee hourly rates to 10 JUL 2026 spreadsheet values
BEGIN TRANSACTION;

-- Update employee hourly rates based on Copy of G. JHB 10 JUL 26 (003).xlsx
UPDATE Employees SET HourlyRate = 34.98 WHERE EmployeeNumber = '444'; -- AARON MOSELANE
UPDATE Employees SET HourlyRate = 35.95 WHERE EmployeeNumber = '458'; -- ALLEN MSIMANGA
UPDATE Employees SET HourlyRate = 42.40 WHERE EmployeeNumber = '459'; -- ANDREW  MASILELA
UPDATE Employees SET HourlyRate = 42.40 WHERE EmployeeNumber = '483'; -- AUBREY MATHEBULA
UPDATE Employees SET HourlyRate = 47.10 WHERE EmployeeNumber = '398'; -- BENEDICTOR PHOSWA
UPDATE Employees SET HourlyRate = 40.45 WHERE EmployeeNumber = '331'; -- BLONDY MALEPE
UPDATE Employees SET HourlyRate = 40.45 WHERE EmployeeNumber = '122'; -- COSTER  MALEPE
UPDATE Employees SET HourlyRate = 36.59 WHERE EmployeeNumber = '332'; -- DANGER  MAWALELA
UPDATE Employees SET HourlyRate = 42.40 WHERE EmployeeNumber = '421'; -- DONALD  JIYA
UPDATE Employees SET HourlyRate = 34.98 WHERE EmployeeNumber = '445'; -- DUBE HAPPINESS
UPDATE Employees SET HourlyRate = 47.40 WHERE EmployeeNumber = '119'; -- DUBE KUDAKWASHE
UPDATE Employees SET HourlyRate = 37.43 WHERE EmployeeNumber = '383'; -- DUMISANI  MASANGO
UPDATE Employees SET HourlyRate = 42.40 WHERE EmployeeNumber = '354'; -- ELVIS  NSIMBINI
UPDATE Employees SET HourlyRate = 47.64 WHERE EmployeeNumber = '109'; -- GIBBS MASETE
UPDATE Employees SET HourlyRate = 32.04 WHERE EmployeeNumber = '460'; -- HERIS  MTHOMBENI
UPDATE Employees SET HourlyRate = 37.43 WHERE EmployeeNumber = '399'; -- HERMAN  NGIDI
UPDATE Employees SET HourlyRate = 42.40 WHERE EmployeeNumber = '389'; -- JACK  MOICHELO
UPDATE Employees SET HourlyRate = 34.98 WHERE EmployeeNumber = '439'; -- JANUARY  SITOE
UPDATE Employees SET HourlyRate = 42.40 WHERE EmployeeNumber = '461'; -- JOHANNES (BAMPI) MTHOMBENI
UPDATE Employees SET HourlyRate = 41.10 WHERE EmployeeNumber = '443'; -- JOHANNES SEGAFA
UPDATE Employees SET HourlyRate = 32.04 WHERE EmployeeNumber = '471'; -- KGASHANE (FRANS)  MABETWA
UPDATE Employees SET HourlyRate = 33.30 WHERE EmployeeNumber = '447'; -- LIVINGSTONE  MALONGANE
UPDATE Employees SET HourlyRate = 44.52 WHERE EmployeeNumber = '334'; -- LUCKY  MAKUBULE
UPDATE Employees SET HourlyRate = 42.40 WHERE EmployeeNumber = '462'; -- LUCKY PYUNGU
UPDATE Employees SET HourlyRate = 33.30 WHERE EmployeeNumber = '108'; -- MAECA MAHLANGU
UPDATE Employees SET HourlyRate = 40.45 WHERE EmployeeNumber = '125'; -- MANDLA NDLOVU
UPDATE Employees SET HourlyRate = 33.30 WHERE EmployeeNumber = '335'; -- MBONGENI  SOKUDELA
UPDATE Employees SET HourlyRate = 37.83 WHERE EmployeeNumber = '463'; -- MORRIES  MALEPE
UPDATE Employees SET HourlyRate = 32.04 WHERE EmployeeNumber = '451'; -- MPHO MAKGOPO
UPDATE Employees SET HourlyRate = 32.04 WHERE EmployeeNumber = '385'; -- MPHO SITHUGA
UPDATE Employees SET HourlyRate = 42.40 WHERE EmployeeNumber = '453'; -- MSIZI  MKHALIPHI
UPDATE Employees SET HourlyRate = 33.30 WHERE EmployeeNumber = '433'; -- NDIKHO SOKUDELA
UPDATE Employees SET HourlyRate = 42.40 WHERE EmployeeNumber = '448'; -- NELSON (MATOME)  SEKGOPO
UPDATE Employees SET HourlyRate = 40.45 WHERE EmployeeNumber = '116'; -- NKOSINATHI  KHUMALO
UPDATE Employees SET HourlyRate = 34.73 WHERE EmployeeNumber = '423'; -- NTOBEKO GABHILE
UPDATE Employees SET HourlyRate = 34.73 WHERE EmployeeNumber = '424'; -- OUPA MPHOTU
UPDATE Employees SET HourlyRate = 42.40 WHERE EmployeeNumber = '114'; -- PATRICK (MOOIMAN ALFRED) MASILELA
UPDATE Employees SET HourlyRate = 44.94 WHERE EmployeeNumber = '340'; -- PEACE  MAKHOKHA
UPDATE Employees SET HourlyRate = 35.95 WHERE EmployeeNumber = '338'; -- PETROS SHITHLANGU
UPDATE Employees SET HourlyRate = 41.08 WHERE EmployeeNumber = '341'; -- PHILISANI NDLOVU
UPDATE Employees SET HourlyRate = 33.30 WHERE EmployeeNumber = '218'; -- ROVER BULUNGA
UPDATE Employees SET HourlyRate = 42.40 WHERE EmployeeNumber = '464'; -- SANYANE BUDHA
UPDATE Employees SET HourlyRate = 34.98 WHERE EmployeeNumber = '435'; -- SFISO  MAHLANGU
UPDATE Employees SET HourlyRate = 44.94 WHERE EmployeeNumber = '434'; -- SHERIFF MASELANE
UPDATE Employees SET HourlyRate = 32.04 WHERE EmployeeNumber = '492'; -- SIMBONGILE  MPHEPO
UPDATE Employees SET HourlyRate = 32.04 WHERE EmployeeNumber = '449'; -- SIMILO MAZIBUKO
UPDATE Employees SET HourlyRate = 33.30 WHERE EmployeeNumber = '405'; -- SIPHIWE MASHITENG
UPDATE Employees SET HourlyRate = 37.10 WHERE EmployeeNumber = '126'; -- SIX MATEPSA
UPDATE Employees SET HourlyRate = 42.40 WHERE EmployeeNumber = '489'; -- STUART  KHOZA
UPDATE Employees SET HourlyRate = 34.83 WHERE EmployeeNumber = '450'; -- TAKALANI NYONI
UPDATE Employees SET HourlyRate = 34.83 WHERE EmployeeNumber = '476'; -- THOKOZANI  SIBANYONI
UPDATE Employees SET HourlyRate = 38.16 WHERE EmployeeNumber = '207'; -- THUSO MULELU
UPDATE Employees SET HourlyRate = 32.04 WHERE EmployeeNumber = '430'; -- TSULUFELO NCUBE
UPDATE Employees SET HourlyRate = 34.68 WHERE EmployeeNumber = '431'; -- XOLANI  MTSHWENI
UPDATE Employees SET HourlyRate = 34.83 WHERE EmployeeNumber = '428'; -- ZAZI NDLOVU
UPDATE Employees SET HourlyRate = 33.30 WHERE EmployeeNumber = '339'; -- ZONGEZILE NYEMBEZI

COMMIT TRANSACTION;
