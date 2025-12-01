using System;
using System.Collections.Generic;

namespace EFCore.Native.BulkOperations;

/// <summary>
/// Configuration options for bulk operations.
/// </summary>
public class BulkConfig
{
    /// <summary>
    /// Gets or sets whether to retrieve and set the identity values after insert.
    /// When true, the identity values will be populated back to the entities.
    /// Default is false.
    /// </summary>
    public bool SetOutputIdentity { get; set; } = false;

    /// <summary>
    /// Gets or sets the list of property names to include in the operation.
    /// If null or empty, all properties are included (except those excluded).
    /// </summary>
    public List<string>? PropertiesToInclude { get; set; }

    /// <summary>
    /// Gets or sets the list of property names to exclude from the operation.
    /// </summary>
    public List<string>? PropertiesToExclude { get; set; }

    /// <summary>
    /// Gets or sets the batch size for bulk operations.
    /// Default is 10000.
    /// </summary>
    public int BatchSize { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the timeout in seconds for bulk copy operations.
    /// If null, the default SqlBulkCopy timeout is used.
    /// </summary>
    public int? BulkCopyTimeout { get; set; }

    /// <summary>
    /// Gets or sets whether to enable streaming for SqlBulkCopy.
    /// Default is true.
    /// </summary>
    public bool EnableStreaming { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to preserve insert order.
    /// When true, the order of entities in the source collection is preserved.
    /// Default is false.
    /// </summary>
    public bool PreserveInsertOrder { get; set; } = false;

    /// <summary>
    /// Gets or sets the property names to use for matching entities in upsert operations.
    /// If null or empty, the primary key is used.
    /// </summary>
    public List<string>? UpdateByProperties { get; set; }

    /// <summary>
    /// Gets or sets whether to use table lock for bulk insert operations.
    /// Default is false.
    /// </summary>
    public bool UseTableLock { get; set; } = false;

    /// <summary>
    /// Gets or sets the notification callback for bulk copy progress.
    /// </summary>
    public Action<long>? NotifyAfter { get; set; }

    /// <summary>
    /// Gets or sets the interval at which the notification callback is invoked.
    /// Default is 1000.
    /// </summary>
    public int NotifyAfterRows { get; set; } = 1000;
}
