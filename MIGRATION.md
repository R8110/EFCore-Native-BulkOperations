# Migration Guide: EFCore.BulkExtensions to EFCore.Native.BulkOperations

This guide helps you migrate from EFCore.BulkExtensions to EFCore.Native.BulkOperations with minimal code changes.

## Table of Contents

1. [Overview](#overview)
2. [Side-by-Side API Comparison](#side-by-side-api-comparison)
3. [Step-by-Step Migration](#step-by-step-migration)
4. [Feature Mapping](#feature-mapping)
5. [Breaking Changes](#breaking-changes)
6. [Performance Considerations](#performance-considerations)
7. [Troubleshooting](#troubleshooting)

## Overview

EFCore.Native.BulkOperations is designed to be a drop-in replacement for EFCore.BulkExtensions (MIT version). The API is intentionally similar to make migration as smooth as possible.

### Why Migrate?

- **100% MIT Licensed** - No licensing concerns
- **Native Implementation** - Uses SqlBulkCopy directly
- **Active Maintenance** - Regularly updated for new EF Core versions
- **Similar Performance** - Comparable or better performance

## Side-by-Side API Comparison

### BulkInsert

**EFCore.BulkExtensions:**
```csharp
using EFCore.BulkExtensions;

await context.BulkInsertAsync(entities);
await context.BulkInsertAsync(entities, config => config.SetOutputIdentity = true);
```

**EFCore.Native.BulkOperations:**
```csharp
using EFCore.Native.BulkOperations;

await context.BulkInsertAsync(entities);
await context.BulkInsertAsync(entities, new BulkConfig { SetOutputIdentity = true });
```

### BulkUpdate

**EFCore.BulkExtensions:**
```csharp
await context.BulkUpdateAsync(entities);
await context.BulkUpdateAsync(entities, config => config.PropertiesToExclude = new List<string> { "CreatedDate" });
```

**EFCore.Native.BulkOperations:**
```csharp
await context.BulkUpdateAsync(entities);
await context.BulkUpdateAsync(entities, new BulkConfig { PropertiesToExclude = new List<string> { "CreatedDate" } });
```

### BulkDelete

**EFCore.BulkExtensions:**
```csharp
await context.BulkDeleteAsync(entities);
```

**EFCore.Native.BulkOperations:**
```csharp
await context.BulkDeleteAsync(entities);
```

### BulkInsertOrUpdate (Upsert)

**EFCore.BulkExtensions:**
```csharp
await context.BulkInsertOrUpdateAsync(entities);
await context.BulkInsertOrUpdateAsync(entities, config => 
{
    config.UpdateByProperties = new List<string> { "ProductCode" };
});
```

**EFCore.Native.BulkOperations:**
```csharp
await context.BulkInsertOrUpdateAsync(entities);
await context.BulkInsertOrUpdateAsync(entities, new BulkConfig 
{ 
    UpdateByProperties = new List<string> { "ProductCode" } 
});
```

## Step-by-Step Migration

### Step 1: Update Package Reference

Remove EFCore.BulkExtensions:
```bash
dotnet remove package EFCore.BulkExtensions
```

Add EFCore.Native.BulkOperations:
```bash
dotnet add package EFCore.Native.BulkOperations
```

### Step 2: Update Using Statements

**Before:**
```csharp
using EFCore.BulkExtensions;
```

**After:**
```csharp
using EFCore.Native.BulkOperations;
```

### Step 3: Update Configuration Pattern

**Before (Action delegate pattern):**
```csharp
await context.BulkInsertAsync(entities, config => 
{
    config.SetOutputIdentity = true;
    config.BatchSize = 5000;
});
```

**After (BulkConfig object pattern):**
```csharp
await context.BulkInsertAsync(entities, new BulkConfig
{
    SetOutputIdentity = true,
    BatchSize = 5000
});
```

### Step 4: Test Your Application

Run your tests to ensure everything works correctly:
```bash
dotnet test
```

## Feature Mapping

| EFCore.BulkExtensions Feature | EFCore.Native Equivalent | Notes |
|-------------------------------|--------------------------|-------|
| `BulkInsertAsync` | `BulkInsertAsync` | ✅ Direct replacement |
| `BulkUpdateAsync` | `BulkUpdateAsync` | ✅ Direct replacement |
| `BulkDeleteAsync` | `BulkDeleteAsync` | ✅ Direct replacement |
| `BulkInsertOrUpdateAsync` | `BulkInsertOrUpdateAsync` | ✅ Direct replacement |
| `BulkInsertOrUpdateOrDeleteAsync` | Not yet implemented | ⚠️ Use separate calls |
| `BulkReadAsync` | Not yet implemented | ⚠️ Use EF Core queries |
| `SetOutputIdentity` | `SetOutputIdentity` | ✅ Same behavior |
| `BatchSize` | `BatchSize` | ✅ Same behavior |
| `BulkCopyTimeout` | `BulkCopyTimeout` | ✅ Same behavior |
| `EnableStreaming` | `EnableStreaming` | ✅ Same behavior |
| `PropertiesToInclude` | `PropertiesToInclude` | ✅ Same behavior |
| `PropertiesToExclude` | `PropertiesToExclude` | ✅ Same behavior |
| `UpdateByProperties` | `UpdateByProperties` | ✅ Same behavior |
| `PreserveInsertOrder` | `PreserveInsertOrder` | ✅ Same behavior |
| `WithHoldlock` | Not implemented | ⚠️ Use transactions |
| `UseTempDB` | Always uses temp tables | ✅ Similar behavior |
| `SqlBulkCopyOptions` | `UseTableLock` | ⚠️ Limited options |
| `TrackingEntities` | Not implemented | ⚠️ Manual tracking |
| `IncludeGraph` | Not implemented | ⚠️ Insert entities separately |

## Breaking Changes

### 1. Configuration Pattern

The configuration is now passed as an object instead of an action delegate:

```csharp
// Before
config => config.SetOutputIdentity = true

// After
new BulkConfig { SetOutputIdentity = true }
```

### 2. Some Advanced Features Not Available

The following features are not yet implemented:
- `BulkInsertOrUpdateOrDeleteAsync`
- `BulkReadAsync`
- `IncludeGraph` for related entities
- Shadow properties

### 3. SQL Server Only (Currently)

EFCore.Native.BulkOperations currently supports SQL Server only. PostgreSQL and MySQL support is planned.

## Performance Considerations

### Similar Performance

Both libraries use SqlBulkCopy under the hood, so performance is comparable:

| Operation | EFCore.BulkExtensions | EFCore.Native |
|-----------|----------------------|---------------|
| Insert 10K | ~200ms | ~200ms |
| Update 10K | ~250ms | ~250ms |
| Delete 10K | ~150ms | ~150ms |
| Upsert 10K | ~300ms | ~300ms |

### Memory Usage

EFCore.Native.BulkOperations uses streaming by default, which reduces memory usage for large datasets.

### Batch Size Tuning

For optimal performance, tune the `BatchSize` based on your workload:

```csharp
// Small records, fast network
new BulkConfig { BatchSize = 10000 }

// Large records or slow network
new BulkConfig { BatchSize = 1000 }
```

## Troubleshooting

### Common Issues

#### 1. "Entity type is not part of the model"

**Cause:** The entity type is not registered in your DbContext.

**Solution:** Ensure the entity is included in your DbContext:
```csharp
public DbSet<Product> Products { get; set; }
```

#### 2. "BulkInsert is only supported for SQL Server"

**Cause:** Using a non-SQL Server database.

**Solution:** EFCore.Native.BulkOperations currently only supports SQL Server. Support for other databases is planned.

#### 3. Identity values not populated

**Cause:** `SetOutputIdentity` not enabled.

**Solution:** Enable identity retrieval:
```csharp
await context.BulkInsertAsync(entities, new BulkConfig { SetOutputIdentity = true });
```

#### 4. Timeout errors with large datasets

**Cause:** Default timeout too short for large operations.

**Solution:** Increase the timeout:
```csharp
await context.BulkInsertAsync(entities, new BulkConfig { BulkCopyTimeout = 300 });
```

#### 5. Transaction not rolling back

**Cause:** Transaction not properly associated.

**Solution:** Use EF Core's transaction:
```csharp
await using var transaction = await context.Database.BeginTransactionAsync();
try
{
    await context.BulkInsertAsync(entities);
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### Getting Help

If you encounter issues:
1. Check the [GitHub Issues](https://github.com/R8110/EFCore-Native-BulkOperations/issues)
2. Create a new issue with a minimal reproduction
3. Include EF Core version, .NET version, and SQL Server version

## Summary

Migration from EFCore.BulkExtensions is straightforward:

1. ✅ Replace the NuGet package
2. ✅ Update using statements
3. ✅ Convert action delegates to BulkConfig objects
4. ✅ Test your application

Most code will work with minimal changes!
