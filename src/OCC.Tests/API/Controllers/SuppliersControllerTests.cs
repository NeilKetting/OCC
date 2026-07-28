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
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.API.Controllers
{
    public class SuppliersControllerTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<SuppliersController>> _mockLogger;
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public SuppliersControllerTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockLogger = new Mock<ILogger<SuppliersController>>();
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
            Assert.Throws<ArgumentNullException>(() => new SuppliersController(null!, _mockHubContext.Object, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => new SuppliersController(context, null!, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => new SuppliersController(context, _mockHubContext.Object, null!));
        }

        [Fact]
        public async Task GetSupplierSummaries_ReturnsOkWithFormattedAddresses()
        {
            using var context = new AppDbContext(_dbOptions);
            context.Suppliers.Add(new Supplier
            {
                Id = Guid.NewGuid(),
                Name = "Alpha Hardware",
                Email = "alpha@hardware.com",
                Address = "Line 1\r\nLine 2"
            });
            await context.SaveChangesAsync();

            var controller = new SuppliersController(context, _mockHubContext.Object, _mockLogger.Object);
            var result = await controller.GetSupplierSummaries();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var summaries = Assert.IsAssignableFrom<IEnumerable<SupplierSummaryDto>>(okResult.Value);
            Assert.Single(summaries);
            Assert.Equal("Line 1, Line 2", summaries.First().Address);
        }

        [Fact]
        public async Task GetSuppliers_ReturnsAllSuppliersWithContacts()
        {
            using var context = new AppDbContext(_dbOptions);
            var supplierId = Guid.NewGuid();
            context.Suppliers.Add(new Supplier
            {
                Id = supplierId,
                Name = "Beta Steel",
                Contacts = new List<SupplierContact>
                {
                    new SupplierContact { Id = Guid.NewGuid(), SupplierId = supplierId, ContactName = "John Smith" }
                }
            });
            await context.SaveChangesAsync();

            var controller = new SuppliersController(context, _mockHubContext.Object, _mockLogger.Object);
            var result = await controller.GetSuppliers();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var suppliers = Assert.IsAssignableFrom<IEnumerable<Supplier>>(okResult.Value);
            Assert.Single(suppliers);
            Assert.Single(suppliers.First().Contacts);
        }

        [Fact]
        public async Task GetSupplier_EmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SuppliersController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.GetSupplier(Guid.Empty);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetSupplier_NonExistent_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SuppliersController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.GetSupplier(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetSupplier_ValidId_ReturnsSupplier()
        {
            using var context = new AppDbContext(_dbOptions);
            var id = Guid.NewGuid();
            context.Suppliers.Add(new Supplier { Id = id, Name = "Gamma Timber" });
            await context.SaveChangesAsync();

            var controller = new SuppliersController(context, _mockHubContext.Object, _mockLogger.Object);
            var result = await controller.GetSupplier(id);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var supplier = Assert.IsType<Supplier>(okResult.Value);
            Assert.Equal(id, supplier.Id);
        }

        [Fact]
        public async Task PostSupplier_NullOrEmptyName_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SuppliersController(context, _mockHubContext.Object, _mockLogger.Object);

            var nullRes = await controller.PostSupplier(null!);
            Assert.IsType<BadRequestObjectResult>(nullRes.Result);

            var emptyNameSupplier = new Supplier { Name = "   " };
            var emptyRes = await controller.PostSupplier(emptyNameSupplier);
            Assert.IsType<BadRequestObjectResult>(emptyRes.Result);
        }

        [Fact]
        public async Task PostSupplier_ValidSupplier_CreatesAndEmitsSignalR()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SuppliersController(context, _mockHubContext.Object, _mockLogger.Object);

            var newSupplier = new Supplier
            {
                Name = "Delta Electrical",
                Email = "info@delta.co.za",
                Contacts = new List<SupplierContact>
                {
                    new SupplierContact { ContactName = "Alice Manager" }
                }
            };

            var result = await controller.PostSupplier(newSupplier);
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returned = Assert.IsType<Supplier>(created.Value);

            Assert.NotEqual(Guid.Empty, returned.Id);
            Assert.Single(returned.Contacts);
            Assert.Equal(returned.Id, returned.Contacts.First().SupplierId);

            _mockClientProxy.Verify(c => c.SendCoreAsync("EntityUpdate", It.Is<object[]>(o => o[0].ToString() == "Supplier" && o[1].ToString() == "Create"), default), Times.Once);
        }

        [Fact]
        public async Task PutSupplier_NullOrMismatchId_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SuppliersController(context, _mockHubContext.Object, _mockLogger.Object);

            var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier" };
            var result = await controller.PutSupplier(Guid.NewGuid(), supplier);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task PutSupplier_NonExistent_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SuppliersController(context, _mockHubContext.Object, _mockLogger.Object);

            var id = Guid.NewGuid();
            var supplier = new Supplier { Id = id, Name = "Non Existent" };

            var result = await controller.PutSupplier(id, supplier);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task PutSupplier_ValidUpdate_UpdatesContactsAndReturnsNoContent()
        {
            using var context = new AppDbContext(_dbOptions);
            var supplierId = Guid.NewGuid();
            context.Suppliers.Add(new Supplier
            {
                Id = supplierId,
                Name = "Old Name",
                Contacts = new List<SupplierContact>
                {
                    new SupplierContact { Id = Guid.NewGuid(), SupplierId = supplierId, ContactName = "Old Contact" }
                }
            });
            await context.SaveChangesAsync();

            var controller = new SuppliersController(context, _mockHubContext.Object, _mockLogger.Object);
            var updatePayload = new Supplier
            {
                Id = supplierId,
                Name = "Updated Name",
                Contacts = new List<SupplierContact>
                {
                    new SupplierContact { ContactName = "New Contact 1" },
                    new SupplierContact { ContactName = "New Contact 2" }
                }
            };

            var result = await controller.PutSupplier(supplierId, updatePayload);
            Assert.IsType<NoContentResult>(result);

            var dbSupplier = await context.Suppliers.Include(s => s.Contacts).FirstOrDefaultAsync(s => s.Id == supplierId);
            Assert.NotNull(dbSupplier);
            Assert.Equal("Updated Name", dbSupplier.Name);
            var activeContacts = dbSupplier.Contacts.Where(c => c.IsActive).ToList();
            Assert.Equal(2, activeContacts.Count);
        }

        [Fact]
        public async Task DeleteSupplier_EmptyGuid_ReturnsBadRequest()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SuppliersController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.DeleteSupplier(Guid.Empty);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteSupplier_NonExistent_ReturnsNotFound()
        {
            using var context = new AppDbContext(_dbOptions);
            var controller = new SuppliersController(context, _mockHubContext.Object, _mockLogger.Object);

            var result = await controller.DeleteSupplier(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteSupplier_ValidSupplier_DeletesAndEmitsSignalR()
        {
            using var context = new AppDbContext(_dbOptions);
            var id = Guid.NewGuid();
            context.Suppliers.Add(new Supplier { Id = id, Name = "ToDelete Supplier" });
            await context.SaveChangesAsync();

            var controller = new SuppliersController(context, _mockHubContext.Object, _mockLogger.Object);
            var result = await controller.DeleteSupplier(id);

            Assert.IsType<NoContentResult>(result);
            var dbSupplier = await context.Suppliers.FindAsync(id);
            Assert.False(dbSupplier!.IsActive);

            _mockClientProxy.Verify(c => c.SendCoreAsync("EntityUpdate", It.Is<object[]>(o => o[0].ToString() == "Supplier" && o[1].ToString() == "Delete"), default), Times.Once);
        }
    }
}
