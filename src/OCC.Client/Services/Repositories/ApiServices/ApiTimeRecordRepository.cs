using System.Net.Http;
using System.Threading.Tasks;
using OCC.Shared.Models;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiTimeRecordRepository : BaseApiService<TimeRecord>
    {
        public ApiTimeRecordRepository(HttpClient httpClient, IAuthService authService) : base(authService, httpClient)
        {
        }

        protected override string ApiEndpoint => "TimeRecords";
    }
}
