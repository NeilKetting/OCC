using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IWageService
    {
        Task<IEnumerable<WageRun>> GetWageRunsAsync();
        Task<WageRun?> GetWageRunByIdAsync(Guid id);

        /// <summary>Generates an in-memory draft wage run — not persisted until Finalize is called.</summary>
        Task<WageRun> GenerateDraftRunAsync(
            DateTime startDate,
            DateTime endDate,
            string? payType,
            string? branch,
            decimal totalGasCharge,
            decimal defaultSupervisorFee,
            decimal companyHousingWashingFee,
            string? notes = null);

        /// <summary>Persists a finalized wage run to the API.</summary>
        Task<WageRun> FinalizeRunAsync(WageRun run);

        /// <summary>Deletes a Draft wage run (Finalized runs are rejected by the API).</summary>
        Task DeleteRunAsync(Guid id);
    }
}
