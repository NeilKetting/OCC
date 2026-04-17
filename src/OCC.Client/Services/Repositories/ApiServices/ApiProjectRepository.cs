using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiProjectRepository : BaseApiService<Project>
    {
        public ApiProjectRepository(IAuthService authService) : base(authService)
        {
        }

        protected override string ApiEndpoint => "Projects";

        public async Task<IEnumerable<Project>> GetMyProjectsAsync()
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<Project>>($"{ApiEndpoint}?assignedToMe=true") ?? new System.Collections.Generic.List<Project>();
        }
    }
}
