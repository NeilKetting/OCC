using System;
using System.Collections.Generic;
using System.Linq;
using OCC.Client.Services.Interfaces;

namespace OCC.Client.Services
{
    /// <summary>
    /// Financial calculation engine providing exact 2-decimal place precision for order lines and order totals.
    /// </summary>
    public class OrderCalculationService : IOrderCalculationService
    {
        /// <inheritdoc/>
        public (decimal Net, decimal Vat) CalculateLineTotals(double quantity, decimal unitPrice, decimal taxRate)
        {
            if (quantity < 0) quantity = 0;
            if (unitPrice < 0) unitPrice = 0m;
            if (taxRate < 0) taxRate = 0m;

            decimal qty = (decimal)quantity;
            decimal sub = Math.Round(qty * unitPrice, 2, MidpointRounding.AwayFromZero);
            decimal vat = Math.Round(sub * taxRate, 2, MidpointRounding.AwayFromZero);
            
            return (sub, vat);
        }

        /// <inheritdoc/>
        public (decimal SubTotal, decimal VatTotal, decimal GrandTotal) CalculateOrderTotals(IEnumerable<(decimal Net, decimal Vat)> lines)
        {
            if (lines == null) return (0, 0, 0);

            decimal subTotal = Math.Round(lines.Sum(l => l.Net), 2, MidpointRounding.AwayFromZero);
            decimal vatTotal = Math.Round(lines.Sum(l => l.Vat), 2, MidpointRounding.AwayFromZero);
            decimal grandTotal = subTotal + vatTotal;

            return (subTotal, vatTotal, grandTotal);
        }
    }
}
