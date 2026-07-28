using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.Shared.DTOs;
using OCC.Shared.Enums;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class IncidentsControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<IncidentsController>> _mockLogger;

        public IncidentsControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockLogger = new Mock<ILogger<IncidentsController>>();
        }

        private static ControllerContext CreateControllerContext(string username = "Admin", string role = "Admin")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        private static IFormFile CreateTestFormFile(string fileName, string content, string contentType = "image/png")
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
        public async Task GetIncidents_ReturnsAllIncidents_OrderedByDateDescending()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            context.Incidents.AddRange(
                new Incident { Id = Guid.NewGuid(), Location = "Zone A", Date = DateTime.UtcNow.AddDays(-5), Type = IncidentType.NearMiss },
                new Incident { Id = Guid.NewGuid(), Location = "Zone B", Date = DateTime.UtcNow, Type = IncidentType.Injury }
            );
            await context.SaveChangesAsync();

            var result = await controller.GetIncidents();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dtos = Assert.IsAssignableFrom<IEnumerable<IncidentSummaryDto>>(okResult.Value);
            var list = dtos.ToList();
            Assert.Equal(2, list.Count);
            Assert.Equal("Zone B", list[0].Location);
        }

        [Fact]
        public async Task GetIncident_WithEmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var result = await controller.GetIncident(Guid.Empty);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetIncident_WithNonExistentId_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var result = await controller.GetIncident(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetIncident_WithValidId_ReturnsIncidentDto()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var incidentId = Guid.NewGuid();
            var incident = new Incident
            {
                Id = incidentId,
                Location = "Warehouse 3",
                Description = "Slid on wet floor",
                Severity = IncidentSeverity.Low,
                Type = IncidentType.NearMiss
            };
            incident.Photos.Add(new IncidentPhoto { Id = Guid.NewGuid(), FileName = "photo1.png" });

            context.Incidents.Add(incident);
            await context.SaveChangesAsync();

            var result = await controller.GetIncident(incidentId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<IncidentDto>(okResult.Value);
            Assert.Equal(incidentId, dto.Id);
            Assert.Equal("Warehouse 3", dto.Location);
            Assert.Single(dto.Photos);
        }

        [Fact]
        public async Task PostIncident_WithNullPayload_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var result = await controller.PostIncident(null!);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostIncident_SanitizesXss_AndCreatesRecord()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var incident = new Incident
            {
                Location = "<script>alert('xss')</script>Building B",
                Description = "Spill <iframe src='evil.com'></iframe>reported",
                RootCause = "<b onclick='hack()'>Oily floor</b>",
                CorrectiveAction = "Cleaned up"
            };

            var result = await controller.PostIncident(incident);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<IncidentDto>(createdResult.Value);

            Assert.Equal("Building B", dto.Location);
            Assert.Equal("Spill reported", dto.Description);
            Assert.Equal("Oily floor", dto.RootCause);
            Assert.NotEqual(Guid.Empty, dto.Id);
        }

        [Fact]
        public async Task PutIncident_WithMismatchingId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var incident = new Incident { Id = Guid.NewGuid() };

            var result = await controller.PutIncident(Guid.NewGuid(), incident);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task PutIncident_WithNonExistentId_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var id = Guid.NewGuid();
            var incident = new Incident { Id = id };

            var result = await controller.PutIncident(id, incident);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task PutIncident_ValidUpdate_UpdatesRecord()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var id = Guid.NewGuid();
            context.Incidents.Add(new Incident { Id = id, Location = "Old Location", Description = "Old Desc" });
            await context.SaveChangesAsync();

            var updateIncident = new Incident
            {
                Id = id,
                Location = "New Location",
                Description = "New Description <script>alert(1)</script>"
            };

            var result = await controller.PutIncident(id, updateIncident);

            Assert.IsType<NoContentResult>(result);

            var updated = await context.Incidents.FindAsync(id);
            Assert.NotNull(updated);
            Assert.Equal("New Location", updated.Location);
            Assert.Equal("New Description", updated.Description);
        }

        [Fact]
        public async Task DeleteIncident_WithNonExistentId_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var result = await controller.DeleteIncident(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteIncident_WithValidId_DeletesRecord()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var id = Guid.NewGuid();
            context.Incidents.Add(new Incident { Id = id, Location = "To Delete" });
            await context.SaveChangesAsync();

            var result = await controller.DeleteIncident(id);

            Assert.IsType<NoContentResult>(result);

            var softDeleted = await context.Incidents.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == id);
            Assert.NotNull(softDeleted);
            Assert.False(softDeleted.IsActive);
        }

        [Fact]
        public async Task PostPhoto_WithDisallowedExtension_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var file = CreateTestFormFile("script.pdf", "PDF file", "application/pdf");
            var request = new IncidentsController.IncidentPhotoUploadRequest
            {
                IncidentId = Guid.NewGuid(),
                File = file
            };

            var result = await controller.PostPhoto(request);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("File extension is not allowed for incident photos.", badReq.Value);
        }

        [Fact]
        public async Task PostPhoto_WithValidImage_UploadsPhoto()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object)
            {
                ControllerContext = CreateControllerContext("Inspector")
            };

            var incidentId = Guid.NewGuid();
            context.Incidents.Add(new Incident { Id = incidentId, Location = "Site A" });
            await context.SaveChangesAsync();

            var file = CreateTestFormFile("hazard.png", "PNG Content", "image/png");
            var request = new IncidentsController.IncidentPhotoUploadRequest
            {
                IncidentId = incidentId,
                File = file,
                Description = "Hazard picture <script>alert(1)</script>"
            };

            var result = await controller.PostPhoto(request);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var photoDto = Assert.IsType<IncidentPhotoDto>(okResult.Value);
            Assert.Equal("hazard.png", photoDto.FileName);
            Assert.Equal("Inspector", photoDto.UploadedBy);
        }

        [Fact]
        public async Task PostDocument_WithDisallowedExtension_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var file = CreateTestFormFile("exec.bat", "echo hi", "application/x-bat");
            var request = new IncidentsController.IncidentDocumentUploadRequest
            {
                IncidentId = Guid.NewGuid(),
                File = file
            };

            var result = await controller.PostDocument(request);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("File extension is not allowed for incident documents.", badReq.Value);
        }

        [Fact]
        public async Task PostDocument_WithValidPdf_UploadsDocument()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object)
            {
                ControllerContext = CreateControllerContext("Officer")
            };

            var incidentId = Guid.NewGuid();
            context.Incidents.Add(new Incident { Id = incidentId, Location = "Site B" });
            await context.SaveChangesAsync();

            var file = CreateTestFormFile("incident_report.pdf", "PDF Content", "application/pdf");
            var request = new IncidentsController.IncidentDocumentUploadRequest
            {
                IncidentId = incidentId,
                File = file
            };

            var result = await controller.PostDocument(request);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var docDto = Assert.IsType<IncidentDocumentDto>(okResult.Value);
            Assert.Equal("incident_report.pdf", docDto.FileName);
            Assert.Equal("Officer", docDto.UploadedBy);
        }

        [Fact]
        public async Task DeleteDocument_WithValidId_RemovesDocument()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var docId = Guid.NewGuid();
            context.IncidentDocuments.Add(new IncidentDocument { Id = docId, FileName = "doc.pdf" });
            await context.SaveChangesAsync();

            var result = await controller.DeleteDocument(docId);

            Assert.IsType<NoContentResult>(result);

            var softDeleted = await context.IncidentDocuments.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == docId);
            Assert.NotNull(softDeleted);
            Assert.False(softDeleted.IsActive);
        }

        [Fact]
        public async Task DeletePhoto_WithValidId_RemovesPhoto()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new IncidentsController(context, _mockLogger.Object);

            var photoId = Guid.NewGuid();
            context.IncidentPhotos.Add(new IncidentPhoto { Id = photoId, FileName = "photo.png" });
            await context.SaveChangesAsync();

            var result = await controller.DeletePhoto(photoId);

            Assert.IsType<NoContentResult>(result);

            var softDeleted = await context.IncidentPhotos.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == photoId);
            Assert.NotNull(softDeleted);
            Assert.False(softDeleted.IsActive);
        }
    }
}
