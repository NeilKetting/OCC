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

            var adminEmail = "neil@mdk.co.za";
            var adminUser = context.Users.FirstOrDefault(u => u.Email == adminEmail);

            if (adminUser == null)
            {
                logger.LogInformation("Seeding default admin user...");
                adminUser = new User
                {
                    Email = adminEmail,
                    Password = hasher.HashPassword("ChangeMe123!"), // Default seed password - CHANGE IN PRODUCTION
                    FirstName = "Neil",
                    LastName = "Admin",
                    UserRole = UserRole.Admin,
                    IsApproved = true,
                    IsEmailVerified = true
                };
                context.Users.Add(adminUser);
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
                SeedProjects(context, logger);
                SeedTasks(context, logger);
            }
            else
            {
                logger.LogInformation("Skipped: Not in Development Environment.");
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
            var projects = context.Projects.ToList();
            var employees = context.Employees.ToList();
            
            if (!projects.Any() || !employees.Any())
            {
                logger.LogWarning("Cannot seed tasks: Projects or Employees missing.");
                return;
            }

            var random = new Random();
            var taskNames = new[] { "Site Clearance", "Excavation", "Foundation Pouring", "Brickwork Level 1", "Electrical First Fix", "Plumbing Rough-in", "Roof Truss Installation", "Window Fitting", "Plastering", "Floor Screeding" };
            
            // 1. First, ensure EVERY employee has at least one task assigned for testing
            int taskIndex = 0;
            foreach (var employee in employees)
            {
                var project = projects[random.Next(projects.Count)];
                var name = taskNames[taskIndex % taskNames.Length];
                taskIndex++;

                var task = new ProjectTask
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    Name = $"{name} ({employee.FirstName})",
                    Description = $"Assigned task for {employee.FirstName}",
                    StartDate = DateTime.UtcNow.AddDays(-5),
                    FinishDate = DateTime.UtcNow.AddDays(5),
                    Status = "Started",
                    PercentComplete = 10,
                    Priority = "Medium",
                    Type = TaskType.Task,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = "System"
                };
                context.ProjectTasks.Add(task);

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
                        CachedHourlyRate = (decimal)emp.HourlyRate,
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
            var projects = new List<Project>
            {
                new Project { Name = "Engen Bendor", Description = "Fuel station renovation", StartDate = DateTime.Today.AddDays(-30), EndDate = DateTime.Today.AddDays(60), CustomerId = customer.Id, Status = "Active", Priority = "High", StreetLine1 = "123 Bendor Drive", City = "Polokwane", Country = "South Africa" },
                new Project { Name = "Mall of North", Description = "Expansion project", StartDate = DateTime.Today.AddDays(-10), EndDate = DateTime.Today.AddDays(180), CustomerId = customer.Id, Status = "Active", Priority = "Medium", StreetLine1 = "456 Mall St", City = "Polokwane", Country = "South Africa" },
                new Project { Name = "Savannah Office", Description = "New office complex", StartDate = DateTime.Today.AddDays(-60), EndDate = DateTime.Today.AddDays(-5), CustomerId = customer.Id, Status = "Completed", Priority = "Low", StreetLine1 = "789 Savannah Rd", City = "Polokwane", Country = "South Africa" }
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
    }
}
