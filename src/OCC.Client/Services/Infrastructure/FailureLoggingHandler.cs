using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace OCC.Client.Services.Infrastructure
{
    public class FailureLoggingHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    Log.Error("API FAILURE | {Method} {Url} | Status: {StatusCode} ({Reason}) | Content: {Content}",
                        request.Method,
                        request.RequestUri,
                        (int)response.StatusCode,
                        response.ReasonPhrase,
                        content);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "FAILED TO LOG API FAILURE for {Url}", request.RequestUri);
                }
            }

            return response;
        }
    }
}
