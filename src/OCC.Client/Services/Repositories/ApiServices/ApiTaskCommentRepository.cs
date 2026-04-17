using OCC.Shared.DTOs;
using System.Threading.Tasks;
using OCC.Shared.Models;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;
using System.Net.Http.Json;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiTaskCommentRepository : BaseApiService<TaskComment>
    {
        public ApiTaskCommentRepository(IAuthService authService) : base(authService)
        {
        }

        protected override string ApiEndpoint => "TaskComments";
    }
}
