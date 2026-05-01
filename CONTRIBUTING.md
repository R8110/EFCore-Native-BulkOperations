# Contributing to EFCore.Native.BulkOperations

Thank you for your interest in contributing to EFCore.Native.BulkOperations! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Submitting Changes](#submitting-changes)
- [Coding Standards](#coding-standards)
- [Testing](#testing)
- [Documentation](#documentation)

## Code of Conduct

This project adheres to a code of conduct. By participating, you are expected to:

- Be respectful and inclusive
- Be patient with new contributors
- Focus on constructive feedback
- Help others learn and grow

## Getting Started

### Prerequisites

- .NET 6.0 SDK or later
- .NET 8.0 SDK (for running all targets)
- SQL Server (LocalDB, Express, or full version) for integration tests
- Docker (optional, for Testcontainers)
- Visual Studio 2022, VS Code, or JetBrains Rider

### Fork and Clone

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/EFCore-Native-BulkOperations.git
   cd EFCore-Native-BulkOperations
   ```
3. Add the upstream remote:
   ```bash
   git remote add upstream https://github.com/R8110/EFCore-Native-BulkOperations.git
   ```

## Development Setup

### Building the Project

```bash
# Restore dependencies
dotnet restore

# Build all projects
dotnet build

# Build in Release mode
dotnet build -c Release
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/EFCore.Native.BulkOperations.Tests
```

**Note:** Integration tests require Docker or SQL Server LocalDB.

### Running Benchmarks

```bash
cd benchmarks/EFCore.Native.BulkOperations.Benchmarks
dotnet run -c Release
```

### Running the Sample

```bash
cd samples/EFCore.Native.BulkOperations.Sample
dotnet run
```

## Submitting Changes

### Creating a Branch

```bash
git checkout -b feature/your-feature-name
# or
git checkout -b fix/your-bug-fix
```

### Commit Guidelines

Follow these commit message guidelines:

- Use present tense ("Add feature" not "Added feature")
- Use imperative mood ("Move cursor to..." not "Moves cursor to...")
- Limit first line to 72 characters
- Reference issues and pull requests when relevant

Example:
```
Add support for PostgreSQL bulk operations

- Implement NpgsqlBulkCopy wrapper
- Add PostgreSQL-specific temp table handling
- Update documentation

Fixes #42
```

### Pull Request Process

1. Update your branch with the latest upstream changes:
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```

2. Ensure all tests pass:
   ```bash
   dotnet test
   ```

3. Push your branch:
   ```bash
   git push origin feature/your-feature-name
   ```

4. Create a Pull Request on GitHub with:
   - Clear description of changes
   - Link to related issues
   - Screenshots/examples if applicable

5. Wait for review and address feedback

## Coding Standards

### C# Style Guidelines

- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use `var` when the type is obvious
- Use meaningful variable and method names
- Keep methods focused and small
- Add XML documentation for public APIs

### Example

```csharp
/// <summary>
/// Performs a bulk insert of entities into the database.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <param name="entities">The entities to insert.</param>
/// <param name="config">Optional configuration for the operation.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task representing the async operation.</returns>
public async Task BulkInsertAsync<T>(
    IEnumerable<T> entities,
    BulkConfig? config = null,
    CancellationToken cancellationToken = default) where T : class
{
    if (entities == null)
        throw new ArgumentNullException(nameof(entities));

    var entityList = entities.ToList();
    if (entityList.Count == 0)
        return;

    // Implementation...
}
```

### File Organization

- One class per file (with some exceptions for small related classes)
- Group related files in folders
- Use meaningful namespaces

## Testing

### Unit Tests

- Test individual components in isolation
- Use mocking when appropriate
- Name tests descriptively: `MethodName_Scenario_ExpectedResult`

Example:
```csharp
[Fact]
public void BulkConfig_DefaultValues_ShouldBeCorrect()
{
    var config = new BulkConfig();

    config.BatchSize.Should().Be(10000);
    config.SetOutputIdentity.Should().BeFalse();
}
```

### Integration Tests

- Test real database operations
- Use Testcontainers for SQL Server
- Clean up after tests

Example:
```csharp
[Fact]
public async Task BulkInsertAsync_WithProducts_ShouldInsertAllRecords()
{
    using var context = _fixture.CreateContext();
    await context.Database.ExecuteSqlRawAsync("DELETE FROM Products");

    var products = GenerateProducts(100);
    await context.BulkInsertAsync(products);

    var count = await context.Products.CountAsync();
    count.Should().Be(100);
}
```

### Test Coverage

- Aim for >80% code coverage
- Focus on critical paths and edge cases
- Don't sacrifice test quality for coverage numbers

## Documentation

### XML Documentation

All public APIs must have XML documentation:

```csharp
/// <summary>
/// Configuration options for bulk operations.
/// </summary>
public class BulkConfig
{
    /// <summary>
    /// Gets or sets whether to retrieve identity values after insert.
    /// Default is false.
    /// </summary>
    public bool SetOutputIdentity { get; set; } = false;
}
```

### README Updates

Update the README.md when:
- Adding new features
- Changing public APIs
- Adding new configuration options
- Fixing documentation errors

### Migration Guide

Update MIGRATION.md when:
- Adding features that differ from EFCore.BulkExtensions
- Changing APIs
- Adding breaking changes

## Questions?

If you have questions, please:
1. Check existing issues and documentation
2. Open a new issue for discussion
3. Be patient and respectful

Thank you for contributing!
