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

            try
            {
                return await base.SendAsync(request, cancellationToken);
            }
            catch (System.Net.Http.HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
            {
                // Return a custom response instead of crashing
                return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("API server is unreachable.")
                };
            }
        }
    }
}
