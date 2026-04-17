using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiProjectTaskRepository : BaseApiService<ProjectTask>, IProjectTaskRepository
    {
        public ApiProjectTaskRepository(IAuthService authService) : base(authService)
        {
        }

        protected override string ApiEndpoint => "ProjectTasks";

        public async Task<IEnumerable<ProjectTask>> GetMyTasksAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<ProjectTask>>(GetFullUrl($"api/{ApiEndpoint}?assignedToMe=true")) ?? new List<ProjectTask>();
        }
    }
}
