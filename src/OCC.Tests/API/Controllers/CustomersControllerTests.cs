using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Controllers;
using OCC.API.Data;
using OCC.API.Hubs;
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
    public class CustomersControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<CustomersController>> _mockLogger;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public CustomersControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockLogger = new Mock<ILogger<CustomersController>>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();

            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
        }

        [Fact]
        public void Constructor_NullArguments_ThrowsArgumentNullException()
        {
            using var context = new AppDbContext(_dbOptions);
            Assert.Throws<ArgumentNullException>(() => new CustomersController(null!, _mockHubContext.Object, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => new CustomersController(context, null!, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => new CustomersController(context, _mockHubContext.Object, null!));
        }

        [Fact]
        public async Task GetCustomerSummaries_ReturnsOkWithSummaries()
        {
            using var context = new AppDbContext(_dbOptions);
            context.Customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                Name = "Acme Corp",
                Email = "contact@acme.com",
                Phone = "1234567890"
            });
            await context.SaveChangesAsync();

            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);
            var result = await controller.GetCustomerSummaries();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var summaries = Assert.IsAssignableFrom<IEnumerable<CustomerSummaryDto>>(okResult.Value);
            Assert.Single(summaries);
            Assert.Equal("Acme Corp", summaries.First().Name);
        }

        [Fact]
        public async Task GetCustomers_ReturnsAllCustomers()
        {
            using var context = new AppDbContext(_dbOptions);
            context.Customers.Add(new Customer { Id = Guid.NewGuid(), Name = "Client A" });
            context.Customers.Add(new Customer { Id = Guid.NewGuid(), Name = "Client B" });
            await context.SaveChangesAsync();

            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);
            var result = await controller.GetCustomers();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var customers = Assert.IsAssignableFrom<IEnumerable<Customer>>(okResult.Value);
            Assert.Equal(2, customers.Count());
        }

        [Fact]
        public async Task GetCustomer_EmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.GetCustomer(Guid.Empty);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetCustomer_NonExistent_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.GetCustomer(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetCustomer_ValidId_ReturnsCustomerWithContacts()
        {
            using var context = new AppDbContext(_dbOptions);
            var id = Guid.NewGuid();
            context.Customers.Add(new Customer
            {
                Id = id,
                Name = "BuildCo",
                Contacts = new List<CustomerContact>
                {
                    new CustomerContact { Id = Guid.NewGuid(), CustomerId = id, Name = "Jane Contact" }
                }
            });
            await context.SaveChangesAsync();

            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);
            var result = await controller.GetCustomer(id);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var customer = Assert.IsType<Customer>(okResult.Value);
            Assert.Equal(id, customer.Id);
            Assert.Single(customer.Contacts);
        }

        [Fact]
        public async Task PostCustomer_NullOrEmptyName_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);

            var nullRes = await controller.PostCustomer(null!);
            Assert.IsType<BadRequestObjectResult>(nullRes.Result);

            var emptyNameCustomer = new Customer { Name = "   " };
            var emptyRes = await controller.PostCustomer(emptyNameCustomer);
            Assert.IsType<BadRequestObjectResult>(emptyRes.Result);
        }

        [Fact]
        public async Task PostCustomer_ValidCustomer_CreatesAndEmitsSignalR()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);

            var newCustomer = new Customer
            {
                Name = "Apex Developments",
                Email = "info@apex.com",
                Contacts = new List<CustomerContact>
                {
                    new CustomerContact { Name = "Bob Manager" }
                }
            };

            var result = await controller.PostCustomer(newCustomer);
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returned = Assert.IsType<Customer>(created.Value);

            Assert.NotEqual(Guid.Empty, returned.Id);
            Assert.Single(returned.Contacts);

            _mockClientProxy.Verify(c => c.SendCoreAsync("EntityUpdate", It.Is<object[]>(o => o[0].ToString() == "Customer" && o[1].ToString() == "Create"), default), Times.Once);
        }

        [Fact]
        public async Task PutCustomer_NullOrMismatchId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);

            var customer = new Customer { Id = Guid.NewGuid(), Name = "Cust" };
            var result = await controller.PutCustomer(Guid.NewGuid(), customer);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task PutCustomer_ValidUpdate_SyncsContactsAndReturnsNoContent()
        {
            using var context = new AppDbContext(_dbOptions);
            var customerId = Guid.NewGuid();
            var contact1Id = Guid.NewGuid();
            var contact2Id = Guid.NewGuid();

            context.Customers.Add(new Customer
            {
                Id = customerId,
                Name = "Old Cust Name",
                Contacts = new List<CustomerContact>
                {
                    new CustomerContact { Id = contact1Id, CustomerId = customerId, Name = "Contact 1" },
                    new CustomerContact { Id = contact2Id, CustomerId = customerId, Name = "Contact 2" }
                }
            });
            await context.SaveChangesAsync();

            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);
            var updatePayload = new Customer
            {
                Id = customerId,
                Name = "Updated Cust Name",
                Contacts = new List<CustomerContact>
                {
                    new CustomerContact { Id = contact1Id, CustomerId = customerId, Name = "Updated Contact 1" }
                }
            };

            var result = await controller.PutCustomer(customerId, updatePayload);
            Assert.IsType<NoContentResult>(result);

            var dbCustomer = await context.Customers.Include(c => c.Contacts).FirstOrDefaultAsync(c => c.Id == customerId);
            Assert.NotNull(dbCustomer);
            Assert.Equal("Updated Cust Name", dbCustomer.Name);
            var activeContacts = dbCustomer.Contacts.Where(c => c.IsActive).ToList();
            Assert.Single(activeContacts);
        }

        [Fact]
        public async Task DeleteCustomer_EmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.DeleteCustomer(Guid.Empty);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteCustomer_NonExistent_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.DeleteCustomer(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteCustomer_ValidId_DeletesAndEmitsSignalR()
        {
            using var context = new AppDbContext(_dbOptions);
            var id = Guid.NewGuid();
            context.Customers.Add(new Customer { Id = id, Name = "ToDelete Customer" });
            await context.SaveChangesAsync();

            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);
            var result = await controller.DeleteCustomer(id);

            Assert.IsType<NoContentResult>(result);
            var dbCust = await context.Customers.FindAsync(id);
            Assert.False(dbCust!.IsActive);

            _mockClientProxy.Verify(c => c.SendCoreAsync("EntityUpdate", It.Is<object[]>(o => o[0].ToString() == "Customer" && o[1].ToString() == "Delete"), default), Times.Once);
        }

        [Fact]
        public async Task UploadLogo_InvalidFileOrId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);

            var r1 = await controller.UploadLogo(Guid.Empty, null!);
            Assert.IsType<BadRequestObjectResult>(r1.Result);

            var r2 = await controller.UploadLogo(Guid.NewGuid(), null!);
            Assert.IsType<BadRequestObjectResult>(r2.Result);

            var mockFileZero = new Mock<IFormFile>();
            mockFileZero.Setup(f => f.Length).Returns(0);
            var r3 = await controller.UploadLogo(Guid.NewGuid(), mockFileZero.Object);
            Assert.IsType<BadRequestObjectResult>(r3.Result);

            var mockFileLarge = new Mock<IFormFile>();
            mockFileLarge.Setup(f => f.Length).Returns(6 * 1024 * 1024);
            var r4 = await controller.UploadLogo(Guid.NewGuid(), mockFileLarge.Object);
            var bad4 = Assert.IsType<BadRequestObjectResult>(r4.Result);
            Assert.Contains("maximum allowed size", bad4.Value?.ToString());

            var mockFileExe = new Mock<IFormFile>();
            mockFileExe.Setup(f => f.Length).Returns(1024);
            mockFileExe.Setup(f => f.FileName).Returns("malicious.exe");
            var r5 = await controller.UploadLogo(Guid.NewGuid(), mockFileExe.Object);
            var bad5 = Assert.IsType<BadRequestObjectResult>(r5.Result);
            Assert.Contains("Invalid image file type", bad5.Value?.ToString());
        }

        [Fact]
        public async Task UploadLogo_CustomerNotFound_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("logo.png");

            var result = await controller.UploadLogo(Guid.NewGuid(), mockFile.Object);
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task UploadLogo_ValidImage_SavesFileAndUpdateLogoUrl()
        {
            using var context = new AppDbContext(_dbOptions);
            var customerId = Guid.NewGuid();
            context.Customers.Add(new Customer { Id = customerId, Name = "Logo Corp" });
            await context.SaveChangesAsync();

            var controller = new CustomersController(context, _mockHubContext.Object, _mockLogger.Object);

            var fileBytes = Encoding.UTF8.GetBytes("fake image data");
            var stream = new MemoryStream(fileBytes);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(stream.Length);
            mockFile.Setup(f => f.FileName).Returns("company_logo.png");
            mockFile.Setup(f => f.OpenReadStream()).Returns(stream);
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns((Stream target, CancellationToken token) => stream.CopyToAsync(target, token));

            var result = await controller.UploadLogo(customerId, mockFile.Object);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var logoUrl = Assert.IsType<string>(okResult.Value);

            Assert.StartsWith("/uploads/customer_logos/", logoUrl);
            Assert.EndsWith(".png", logoUrl);

            var dbCust = await context.Customers.FindAsync(customerId);
            Assert.NotNull(dbCust);
            Assert.Equal(logoUrl, dbCust.LogoUrl);

            _mockClientProxy.Verify(c => c.SendCoreAsync("EntityUpdate", It.Is<object[]>(o => o[0].ToString() == "Customer" && o[1].ToString() == "Update"), default), Times.Once);
        }
    }
}
