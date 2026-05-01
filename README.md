# EFCore.Native.BulkOperations

[![Build](https://github.com/R8110/EFCore-Native-BulkOperations/actions/workflows/build.yml/badge.svg)](https://github.com/R8110/EFCore-Native-BulkOperations/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/EFCore.Native.BulkOperations.svg)](https://www.nuget.org/packages/EFCore.Native.BulkOperations/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Production-ready, MIT-licensed bulk operations for EF Core using native .NET features.

## Overview

EFCore.Native.BulkOperations provides high-performance bulk operations for Entity Framework Core using native .NET features:

- **BulkInsert** - Uses `SqlBulkCopy` for maximum insert performance
- **BulkUpdate** - Uses temp tables and MERGE statement for efficient updates
- **BulkDelete** - Uses temp tables and MERGE statement for efficient deletes
- **BulkInsertOrUpdate** - Upsert operations using MERGE statement

### Why This Library?

- **100% MIT Licensed** - No licensing concerns for commercial use
- **Native Implementation** - Built on top of `SqlBulkCopy` and EF Core features
- **Drop-in Replacement** - Similar API to popular libraries for easy migration
- **High Performance** - Comparable or better performance than alternatives
- **Identity Support** - Retrieve generated identity values after insert
- **Transaction Support** - Full support for EF Core transactions

## Requirements

- **.NET 6.0** or **.NET 8.0**
- **Entity Framework Core 6.0+** or **8.0+**
- **SQL Server** (other databases planned for future releases)

## Installation

```bash
dotnet add package EFCore.Native.BulkOperations
```

Or via Package Manager Console:
```powershell
Install-Package EFCore.Native.BulkOperations
```

## Quick Start

```csharp
using EFCore.Native.BulkOperations;

// BulkInsert - Insert thousands of records efficiently
var products = GenerateProducts(10000);
await context.BulkInsertAsync(products);

// BulkInsert with identity retrieval
await context.BulkInsertAsync(products, new BulkConfig { SetOutputIdentity = true });
// Now products have their IDs populated

// BulkUpdate - Update multiple records at once
foreach (var product in products)
    product.Price *= 1.1m;
await context.BulkUpdateAsync(products);

// BulkDelete - Delete multiple records efficiently
await context.BulkDeleteAsync(products);

// BulkInsertOrUpdate - Upsert operations
await context.BulkInsertOrUpdateAsync(products);
```

## Configuration Options

```csharp
var config = new BulkConfig
{
    // Retrieve identity values after insert
    SetOutputIdentity = true,
    
    // Batch size for operations (default: 10000)
    BatchSize = 5000,
    
    // Timeout in seconds for bulk copy
    BulkCopyTimeout = 120,
    
    // Enable streaming for large datasets
    EnableStreaming = true,
    
    // Properties to include/exclude
    PropertiesToInclude = new List<string> { "Name", "Price" },
    PropertiesToExclude = new List<string> { "CreatedDate" },
    
    // For upsert: specify match properties
    UpdateByProperties = new List<string> { "ProductCode" },
    
    // Progress notification
    NotifyAfterRows = 1000,
    NotifyAfter = count => Console.WriteLine($"Copied {count} rows")
};

await context.BulkInsertAsync(products, config);
```

## Usage Examples

### Basic Insert

```csharp
var products = Enumerable.Range(1, 10000)
    .Select(i => new Product
    {
        Name = $"Product {i}",
        Price = 9.99m,
        CreatedDate = DateTime.UtcNow
    })
    .ToList();

await context.BulkInsertAsync(products);
```

### Insert with Identity Retrieval

```csharp
var products = GenerateProducts(1000);

// Before: products have Id = 0
await context.BulkInsertAsync(products, new BulkConfig { SetOutputIdentity = true });
// After: products have their database-assigned IDs
```

### Bulk Update

```csharp
var products = await context.Products.Where(p => p.IsActive).ToListAsync();

foreach (var product in products)
{
    product.Price *= 1.1m; // 10% price increase
    product.ModifiedDate = DateTime.UtcNow;
}

await context.BulkUpdateAsync(products);
```

### Bulk Delete

```csharp
var inactiveProducts = await context.Products
    .Where(p => !p.IsActive)
    .ToListAsync();

await context.BulkDeleteAsync(inactiveProducts);
```

### Upsert (Insert or Update)

```csharp
var existingProducts = await context.Products.Take(100).ToListAsync();
foreach (var p in existingProducts)
    p.Price = 99.99m;

var newProducts = GenerateNewProducts(50);
var allProducts = existingProducts.Concat(newProducts).ToList();

await context.BulkInsertOrUpdateAsync(allProducts);
```

### With Transactions

```csharp
await using var transaction = await context.Database.BeginTransactionAsync();

try
{
    await context.BulkInsertAsync(products);
    await context.BulkUpdateAsync(categories);
    
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### Custom Match Properties for Upsert

```csharp
var config = new BulkConfig
{
    UpdateByProperties = new List<string> { "ProductCode" }
};

await context.BulkInsertOrUpdateAsync(products, config);
```

## Performance

The library is designed for high performance:

| Operation | Records | EF Core SaveChanges | Native BulkInsert |
|-----------|---------|---------------------|-------------------|
| Insert    | 1,000   | ~800ms              | ~50ms             |
| Insert    | 10,000  | ~8,000ms            | ~200ms            |
| Insert    | 100,000 | ~80,000ms           | ~2,000ms          |

*Performance varies based on hardware, network, and SQL Server configuration.*

## Supported Features

| Feature | Status |
|---------|--------|
| BulkInsert | ✅ |
| BulkUpdate | ✅ |
| BulkDelete | ✅ |
| BulkInsertOrUpdate (Upsert) | ✅ |
| Identity Value Retrieval | ✅ |
| Transaction Support | ✅ |
| Composite Primary Keys | ✅ |
| Value Converters | ✅ |
| Batch Processing | ✅ |
| Progress Notification | ✅ |
| EF Core 6 | ✅ |
| EF Core 8 | ✅ |
| SQL Server | ✅ |
| PostgreSQL | 🔜 Planned |
| MySQL | 🔜 Planned |

## Project Structure

```
├── src/
│   └── EFCore.Native.BulkOperations/     # Main library
├── tests/
│   └── EFCore.Native.BulkOperations.Tests/   # Unit and integration tests
├── samples/
│   └── EFCore.Native.BulkOperations.Sample/  # Sample application
├── benchmarks/
│   └── EFCore.Native.BulkOperations.Benchmarks/  # Performance benchmarks
└── docs/
    └── MIGRATION.md                      # Migration guide
```

## Migration from EFCore.BulkExtensions

See the [Migration Guide](MIGRATION.md) for detailed instructions on migrating from EFCore.BulkExtensions.

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history.
