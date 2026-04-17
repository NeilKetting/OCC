using OCC.Shared.DTOs;
using System.Threading.Tasks;
using OCC.Shared.Models;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;
using System.Net.Http.Json;

namespace OCC.Client.Services.Repositories.ApiServices
{
    // TaskAssignments are handled via ProjectTasks usually but might need direct access
    public class ApiTaskAssignmentRepository : BaseApiService<TaskAssignment>
    {
        public ApiTaskAssignmentRepository(IAuthService authService) : base(authService)
        {
        }

        protected override string ApiEndpoint => "TaskAssignments";
    }
}
