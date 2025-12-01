using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace EFCore.Native.BulkOperations;

/// <summary>
/// Extension methods for DbContext to perform bulk operations.
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Performs a bulk insert of entities into the database.
    /// Uses SqlBulkCopy for high-performance inserts.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="context">The DbContext.</param>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="config">Optional configuration for the bulk operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task BulkInsertAsync<T>(
        this DbContext context,
        IEnumerable<T> entities,
        BulkConfig? config = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var operations = new BulkOperations(context);
        return operations.BulkInsertAsync(entities, config, cancellationToken);
    }

    /// <summary>
    /// Performs a bulk update of entities in the database.
    /// Uses EF Core ExecuteUpdateAsync for EF Core 7+ or batched operations for EF Core 6.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="context">The DbContext.</param>
    /// <param name="entities">The entities to update.</param>
    /// <param name="config">Optional configuration for the bulk operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task BulkUpdateAsync<T>(
        this DbContext context,
        IEnumerable<T> entities,
        BulkConfig? config = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var operations = new BulkOperations(context);
        return operations.BulkUpdateAsync(entities, config, cancellationToken);
    }

    /// <summary>
    /// Performs a bulk delete of entities from the database.
    /// Uses EF Core ExecuteDeleteAsync for EF Core 7+ or batched operations for EF Core 6.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="context">The DbContext.</param>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="config">Optional configuration for the bulk operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task BulkDeleteAsync<T>(
        this DbContext context,
        IEnumerable<T> entities,
        BulkConfig? config = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var operations = new BulkOperations(context);
        return operations.BulkDeleteAsync(entities, config, cancellationToken);
    }

    /// <summary>
    /// Performs a bulk insert or update (upsert) of entities in the database.
    /// Uses SQL Server MERGE statement for atomic upsert operations.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="context">The DbContext.</param>
    /// <param name="entities">The entities to insert or update.</param>
    /// <param name="config">Optional configuration for the bulk operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task BulkInsertOrUpdateAsync<T>(
        this DbContext context,
        IEnumerable<T> entities,
        BulkConfig? config = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var operations = new BulkOperations(context);
        return operations.BulkInsertOrUpdateAsync(entities, config, cancellationToken);
    }
}
