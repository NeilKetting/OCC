using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiTeamRepository : BaseApiService<Team>
    {
        public ApiTeamRepository(IAuthService authService) : base(authService)
        {
        }

        protected override string ApiEndpoint => "Teams";
    }
}
