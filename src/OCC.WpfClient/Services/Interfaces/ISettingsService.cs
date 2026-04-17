using OCC.Shared.Models;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface ISettingsService
    {
        Task<CompanyDetails> GetCompanyDetailsAsync();
        Task SaveCompanyDetailsAsync(CompanyDetails details);
    }
}
