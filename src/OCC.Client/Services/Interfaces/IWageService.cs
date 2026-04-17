using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.Client.Services.Interfaces
{
    public interface IWageService
    {
        Task<IEnumerable<WageRun>> GetWageRunsAsync();
        Task<WageRun?> GetWageRunByIdAsync(Guid id);
        Task<WageRun> GenerateDraftRunAsync(DateTime startDate, DateTime endDate, string? payType, string? branch, decimal totalGasCharge, decimal defaultSupervisorFee, decimal companyHousingWashingFee, string? notes = null);
        Task UpdateDraftLinesAsync(Guid id, IEnumerable<WageRunLine> lines);
        Task<WageRun> FinalizeRunAsync(WageRun run);
        Task DeleteRunAsync(Guid id);
    }
}
