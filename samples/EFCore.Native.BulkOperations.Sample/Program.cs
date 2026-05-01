using EFCore.Native.BulkOperations;
using EFCore.Native.BulkOperations.Sample;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

Console.WriteLine("==============================================");
Console.WriteLine("   EFCore.Native.BulkOperations Sample");
Console.WriteLine("==============================================");
Console.WriteLine();

// Connection string - update as needed for your environment
// To run this sample, you need a SQL Server instance
const string connectionString = "Server=(localdb)\\mssqllocaldb;Database=BulkOperationsSample;Trusted_Connection=True;";

// Check if SQL Server is available
Console.WriteLine("NOTE: This sample requires SQL Server (LocalDB or full instance).");
Console.WriteLine($"Connection string: {connectionString}");
Console.WriteLine();
Console.WriteLine("If you don't have SQL Server available, you can see the code examples below.");
Console.WriteLine();

try
{
    // Create DbContext options
    var options = new DbContextOptionsBuilder<SampleDbContext>()
        .UseSqlServer(connectionString)
        .Options;

    using var context = new SampleDbContext(options);
    
    // Ensure database is created
    await context.Database.EnsureCreatedAsync();
    
    Console.WriteLine("Database created successfully!");
    Console.WriteLine();

    // Run examples
    await BulkInsertExample(context);
    await BulkInsertWithIdentityExample(context);
    await BulkUpdateExample(context);
    await BulkDeleteExample(context);
    await BulkInsertOrUpdateExample(context);
    await TransactionExample(context);
    await CustomConfigurationExample(context);
}
catch (Exception ex)
{
    Console.WriteLine($"Could not connect to database: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("Showing code examples only...");
    ShowCodeExamples();
}

Console.WriteLine();
Console.WriteLine("Sample completed!");

// ============ Example Methods ============

static async Task BulkInsertExample(SampleDbContext context)
{
    Console.WriteLine("--- BulkInsert Example ---");
    
    // Clean up
    await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");
    await context.Database.ExecuteSqlRawAsync("DELETE FROM Categories");
    
    // Insert categories first
    var categories = Enumerable.Range(1, 5)
        .Select(i => new Category
        {
            Name = $"Category {i}",
            Description = $"Description for category {i}"
        })
        .ToList();

    await context.BulkInsertAsync(categories, new BulkConfig { SetOutputIdentity = true });
    
    // Create products
    var products = Enumerable.Range(1, 1000)
        .Select(i => new Product
        {
            Name = $"Product {i}",
            Description = $"A great product #{i}",
            Price = 9.99m + (i % 100),
            Quantity = 100 + i,
            CreatedDate = DateTime.UtcNow,
            IsActive = true,
            ProductCode = Guid.NewGuid(),
            CategoryId = categories[i % categories.Count].Id
        })
        .ToList();

    var sw = Stopwatch.StartNew();
    await context.BulkInsertAsync(products);
    sw.Stop();

    var count = await context.Products.CountAsync();
    Console.WriteLine($"Inserted {count} products in {sw.ElapsedMilliseconds}ms");
    Console.WriteLine();
}

static async Task BulkInsertWithIdentityExample(SampleDbContext context)
{
    Console.WriteLine("--- BulkInsert with Identity Retrieval Example ---");
    
    await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");
    
    var firstCategory = await context.Categories.FirstAsync();
    
    var products = Enumerable.Range(1, 100)
        .Select(i => new Product
        {
            Name = $"Product with ID {i}",
            Price = 19.99m,
            Quantity = 50,
            CreatedDate = DateTime.UtcNow,
            IsActive = true,
            ProductCode = Guid.NewGuid(),
            CategoryId = firstCategory.Id
        })
        .ToList();

    // Before insert, all IDs are 0
    Console.WriteLine($"Before insert - First product ID: {products[0].Id}");

    await context.BulkInsertAsync(products, new BulkConfig { SetOutputIdentity = true });

    // After insert, IDs are populated
    Console.WriteLine($"After insert - First product ID: {products[0].Id}");
    Console.WriteLine($"After insert - Last product ID: {products[^1].Id}");
    Console.WriteLine();
}

static async Task BulkUpdateExample(SampleDbContext context)
{
    Console.WriteLine("--- BulkUpdate Example ---");
    
    // Get existing products
    var products = await context.Products.Take(100).ToListAsync();
    
    // Modify products
    foreach (var product in products)
    {
        product.Price *= 1.1m; // 10% price increase
        product.ModifiedDate = DateTime.UtcNow;
    }

    var sw = Stopwatch.StartNew();
    await context.BulkUpdateAsync(products);
    sw.Stop();

    Console.WriteLine($"Updated {products.Count} products in {sw.ElapsedMilliseconds}ms");
    
    // Verify update
    var updatedProduct = await context.Products.FindAsync(products[0].Id);
    Console.WriteLine($"Verified - Product {updatedProduct!.Id} new price: {updatedProduct.Price:C}");
    Console.WriteLine();
}

static async Task BulkDeleteExample(SampleDbContext context)
{
    Console.WriteLine("--- BulkDelete Example ---");
    
    // Get some products to delete
    var productsToDelete = await context.Products.Take(50).ToListAsync();
    var initialCount = await context.Products.CountAsync();

    var sw = Stopwatch.StartNew();
    await context.BulkDeleteAsync(productsToDelete);
    sw.Stop();

    var finalCount = await context.Products.CountAsync();
    Console.WriteLine($"Deleted {initialCount - finalCount} products in {sw.ElapsedMilliseconds}ms");
    Console.WriteLine();
}

static async Task BulkInsertOrUpdateExample(SampleDbContext context)
{
    Console.WriteLine("--- BulkInsertOrUpdate (Upsert) Example ---");
    
    var firstCategory = await context.Categories.FirstAsync();
    
    // Get existing products
    var existingProducts = await context.Products.Take(20).ToListAsync();
    
    // Modify existing products
    foreach (var product in existingProducts)
    {
        product.Description = "Updated via upsert";
        product.Price = 29.99m;
    }
    
    // Add new products
    var newProducts = Enumerable.Range(1, 10)
        .Select(i => new Product
        {
            Name = $"New Upsert Product {i}",
            Description = "Created via upsert",
            Price = 39.99m,
            Quantity = 25,
            CreatedDate = DateTime.UtcNow,
            IsActive = true,
            ProductCode = Guid.NewGuid(),
            CategoryId = firstCategory.Id
        })
        .ToList();

    var allProducts = existingProducts.Concat(newProducts).ToList();

    var sw = Stopwatch.StartNew();
    await context.BulkInsertOrUpdateAsync(allProducts);
    sw.Stop();

    Console.WriteLine($"Upserted {allProducts.Count} products ({existingProducts.Count} updated, {newProducts.Count} inserted) in {sw.ElapsedMilliseconds}ms");
    Console.WriteLine();
}

static async Task TransactionExample(SampleDbContext context)
{
    Console.WriteLine("--- Transaction Example ---");
    
    var firstCategory = await context.Categories.FirstAsync();
    
    var products = Enumerable.Range(1, 10)
        .Select(i => new Product
        {
            Name = $"Transaction Product {i}",
            Price = 15.99m,
            Quantity = 10,
            CreatedDate = DateTime.UtcNow,
            IsActive = true,
            ProductCode = Guid.NewGuid(),
            CategoryId = firstCategory.Id
        })
        .ToList();

    // Begin transaction
    await using var transaction = await context.Database.BeginTransactionAsync();
    
    try
    {
        await context.BulkInsertAsync(products, new BulkConfig { SetOutputIdentity = true });
        Console.WriteLine($"Inserted {products.Count} products within transaction");
        
        // Simulate a condition that might cause rollback
        // In a real scenario, this could be a validation failure
        var shouldRollback = false;
        
        if (shouldRollback)
        {
            await transaction.RollbackAsync();
            Console.WriteLine("Transaction rolled back");
        }
        else
        {
            await transaction.CommitAsync();
            Console.WriteLine("Transaction committed successfully");
        }
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        Console.WriteLine($"Transaction rolled back due to error: {ex.Message}");
    }
    
    Console.WriteLine();
}

static async Task CustomConfigurationExample(SampleDbContext context)
{
    Console.WriteLine("--- Custom Configuration Example ---");
    
    var firstCategory = await context.Categories.FirstAsync();
    
    var products = Enumerable.Range(1, 500)
        .Select(i => new Product
        {
            Name = $"Config Product {i}",
            Description = "With custom configuration",
            Price = 12.99m,
            Quantity = 5,
            CreatedDate = DateTime.UtcNow,
            IsActive = true,
            ProductCode = Guid.NewGuid(),
            CategoryId = firstCategory.Id
        })
        .ToList();

    long rowsCopied = 0;
    
    var config = new BulkConfig
    {
        BatchSize = 100,
        BulkCopyTimeout = 60,
        EnableStreaming = true,
        SetOutputIdentity = true,
        NotifyAfterRows = 100,
        NotifyAfter = count =>
        {
            rowsCopied = count;
            Console.WriteLine($"  Progress: {count} rows copied...");
        }
    };

    await context.BulkInsertAsync(products, config);
    
    Console.WriteLine($"Final: Inserted {products.Count} products with custom config");
    Console.WriteLine($"All products have IDs assigned: {products.All(p => p.Id > 0)}");
    Console.WriteLine();
}

static void ShowCodeExamples()
{
    Console.WriteLine(@"
=== Code Examples ===

1. Basic BulkInsert:
   var products = GetProducts();
   await context.BulkInsertAsync(products);

2. BulkInsert with Identity Retrieval:
   await context.BulkInsertAsync(products, new BulkConfig { SetOutputIdentity = true });
   // Now products have their IDs populated

3. BulkUpdate:
   foreach (var product in products)
       product.Price *= 1.1m;
   await context.BulkUpdateAsync(products);

4. BulkDelete:
   var productsToDelete = await context.Products.Where(p => !p.IsActive).ToListAsync();
   await context.BulkDeleteAsync(productsToDelete);

5. BulkInsertOrUpdate (Upsert):
   var allProducts = existingProducts.Concat(newProducts).ToList();
   await context.BulkInsertOrUpdateAsync(allProducts);

6. With Transaction:
   await using var transaction = await context.Database.BeginTransactionAsync();
   try
   {
       await context.BulkInsertAsync(products);
       await context.BulkUpdateAsync(otherProducts);
       await transaction.CommitAsync();
   }
   catch
   {
       await transaction.RollbackAsync();
   }

7. Custom Configuration:
   var config = new BulkConfig
   {
       BatchSize = 5000,
       BulkCopyTimeout = 120,
       SetOutputIdentity = true,
       PropertiesToExclude = new List<string> { ""CreatedDate"" }
   };
   await context.BulkInsertAsync(products, config);
");
}
