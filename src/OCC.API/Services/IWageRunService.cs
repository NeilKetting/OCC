using OCC.Shared.Models;
using System;
using System.Threading.Tasks;

namespace OCC.API.Services
{
    public interface IWageRunService
    {
        Task<WageRun> GenerateDraftAsync(WageRun request);
        Task<WageRun> FinalizeRunAsync(WageRun run);
    }
}
