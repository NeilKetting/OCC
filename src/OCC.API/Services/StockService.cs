using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.Shared.Models;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OCC.API.Services
{
    /// <summary>
    /// Service implementation for managing inventory stock adjustments and multi-branch quantities.
    /// </summary>
    public class StockService : IStockService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StockService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StockService"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        public StockService(AppDbContext context, ILogger<StockService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task AdjustStockAsync(Guid itemId, double quantityChange, Branch branch)
        {
            if (itemId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to adjust stock with an empty item ID.");
                return;
            }

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

            // We don't SaveChanges here, let the caller controller/transaction handle persistence
        }
    }
}
