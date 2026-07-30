using System;

namespace OCC.Shared.DTOs
{
    /// <summary>
    /// Generic DTO for broadcasting entity-level delta payloads over SignalR real-time hubs.
    /// </summary>
    /// <typeparam name="T">The entity or model type.</typeparam>
    public class EntityChangeDto<T>
    {
        /// <summary>
        /// The mutation action performed ("Created", "Updated", "Deleted").
        /// </summary>
        public string Action { get; set; } = "Updated";

        /// <summary>
        /// The complete entity payload for real-time in-place UI updates.
        /// </summary>
        public T Entity { get; set; } = default!;

        /// <summary>
        /// The unique primary key of the affected entity.
        /// </summary>
        public Guid EntityId { get; set; }
    }
}
