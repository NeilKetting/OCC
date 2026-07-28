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
    public class HseqAuditsControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<HseqAuditsController>> _mockLogger;

        public HseqAuditsControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockLogger = new Mock<ILogger<HseqAuditsController>>();
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
        public async Task GetAudits_ReturnsAllAudits_OrderedByDateDescending()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object)
            {
                ControllerContext = CreateControllerContext()
            };

            context.HseqAudits.AddRange(
                new HseqAudit { Id = Guid.NewGuid(), SiteName = "Site A", Date = DateTime.UtcNow.AddDays(-2), AuditNumber = "AUD-01" },
                new HseqAudit { Id = Guid.NewGuid(), SiteName = "Site B", Date = DateTime.UtcNow, AuditNumber = "AUD-02" }
            );
            await context.SaveChangesAsync();

            var result = await controller.GetAudits(null);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dtos = Assert.IsAssignableFrom<IEnumerable<AuditSummaryDto>>(okResult.Value);
            var list = dtos.ToList();
            Assert.Equal(2, list.Count);
            Assert.Equal("AUD-02", list[0].AuditNumber);
        }

        [Fact]
        public async Task GetAudits_WithProjectIdFilter_FiltersCorrectly()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object)
            {
                ControllerContext = CreateControllerContext()
            };

            var projId = Guid.NewGuid();
            var project = new Project { Id = projId, Name = "Alpha Tower" };
            context.Projects.Add(project);

            context.HseqAudits.AddRange(
                new HseqAudit { Id = Guid.NewGuid(), ProjectId = projId, SiteName = "Alpha Site", Date = DateTime.UtcNow, AuditNumber = "AUD-1" },
                new HseqAudit { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), SiteName = "Other Site", Date = DateTime.UtcNow, AuditNumber = "AUD-2" }
            );
            await context.SaveChangesAsync();

            var result = await controller.GetAudits(projId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dtos = Assert.IsAssignableFrom<IEnumerable<AuditSummaryDto>>(okResult.Value);
            var list = dtos.ToList();
            Assert.Single(list);
            Assert.Equal("AUD-1", list[0].AuditNumber);
        }

        [Fact]
        public async Task GetAudit_WithEmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var result = await controller.GetAudit(Guid.Empty);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetAudit_WithNonExistentId_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var result = await controller.GetAudit(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetAudit_WithValidId_ReturnsDetailDto()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var auditId = Guid.NewGuid();
            var audit = new HseqAudit
            {
                Id = auditId,
                SiteName = "Main Building",
                AuditNumber = "AUD-100",
                Status = AuditStatus.InProgress
            };
            audit.Sections.Add(new HseqAuditSection { Id = Guid.NewGuid(), Name = "Scaffolding", PossibleScore = 100, ActualScore = 90 });
            audit.NonComplianceItems.Add(new HseqAuditNonComplianceItem { Id = Guid.NewGuid(), Description = "Missing harness" });

            context.HseqAudits.Add(audit);
            await context.SaveChangesAsync();

            var result = await controller.GetAudit(auditId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<AuditDto>(okResult.Value);
            Assert.Equal(auditId, dto.Id);
            Assert.Equal("Main Building", dto.SiteName);
            Assert.Single(dto.Sections);
            Assert.Single(dto.NonComplianceItems);
        }

        [Fact]
        public async Task PostAudit_WithNullPayload_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var result = await controller.PostAudit(null!);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostAudit_SanitizesXss_AndCreatesAudit()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object)
            {
                ControllerContext = CreateControllerContext()
            };

            var dto = new AuditDto
            {
                SiteName = "<script>alert('xss')</script>Site Security",
                ScopeOfWorks = "Building <b onclick='evil()'>Foundations</b>",
                AuditNumber = "AUD-500",
                Sections = new List<AuditSectionDto>
                {
                    new AuditSectionDto { Name = "<script>bad</script>Electrical" }
                },
                NonComplianceItems = new List<AuditNonComplianceItemDto>
                {
                    new AuditNonComplianceItemDto { Description = "Exposed wire <iframe src='bad.html'></iframe>" }
                }
            };

            var result = await controller.PostAudit(dto);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var createdDto = Assert.IsType<AuditDto>(createdResult.Value);

            Assert.Equal("Site Security", createdDto.SiteName);
            Assert.Equal("Building Foundations", createdDto.ScopeOfWorks);
            Assert.Equal("Electrical", createdDto.Sections[0].Name);
            Assert.Equal("Exposed wire", createdDto.NonComplianceItems[0].Description);
        }

        [Fact]
        public async Task PutAudit_WithMismatchingId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var dto = new AuditDto { Id = Guid.NewGuid() };

            var result = await controller.PutAudit(Guid.NewGuid(), dto);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task PutAudit_WithNonExistentId_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var id = Guid.NewGuid();
            var dto = new AuditDto { Id = id };

            var result = await controller.PutAudit(id, dto);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task PutAudit_ValidUpdate_UpdatesAuditAndChildren()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var auditId = Guid.NewGuid();
            var sectionId = Guid.NewGuid();
            var itemId = Guid.NewGuid();

            var audit = new HseqAudit
            {
                Id = auditId,
                SiteName = "Old Site",
                AuditNumber = "AUD-1"
            };
            audit.Sections.Add(new HseqAuditSection { Id = sectionId, Name = "Old Section" });
            audit.NonComplianceItems.Add(new HseqAuditNonComplianceItem { Id = itemId, Description = "Old Item" });

            context.HseqAudits.Add(audit);
            await context.SaveChangesAsync();

            var updateDto = new AuditDto
            {
                Id = auditId,
                SiteName = "New Site Name",
                AuditNumber = "AUD-1-REV",
                Sections = new List<AuditSectionDto>
                {
                    new AuditSectionDto { Id = sectionId, Name = "Updated Section Name", PossibleScore = 10, ActualScore = 8 },
                    new AuditSectionDto { Name = "New Added Section", PossibleScore = 10, ActualScore = 9 }
                },
                NonComplianceItems = new List<AuditNonComplianceItemDto>
                {
                    new AuditNonComplianceItemDto { Id = itemId, Description = "Updated Item Description" }
                }
            };

            var result = await controller.PutAudit(auditId, updateDto);

            Assert.NotNull(result);

            var updatedAudit = await context.HseqAudits
                .Include(a => a.Sections)
                .Include(a => a.NonComplianceItems)
                .FirstOrDefaultAsync(a => a.Id == auditId);

            Assert.NotNull(updatedAudit);
            Assert.Equal("New Site Name", updatedAudit.SiteName);
            Assert.Equal(2, updatedAudit.Sections.Count);
            Assert.Equal("Updated Item Description", updatedAudit.NonComplianceItems.First().Description);
        }

        [Fact]
        public async Task GetAuditDeviations_ReturnsNonComplianceItems()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var auditId = Guid.NewGuid();
            context.HseqAuditNonComplianceItems.AddRange(
                new HseqAuditNonComplianceItem { Id = Guid.NewGuid(), AuditId = auditId, Description = "Deviation 1" },
                new HseqAuditNonComplianceItem { Id = Guid.NewGuid(), AuditId = auditId, Description = "Deviation 2" }
            );
            await context.SaveChangesAsync();

            var result = await controller.GetAuditDeviations(auditId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dtos = Assert.IsAssignableFrom<IEnumerable<AuditNonComplianceItemDto>>(okResult.Value);
            Assert.Equal(2, dtos.Count());
        }

        [Fact]
        public async Task PostAttachment_WithEmptyFile_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var request = new HseqAuditsController.HseqAuditAttachmentRequest
            {
                AuditId = Guid.NewGuid(),
                File = null
            };

            var result = await controller.PostAttachment(request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task PostAttachment_WithDisallowedExtension_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var file = CreateTestFormFile("malicious.exe", "echo off", "application/x-msdownload");
            var request = new HseqAuditsController.HseqAuditAttachmentRequest
            {
                AuditId = Guid.NewGuid(),
                File = file
            };

            var result = await controller.PostAttachment(request);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("File extension is not allowed.", badReq.Value);
        }

        [Fact]
        public async Task PostAttachment_WithUnsafeFileName_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var auditId = Guid.NewGuid();
            context.HseqAudits.Add(new HseqAudit { Id = auditId, SiteName = "Test Site" });
            await context.SaveChangesAsync();

            var file = CreateTestFormFile("../../../etc/passwd.pdf", "data", "application/pdf");
            var request = new HseqAuditsController.HseqAuditAttachmentRequest
            {
                AuditId = auditId,
                File = file
            };

            var result = await controller.PostAttachment(request);

            var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("File name contains invalid characters or path traversal vectors.", badReq.Value);
        }

        [Fact]
        public async Task PostAttachment_WithValidFile_CreatesAttachment()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object)
            {
                ControllerContext = CreateControllerContext("Auditor")
            };

            var auditId = Guid.NewGuid();
            context.HseqAudits.Add(new HseqAudit { Id = auditId, SiteName = "Test Site" });
            await context.SaveChangesAsync();

            var file = CreateTestFormFile("report.pdf", "PDF Content", "application/pdf");
            var request = new HseqAuditsController.HseqAuditAttachmentRequest
            {
                AuditId = auditId,
                File = file
            };

            var result = await controller.PostAttachment(request);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var attachment = Assert.IsType<HseqAuditAttachment>(okResult.Value);
            Assert.Equal("report.pdf", attachment.FileName);
            Assert.Equal("Auditor", attachment.UploadedBy);
        }

        [Fact]
        public async Task DeleteAudit_WithNonExistentId_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var result = await controller.DeleteAudit(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteAudit_WithValidId_RemovesAudit()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var auditId = Guid.NewGuid();
            context.HseqAudits.Add(new HseqAudit { Id = auditId, SiteName = "To Delete" });
            await context.SaveChangesAsync();

            var result = await controller.DeleteAudit(auditId);

            Assert.IsType<NoContentResult>(result);

            var softDeleted = await context.HseqAudits.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == auditId);
            Assert.NotNull(softDeleted);
            Assert.False(softDeleted.IsActive);
        }

        [Fact]
        public async Task DeleteAttachment_WithNonExistentId_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var result = await controller.DeleteAttachment(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteAttachment_WithValidId_RemovesAttachment()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new HseqAuditsController(context, _mockLogger.Object);

            var attachmentId = Guid.NewGuid();
            context.HseqAuditAttachments.Add(new HseqAuditAttachment { Id = attachmentId, FileName = "test.pdf" });
            await context.SaveChangesAsync();

            var result = await controller.DeleteAttachment(attachmentId);

            Assert.IsType<NoContentResult>(result);

            var softDeleted = await context.HseqAuditAttachments.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == attachmentId);
            Assert.NotNull(softDeleted);
            Assert.False(softDeleted.IsActive);
        }
    }
}
