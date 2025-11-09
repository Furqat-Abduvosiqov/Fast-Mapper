using BenchmarkDotNet.Running;
using Fast_Mapper.Benchmarks;

Console.WriteLine("Fast-Mapper Benchmarks");
Console.WriteLine("======================");
Console.WriteLine();
Console.WriteLine("Select benchmark suite to run:");
Console.WriteLine("1. Basic Mapping Benchmarks");
Console.WriteLine("2. Collection Mapping Benchmarks");
Console.WriteLine("3. Custom Mapping Benchmarks");
Console.WriteLine("4. Caching Benchmarks");
Console.WriteLine("5. Memory Allocation Benchmarks");
Console.WriteLine("6. Comparison Benchmarks (Optimized vs Cold Start)");
Console.WriteLine("7. Run All Benchmarks");
Console.WriteLine("0. Exit");
Console.WriteLine();
Console.Write("Enter your choice: ");

var choice = Console.ReadLine();

switch (choice)
{
    case "1":
        BenchmarkRunner.Run<BasicMappingBenchmarks>();
        break;
    case "2":
        BenchmarkRunner.Run<CollectionMappingBenchmarks>();
        break;
    case "3":
        BenchmarkRunner.Run<CustomMappingBenchmarks>();
        break;
    case "4":
        BenchmarkRunner.Run<CachingBenchmarks>();
        break;
    case "5":
        BenchmarkRunner.Run<MemoryAllocationBenchmarks>();
        break;
    case "6":
        BenchmarkRunner.Run<ComparisonBenchmarks>();
        break;
    case "7":
        BenchmarkRunner.Run<BasicMappingBenchmarks>();
        BenchmarkRunner.Run<CollectionMappingBenchmarks>();
        BenchmarkRunner.Run<CustomMappingBenchmarks>();
        BenchmarkRunner.Run<CachingBenchmarks>();
        BenchmarkRunner.Run<MemoryAllocationBenchmarks>();
        BenchmarkRunner.Run<ComparisonBenchmarks>();
        break;
    case "0":
        Console.WriteLine("Exiting...");
        break;
    default:
        Console.WriteLine("Invalid choice. Exiting...");
        break;
}
