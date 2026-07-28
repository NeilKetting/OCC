using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class HseqTrainingControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<IWebHostEnvironment> _mockEnv;
        private readonly Mock<ILogger<HseqTrainingController>> _mockLogger;

        public HseqTrainingControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockEnv = new Mock<IWebHostEnvironment>();
            _mockEnv.Setup(e => e.WebRootPath).Returns(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));

            _mockLogger = new Mock<ILogger<HseqTrainingController>>();
        }

        private static IFormFile CreateTestFormFile(string fileName, string content, string contentType = "application/pdf")
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        [Fact]
        public async Task GetTrainingRecords_ReturnsAllRecords()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            context.HseqTrainingRecords.AddRange(
                new HseqTrainingRecord { Id = Guid.NewGuid(), EmployeeName = "John Doe", DateCompleted = DateTime.UtcNow.AddDays(-10) },
                new HseqTrainingRecord { Id = Guid.NewGuid(), EmployeeName = "Jane Smith", DateCompleted = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            var result = await controller.GetTrainingRecords();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsAssignableFrom<IEnumerable<HseqTrainingRecord>>(okResult.Value).ToList();
            Assert.Equal(2, list.Count);
            Assert.Equal("Jane Smith", list[0].EmployeeName);
        }

        [Fact]
        public async Task GetTrainingRecord_WithEmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var result = await controller.GetTrainingRecord(Guid.Empty);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetTrainingRecord_WithNonExistentId_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var result = await controller.GetTrainingRecord(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetTrainingRecord_WithValidId_ReturnsRecord()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var id = Guid.NewGuid();
            context.HseqTrainingRecords.Add(new HseqTrainingRecord { Id = id, EmployeeName = "Alice Brown" });
            await context.SaveChangesAsync();

            var result = await controller.GetTrainingRecord(id);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var record = Assert.IsType<HseqTrainingRecord>(okResult.Value);
            Assert.Equal("Alice Brown", record.EmployeeName);
        }

        [Fact]
        public async Task GetExpiringTraining_WithInvalidDays_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var result = await controller.GetExpiringTraining(-5);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetExpiringTraining_ReturnsExpiringRecordsWithinThreshold()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            context.HseqTrainingRecords.AddRange(
                new HseqTrainingRecord { Id = Guid.NewGuid(), EmployeeName = "Expiring Soon", ValidUntil = DateTime.UtcNow.AddDays(15) },
                new HseqTrainingRecord { Id = Guid.NewGuid(), EmployeeName = "Far Future", ValidUntil = DateTime.UtcNow.AddDays(100) }
            );
            await context.SaveChangesAsync();

            var result = await controller.GetExpiringTraining(30);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsAssignableFrom<IEnumerable<HseqTrainingRecord>>(okResult.Value).ToList();
            Assert.Single(list);
            Assert.Equal("Expiring Soon", list[0].EmployeeName);
        }

        [Fact]
        public async Task GetTrainingSummaries_ReturnsSummaryDtos()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            context.HseqTrainingRecords.Add(new HseqTrainingRecord { Id = Guid.NewGuid(), EmployeeName = "Tom Wilson", TrainingTopic = "Working at Height" });
            await context.SaveChangesAsync();

            var result = await controller.GetTrainingSummaries();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dtos = Assert.IsAssignableFrom<IEnumerable<HseqTrainingSummaryDto>>(okResult.Value).ToList();
            Assert.Single(dtos);
            Assert.Equal("Working at Height", dtos[0].TrainingTopic);
        }

        [Fact]
        public async Task PostTrainingRecord_WithNullPayload_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var result = await controller.PostTrainingRecord(null!);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostTrainingRecord_SanitizesInputs_AndCreatesRecord()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var record = new HseqTrainingRecord
            {
                EmployeeName = "<script>alert(1)</script>Bob Vance",
                TrainingTopic = "First Aid <iframe src='x'></iframe>",
                CertificateType = "Level 2"
            };

            var result = await controller.PostTrainingRecord(record);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var created = Assert.IsType<HseqTrainingRecord>(createdResult.Value);

            Assert.Equal("Bob Vance", created.EmployeeName);
            Assert.Equal("First Aid", created.TrainingTopic);
            Assert.NotEqual(Guid.Empty, created.Id);
        }

        [Fact]
        public async Task PutTrainingRecord_WithMismatchingId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var record = new HseqTrainingRecord { Id = Guid.NewGuid() };

            var result = await controller.PutTrainingRecord(Guid.NewGuid(), record);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task PutTrainingRecord_WithNonExistentId_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var id = Guid.NewGuid();
            var record = new HseqTrainingRecord { Id = id };

            var result = await controller.PutTrainingRecord(id, record);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task PutTrainingRecord_ValidUpdate_UpdatesRecord()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var id = Guid.NewGuid();
            context.HseqTrainingRecords.Add(new HseqTrainingRecord { Id = id, EmployeeName = "Old Name" });
            await context.SaveChangesAsync();

            var updateRecord = new HseqTrainingRecord
            {
                Id = id,
                EmployeeName = "New Sanitized Name <script>bad()</script>",
                TrainingTopic = "Fire Safety"
            };

            var result = await controller.PutTrainingRecord(id, updateRecord);

            Assert.IsType<NoContentResult>(result);

            var updated = await context.HseqTrainingRecords.FindAsync(id);
            Assert.NotNull(updated);
            Assert.Equal("New Sanitized Name", updated.EmployeeName);
        }

        [Fact]
        public async Task UploadCertificate_WithUnsafeFileName_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var file = CreateTestFormFile("../../../cert.pdf", "PDF Content", "application/pdf");

            var result = await controller.UploadCertificate(file);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("File name contains invalid characters or path traversal vectors.", badReq.Value);
        }

        [Fact]
        public async Task UploadCertificate_WithDisallowedExtension_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var file = CreateTestFormFile("script.exe", "binary content", "application/octet-stream");

            var result = await controller.UploadCertificate(file);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("File extension is not allowed for certificates.", badReq.Value);
        }

        [Fact]
        public async Task UploadCertificate_WithValidPdf_SavesFile()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var file = CreateTestFormFile("certificate.pdf", "PDF Content", "application/pdf");

            var result = await controller.UploadCertificate(file);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task DeleteTrainingRecord_WithNonExistentId_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var result = await controller.DeleteTrainingRecord(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteTrainingRecord_WithValidId_DeletesRecord()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqTrainingController(context, _mockEnv.Object, _mockLogger.Object);

            var id = Guid.NewGuid();
            context.HseqTrainingRecords.Add(new HseqTrainingRecord { Id = id, EmployeeName = "Delete Me" });
            await context.SaveChangesAsync();

            var result = await controller.DeleteTrainingRecord(id);

            Assert.IsType<NoContentResult>(result);

            var softDeleted = await context.HseqTrainingRecords.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
            Assert.NotNull(softDeleted);
            Assert.False(softDeleted.IsActive);
        }
    }
}
