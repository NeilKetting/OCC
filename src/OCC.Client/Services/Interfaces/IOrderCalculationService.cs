using System.Collections.Generic;

namespace OCC.Client.Services.Interfaces
{
    /// <summary>
    /// Financial order calculation service for determining line item subtotals, VAT, and grand totals.
    /// </summary>
    public interface IOrderCalculationService
    {
        /// <summary>
        /// Calculates net line total and VAT amount based on quantity, unit price, and tax rate.
        /// </summary>
        /// <param name="quantity">The ordered quantity.</param>
        /// <param name="unitPrice">The price per unit.</param>
        /// <param name="taxRate">The tax rate (e.g. 0.15 for 15% VAT).</param>
        /// <returns>A tuple containing (Net total, Vat amount).</returns>
        (decimal Net, decimal Vat) CalculateLineTotals(double quantity, decimal unitPrice, decimal taxRate);

        /// <summary>
        /// Aggregates net line totals and VAT to compute overall order financial summary.
        /// </summary>
        /// <param name="lines">Collection of line net and VAT amounts.</param>
        /// <returns>A tuple containing (SubTotal, VatTotal, GrandTotal).</returns>
        (decimal SubTotal, decimal VatTotal, decimal GrandTotal) CalculateOrderTotals(IEnumerable<(decimal Net, decimal Vat)> lines);
    }
}
