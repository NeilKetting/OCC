using OCC.Shared.Models;
using System.Threading.Tasks;

namespace OCC.Client.Services.Interfaces
{
    public interface ISettingsService
    {
        Task<CompanyDetails> GetCompanyDetailsAsync();
        Task SaveCompanyDetailsAsync(CompanyDetails details);
    }
}
