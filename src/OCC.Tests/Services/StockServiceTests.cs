using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OCC.API.Data;
using OCC.API.Services;
using OCC.Shared.Models;
using System;
using System.Threading.Tasks;
using Xunit;

namespace OCC.Tests.Services
{
    public class StockServiceTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly Mock<ILogger<StockService>> _mockLogger;

        public StockServiceTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockLogger = new Mock<ILogger<StockService>>();
        }

        [Fact]
        public void Constructor_NullContext_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new StockService(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            using var context = new AppDbContext(_dbOptions);
            Assert.Throws<ArgumentNullException>(() => new StockService(context, null!));
        }

        [Fact]
        public async Task AdjustStockAsync_EmptyItemId_LogsWarningAndDoesNothing()
        {
            using var context = new AppDbContext(_dbOptions);
            var service = new StockService(context, _mockLogger.Object);

            await service.AdjustStockAsync(Guid.Empty, 10, Branch.JHB);

            // Verify no changes in DB
            Assert.Empty(context.InventoryItems);
        }

        [Fact]
        public async Task AdjustStockAsync_NonExistentItem_LogsWarningAndDoesNothing()
        {
            using var context = new AppDbContext(_dbOptions);
            var service = new StockService(context, _mockLogger.Object);

            await service.AdjustStockAsync(Guid.NewGuid(), 10, Branch.JHB);

            Assert.Empty(context.InventoryItems);
        }

        [Fact]
        public async Task AdjustStockAsync_JhbBranch_IncrementsJhbQuantityAndQuantityOnHand()
        {
            using var context = new AppDbContext(_dbOptions);
            var itemId = Guid.NewGuid();
            var item = new InventoryItem
            {
                Id = itemId,
                Sku = "SKU-JHB",
                Description = "JHB Stock Item",
                JhbQuantity = 50,
                CptQuantity = 20,
                QuantityOnHand = 70
            };
            context.InventoryItems.Add(item);
            await context.SaveChangesAsync();

            var service = new StockService(context, _mockLogger.Object);
            await service.AdjustStockAsync(itemId, 15, Branch.JHB);

            var dbItem = await context.InventoryItems.FindAsync(itemId);
            Assert.NotNull(dbItem);
            Assert.Equal(65, dbItem.JhbQuantity);
            Assert.Equal(20, dbItem.CptQuantity);
            Assert.Equal(85, dbItem.QuantityOnHand);
        }

        [Fact]
        public async Task AdjustStockAsync_CptBranch_IncrementsCptQuantityAndQuantityOnHand()
        {
            using var context = new AppDbContext(_dbOptions);
            var itemId = Guid.NewGuid();
            var item = new InventoryItem
            {
                Id = itemId,
                Sku = "SKU-CPT",
                Description = "CPT Stock Item",
                JhbQuantity = 10,
                CptQuantity = 30,
                QuantityOnHand = 40
            };
            context.InventoryItems.Add(item);
            await context.SaveChangesAsync();

            var service = new StockService(context, _mockLogger.Object);
            await service.AdjustStockAsync(itemId, -5, Branch.CPT);

            var dbItem = await context.InventoryItems.FindAsync(itemId);
            Assert.NotNull(dbItem);
            Assert.Equal(10, dbItem.JhbQuantity);
            Assert.Equal(25, dbItem.CptQuantity);
            Assert.Equal(35, dbItem.QuantityOnHand);
        }
    }
}
