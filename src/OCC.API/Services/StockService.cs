using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using System;
using System.Threading.Tasks;

namespace OCC.API.Services
{
    public class StockService : IStockService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StockService> _logger;

        public StockService(AppDbContext context, ILogger<StockService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task AdjustStockAsync(Guid itemId, double quantityChange, Branch branch)
        {
            var item = await _context.InventoryItems.FindAsync(itemId);
            if (item == null)
            {
                _logger.LogWarning("Attempted to adjust stock for non-existent item {ItemId}", itemId);
                return;
            }

            if (branch == Branch.JHB)
            {
                item.JhbQuantity += quantityChange;
            }
            else if (branch == Branch.CPT)
            {
                item.CptQuantity += quantityChange;
            }

            // Sync aggregate total
            item.QuantityOnHand = item.JhbQuantity + item.CptQuantity;

            _logger.LogInformation("Adjusted stock for {Description} ({Sku}) by {Change} in {Branch}. New Total: {Total}", 
                item.Description, item.Sku, quantityChange, branch, item.QuantityOnHand);

            // We don't SaveChanges here, let the controller handle transactionality
        }
    }
}
