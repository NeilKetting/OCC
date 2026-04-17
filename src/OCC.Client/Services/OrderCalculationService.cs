using System;
using System.Collections.Generic;
using System.Linq;
using OCC.Client.Services.Interfaces;

namespace OCC.Client.Services
{
    public class OrderCalculationService : IOrderCalculationService
    {
        public (decimal Net, decimal Vat) CalculateLineTotals(double quantity, decimal unitPrice, decimal taxRate)
        {
            decimal qty = (decimal)quantity;
            decimal sub = Math.Round(qty * unitPrice, 2, MidpointRounding.AwayFromZero);
            decimal vat = Math.Round(sub * taxRate, 2, MidpointRounding.AwayFromZero);
            
            return (sub, vat);
        }

        public (decimal SubTotal, decimal VatTotal, decimal GrandTotal) CalculateOrderTotals(IEnumerable<(decimal Net, decimal Vat)> lines)
        {
            if (lines == null) return (0, 0, 0);

            decimal subTotal = lines.Sum(l => l.Net);
            decimal vatTotal = lines.Sum(l => l.Vat);
            decimal grandTotal = subTotal + vatTotal;

            return (subTotal, vatTotal, grandTotal);
        }
    }
}
