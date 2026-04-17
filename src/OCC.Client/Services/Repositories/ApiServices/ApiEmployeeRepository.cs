using System.Net.Http;
using System.Threading.Tasks;
using OCC.Shared.Models;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiEmployeeRepository : BaseApiService<Employee>
    {
        public ApiEmployeeRepository(HttpClient httpClient, IAuthService authService) : base(authService, httpClient)
        {
        }

        protected override string ApiEndpoint => "Employees";
    }
}
