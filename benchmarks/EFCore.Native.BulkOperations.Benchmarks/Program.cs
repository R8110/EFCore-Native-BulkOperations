using BenchmarkDotNet.Running;
using EFCore.Native.BulkOperations.Benchmarks;

Console.WriteLine("==============================================");
Console.WriteLine("   EFCore.Native.BulkOperations Benchmarks");
Console.WriteLine("==============================================");
Console.WriteLine();
Console.WriteLine("NOTE: These benchmarks require SQL Server LocalDB.");
Console.WriteLine("Make sure you have LocalDB installed and running.");
Console.WriteLine();

#if DEBUG
Console.WriteLine("WARNING: Running benchmarks in DEBUG mode.");
Console.WriteLine("For accurate results, run in RELEASE mode:");
Console.WriteLine("  dotnet run -c Release");
Console.WriteLine();
#endif

// Run all benchmarks
var summary = BenchmarkRunner.Run<BulkInsertBenchmarks>();
BenchmarkRunner.Run<BulkUpdateBenchmarks>();
BenchmarkRunner.Run<BulkDeleteBenchmarks>();

Console.WriteLine("Benchmarks completed!");
