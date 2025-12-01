using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EFCore.Native.BulkOperations;

/// <summary>
/// Interface for bulk operations on entities.
/// </summary>
public interface IBulkOperations
{
    /// <summary>
    /// Performs a bulk insert of entities into the database.
    /// Uses SqlBulkCopy for high-performance inserts.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="config">Optional configuration for the bulk operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BulkInsertAsync<T>(IEnumerable<T> entities, BulkConfig? config = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Performs a bulk update of entities in the database.
    /// Uses EF Core ExecuteUpdateAsync for EF Core 7+ or batched operations for EF Core 6.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to update.</param>
    /// <param name="config">Optional configuration for the bulk operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BulkUpdateAsync<T>(IEnumerable<T> entities, BulkConfig? config = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Performs a bulk delete of entities from the database.
    /// Uses EF Core ExecuteDeleteAsync for EF Core 7+ or batched operations for EF Core 6.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="config">Optional configuration for the bulk operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BulkDeleteAsync<T>(IEnumerable<T> entities, BulkConfig? config = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Performs a bulk insert or update (upsert) of entities in the database.
    /// Uses SQL Server MERGE statement or split insert/update operations.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to insert or update.</param>
    /// <param name="config">Optional configuration for the bulk operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BulkInsertOrUpdateAsync<T>(IEnumerable<T> entities, BulkConfig? config = null, CancellationToken cancellationToken = default) where T : class;
}
