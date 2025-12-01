using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFCore.Native.BulkOperations.Tests;

/// <summary>
/// Integration tests for bulk operations using SQL Server.
/// </summary>
[Collection("SqlServer")]
public class BulkOperationsIntegrationTests
{
    private readonly SqlServerFixture _fixture;

    public BulkOperationsIntegrationTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BulkInsertAsync_WithEmptyCollection_ShouldNotThrow()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var products = Array.Empty<Product>();

        // Act & Assert
        await context.Invoking(c => c.BulkInsertAsync(products))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task BulkInsertAsync_WithProducts_ShouldInsertAllRecords()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        var products = Enumerable.Range(1, 100)
            .Select(i => new Product
            {
                Name = $"Product {i}",
                Description = $"Description for product {i}",
                Price = 10.99m + i,
                Quantity = i * 10,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                ProductCode = Guid.NewGuid(),
                CategoryId = 1
            })
            .ToList();

        // Act
        await context.BulkInsertAsync(products);

        // Assert
        var count = await context.Products.CountAsync();
        count.Should().Be(100);
    }

    [Fact]
    public async Task BulkInsertAsync_WithSetOutputIdentity_ShouldPopulateIds()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        var products = Enumerable.Range(1, 10)
            .Select(i => new Product
            {
                Name = $"Product {i}",
                Price = 10.99m,
                Quantity = 1,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                ProductCode = Guid.NewGuid(),
                CategoryId = 1
            })
            .ToList();

        // Act
        await context.BulkInsertAsync(products, new BulkConfig { SetOutputIdentity = true });

        // Assert
        products.All(p => p.Id > 0).Should().BeTrue();
        products.Select(p => p.Id).Distinct().Count().Should().Be(10);
    }

    [Fact]
    public async Task BulkUpdateAsync_WithEmptyCollection_ShouldNotThrow()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var products = Array.Empty<Product>();

        // Act & Assert
        await context.Invoking(c => c.BulkUpdateAsync(products))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task BulkUpdateAsync_ShouldUpdateAllRecords()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        var products = Enumerable.Range(1, 10)
            .Select(i => new Product
            {
                Name = $"Product {i}",
                Price = 10.99m,
                Quantity = 1,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                ProductCode = Guid.NewGuid(),
                CategoryId = 1
            })
            .ToList();

        await context.BulkInsertAsync(products, new BulkConfig { SetOutputIdentity = true });

        // Modify products
        foreach (var product in products)
        {
            product.Price = 99.99m;
            product.ModifiedDate = DateTime.UtcNow;
        }

        // Act
        await context.BulkUpdateAsync(products);

        // Assert
        var updatedProducts = await context.Products.ToListAsync();
        updatedProducts.All(p => p.Price == 99.99m).Should().BeTrue();
        updatedProducts.All(p => p.ModifiedDate.HasValue).Should().BeTrue();
    }

    [Fact]
    public async Task BulkDeleteAsync_WithEmptyCollection_ShouldNotThrow()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var products = Array.Empty<Product>();

        // Act & Assert
        await context.Invoking(c => c.BulkDeleteAsync(products))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task BulkDeleteAsync_ShouldDeleteAllRecords()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        var products = Enumerable.Range(1, 10)
            .Select(i => new Product
            {
                Name = $"Product {i}",
                Price = 10.99m,
                Quantity = 1,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                ProductCode = Guid.NewGuid(),
                CategoryId = 1
            })
            .ToList();

        await context.BulkInsertAsync(products, new BulkConfig { SetOutputIdentity = true });

        // Act
        await context.BulkDeleteAsync(products);

        // Assert
        var count = await context.Products.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task BulkInsertOrUpdateAsync_WithEmptyCollection_ShouldNotThrow()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var products = Array.Empty<Product>();

        // Act & Assert
        await context.Invoking(c => c.BulkInsertOrUpdateAsync(products))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task BulkInsertOrUpdateAsync_WithNewAndExisting_ShouldUpsertCorrectly()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        // Insert initial products
        var existingProducts = Enumerable.Range(1, 5)
            .Select(i => new Product
            {
                Name = $"Existing Product {i}",
                Price = 10.99m,
                Quantity = 1,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                ProductCode = Guid.NewGuid(),
                CategoryId = 1
            })
            .ToList();

        await context.BulkInsertAsync(existingProducts, new BulkConfig { SetOutputIdentity = true });

        // Modify existing products
        foreach (var product in existingProducts)
        {
            product.Price = 99.99m;
        }

        // Add new products
        var allProducts = existingProducts.Concat(Enumerable.Range(6, 5)
            .Select(i => new Product
            {
                Name = $"New Product {i}",
                Price = 50.00m,
                Quantity = 1,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                ProductCode = Guid.NewGuid(),
                CategoryId = 1
            }))
            .ToList();

        // Act
        await context.BulkInsertOrUpdateAsync(allProducts);

        // Assert
        var allDbProducts = await context.Products.ToListAsync();
        allDbProducts.Should().HaveCount(10);
        allDbProducts.Where(p => p.Name.StartsWith("Existing")).All(p => p.Price == 99.99m).Should().BeTrue();
        allDbProducts.Where(p => p.Name.StartsWith("New")).All(p => p.Price == 50.00m).Should().BeTrue();
    }

    [Fact]
    public async Task BulkInsertAsync_WithCompositeKey_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM OrderItems");

        var orderItems = Enumerable.Range(1, 10)
            .Select(i => new OrderItem
            {
                OrderId = i,
                ProductId = i,
                Quantity = i * 2,
                UnitPrice = 10.50m * i
            })
            .ToList();

        // Act
        await context.BulkInsertAsync(orderItems);

        // Assert
        var count = await context.OrderItems.CountAsync();
        count.Should().Be(10);
    }

    [Fact]
    public async Task BulkInsertAsync_WithGuidPrimaryKey_ShouldWork()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Orders");

        var orders = Enumerable.Range(1, 10)
            .Select(i => new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = i,
                OrderDate = DateTime.UtcNow,
                TotalAmount = 100.00m * i,
                Status = "Pending"
            })
            .ToList();

        // Act
        await context.BulkInsertAsync(orders);

        // Assert
        var count = await context.Orders.CountAsync();
        count.Should().Be(10);
    }

    [Fact]
    public async Task BulkInsertAsync_WithLargeDataset_ShouldWorkWithBatching()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        var products = Enumerable.Range(1, 5000)
            .Select(i => new Product
            {
                Name = $"Product {i}",
                Description = $"Description for product {i}",
                Price = 10.99m + i,
                Quantity = i * 10,
                CreatedDate = DateTime.UtcNow,
                IsActive = i % 2 == 0,
                ProductCode = Guid.NewGuid(),
                CategoryId = 1
            })
            .ToList();

        // Act
        await context.BulkInsertAsync(products, new BulkConfig { BatchSize = 1000 });

        // Assert
        var count = await context.Products.CountAsync();
        count.Should().Be(5000);
    }

    [Fact]
    public async Task BulkOperations_WithTransaction_ShouldRollbackOnError()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

        var products = Enumerable.Range(1, 10)
            .Select(i => new Product
            {
                Name = $"Product {i}",
                Price = 10.99m,
                Quantity = 1,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                ProductCode = Guid.NewGuid(),
                CategoryId = 1
            })
            .ToList();

        // Act
        using var transaction = await context.Database.BeginTransactionAsync();
        await context.BulkInsertAsync(products, new BulkConfig { SetOutputIdentity = true });
        
        var countBeforeRollback = await context.Products.CountAsync();
        countBeforeRollback.Should().Be(10);

        await transaction.RollbackAsync();

        // Assert - Need fresh context to see rollback effect
        using var context2 = _fixture.CreateContext();
        var countAfterRollback = await context2.Products.CountAsync();
        countAfterRollback.Should().Be(0);
    }
}
