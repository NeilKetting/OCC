using System.Net.Http;
using OCC.Shared.Models;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiAttendanceRecordRepository : BaseApiService<AttendanceRecord>
    {
        public ApiAttendanceRecordRepository(HttpClient httpClient, IAuthService authService) : base(authService, httpClient)
        {
        }

        protected override string ApiEndpoint => "AttendanceRecords";
    }
}
