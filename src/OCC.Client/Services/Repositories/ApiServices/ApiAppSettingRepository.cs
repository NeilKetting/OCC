using System.Threading.Tasks;
using OCC.Shared.Models;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiAppSettingRepository : BaseApiService<AppSetting>
    {
        public ApiAppSettingRepository(IAuthService authService) : base(authService)
        {
        }

        protected override string ApiEndpoint => "AppSettings";
    }
}
