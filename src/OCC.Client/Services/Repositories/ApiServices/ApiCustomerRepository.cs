using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;
using System.Net.Http.Json;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiCustomerRepository : BaseApiService<Customer>
    {
        public ApiCustomerRepository(IAuthService authService) : base(authService)
        {
        }

        protected override string ApiEndpoint => "Customers";
    }
}
