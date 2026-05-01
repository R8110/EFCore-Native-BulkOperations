using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EFCore.Native.BulkOperations.Internal;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EFCore.Native.BulkOperations;

/// <summary>
/// Implementation of bulk operations using native .NET features.
/// </summary>
public class BulkOperations : IBulkOperations
{
    private readonly DbContext _context;

    /// <summary>
    /// Creates a new instance of BulkOperations.
    /// </summary>
    /// <param name="context">The DbContext to use for operations.</param>
    public BulkOperations(DbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task BulkInsertAsync<T>(IEnumerable<T> entities, BulkConfig? config = null, CancellationToken cancellationToken = default) where T : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));

        var entityList = entities.ToList();
        if (entityList.Count == 0)
            return;

        config ??= new BulkConfig();
        var metadata = EntityMetadata.Create<T>(_context, config);
        var insertableProperties = metadata.GetInsertableProperties();

        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;

        try
        {
            if (!wasOpen)
                await connection.OpenAsync(cancellationToken);

            var sqlConnection = connection as SqlConnection
                ?? throw new InvalidOperationException("BulkInsert is only supported for SQL Server connections.");

            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction;

            if (config.SetOutputIdentity && metadata.HasIdentity)
            {
                await BulkInsertWithIdentityAsync(sqlConnection, transaction, entityList, metadata, insertableProperties, config, cancellationToken);
            }
            else
            {
                await BulkInsertInternalAsync(sqlConnection, transaction, entityList, metadata, insertableProperties, config, cancellationToken);
            }
        }
        finally
        {
            if (!wasOpen && connection.State == ConnectionState.Open)
                await connection.CloseAsync();
        }
    }

    private async Task BulkInsertInternalAsync<T>(
        SqlConnection connection,
        SqlTransaction? transaction,
        IList<T> entities,
        EntityMetadata metadata,
        IReadOnlyList<PropertyMapping> properties,
        BulkConfig config,
        CancellationToken cancellationToken) where T : class
    {
        var options = SqlBulkCopyOptions.Default;
        if (config.UseTableLock)
            options |= SqlBulkCopyOptions.TableLock;

        using var bulkCopy = new SqlBulkCopy(connection, options, transaction)
        {
            DestinationTableName = metadata.FullTableName,
            BatchSize = config.BatchSize,
            EnableStreaming = config.EnableStreaming
        };

        if (config.BulkCopyTimeout.HasValue)
            bulkCopy.BulkCopyTimeout = config.BulkCopyTimeout.Value;

        if (config.NotifyAfter != null)
        {
            bulkCopy.NotifyAfter = config.NotifyAfterRows;
            bulkCopy.SqlRowsCopied += (_, e) => config.NotifyAfter(e.RowsCopied);
        }

        // Set up column mappings
        foreach (var property in properties)
        {
            bulkCopy.ColumnMappings.Add(property.ColumnName, property.ColumnName);
        }

        using var reader = new EntityDataReader<T>(entities, properties);
        await bulkCopy.WriteToServerAsync(reader, cancellationToken);
    }

    private async Task BulkInsertWithIdentityAsync<T>(
        SqlConnection connection,
        SqlTransaction? transaction,
        IList<T> entities,
        EntityMetadata metadata,
        IReadOnlyList<PropertyMapping> properties,
        BulkConfig config,
        CancellationToken cancellationToken) where T : class
    {
        var identityProperty = metadata.IdentityProperty!;

        // Create a temp table to store data with row numbers
        var tempTableName = $"#TempBulkInsert_{Guid.NewGuid():N}";
        var columnsForTempTable = new StringBuilder();
        var columnsForInsert = new StringBuilder();
        var columnsSelect = new StringBuilder();
        var isFirst = true;

        // Include a row number column for ordering
        columnsForTempTable.Append("[__RowNumber] INT, ");

        foreach (var property in properties)
        {
            if (!isFirst)
            {
                columnsForTempTable.Append(", ");
                columnsForInsert.Append(", ");
                columnsSelect.Append(", ");
            }
            columnsForTempTable.Append($"[{property.ColumnName}] {GetSqlType(property)}");
            columnsForInsert.Append($"[{property.ColumnName}]");
            columnsSelect.Append($"[{property.ColumnName}]");
            isFirst = false;
        }

        // Create temp table
        var createTempTableSql = $"CREATE TABLE {tempTableName} ({columnsForTempTable})";
        using (var cmd = new SqlCommand(createTempTableSql, connection, transaction))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            // Bulk insert into temp table with row numbers
            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
            {
                DestinationTableName = tempTableName,
                BatchSize = config.BatchSize,
                EnableStreaming = config.EnableStreaming
            };

            if (config.BulkCopyTimeout.HasValue)
                bulkCopy.BulkCopyTimeout = config.BulkCopyTimeout.Value;

            bulkCopy.ColumnMappings.Add("__RowNumber", "__RowNumber");
            foreach (var property in properties)
            {
                bulkCopy.ColumnMappings.Add(property.ColumnName, property.ColumnName);
            }

            using var reader = new EntityDataReaderWithRowNumber<T>(entities, properties);
            await bulkCopy.WriteToServerAsync(reader, cancellationToken);

            // Use MERGE to insert and capture identities with row numbers
            var mergeSql = $@"
                MERGE {metadata.FullTableName} AS T
                USING {tempTableName} AS S
                ON 1 = 0
                WHEN NOT MATCHED THEN
                    INSERT ({columnsSelect})
                    VALUES ({string.Join(", ", properties.Select(p => $"S.[{p.ColumnName}]"))})
                OUTPUT inserted.[{identityProperty.ColumnName}], S.[__RowNumber];";

            using var insertCmd = new SqlCommand(mergeSql, connection, transaction);
            using var reader2 = await insertCmd.ExecuteReaderAsync(cancellationToken);

            var entityArray = entities.ToArray();
            while (await reader2.ReadAsync(cancellationToken))
            {
                var identityValue = reader2.GetValue(0);
                var rowNumber = reader2.GetInt32(1);
                if (rowNumber >= 0 && rowNumber < entityArray.Length)
                {
                    identityProperty.SetValue(entityArray[rowNumber], identityValue);
                }
            }
        }
        finally
        {
            // Drop temp table
            using var dropCmd = new SqlCommand($"DROP TABLE IF EXISTS {tempTableName}", connection, transaction);
            await dropCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task BulkUpdateAsync<T>(IEnumerable<T> entities, BulkConfig? config = null, CancellationToken cancellationToken = default) where T : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));

        var entityList = entities.ToList();
        if (entityList.Count == 0)
            return;

        config ??= new BulkConfig();
        var metadata = EntityMetadata.Create<T>(_context, config);

        if (metadata.PrimaryKeyProperties.Count == 0)
            throw new InvalidOperationException($"Entity type {typeof(T).Name} has no primary key defined.");

        var updatableProperties = metadata.GetUpdatableProperties();
        if (updatableProperties.Count == 0)
            return;

        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;

        try
        {
            if (!wasOpen)
                await connection.OpenAsync(cancellationToken);

            var sqlConnection = connection as SqlConnection
                ?? throw new InvalidOperationException("BulkUpdate is only supported for SQL Server connections.");

            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction;

            await BulkUpdateInternalAsync(sqlConnection, transaction, entityList, metadata, updatableProperties, config, cancellationToken);
        }
        finally
        {
            if (!wasOpen && connection.State == ConnectionState.Open)
                await connection.CloseAsync();
        }
    }

    private async Task BulkUpdateInternalAsync<T>(
        SqlConnection connection,
        SqlTransaction? transaction,
        IList<T> entities,
        EntityMetadata metadata,
        IReadOnlyList<PropertyMapping> updatableProperties,
        BulkConfig config,
        CancellationToken cancellationToken) where T : class
    {
        // Create temp table for updates
        var tempTableName = $"#TempBulkUpdate_{Guid.NewGuid():N}";
        var allProperties = metadata.PrimaryKeyProperties.Concat(updatableProperties).ToList();

        var columnsForTempTable = new StringBuilder();
        var isFirst = true;

        foreach (var property in allProperties)
        {
            if (!isFirst)
                columnsForTempTable.Append(", ");
            columnsForTempTable.Append($"[{property.ColumnName}] {GetSqlType(property)}");
            isFirst = false;
        }

        // Create temp table
        var createTempTableSql = $"CREATE TABLE {tempTableName} ({columnsForTempTable})";
        using (var cmd = new SqlCommand(createTempTableSql, connection, transaction))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            // Bulk insert into temp table
            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
            {
                DestinationTableName = tempTableName,
                BatchSize = config.BatchSize,
                EnableStreaming = config.EnableStreaming
            };

            if (config.BulkCopyTimeout.HasValue)
                bulkCopy.BulkCopyTimeout = config.BulkCopyTimeout.Value;

            foreach (var property in allProperties)
            {
                bulkCopy.ColumnMappings.Add(property.ColumnName, property.ColumnName);
            }

            using var reader = new EntityDataReader<T>(entities, allProperties);
            await bulkCopy.WriteToServerAsync(reader, cancellationToken);

            // Build UPDATE statement using MERGE
            var updateSetClauses = string.Join(", ", updatableProperties.Select(p => $"T.[{p.ColumnName}] = S.[{p.ColumnName}]"));
            var joinCondition = string.Join(" AND ", metadata.PrimaryKeyProperties.Select(p => $"T.[{p.ColumnName}] = S.[{p.ColumnName}]"));

            var mergeSql = $@"
                MERGE {metadata.FullTableName} AS T
                USING {tempTableName} AS S
                ON {joinCondition}
                WHEN MATCHED THEN
                    UPDATE SET {updateSetClauses};";

            using var mergeCmd = new SqlCommand(mergeSql, connection, transaction);
            await mergeCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            // Drop temp table
            using var dropCmd = new SqlCommand($"DROP TABLE IF EXISTS {tempTableName}", connection, transaction);
            await dropCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task BulkDeleteAsync<T>(IEnumerable<T> entities, BulkConfig? config = null, CancellationToken cancellationToken = default) where T : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));

        var entityList = entities.ToList();
        if (entityList.Count == 0)
            return;

        config ??= new BulkConfig();
        var metadata = EntityMetadata.Create<T>(_context, config);

        if (metadata.PrimaryKeyProperties.Count == 0)
            throw new InvalidOperationException($"Entity type {typeof(T).Name} has no primary key defined.");

        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;

        try
        {
            if (!wasOpen)
                await connection.OpenAsync(cancellationToken);

            var sqlConnection = connection as SqlConnection
                ?? throw new InvalidOperationException("BulkDelete is only supported for SQL Server connections.");

            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction;

            await BulkDeleteInternalAsync(sqlConnection, transaction, entityList, metadata, config, cancellationToken);
        }
        finally
        {
            if (!wasOpen && connection.State == ConnectionState.Open)
                await connection.CloseAsync();
        }
    }

    private async Task BulkDeleteInternalAsync<T>(
        SqlConnection connection,
        SqlTransaction? transaction,
        IList<T> entities,
        EntityMetadata metadata,
        BulkConfig config,
        CancellationToken cancellationToken) where T : class
    {
        // Create temp table for deletes (only need primary key columns)
        var tempTableName = $"#TempBulkDelete_{Guid.NewGuid():N}";
        var pkProperties = metadata.PrimaryKeyProperties;

        var columnsForTempTable = new StringBuilder();
        var isFirst = true;

        foreach (var property in pkProperties)
        {
            if (!isFirst)
                columnsForTempTable.Append(", ");
            columnsForTempTable.Append($"[{property.ColumnName}] {GetSqlType(property)}");
            isFirst = false;
        }

        // Create temp table
        var createTempTableSql = $"CREATE TABLE {tempTableName} ({columnsForTempTable})";
        using (var cmd = new SqlCommand(createTempTableSql, connection, transaction))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            // Bulk insert primary keys into temp table
            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
            {
                DestinationTableName = tempTableName,
                BatchSize = config.BatchSize,
                EnableStreaming = config.EnableStreaming
            };

            if (config.BulkCopyTimeout.HasValue)
                bulkCopy.BulkCopyTimeout = config.BulkCopyTimeout.Value;

            foreach (var property in pkProperties)
            {
                bulkCopy.ColumnMappings.Add(property.ColumnName, property.ColumnName);
            }

            using var reader = new EntityDataReader<T>(entities, pkProperties);
            await bulkCopy.WriteToServerAsync(reader, cancellationToken);

            // Build DELETE statement using MERGE
            var joinCondition = string.Join(" AND ", pkProperties.Select(p => $"T.[{p.ColumnName}] = S.[{p.ColumnName}]"));

            var mergeSql = $@"
                MERGE {metadata.FullTableName} AS T
                USING {tempTableName} AS S
                ON {joinCondition}
                WHEN MATCHED THEN DELETE;";

            using var mergeCmd = new SqlCommand(mergeSql, connection, transaction);
            await mergeCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            // Drop temp table
            using var dropCmd = new SqlCommand($"DROP TABLE IF EXISTS {tempTableName}", connection, transaction);
            await dropCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task BulkInsertOrUpdateAsync<T>(IEnumerable<T> entities, BulkConfig? config = null, CancellationToken cancellationToken = default) where T : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));

        var entityList = entities.ToList();
        if (entityList.Count == 0)
            return;

        config ??= new BulkConfig();
        var metadata = EntityMetadata.Create<T>(_context, config);

        // Determine match properties (either from config or primary key)
        var matchProperties = config.UpdateByProperties?.Count > 0
            ? metadata.Properties.Where(p => config.UpdateByProperties.Contains(p.PropertyName)).ToList()
            : metadata.PrimaryKeyProperties.ToList();

        if (matchProperties.Count == 0)
            throw new InvalidOperationException($"No properties found to match entities for upsert operation.");

        var insertableProperties = metadata.GetInsertableProperties();
        var updatableProperties = metadata.GetUpdatableProperties()
            .Where(p => !matchProperties.Any(m => m.PropertyName == p.PropertyName))
            .ToList();

        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;

        try
        {
            if (!wasOpen)
                await connection.OpenAsync(cancellationToken);

            var sqlConnection = connection as SqlConnection
                ?? throw new InvalidOperationException("BulkInsertOrUpdate is only supported for SQL Server connections.");

            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction;

            await BulkInsertOrUpdateInternalAsync(sqlConnection, transaction, entityList, metadata, insertableProperties, updatableProperties, matchProperties, config, cancellationToken);
        }
        finally
        {
            if (!wasOpen && connection.State == ConnectionState.Open)
                await connection.CloseAsync();
        }
    }

    private async Task BulkInsertOrUpdateInternalAsync<T>(
        SqlConnection connection,
        SqlTransaction? transaction,
        IList<T> entities,
        EntityMetadata metadata,
        IReadOnlyList<PropertyMapping> insertableProperties,
        IReadOnlyList<PropertyMapping> updatableProperties,
        IReadOnlyList<PropertyMapping> matchProperties,
        BulkConfig config,
        CancellationToken cancellationToken) where T : class
    {
        // Create temp table with all needed properties
        var tempTableName = $"#TempBulkUpsert_{Guid.NewGuid():N}";
        var allProperties = matchProperties
            .Concat(insertableProperties.Where(p => !matchProperties.Any(m => m.PropertyName == p.PropertyName)))
            .Distinct()
            .ToList();

        var columnsForTempTable = new StringBuilder();
        columnsForTempTable.Append("[__RowNumber] INT, ");
        var isFirst = true;

        foreach (var property in allProperties)
        {
            if (!isFirst)
                columnsForTempTable.Append(", ");
            columnsForTempTable.Append($"[{property.ColumnName}] {GetSqlType(property)}");
            isFirst = false;
        }

        // Add identity column if tracking output
        if (config.SetOutputIdentity && metadata.HasIdentity)
        {
            columnsForTempTable.Append($", [{metadata.IdentityProperty!.ColumnName}] {GetSqlType(metadata.IdentityProperty!)} NULL");
        }

        // Create temp table
        var createTempTableSql = $"CREATE TABLE {tempTableName} ({columnsForTempTable})";
        using (var cmd = new SqlCommand(createTempTableSql, connection, transaction))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            // Bulk insert into temp table
            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
            {
                DestinationTableName = tempTableName,
                BatchSize = config.BatchSize,
                EnableStreaming = config.EnableStreaming
            };

            if (config.BulkCopyTimeout.HasValue)
                bulkCopy.BulkCopyTimeout = config.BulkCopyTimeout.Value;

            bulkCopy.ColumnMappings.Add("__RowNumber", "__RowNumber");
            foreach (var property in allProperties)
            {
                bulkCopy.ColumnMappings.Add(property.ColumnName, property.ColumnName);
            }

            using var reader = new EntityDataReaderWithRowNumber<T>(entities, allProperties);
            await bulkCopy.WriteToServerAsync(reader, cancellationToken);

            // Build MERGE statement
            var joinCondition = string.Join(" AND ", matchProperties.Select(p => $"T.[{p.ColumnName}] = S.[{p.ColumnName}]"));
            
            var updateSetClauses = updatableProperties.Count > 0
                ? string.Join(", ", updatableProperties.Select(p => $"T.[{p.ColumnName}] = S.[{p.ColumnName}]"))
                : null;

            var insertColumns = string.Join(", ", insertableProperties.Select(p => $"[{p.ColumnName}]"));
            var insertValues = string.Join(", ", insertableProperties.Select(p => $"S.[{p.ColumnName}]"));

            string mergeSql;
            if (config.SetOutputIdentity && metadata.HasIdentity)
            {
                var outputSql = $@"
                    DECLARE @OutputTable TABLE ([{metadata.IdentityProperty!.ColumnName}] {GetSqlType(metadata.IdentityProperty!)}, [__RowNumber] INT, [__Action] NVARCHAR(10));
                    
                    MERGE {metadata.FullTableName} AS T
                    USING {tempTableName} AS S
                    ON {joinCondition}
                    {(updateSetClauses != null ? $"WHEN MATCHED THEN UPDATE SET {updateSetClauses}" : "")}
                    WHEN NOT MATCHED THEN INSERT ({insertColumns}) VALUES ({insertValues})
                    OUTPUT inserted.[{metadata.IdentityProperty!.ColumnName}], S.[__RowNumber], $action INTO @OutputTable;

                    SELECT [{metadata.IdentityProperty!.ColumnName}], [__RowNumber] FROM @OutputTable WHERE [__Action] = 'INSERT' ORDER BY [__RowNumber];";

                using var mergeCmd = new SqlCommand(outputSql, connection, transaction);
                using var resultReader = await mergeCmd.ExecuteReaderAsync(cancellationToken);

                var entityArray = entities.ToArray();
                while (await resultReader.ReadAsync(cancellationToken))
                {
                    var identityValue = resultReader.GetValue(0);
                    var rowNumber = resultReader.GetInt32(1);
                    if (rowNumber >= 0 && rowNumber < entityArray.Length)
                    {
                        metadata.IdentityProperty!.SetValue(entityArray[rowNumber], identityValue);
                    }
                }
            }
            else
            {
                mergeSql = $@"
                    MERGE {metadata.FullTableName} AS T
                    USING {tempTableName} AS S
                    ON {joinCondition}
                    {(updateSetClauses != null ? $"WHEN MATCHED THEN UPDATE SET {updateSetClauses}" : "")}
                    WHEN NOT MATCHED THEN INSERT ({insertColumns}) VALUES ({insertValues});";

                using var mergeCmd = new SqlCommand(mergeSql, connection, transaction);
                await mergeCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            // Drop temp table
            using var dropCmd = new SqlCommand($"DROP TABLE IF EXISTS {tempTableName}", connection, transaction);
            await dropCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static string GetSqlType(PropertyMapping property)
    {
        // If we have explicit column type from EF Core, use it
        if (!string.IsNullOrEmpty(property.ColumnType))
        {
            return property.ColumnType;
        }

        var type = property.ClrType;
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(int)) return "INT";
        if (underlyingType == typeof(long)) return "BIGINT";
        if (underlyingType == typeof(short)) return "SMALLINT";
        if (underlyingType == typeof(byte)) return "TINYINT";
        if (underlyingType == typeof(bool)) return "BIT";
        if (underlyingType == typeof(decimal))
        {
            var precision = property.Precision ?? 18;
            var scale = property.Scale ?? 2;
            return $"DECIMAL({precision},{scale})";
        }
        if (underlyingType == typeof(double)) return "FLOAT";
        if (underlyingType == typeof(float)) return "REAL";
        if (underlyingType == typeof(DateTime)) return "DATETIME2";
        if (underlyingType == typeof(DateTimeOffset)) return "DATETIMEOFFSET";
        if (underlyingType == typeof(TimeSpan)) return "TIME";
        if (underlyingType == typeof(Guid)) return "UNIQUEIDENTIFIER";
        if (underlyingType == typeof(string))
        {
            var maxLength = property.MaxLength;
            return maxLength.HasValue ? $"NVARCHAR({maxLength.Value})" : "NVARCHAR(MAX)";
        }
        if (underlyingType == typeof(byte[])) return "VARBINARY(MAX)";

        return "NVARCHAR(MAX)";
    }
}

/// <summary>
/// IDataReader implementation that includes row numbers for tracking insert order.
/// </summary>
internal class EntityDataReaderWithRowNumber<T> : IDataReader where T : class
{
    private readonly IEnumerator<T> _enumerator;
    private readonly IReadOnlyList<PropertyMapping> _properties;
    private readonly Dictionary<string, int> _columnOrdinals;
    private int _currentRowNumber;
    private bool _disposed;

    public EntityDataReaderWithRowNumber(IEnumerable<T> entities, IReadOnlyList<PropertyMapping> properties)
    {
        _enumerator = entities.GetEnumerator();
        _properties = properties;
        _columnOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _columnOrdinals["__RowNumber"] = 0;
        for (int i = 0; i < properties.Count; i++)
        {
            _columnOrdinals[properties[i].ColumnName] = i + 1;
        }
        _currentRowNumber = -1;
    }

    public int FieldCount => _properties.Count + 1;

    public object this[int i] => GetValue(i);
    public object this[string name] => GetValue(GetOrdinal(name));

    public bool Read()
    {
        _currentRowNumber++;
        return _enumerator.MoveNext();
    }

    public object GetValue(int i)
    {
        if (i == 0)
            return _currentRowNumber;

        var property = _properties[i - 1];
        var value = property.GetValue(_enumerator.Current!);
        return value ?? DBNull.Value;
    }

    public int GetOrdinal(string name)
    {
        if (_columnOrdinals.TryGetValue(name, out var ordinal))
            return ordinal;
        throw new IndexOutOfRangeException($"Column '{name}' not found.");
    }

    public string GetName(int i)
    {
        if (i == 0)
            return "__RowNumber";
        return _properties[i - 1].ColumnName;
    }

    public Type GetFieldType(int i)
    {
        if (i == 0)
            return typeof(int);
        var type = _properties[i - 1].ClrType;
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    public bool IsDBNull(int i)
    {
        if (i == 0)
            return false;
        var value = _properties[i - 1].GetValue(_enumerator.Current!);
        return value == null;
    }

    // IDataReader required members
    public int Depth => 0;
    public bool IsClosed => _disposed;
    public int RecordsAffected => -1;

    public void Close() => Dispose();

    public void Dispose()
    {
        if (!_disposed)
        {
            _enumerator.Dispose();
            _disposed = true;
        }
    }

    public DataTable GetSchemaTable()
    {
        var table = new DataTable();
        table.Columns.Add("ColumnName", typeof(string));
        table.Columns.Add("ColumnOrdinal", typeof(int));
        table.Columns.Add("DataType", typeof(Type));
        table.Columns.Add("AllowDBNull", typeof(bool));

        var row = table.NewRow();
        row["ColumnName"] = "__RowNumber";
        row["ColumnOrdinal"] = 0;
        row["DataType"] = typeof(int);
        row["AllowDBNull"] = false;
        table.Rows.Add(row);

        for (int i = 0; i < _properties.Count; i++)
        {
            var property = _properties[i];
            row = table.NewRow();
            row["ColumnName"] = property.ColumnName;
            row["ColumnOrdinal"] = i + 1;
            row["DataType"] = GetFieldType(i + 1);
            row["AllowDBNull"] = property.IsNullable;
            table.Rows.Add(row);
        }

        return table;
    }

    public bool NextResult() => false;

    // IDataRecord implementation (minimal)
    public bool GetBoolean(int i) => (bool)GetValue(i);
    public byte GetByte(int i) => (byte)GetValue(i);
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
    public char GetChar(int i) => (char)GetValue(i);
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
    public IDataReader GetData(int i) => throw new NotSupportedException();
    public string GetDataTypeName(int i) => GetFieldType(i).Name;
    public DateTime GetDateTime(int i) => (DateTime)GetValue(i);
    public decimal GetDecimal(int i) => (decimal)GetValue(i);
    public double GetDouble(int i) => (double)GetValue(i);
    public float GetFloat(int i) => (float)GetValue(i);
    public Guid GetGuid(int i) => (Guid)GetValue(i);
    public short GetInt16(int i) => (short)GetValue(i);
    public int GetInt32(int i) => (int)GetValue(i);
    public long GetInt64(int i) => (long)GetValue(i);
    public string GetString(int i) => (string)GetValue(i);
    public int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (int i = 0; i < count; i++)
            values[i] = GetValue(i);
        return count;
    }
}
