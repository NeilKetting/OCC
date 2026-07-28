using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.Shared.Enums;
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
    public class HseqDocumentsControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<HseqDocumentsController>> _mockLogger;

        public HseqDocumentsControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockLogger = new Mock<ILogger<HseqDocumentsController>>();
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
        public async Task GetDocuments_WithoutProjectId_ReturnsGlobalDocuments()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqDocumentsController(context, _mockLogger.Object);

            var projId = Guid.NewGuid();
            context.HseqDocuments.AddRange(
                new HseqDocument { Id = Guid.NewGuid(), Title = "Global Safety Policy", ProjectId = null },
                new HseqDocument { Id = Guid.NewGuid(), Title = "Project Plan", ProjectId = projId }
            );
            await context.SaveChangesAsync();

            var result = await controller.GetDocuments(Guid.Empty);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var docs = Assert.IsAssignableFrom<IEnumerable<HseqDocument>>(okResult.Value).ToList();
            Assert.Single(docs);
            Assert.Equal("Global Safety Policy", docs[0].Title);
        }

        [Fact]
        public async Task GetDocuments_WithProjectId_ReturnsProjectDocuments()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqDocumentsController(context, _mockLogger.Object);

            var projId = Guid.NewGuid();
            context.HseqDocuments.AddRange(
                new HseqDocument { Id = Guid.NewGuid(), Title = "Global Policy", ProjectId = null },
                new HseqDocument { Id = Guid.NewGuid(), Title = "Project Spec", ProjectId = projId }
            );
            await context.SaveChangesAsync();

            var result = await controller.GetDocuments(projId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var docs = Assert.IsAssignableFrom<IEnumerable<HseqDocument>>(okResult.Value).ToList();
            Assert.Single(docs);
            Assert.Equal("Project Spec", docs[0].Title);
        }

        [Fact]
        public async Task UploadDocument_WithNullPayload_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqDocumentsController(context, _mockLogger.Object);

            var result = await controller.UploadDocument(null!, null);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task UploadDocument_WithDisallowedExtension_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqDocumentsController(context, _mockLogger.Object);

            var document = new HseqDocument { Title = "Malicious file", Category = DocumentCategory.Policy };
            var file = CreateTestFormFile("virus.exe", "dangerous code", "application/x-msdownload");

            var result = await controller.UploadDocument(document, file);

            var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
            Assert.Equal(400, objectResult.StatusCode);
            Assert.Equal("File extension is not allowed for HSEQ documents.", objectResult.Value);
        }

        [Fact]
        public async Task UploadDocument_WithUnsafeFileName_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqDocumentsController(context, _mockLogger.Object);

            var document = new HseqDocument { Title = "Path Traversal", Category = DocumentCategory.Policy };
            var file = CreateTestFormFile("../../../boot.ini.pdf", "data", "application/pdf");

            var result = await controller.UploadDocument(document, file);

            var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
            Assert.Equal(400, objectResult.StatusCode);
            Assert.Equal("File name contains invalid characters or path traversal vectors.", objectResult.Value);
        }

        [Fact]
        public async Task UploadDocument_WithValidPdf_SavesDocumentAndFile()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqDocumentsController(context, _mockLogger.Object);

            var document = new HseqDocument
            {
                Title = "Safety Standard <script>alert(1)</script>",
                Category = DocumentCategory.Procedure,
                Version = "1.0 <iframe src='x'></iframe>"
            };
            var file = CreateTestFormFile("safety_procedure.pdf", "Procedure PDF content", "application/pdf");

            var result = await controller.UploadDocument(document, file);

            var createdResult = Assert.IsAssignableFrom<CreatedAtActionResult>(result.Result);
            var createdDoc = Assert.IsType<HseqDocument>(createdResult.Value);

            Assert.Equal("Safety Standard", createdDoc.Title);
            Assert.Equal("1.0", createdDoc.Version);
            Assert.Contains("/uploads/hseq/Procedure/", createdDoc.FilePath);
            Assert.False(string.IsNullOrEmpty(createdDoc.FileSize));
        }

        [Fact]
        public async Task DeleteDocument_WithEmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqDocumentsController(context, _mockLogger.Object);

            var result = await controller.DeleteDocument(Guid.Empty);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteDocument_WithNonExistentId_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqDocumentsController(context, _mockLogger.Object);

            var result = await controller.DeleteDocument(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteDocument_WithValidId_DeletesDocument()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqDocumentsController(context, _mockLogger.Object);

            var id = Guid.NewGuid();
            context.HseqDocuments.Add(new HseqDocument { Id = id, Title = "To Delete" });
            await context.SaveChangesAsync();

            var result = await controller.DeleteDocument(id);

            Assert.IsType<NoContentResult>(result);

            var softDeleted = await context.HseqDocuments.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == id);
            Assert.NotNull(softDeleted);
            Assert.False(softDeleted.IsActive);
        }
    }
}
