using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.EntityFrameworkCore;

namespace EFCore.Native.BulkOperations.Benchmarks;

/// <summary>
/// Benchmarks comparing native bulk operations with standard EF Core operations.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class BulkInsertBenchmarks
{
    // Connection string - update for your environment
    private const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=BulkOperationsBenchmark;Trusted_Connection=True;";

    private List<Product> _products1000 = null!;
    private List<Product> _products10000 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _products1000 = GenerateProducts(1000);
        _products10000 = GenerateProducts(10000);

        // Ensure database exists
        using var context = new BenchmarkDbContext(ConnectionString);
        context.Database.EnsureCreated();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        context.Database.EnsureDeleted();
    }

    private static List<Product> GenerateProducts(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new Product
            {
                Name = $"Product {i}",
                Description = $"Description for product {i}",
                Price = 10.99m + (i % 100),
                Quantity = i * 10,
                CreatedDate = DateTime.UtcNow,
                IsActive = i % 2 == 0,
                ProductCode = Guid.NewGuid()
            })
            .ToList();
    }

    [Benchmark(Description = "EF Core AddRange (1K)")]
    public async Task EFCore_AddRange_1000()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        var products = GenerateProducts(1000);
        context.Products.AddRange(products);
        await context.SaveChangesAsync();
    }

    [Benchmark(Description = "Native BulkInsert (1K)")]
    public async Task Native_BulkInsert_1000()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        var products = GenerateProducts(1000);
        await context.BulkInsertAsync(products);
    }

    [Benchmark(Description = "EF Core AddRange (10K)")]
    public async Task EFCore_AddRange_10000()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        var products = GenerateProducts(10000);
        context.Products.AddRange(products);
        await context.SaveChangesAsync();
    }

    [Benchmark(Description = "Native BulkInsert (10K)")]
    public async Task Native_BulkInsert_10000()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        var products = GenerateProducts(10000);
        await context.BulkInsertAsync(products);
    }

    [Benchmark(Description = "Native BulkInsert with Identity (1K)")]
    public async Task Native_BulkInsert_WithIdentity_1000()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        var products = GenerateProducts(1000);
        await context.BulkInsertAsync(products, new BulkConfig { SetOutputIdentity = true });
    }
}

/// <summary>
/// Benchmarks for update operations.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class BulkUpdateBenchmarks
{
    private const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=BulkOperationsBenchmark;Trusted_Connection=True;";

    [GlobalSetup]
    public void Setup()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        context.Database.EnsureCreated();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        context.Database.EnsureDeleted();
    }

    private static List<Product> GenerateProducts(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new Product
            {
                Name = $"Product {i}",
                Description = $"Description for product {i}",
                Price = 10.99m + (i % 100),
                Quantity = i * 10,
                CreatedDate = DateTime.UtcNow,
                IsActive = i % 2 == 0,
                ProductCode = Guid.NewGuid()
            })
            .ToList();
    }

    [Benchmark(Description = "EF Core Update (1K)")]
    public async Task EFCore_Update_1000()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        // Insert products first
        var products = GenerateProducts(1000);
        context.Products.AddRange(products);
        await context.SaveChangesAsync();

        // Update products
        foreach (var product in products)
        {
            product.Price *= 1.1m;
            product.ModifiedDate = DateTime.UtcNow;
        }
        await context.SaveChangesAsync();
    }

    [Benchmark(Description = "Native BulkUpdate (1K)")]
    public async Task Native_BulkUpdate_1000()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        // Insert products first
        var products = GenerateProducts(1000);
        await context.BulkInsertAsync(products, new BulkConfig { SetOutputIdentity = true });

        // Update products
        foreach (var product in products)
        {
            product.Price *= 1.1m;
            product.ModifiedDate = DateTime.UtcNow;
        }
        await context.BulkUpdateAsync(products);
    }
}

/// <summary>
/// Benchmarks for delete operations.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class BulkDeleteBenchmarks
{
    private const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=BulkOperationsBenchmark;Trusted_Connection=True;";

    [GlobalSetup]
    public void Setup()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        context.Database.EnsureCreated();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        context.Database.EnsureDeleted();
    }

    private static List<Product> GenerateProducts(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new Product
            {
                Name = $"Product {i}",
                Description = $"Description for product {i}",
                Price = 10.99m + (i % 100),
                Quantity = i * 10,
                CreatedDate = DateTime.UtcNow,
                IsActive = i % 2 == 0,
                ProductCode = Guid.NewGuid()
            })
            .ToList();
    }

    [Benchmark(Description = "EF Core RemoveRange (1K)")]
    public async Task EFCore_RemoveRange_1000()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        // Insert products first
        var products = GenerateProducts(1000);
        context.Products.AddRange(products);
        await context.SaveChangesAsync();

        // Delete products
        context.Products.RemoveRange(products);
        await context.SaveChangesAsync();
    }

    [Benchmark(Description = "Native BulkDelete (1K)")]
    public async Task Native_BulkDelete_1000()
    {
        using var context = new BenchmarkDbContext(ConnectionString);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        // Insert products first
        var products = GenerateProducts(1000);
        await context.BulkInsertAsync(products, new BulkConfig { SetOutputIdentity = true });

        // Delete products
        await context.BulkDeleteAsync(products);
    }
}
