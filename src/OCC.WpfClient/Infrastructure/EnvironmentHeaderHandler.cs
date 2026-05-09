using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OCC.WpfClient.Services.Infrastructure;

namespace OCC.WpfClient.Infrastructure
{
    public class EnvironmentHeaderHandler : DelegatingHandler
    {
        private readonly ConnectionSettings _connectionSettings;

        public EnvironmentHeaderHandler(ConnectionSettings connectionSettings)
        {
            _connectionSettings = connectionSettings;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_connectionSettings.SelectedEnvironment == ConnectionSettings.AppEnvironment.Test)
            {
                request.Headers.Add("X-Environment", "Test");
            }
            else if (_connectionSettings.SelectedEnvironment == ConnectionSettings.AppEnvironment.Live)
            {
                request.Headers.Add("X-Environment", "Live");
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
