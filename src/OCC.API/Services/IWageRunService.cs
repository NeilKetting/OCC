using OCC.Shared.Models;

namespace OCC.API.Services
{
    /// <summary>
    /// Defines wage run calculation, draft generation, and finalization services.
    /// </summary>
    public interface IWageRunService
    {
        /// <summary>
        /// Generates a draft wage run for a given period, branch, and pay type.
        /// </summary>
        /// <param name="request">The requested wage run parameters.</param>
        /// <returns>The calculated draft <see cref="WageRun"/> entity.</returns>
        Task<WageRun> GenerateDraftAsync(WageRun request);

        /// <summary>
        /// Finalizes a wage run, updating loan balances, database records, and attendance markers.
        /// </summary>
        /// <param name="run">The wage run entity to finalize.</param>
        /// <returns>The finalized <see cref="WageRun"/> entity.</returns>
        Task<WageRun> FinalizeRunAsync(WageRun run);
    }
}
