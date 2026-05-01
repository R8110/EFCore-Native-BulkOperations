using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EFCore.Native.BulkOperations.Internal;

/// <summary>
/// Provides metadata information about entities for bulk operations.
/// </summary>
internal class EntityMetadata
{
    public string TableName { get; }
    public string? Schema { get; }
    public string FullTableName => Schema != null ? $"[{Schema}].[{TableName}]" : $"[{TableName}]";
    public IReadOnlyList<PropertyMapping> Properties { get; }
    public IReadOnlyList<PropertyMapping> PrimaryKeyProperties { get; }
    public PropertyMapping? IdentityProperty { get; }
    public bool HasIdentity => IdentityProperty != null;

    private EntityMetadata(
        string tableName,
        string? schema,
        IReadOnlyList<PropertyMapping> properties,
        IReadOnlyList<PropertyMapping> primaryKeyProperties,
        PropertyMapping? identityProperty)
    {
        TableName = tableName;
        Schema = schema;
        Properties = properties;
        PrimaryKeyProperties = primaryKeyProperties;
        IdentityProperty = identityProperty;
    }

    public static EntityMetadata Create<T>(DbContext context, BulkConfig? config = null) where T : class
    {
        var entityType = context.Model.FindEntityType(typeof(T))
            ?? throw new InvalidOperationException($"Entity type {typeof(T).Name} is not part of the model.");

        var tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException($"Could not determine table name for entity type {typeof(T).Name}.");
        var schema = entityType.GetSchema();

        var storeObject = StoreObjectIdentifier.Table(tableName, schema);

        var allProperties = entityType.GetProperties()
            .Where(p => !p.IsShadowProperty())
            .Where(p => p.GetColumnName(storeObject) != null)
            .ToList();

        var primaryKey = entityType.FindPrimaryKey();
        var primaryKeyPropertyNames = primaryKey?.Properties.Select(p => p.Name).ToHashSet() ?? new HashSet<string>();

        var propertiesToInclude = config?.PropertiesToInclude?.ToHashSet();
        var propertiesToExclude = config?.PropertiesToExclude?.ToHashSet() ?? new HashSet<string>();

        var properties = new List<PropertyMapping>();
        PropertyMapping? identityProperty = null;

        foreach (var property in allProperties)
        {
            // Skip navigation properties (shouldn't be in GetProperties, but be safe)
            if (property.PropertyInfo == null)
                continue;

            // Skip properties based on include/exclude lists
            if (propertiesToInclude != null && !propertiesToInclude.Contains(property.Name))
                continue;
            if (propertiesToExclude.Contains(property.Name))
                continue;

            var columnName = property.GetColumnName(storeObject)!;
            var valueGenerationStrategy = property.GetValueGenerationStrategy();
            var isComputed = property.GetComputedColumnSql() != null;
            var isIdentity = valueGenerationStrategy == SqlServerValueGenerationStrategy.IdentityColumn;
            var isPrimaryKey = primaryKeyPropertyNames.Contains(property.Name);
            var valueConverter = property.GetValueConverter();
            
            // Get column type information
            var columnType = property.GetColumnType(storeObject);
            var maxLength = property.GetMaxLength();
            var precision = property.GetPrecision();
            var scale = property.GetScale();

            var mapping = new PropertyMapping(
                property.Name,
                columnName,
                property.PropertyInfo,
                property.ClrType,
                isPrimaryKey,
                isIdentity,
                isComputed,
                property.IsNullable,
                valueConverter,
                columnType,
                maxLength,
                precision,
                scale);

            properties.Add(mapping);

            if (isIdentity)
                identityProperty = mapping;
        }

        var pkMappings = properties.Where(p => p.IsPrimaryKey).ToList();

        return new EntityMetadata(tableName, schema, properties, pkMappings, identityProperty);
    }

    public IReadOnlyList<PropertyMapping> GetInsertableProperties()
    {
        return Properties
            .Where(p => !p.IsIdentity && !p.IsComputed)
            .ToList();
    }

    public IReadOnlyList<PropertyMapping> GetUpdatableProperties()
    {
        return Properties
            .Where(p => !p.IsPrimaryKey && !p.IsIdentity && !p.IsComputed)
            .ToList();
    }
}

/// <summary>
/// Represents a mapping between an entity property and a database column.
/// </summary>
internal class PropertyMapping
{
    public string PropertyName { get; }
    public string ColumnName { get; }
    public PropertyInfo PropertyInfo { get; }
    public Type ClrType { get; }
    public bool IsPrimaryKey { get; }
    public bool IsIdentity { get; }
    public bool IsComputed { get; }
    public bool IsNullable { get; }
    public Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter? ValueConverter { get; }
    public string? ColumnType { get; }
    public int? MaxLength { get; }
    public int? Precision { get; }
    public int? Scale { get; }

    public PropertyMapping(
        string propertyName,
        string columnName,
        PropertyInfo propertyInfo,
        Type clrType,
        bool isPrimaryKey,
        bool isIdentity,
        bool isComputed,
        bool isNullable,
        Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter? valueConverter,
        string? columnType = null,
        int? maxLength = null,
        int? precision = null,
        int? scale = null)
    {
        PropertyName = propertyName;
        ColumnName = columnName;
        PropertyInfo = propertyInfo;
        ClrType = clrType;
        IsPrimaryKey = isPrimaryKey;
        IsIdentity = isIdentity;
        IsComputed = isComputed;
        IsNullable = isNullable;
        ValueConverter = valueConverter;
        ColumnType = columnType;
        MaxLength = maxLength;
        Precision = precision;
        Scale = scale;
    }

    public object? GetValue(object entity)
    {
        var value = PropertyInfo.GetValue(entity);
        if (ValueConverter != null && value != null)
        {
            return ValueConverter.ConvertToProvider(value);
        }
        return value;
    }

    public void SetValue(object entity, object? value)
    {
        if (ValueConverter != null && value != null)
        {
            value = ValueConverter.ConvertFromProvider(value);
        }
        PropertyInfo.SetValue(entity, value);
    }
}
