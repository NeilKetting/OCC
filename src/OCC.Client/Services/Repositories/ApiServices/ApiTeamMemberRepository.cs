using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiTeamMemberRepository : BaseApiService<TeamMember>
    {
        public ApiTeamMemberRepository(IAuthService authService) : base(authService)
        {
        }

        protected override string ApiEndpoint => "TeamMembers";
    }
}
