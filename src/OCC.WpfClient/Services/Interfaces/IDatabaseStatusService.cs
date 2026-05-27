using System.Threading;
using System.Threading.Tasks;
using OCC.WpfClient.Services.Results;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IDatabaseStatusService
    {
        Task<DatabaseStatusResult> CheckAsync(CancellationToken cancellationToken);
    }
}
