using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace OCC.Client.Services.Repositories.Interfaces
{
    /// <summary>
    /// Generic repository interface for handling CRUD operations for a specific entity type.
    /// </summary>
    /// <typeparam name="T">The entity type, which must implement IEntity.</typeparam>
    public interface IRepository<T> where T : class, OCC.Shared.Models.IEntity
    {
        /// <summary>
        /// Retrieves all entities of type T.
        /// </summary>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// Retrieves a single entity by its unique identifier.
        /// </summary>
        Task<T?> GetByIdAsync(Guid id);

        /// <summary>
        /// Finds entities based on a specified predicate.
        /// </summary>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Adds a new entity to the repository.
        /// </summary>
        Task AddAsync(T entity);

        /// <summary>
        /// Updates an existing entity in the repository.
        /// </summary>
        Task UpdateAsync(T entity);

        /// <summary>
        /// Deletes an entity by its unique identifier.
        /// </summary>
        Task DeleteAsync(Guid id);
    }
}
