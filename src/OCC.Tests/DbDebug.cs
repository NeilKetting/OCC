using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;

namespace OCC.Tests
{
    public class DbDebug
    {
        private readonly ITestOutputHelper _output;

        public DbDebug(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task DebugFransLeave()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS;Database=OCC_V2_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;

            using var context = new AppDbContext(options);

            var sb = new System.Text.StringBuilder();

            var employees = await context.Employees.ToListAsync();
            sb.AppendLine($"Total employees: {employees.Count}");
            foreach (var emp in employees.Take(30))
            {
                sb.AppendLine($"- ID: {emp.Id}, Name: {emp.FirstName} {emp.LastName}, Number: {emp.EmployeeNumber}");
            }

            System.IO.File.WriteAllText(@"c:\Users\Neil\source\repos\OCC\db_debug_output.txt", sb.ToString());
        }
    }
}
