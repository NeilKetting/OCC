using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.Client.Services.Interfaces
{
    /// <summary>
    /// Defines a service for handling role-based permission checks.
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>
        /// Checks if the current user has permission to access the specified route.
        /// </summary>
        /// <param name="route">The route or resource identifier.</param>
        /// <returns>True if access is granted.</returns>
        bool CanAccess(string route);

        /// <summary>
        /// Gets a value indicating whether the current user is a developer.
        /// </summary>
        bool IsDev { get; }
    }
}
