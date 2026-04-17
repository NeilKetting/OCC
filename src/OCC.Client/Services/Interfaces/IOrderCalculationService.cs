using System.Collections.Generic;

namespace OCC.Client.Services.Interfaces
{
    public interface IOrderCalculationService
    {
        (decimal Net, decimal Vat) CalculateLineTotals(double quantity, decimal unitPrice, decimal taxRate);
        (decimal SubTotal, decimal VatTotal, decimal GrandTotal) CalculateOrderTotals(IEnumerable<(decimal Net, decimal Vat)> lines);
    }
}
