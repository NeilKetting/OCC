using System.Net.Http;
using System.Threading.Tasks;
using OCC.Shared.Models;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiOvertimeRequestRepository : BaseApiService<OvertimeRequest>
    {
        public ApiOvertimeRequestRepository(HttpClient httpClient, IAuthService authService) : base(authService, httpClient)
        {
        }

        protected override string ApiEndpoint => "OvertimeRequests";
    }
}
