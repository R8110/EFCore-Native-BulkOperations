using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace EFCore.Native.BulkOperations.Internal;

/// <summary>
/// IDataReader implementation for reading entity data during SqlBulkCopy operations.
/// </summary>
internal class EntityDataReader<T> : IDataReader where T : class
{
    private readonly IEnumerator<T> _enumerator;
    private readonly IReadOnlyList<PropertyMapping> _properties;
    private readonly Dictionary<string, int> _columnOrdinals;
    private bool _disposed;

    public EntityDataReader(IEnumerable<T> entities, IReadOnlyList<PropertyMapping> properties)
    {
        _enumerator = entities.GetEnumerator();
        _properties = properties;
        _columnOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < properties.Count; i++)
        {
            _columnOrdinals[properties[i].ColumnName] = i;
        }
    }

    public int FieldCount => _properties.Count;

    public object this[int i] => GetValue(i);
    public object this[string name] => GetValue(GetOrdinal(name));

    public bool Read()
    {
        return _enumerator.MoveNext();
    }

    public object GetValue(int i)
    {
        var property = _properties[i];
        var value = property.GetValue(_enumerator.Current!);
        return value ?? DBNull.Value;
    }

    public int GetOrdinal(string name)
    {
        if (_columnOrdinals.TryGetValue(name, out var ordinal))
            return ordinal;
        throw new IndexOutOfRangeException($"Column '{name}' not found.");
    }

    public string GetName(int i) => _properties[i].ColumnName;

    public Type GetFieldType(int i)
    {
        var type = _properties[i].ClrType;
        // Return underlying type for nullable types
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    public bool IsDBNull(int i)
    {
        var value = _properties[i].GetValue(_enumerator.Current!);
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

        for (int i = 0; i < _properties.Count; i++)
        {
            var property = _properties[i];
            var row = table.NewRow();
            row["ColumnName"] = property.ColumnName;
            row["ColumnOrdinal"] = i;
            row["DataType"] = GetFieldType(i);
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
