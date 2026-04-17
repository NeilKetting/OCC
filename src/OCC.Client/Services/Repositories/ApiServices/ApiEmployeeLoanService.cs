using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiEmployeeLoanService : BaseApiService<EmployeeLoan>, IEmployeeLoanService
    {
        public ApiEmployeeLoanService(HttpClient httpClient, IAuthService authService) : base(authService, httpClient)
        {
        }

        protected override string ApiEndpoint => "EmployeeLoans";

        public async Task<IEnumerable<EmployeeLoan>> GetActiveLoansAsync()
        {
            EnsureAuthorization();
            var result = await _httpClient.GetFromJsonAsync<IEnumerable<EmployeeLoan>>(GetFullUrl($"api/{ApiEndpoint}/active"));
            return result ?? new List<EmployeeLoan>();
        }
    }
}
