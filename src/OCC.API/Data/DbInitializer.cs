using OCC.API.Data;
using OCC.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace OCC.API.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context, OCC.API.Services.PasswordHasher hasher, bool isDevelopment, ILogger logger)
        {
            // Prepare DB (Apply Migrations)
            logger.LogInformation("Checking for pending migrations...");
            context.Database.Migrate();

            // Standardize numeric column types in local SQL Server database to FLOAT to match C# double models 1-to-1
            try
            {
                logger.LogInformation("Standardizing SQL column types for Employees, AttendanceRecords, and WageRunLines...");
                context.Database.ExecuteSqlRaw(@"
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'HourlyRate')
                        ALTER TABLE Employees ALTER COLUMN HourlyRate FLOAT NOT NULL;
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'AnnualLeaveBalance')
                        ALTER TABLE Employees ALTER COLUMN AnnualLeaveBalance FLOAT NOT NULL;
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'SickLeaveBalance')
                        ALTER TABLE Employees ALTER COLUMN SickLeaveBalance FLOAT NOT NULL;
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'LeaveBalance')
                        ALTER TABLE Employees ALTER COLUMN LeaveBalance FLOAT NOT NULL;

                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'HoursWorked')
                        ALTER TABLE AttendanceRecords ALTER COLUMN HoursWorked FLOAT NOT NULL;
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'CachedHourlyRate')
                        ALTER TABLE AttendanceRecords ALTER COLUMN CachedHourlyRate FLOAT NULL;
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'PaidLeaveHours')
                        ALTER TABLE AttendanceRecords ALTER COLUMN PaidLeaveHours FLOAT NOT NULL;
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AttendanceRecords') AND name = 'UnpaidLeaveHours')
                        ALTER TABLE AttendanceRecords ALTER COLUMN UnpaidLeaveHours FLOAT NOT NULL;

                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WageRunLines') AND name = 'DaysWorkedWeek1')
                        ALTER TABLE WageRunLines ALTER COLUMN DaysWorkedWeek1 FLOAT NOT NULL;
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WageRunLines') AND name = 'DaysWorkedWeek2')
                        ALTER TABLE WageRunLines ALTER COLUMN DaysWorkedWeek2 FLOAT NOT NULL;
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WageRunLines') AND name = 'DaysWorkedWeek3')
                        ALTER TABLE WageRunLines ALTER COLUMN DaysWorkedWeek3 FLOAT NOT NULL;
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WageRunLines') AND name = 'TotalDaysWorked')
                        ALTER TABLE WageRunLines ALTER COLUMN TotalDaysWorked FLOAT NOT NULL;
                ");
            }
            catch (Exception ex)
            {
                logger.LogWarning("Schema column standardization skipped or failed: {Message}", ex.Message);
            }

            // Manual Schema Patch: Ensure new ProjectId columns exist (since EF migrations tool failed)
            try
            {
                logger.LogInformation("Applying manual schema patches for HSEQ and Incidents...");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('HseqAudits') AND name = 'ProjectId') ALTER TABLE HseqAudits ADD ProjectId UNIQUEIDENTIFIER NULL;");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Incidents') AND name = 'ProjectId') ALTER TABLE Incidents ADD ProjectId UNIQUEIDENTIFIER NULL;");
            }
            catch (Exception ex)
            {
                logger.LogWarning("Manual schema patch skipped or failed: {Message}", ex.Message);
            }

            // Manual Schema Patch: Ensure new WageRunLines columns exist (since EF migrations tool failed)
            try
            {
                logger.LogInformation("Applying manual schema patches for WageRunLines missing columns...");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WageRunLines') AND name = 'BankName') ALTER TABLE WageRunLines ADD BankName NVARCHAR(MAX) NULL;");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WageRunLines') AND name = 'DaysWorkedWeek1') ALTER TABLE WageRunLines ADD DaysWorkedWeek1 FLOAT NOT NULL DEFAULT 0;");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WageRunLines') AND name = 'DaysWorkedWeek2') ALTER TABLE WageRunLines ADD DaysWorkedWeek2 FLOAT NOT NULL DEFAULT 0;");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WageRunLines') AND name = 'DeductionPPE') ALTER TABLE WageRunLines ADD DeductionPPE DECIMAL(18,2) NOT NULL DEFAULT 0;");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WageRunLines') AND name = 'EmploymentType') ALTER TABLE WageRunLines ADD EmploymentType NVARCHAR(MAX) NOT NULL DEFAULT '';");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WageRunLines') AND name = 'TotalDaysWorked') ALTER TABLE WageRunLines ADD TotalDaysWorked FLOAT NOT NULL DEFAULT 0;");
            }
            catch (Exception ex)
            {
                logger.LogWarning("Manual schema patch for WageRunLines columns skipped or failed: {Message}", ex.Message);
            }

            // Manual Schema Patch: Ensure SupplierContacts table exists
            try
            {
                logger.LogInformation("Applying manual schema patch for SupplierContacts table...");
                context.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SupplierContacts]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[SupplierContacts] (
                            [Id] UNIQUEIDENTIFIER NOT NULL,
                            [SupplierId] UNIQUEIDENTIFIER NOT NULL,
                            [ContactName] NVARCHAR(MAX) NOT NULL DEFAULT '',
                            [Email] NVARCHAR(MAX) NOT NULL DEFAULT '',
                            [Phone] NVARCHAR(MAX) NOT NULL DEFAULT '',
                            [Department] NVARCHAR(MAX) NOT NULL DEFAULT '',
                            [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                            [CreatedBy] NVARCHAR(MAX) NOT NULL DEFAULT 'System',
                            [UpdatedAtUtc] DATETIME2 NULL,
                            [UpdatedBy] NVARCHAR(MAX) NULL,
                            [IsActive] BIT NOT NULL DEFAULT 1,
                            [RowVersion] VARBINARY(MAX) NULL,
                            CONSTRAINT [PK_SupplierContacts] PRIMARY KEY CLUSTERED ([Id] ASC),
                            CONSTRAINT [FK_SupplierContacts_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Suppliers] ([Id]) ON DELETE CASCADE
                        );
                        CREATE INDEX [IX_SupplierContacts_SupplierId] ON [dbo].[SupplierContacts] ([SupplierId]);
                    END");
            }
            catch (Exception ex)
            {
                logger.LogWarning("Manual schema patch for SupplierContacts table skipped or failed: {Message}", ex.Message);
            }

            // Manual Schema Patch: Ensure WageSettings table and Overhaul columns exist
            try
            {
                logger.LogInformation("Applying manual schema patch for WageSettings table and overhaul columns...");
                context.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WageSettings]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[WageSettings] (
                            [Id] UNIQUEIDENTIFIER NOT NULL,
                            [CptDefaultPayFrequency] INT NOT NULL DEFAULT 0,
                            [JhbDefaultPayFrequency] INT NOT NULL DEFAULT 1,
                            [WeeklyShiftCutoffDay] INT NOT NULL DEFAULT 3,
                            [BibcRatePerDay] DECIMAL(18,2) NOT NULL DEFAULT 28.75,
                            [DefaultSupervisorFee] DECIMAL(18,2) NOT NULL DEFAULT 500.00,
                            [DefaultCompanyHousingWashingFee] DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                            [DefaultShiftStartTime] TIME(7) NOT NULL DEFAULT '07:00:00',
                            [DefaultShiftEndTime] TIME(7) NOT NULL DEFAULT '17:00:00',
                            [LunchEndHourThreshold] INT NOT NULL DEFAULT 13,
                            [DeductLunchOnSaturday] BIT NOT NULL DEFAULT 0,
                            [DeductLunchOnSunday] BIT NOT NULL DEFAULT 0,
                            [DeductLunchOnPublicHoliday] BIT NOT NULL DEFAULT 1,
                            [EnableProjectedHours] BIT NOT NULL DEFAULT 1,
                            [AutoRecoverAdHocAdvances] BIT NOT NULL DEFAULT 1,
                            [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                            [CreatedBy] NVARCHAR(MAX) NOT NULL DEFAULT 'System',
                            [UpdatedAtUtc] DATETIME2 NULL,
                            [UpdatedBy] NVARCHAR(MAX) NULL,
                            [IsActive] BIT NOT NULL DEFAULT 1,
                            [RowVersion] VARBINARY(MAX) NULL,
                            CONSTRAINT [PK_WageSettings] PRIMARY KEY CLUSTERED ([Id] ASC)
                        );
                    END

                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'WageSettings' AND COLUMN_NAME = 'DefaultShiftStartTime' AND DATA_TYPE <> 'time')
                    BEGIN
                        DECLARE @Constraint1 NVARCHAR(200);
                        SELECT @Constraint1 = d.name
                        FROM sys.default_constraints d
                        JOIN sys.columns c ON d.parent_column_id = c.column_id AND d.parent_object_id = c.object_id
                        WHERE c.object_id = OBJECT_ID('WageSettings') AND c.name = 'DefaultShiftStartTime';

                        IF @Constraint1 IS NOT NULL
                            EXEC('ALTER TABLE [WageSettings] DROP CONSTRAINT [' + @Constraint1 + '];');

                        DECLARE @Constraint2 NVARCHAR(200);
                        SELECT @Constraint2 = d.name
                        FROM sys.default_constraints d
                        JOIN sys.columns c ON d.parent_column_id = c.column_id AND d.parent_object_id = c.object_id
                        WHERE c.object_id = OBJECT_ID('WageSettings') AND c.name = 'DefaultShiftEndTime';

                        IF @Constraint2 IS NOT NULL
                            EXEC('ALTER TABLE [WageSettings] DROP CONSTRAINT [' + @Constraint2 + '];');

                        ALTER TABLE WageSettings ALTER COLUMN DefaultShiftStartTime TIME(7) NOT NULL;
                        ALTER TABLE WageSettings ALTER COLUMN DefaultShiftEndTime TIME(7) NOT NULL;

                        ALTER TABLE WageSettings ADD CONSTRAINT DF_WageSettings_DefaultShiftStartTime DEFAULT '07:00:00' FOR DefaultShiftStartTime;
                        ALTER TABLE WageSettings ADD CONSTRAINT DF_WageSettings_DefaultShiftEndTime DEFAULT '17:00:00' FOR DefaultShiftEndTime;
                    END");

                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WageRuns') AND name = 'RunType') ALTER TABLE WageRuns ADD RunType INT NOT NULL DEFAULT 0;");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WageRuns') AND name = 'PayFrequency') ALTER TABLE WageRuns ADD PayFrequency INT NOT NULL DEFAULT 0;");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WageRunLines') AND name = 'DeductionAdvanceRecovery') ALTER TABLE WageRunLines ADD DeductionAdvanceRecovery DECIMAL(18,2) NOT NULL DEFAULT 0;");
            }
            catch (Exception ex)
            {
                logger.LogWarning("Manual schema patch for WageSettings skipped or failed: {Message}", ex.Message);
            }

            // Standardize task IsGroup data integrity for existing/legacy database records
            try
            {
                logger.LogInformation("Standardizing task IsGroup data integrity...");
                var tasksToSetGroup = context.ProjectTasks
                    .Where(t => !t.IsGroup && context.ProjectTasks.Any(child => child.ParentId == t.Id))
                    .ToList();
                
                foreach (var t in tasksToSetGroup)
                {
                    t.IsGroup = true;
                    logger.LogInformation("Data Integrity Standardizer: Set IsGroup = true for task '{Name}' ({Id})", t.Name, t.Id);
                }

                if (tasksToSetGroup.Any())
                {
                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Failed to standardize task IsGroup data integrity: {Message}", ex.Message);
            }

            // Cleanup corrupt/blank tasks inserted by the legacy update-project-status bug
            try
            {
                logger.LogInformation("Cleaning up corrupt tasks (NULL or empty names)...");
                int deletedCount = context.Database.ExecuteSqlRaw("DELETE FROM ProjectTasks WHERE Name IS NULL OR Name = '';");
                if (deletedCount > 0)
                {
                    logger.LogInformation("Cleaned up {Count} corrupt tasks with NULL or empty names.", deletedCount);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Failed to clean up corrupt tasks: {Message}", ex.Message);
            }

            var adminEmail = "neil@mdk.co.za";
            var adminUser = context.Users.FirstOrDefault(u => u.Email == adminEmail);

            if (adminUser == null)
            {
                logger.LogInformation("Seeding default admin user...");
                adminUser = new User
                {
                    Email = adminEmail,
                    Password = hasher.HashPassword("pass"),
                    FirstName = "Neil",
                    LastName = "Ketting",
                    UserRole = UserRole.Admin,
                    IsApproved = true,
                    IsEmailVerified = true
                };
                context.Users.Add(adminUser);
                context.SaveChanges();
            }
            else
            {
                logger.LogInformation("Updating default admin user password to 'pass'...");
                adminUser.Password = hasher.HashPassword("pass");
                context.SaveChanges();
            }

            var adminEmployee = context.Employees.FirstOrDefault(e => e.Email == adminEmail);
            if (adminEmployee != null && adminEmployee.LinkedUserId != adminUser.Id)
            {
                adminEmployee.LinkedUserId = adminUser.Id;
                context.SaveChanges();
            }

            if (isDevelopment)
            {
                logger.LogInformation("Environment is Development. Starting comprehensive seeding...");
                SeedEmployees(context, logger);
                SeedAttendance(context, logger);
                PatchSubContractorNames(context, logger); // Ensure correct naming for OCC branches
                SeedProjects(context, logger);
                SeedTasks(context, logger);
            }
            else
            {
                logger.LogInformation("Skipped: Not in Development Environment.");
                // Even in prod, we might want to ensure these names are correct if they exist
                PatchSubContractorNames(context, logger);
            }
            SeedNoticeBoard(context, logger);
        }

        private static void PatchSubContractorNames(AppDbContext context, ILogger logger)
        {
            var subs = context.SubContractors.ToList();
            bool changed = false;

            foreach (var sub in subs)
            {
                // Fix generic "Circle Construction" or variations to the requested full names
                if (sub.Name.Contains("Circle Construction", StringComparison.OrdinalIgnoreCase) && !sub.Name.StartsWith("Orange", StringComparison.OrdinalIgnoreCase))
                {
                    var oldName = sub.Name;
                    if (sub.Branch.Equals("Johannesburg", StringComparison.OrdinalIgnoreCase) || sub.Name.Contains("Jhb", StringComparison.OrdinalIgnoreCase))
                    {
                        sub.Name = "Orange Circle Construction JHB";
                    }
                    else if (sub.Branch.Equals("Cape Town", StringComparison.OrdinalIgnoreCase) || sub.Name.Contains("Cpt", StringComparison.OrdinalIgnoreCase))
                    {
                        sub.Name = "Orange Circle Construction CPT";
                    }
                    else
                    {
                        sub.Name = "Orange Circle Construction";
                    }
                    
                    logger.LogInformation("Patched SubContractor name from '{Old}' to '{New}'", oldName, sub.Name);
                    changed = true;
                }
            }

            if (changed)
            {
                context.SaveChanges();
            }
        }

        private static void SeedTasks(AppDbContext context, ILogger logger)
        {
            if (context.ProjectTasks.Any())
            {
                logger.LogInformation("Tasks already exist. Skipping Task Seed.");
                return;
            }

            logger.LogInformation("Seeding Project Tasks...");
            var projects = context.Projects.Include(p => p.TeamMembers).ToList();
            var employees = context.Employees.ToList();
            
            // Only assign to roles that the UI actually shows in the resource list (Managers/Foremen)
            var managementStaff = employees.Where(e => 
                e.Role == EmployeeRole.SiteManager || 
                e.Role == EmployeeRole.SnrForeman || 
                e.Role == EmployeeRole.JnrForeman || 
                e.Role == EmployeeRole.LegacySeniorForeman ||
                e.Role == EmployeeRole.Supervisor).ToList();

            if (!projects.Any() || !managementStaff.Any())
            {
                logger.LogWarning("Cannot seed tasks: Projects or Management Staff missing.");
                return;
            }

            var random = new Random();
            var taskNames = new[] { "Site Clearance", "Excavation", "Foundation Pouring", "Brickwork Level 1", "Electrical First Fix", "Plumbing Rough-in", "Roof Truss Installation", "Window Fitting", "Plastering", "Floor Screeding" };
            
            int taskIndex = 0;
            foreach (var employee in managementStaff)
            {
                var project = projects[random.Next(projects.Count)];
                var name = taskNames[taskIndex % taskNames.Length];
                taskIndex++;

                // Sync task progress with project status
                bool isProjectCompleted = project.Status == "Completed";

                var task = new ProjectTask
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    Name = $"{name} ({employee.FirstName})",
                    Description = $"Assigned task for {employee.FirstName}",
                    AssignedTo = $"{employee.FirstName} {employee.LastName}",
                    StartDate = DateTime.UtcNow.AddDays(-5),
                    FinishDate = DateTime.UtcNow.AddDays(5),
                    Status = isProjectCompleted ? "Completed" : "Started",
                    PercentComplete = isProjectCompleted ? 100 : 10,
                    Priority = "Medium",
                    Type = TaskType.Task,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = "System"
                };
                context.ProjectTasks.Add(task);

                // Ensure employee is also in the project team
                if (!project.TeamMembers.Any(tm => tm.EmployeeId == employee.Id))
                {
                    context.ProjectTeamMembers.Add(new ProjectTeamMember
                    {
                        ProjectId = project.Id,
                        EmployeeId = employee.Id,
                        DateAdded = DateTime.UtcNow
                    });
                }

                context.TaskAssignments.Add(new TaskAssignment
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    AssigneeId = employee.Id,
                    AssigneeName = $"{employee.FirstName} {employee.LastName}",
                    AssigneeType = AssigneeType.Staff
                });
            }

            context.SaveChanges();
            logger.LogInformation("Project Tasks seeded successfully.");
        }

        private static void SeedEmployees(AppDbContext context, ILogger logger)
        {
            int currentCount = context.Employees.Count();
            if (currentCount >= 20)
            {
                logger.LogInformation("Sufficient employees exist ({Count}). Skipping Employee Seed.", currentCount);
                return;
            }

            var random = new Random();
            var firstNames = new[] { "John", "Jane", "Mike", "Sarah", "David", "Emma", "Chris", "Lisa", "Tom", "Anna", "Robert", "Emily", "James", "Olivia", "Peter", "Grace", "Daniel", "Chloe", "Paul", "Mia" };
            var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin" };
            var roles = (EmployeeRole[])Enum.GetValues(typeof(EmployeeRole));
            
            int toAdd = 20 - currentCount;
            logger.LogInformation("Adding {Count} dummy employees...", toAdd);

            for (int i = 0; i < toAdd; i++)
            {
                var fn = firstNames[random.Next(firstNames.Length)];
                var ln = lastNames[random.Next(lastNames.Length)];
                var role = roles[random.Next(roles.Length)];
                
                var emp = new Employee
                {
                    FirstName = fn,
                    LastName = ln,
                    EmployeeNumber = $"EMP{currentCount + i + 100:000}",
                    IdNumber = $"{random.Next(100000, 999999)}{random.Next(1000, 9999)}08{random.Next(1, 9)}",
                    Email = $"{fn.ToLower()}.{ln.ToLower()}{random.Next(1,99)}@example.com",
                    Phone = $"08{random.Next(10000000, 99999999)}",
                    Role = role,
                    HourlyRate = random.Next(25, 150),
                    Branch = random.NextDouble() > 0.5 ? "Johannesburg" : "Cape Town",
                    EmploymentType = EmploymentType.Permanent,
                    ShiftStartTime = new TimeSpan(7, 0, 0),
                    ShiftEndTime = new TimeSpan(16, 45, 0)
                };
                context.Employees.Add(emp);
            }
            context.SaveChanges();
            logger.LogInformation("Employees seeded successfully.");
        }

        private static void SeedAttendance(AppDbContext context, ILogger logger)
        {
            var employees = context.Employees.ToList();
            if (!employees.Any()) return;

            var existingCount = context.AttendanceRecords.Count();
            if (existingCount > 100)
            {
                 logger.LogInformation("Attendance records already exist ({Count}). skipping.", existingCount);
                 return;
            }

            logger.LogInformation("Seeding Attendance History...");
            var existingKeys = context.AttendanceRecords
                .Select(a => new { a.EmployeeId, Date = a.Date })
                .AsEnumerable()
                .Select(x => $"{x.EmployeeId}|{x.Date.Date:yyyyMMdd}")
                .ToHashSet();

            var random = new Random();
            var startDate = DateTime.Today.AddDays(-60);
            var endDate = DateTime.Today;
            int recordsAdded = 0;

            foreach (var emp in employees)
            {
                if (random.NextDouble() > 0.95) continue; 

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) continue;
                    if (existingKeys.Contains($"{emp.Id}|{date:yyyyMMdd}")) continue;
                    if (random.NextDouble() > 0.90) continue;

                    var shiftStart = emp.ShiftStartTime ?? new TimeSpan(7, 0, 0);
                    var shiftEnd = emp.ShiftEndTime ?? new TimeSpan(16, 45, 0);
                    TimeSpan arrival = random.NextDouble() > 0.2 ? shiftStart.Subtract(TimeSpan.FromMinutes(random.Next(0, 15))) : shiftStart.Add(TimeSpan.FromMinutes(random.Next(5, 45)));
                    TimeSpan departure = random.NextDouble() > 0.1 ? shiftEnd.Add(TimeSpan.FromMinutes(random.Next(0, 30))) : shiftEnd.Subtract(TimeSpan.FromMinutes(random.Next(15, 60)));

                    var checkIn = date.Add(arrival);
                    var checkOut = date.Add(departure);
                    var duration = Math.Max(0, (checkOut - checkIn).TotalHours);

                    context.AttendanceRecords.Add(new AttendanceRecord
                    {
                        EmployeeId = emp.Id,
                        Date = date,
                        CheckInTime = checkIn,
                        CheckOutTime = checkOut,
                        ClockInTime = arrival,
                        Status = (arrival > shiftStart) ? AttendanceStatus.Late : AttendanceStatus.Present,
                        Branch = emp.Branch,
                        CachedHourlyRate = emp.HourlyRate,
                        HoursWorked = duration
                    });
                    recordsAdded++;
                }
            }
            
            if (recordsAdded > 0)
            {
                context.SaveChanges();
                logger.LogInformation("Added {Count} attendance records.", recordsAdded);
            }
        }

        private static void SeedProjects(AppDbContext context, ILogger logger)
        {
            if (context.Projects.Any()) 
            {
                logger.LogInformation("Projects already exist. Skipping project seed.");
                return;
            }

            SeedCustomers(context, logger);
            SeedSuppliers(context, logger);

            var customer = context.Customers.First();
            var employees = context.Employees.ToList();
            var managementPool = employees.Where(e => e.Role == EmployeeRole.SiteManager || e.Role == EmployeeRole.SnrForeman).ToList();
            var random = new Random();

            var projects = new List<Project>
            {
                new Project 
                { 
                    Name = "Engen Bendor", 
                    Description = "Fuel station renovation", 
                    StartDate = DateTime.Today.AddDays(-30), 
                    EndDate = DateTime.Today.AddDays(60), 
                    CustomerId = customer.Id, 
                    Status = "Active", 
                    Priority = "High", 
                    StreetLine1 = "123 Bendor Drive", 
                    City = "Polokwane", 
                    Country = "South Africa",
                    ProjectManager = "Neil Admin",
                    SiteManagerId = managementPool.Any() ? managementPool[0].Id : null
                },
                new Project 
                { 
                    Name = "Mall of North", 
                    Description = "Expansion project", 
                    StartDate = DateTime.Today.AddDays(-10), 
                    EndDate = DateTime.Today.AddDays(180), 
                    CustomerId = customer.Id, 
                    Status = "Active", 
                    Priority = "Medium", 
                    StreetLine1 = "456 Mall St", 
                    City = "Polokwane", 
                    Country = "South Africa",
                    ProjectManager = "Neil Admin",
                    SiteManagerId = managementPool.Count > 1 ? managementPool[1].Id : (managementPool.Any() ? managementPool[0].Id : null)
                },
                new Project 
                { 
                    Name = "Savannah Office", 
                    Description = "New office complex", 
                    StartDate = DateTime.Today.AddDays(-60), 
                    EndDate = DateTime.Today.AddDays(-5), 
                    CustomerId = customer.Id, 
                    Status = "Completed", 
                    Priority = "Low", 
                    StreetLine1 = "789 Savannah Rd", 
                    City = "Polokwane", 
                    Country = "South Africa",
                    ProjectManager = "Office Team",
                    SiteManagerId = managementPool.Any() ? managementPool[random.Next(managementPool.Count)].Id : null
                }
            };

            foreach(var p in projects) context.Projects.Add(p);
            context.SaveChanges();
            logger.LogInformation("Projects seeded successfully.");
            SeedInventory(context, logger);
        }

        private static void SeedCustomers(AppDbContext context, ILogger logger)
        {
            if (context.Customers.Any()) return;
            context.Customers.Add(new Customer { Name = "Total Energies", Header = "TotalEnergies", Email = "contact@total.com", Phone = "0112223333", Address = "Johannesburg, SA" });
            context.Customers.Add(new Customer { Name = "Standard Bank", Header = "StandardBank", Email = "procure@standardbank.co.za", Phone = "0114445555", Address = "Simmonds St, JHB" });
            context.SaveChanges();
            logger.LogInformation("Customers seeded.");
        }

        private static void SeedSuppliers(AppDbContext context, ILogger logger)
        {
            if (context.Suppliers.Any()) return;
            context.Suppliers.Add(new Supplier { Name = "BuildIt", Address = "123 Build St", City = "Polokwane", PostalCode = "0700", Phone = "0151112222", Email = "sales@buildit.co.za", ContactPerson = "Builders", BranchCode = "001", SupplierAccountNumber = "ACC001", BankName = "FNB", BankAccountNumber = "123456789", VatNumber = "1234567890" });
            context.Suppliers.Add(new Supplier { Name = "PPC Cement", Address = "456 PPC Way", City = "Johannesburg", PostalCode = "2000", Phone = "0113334444", Email = "orders@ppc.co.za", ContactPerson = "Cement Guy", BranchCode = "002", SupplierAccountNumber = "ACC002", BankName = "Nedbank", BankAccountNumber = "987654321", VatNumber = "0987654321" });
            context.SaveChanges();
            logger.LogInformation("Suppliers seeded.");
        }

        private static void SeedInventory(AppDbContext context, ILogger logger)
        {
            if (context.InventoryItems.Any()) return;
            context.InventoryItems.Add(new InventoryItem { Description = "Cement 50kg PPC", Sku = "CEM-50-PPC", UnitOfMeasure = "Bag", Category = "Building", AverageCost = 110, Price = 150, QuantityOnHand = 100, JhbReorderPoint = 20, CptReorderPoint = 10, Supplier = "PPC Cement", Location = "JHB" });
            context.InventoryItems.Add(new InventoryItem { Description = "Red Brick", Sku = "BRK-RED", UnitOfMeasure = "ea", Category = "Building", AverageCost = 2.5m, Price = 4.5m, QuantityOnHand = 5000, JhbReorderPoint = 1000, CptReorderPoint = 500, Supplier = "BuildIt", Location = "CPT" });
            context.SaveChanges();
            logger.LogInformation("Inventory seeded.");
        }

        private static void SeedNoticeBoard(AppDbContext context, ILogger logger)
        {
            if (context.NoticeBoardItems.Any()) return;

            context.NoticeBoardItems.Add(new NoticeBoardItem
            {
                Title = "Welcome to the OCC Notice Board!",
                Content = "This notice board will show operational updates, bug-testing reminders, and system alerts. Administrators can post new announcements directly from this widget.",
                Category = NoticeCategory.Announcement,
                IsPinned = true,
                CreatedBy = "System"
            });

            context.NoticeBoardItems.Add(new NoticeBoardItem
            {
                Title = "Bug Resolution Process",
                Content = "Please ensure that once a bug is fixed and tested successfully, the snag or bug report is officially marked as Closed in the portal.",
                Category = NoticeCategory.BugTesting,
                IsPinned = false,
                CreatedBy = "Neil Ketting"
            });

            context.NoticeBoardItems.Add(new NoticeBoardItem
            {
                Title = "Server Maintenance Window",
                Content = "Database maintenance is scheduled for Sunday, July 19th from 2:00 AM to 4:00 AM UTC. Expect temporary downtime during this window.",
                Category = NoticeCategory.Maintenance,
                IsPinned = false,
                CreatedBy = "System"
            });

            context.SaveChanges();
            logger.LogInformation("Notice board items seeded successfully.");
        }
    }
}
