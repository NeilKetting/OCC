using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Base interface for all persistable entities in the system.
    /// Ensures a uniform Primary Key structure (<see cref="Guid"/> Id).
    /// </summary>
    /// <remarks>
    /// Used by generic repositories and shared services to perform standard CRUD operations.
    /// </remarks>
    public interface IEntity
    {
        /// <summary> The unique identifier for this entity instance. </summary>
        Guid Id { get; set; }
    }
}
