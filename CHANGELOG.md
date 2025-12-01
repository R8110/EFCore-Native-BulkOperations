# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-01-01

### Added

- Initial release of EFCore.Native.BulkOperations
- `BulkInsertAsync` - High-performance bulk insert using SqlBulkCopy
- `BulkUpdateAsync` - Bulk update using temp tables and MERGE
- `BulkDeleteAsync` - Bulk delete using temp tables and MERGE
- `BulkInsertOrUpdateAsync` - Upsert using MERGE statement
- Identity value retrieval with `SetOutputIdentity`
- Transaction support
- Composite primary key support
- Value converter support
- Batch processing with configurable batch size
- Progress notification during bulk operations
- Support for .NET 6.0 and .NET 8.0
- Support for EF Core 6.x and 8.x
- Comprehensive documentation and migration guide
- Sample application with usage examples
- Benchmark project for performance testing
- Integration tests using Testcontainers

### Configuration Options

- `SetOutputIdentity` - Retrieve generated identity values
- `PropertiesToInclude` - Include specific properties only
- `PropertiesToExclude` - Exclude specific properties
- `BatchSize` - Control batch size (default: 10000)
- `BulkCopyTimeout` - Set operation timeout
- `EnableStreaming` - Enable/disable streaming mode
- `PreserveInsertOrder` - Preserve entity order during insert
- `UpdateByProperties` - Custom properties for upsert matching
- `UseTableLock` - Use table lock during insert
- `NotifyAfter` - Progress notification callback
- `NotifyAfterRows` - Notification interval

## [Unreleased]

### Planned

- PostgreSQL support
- MySQL support
- `BulkInsertOrUpdateOrDeleteAsync` for full sync operations
- Shadow property support
- Better composite key handling for upserts
- Performance optimizations
