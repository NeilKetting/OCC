using OCC.Shared.Models;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IPdfService
    {
        Task<string> GenerateOrderPdfAsync(Order order, bool isPrintVersion = false);
    }
}
