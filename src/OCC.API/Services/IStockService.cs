using OCC.Shared.Models;
using System;
using System.Threading.Tasks;

namespace OCC.API.Services
{
    /// <summary>
    /// Service contract for handling inventory stock adjustments across branches.
    /// </summary>
    public interface IStockService
    {
        /// <summary>
        /// Adjusts the stock quantity for a given inventory item at a specific branch.
        /// </summary>
        /// <param name="itemId">The unique identifier of the inventory item.</param>
        /// <param name="quantityChange">The quantity to add (positive) or deduct (negative).</param>
        /// <param name="branch">The branch location (JHB or CPT).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AdjustStockAsync(Guid itemId, double quantityChange, Branch branch);
    }
}
