# Fast-Mapper Benchmarks

Performance benchmarking suite for the Fast-Mapper library using BenchmarkDotNet.

## Overview

This project contains comprehensive benchmarks to measure and track the performance of Fast-Mapper across various scenarios including:
- Basic object mapping
- Collection mapping
- Custom mapping configurations
- Caching mechanisms
- Memory allocation patterns

## Benchmark Suites

### 1. BasicMappingBenchmarks

Measures performance of fundamental mapping operations.

**Benchmarks:**
- ✅ Map simple object (User → UserDto)
- ✅ Map record type (Contact → ContactDto)
- ✅ Map object with nested properties
- ✅ Map 100 simple objects
- ✅ Map 1,000 simple objects

**Key Metrics:**
- Execution time per operation
- Memory allocations
- Throughput (ops/sec)

### 2. CollectionMappingBenchmarks

Measures performance of collection mapping with various sizes.

**Benchmarks:**
- ✅ Map collection of 10 items
- ✅ Map collection of 100 items
- ✅ Map collection of 1,000 items
- ✅ Map collection of 10,000 items
- ✅ Map 100 objects with nested collections
- ✅ Map 1,000 objects with nested collections
- ✅ Map array to list
- ✅ Map IEnumerable to List

**Key Metrics:**
- Scalability with collection size
- Memory efficiency with large collections
- Capacity pre-allocation benefits

### 3. CustomMappingBenchmarks

Measures performance impact of custom mapping configurations.

**Benchmarks:**
- ✅ Basic mapping (baseline - no customization)
- ✅ Mapping with ForMember
- ✅ Mapping with flattening
- ✅ Mapping with ConvertUsing
- ✅ Mapping with Ignore
- ✅ Complex mapping (ForMember + Flattening)

**Key Metrics:**
- Overhead of custom resolvers
- Performance comparison vs baseline
- Impact of multiple customizations

### 4. CachingBenchmarks

Measures effectiveness of caching mechanisms.

**Benchmarks:**
- ✅ First mapping (cold cache)
- ✅ Repeated mapping (warm cache)
- ✅ Collection mapping - first call
- ✅ Collection mapping - repeated calls
- ✅ Delegate caching benefit
- ✅ Constructor caching benefit

**Key Metrics:**
- Cache hit performance improvement
- Cold vs warm cache comparison
- Caching effectiveness over iterations

### 5. MemoryAllocationBenchmarks

Measures memory allocation patterns and efficiency.

**Benchmarks:**
- ✅ Single object mapping allocation
- ✅ 100 objects mapping allocation
- ✅ Collection of 100 items allocation
- ✅ Collection of 1,000 items allocation
- ✅ Pre-allocated capacity benefit
- ✅ Without pre-allocation (IEnumerable)
- ✅ Repeated mapping memory efficiency

**Key Metrics:**
- Bytes allocated per operation
- Gen 0/1/2 garbage collections
- Memory efficiency improvements

### 6. ComparisonBenchmarks

Compares optimized (warm cache) vs non-optimized (cold start) approaches to demonstrate the value of caching and expression compilation.

**Benchmarks:**
- ✅ Optimized: Warm cache with compiled expressions (baseline)
- ✅ Cold start: New mapper instance each time
- ✅ Optimized: Collection with cached delegates
- ✅ Cold start: Collection without cache
- ✅ Optimized: 10 sequential mappings (cache benefit)
- ✅ Cold start: 10 sequential mappings (no cache)

**Key Metrics:**
- Performance improvement from caching
- Cost of cold starts
- ROI of optimization techniques
- Relative performance ratios

## Running Benchmarks

### Prerequisites
- .NET 9.0 SDK or later
- Release build configuration (required for accurate benchmarks)

### Run All Benchmarks
```bash
cd tests/Fast-Mapper.Benchmarks
dotnet run -c Release
```

Then select option `6` to run all benchmarks.

### Run Specific Benchmark Suite
```bash
cd tests/Fast-Mapper.Benchmarks
dotnet run -c Release
```

Then select the specific benchmark suite:
- `1` - Basic Mapping Benchmarks
- `2` - Collection Mapping Benchmarks
- `3` - Custom Mapping Benchmarks
- `4` - Caching Benchmarks
- `5` - Memory Allocation Benchmarks
- `6` - Comparison Benchmarks (Optimized vs Cold Start)
- `7` - Run All Benchmarks

### Run from Command Line (Direct)
```bash
# Run specific benchmark class
dotnet run -c Release --filter "*BasicMappingBenchmarks*"

# Run all benchmarks
dotnet run -c Release --filter "*"
```

### Run with Custom Configuration
```bash
# Run with specific job configuration
dotnet run -c Release -- --job short

# Run with memory diagnoser only
dotnet run -c Release -- --memory

# Export results to different formats
dotnet run -c Release -- --exporters json,html,csv
```

## Understanding Results

### Benchmark Output

BenchmarkDotNet provides detailed output including:

```
| Method                          | Mean      | Error    | StdDev   | Rank | Gen0   | Allocated |
|-------------------------------- |----------:|---------:|---------:|-----:|-------:|----------:|
| Map simple object               | 123.4 ns  | 2.1 ns   | 1.9 ns   | 1    | 0.0153 | 128 B     |
| Map 100 simple objects          | 12.34 μs  | 0.21 μs  | 0.19 μs  | 2    | 1.5320 | 12.8 KB   |
```

**Columns Explained:**
- **Method**: Benchmark name
- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Rank**: Relative performance ranking (1 = fastest)
- **Gen0/1/2**: Garbage collection counts per 1000 operations
- **Allocated**: Total memory allocated per operation

### Performance Targets

Based on optimizations implemented:

| Scenario | Target | Notes |
|----------|--------|-------|
| Single object mapping | < 200 ns | With warm cache |
| Collection (100 items) | < 20 μs | With capacity pre-allocation |
| Collection (1,000 items) | < 200 μs | Linear scaling expected |
| Collection (10,000 items) | < 2 ms | Linear scaling expected |
| Memory per object | < 200 B | Minimal allocations |

## Optimization Techniques Measured

### 1. Expression Compilation Caching
- Compiled getters/setters cached per property
- Compiled constructors cached per type
- Significant performance improvement on repeated mappings

### 2. Element Type Caching
- Collection element types cached
- Eliminates repeated reflection calls
- Improves collection mapping performance

### 3. Delegate Caching
- Collection mapping delegates cached per type pair
- Reduces delegate creation overhead
- Benefits repeated collection mappings

### 4. Capacity Pre-allocation
- List capacity pre-allocated based on source collection size
- Reduces array resizing operations
- Improves memory efficiency

### 5. Avoiding Reflection
- `Activator.CreateInstance` replaced with compiled expressions
- `MethodInfo.Invoke` replaced with compiled delegates
- Dramatic performance improvement

## Comparing Results

### Baseline Comparison
Use the `Baseline = true` attribute to compare against a baseline:

```csharp
[Benchmark(Baseline = true)]
public UserDto BasicMapping() { ... }

[Benchmark]
public UserDto MappingWithForMember() { ... }
```

Results will show relative performance:
```
| Method                  | Mean     | Ratio |
|------------------------ |---------:|------:|
| BasicMapping            | 123.4 ns | 1.00  |
| MappingWithForMember    | 145.6 ns | 1.18  |
```

### Tracking Performance Over Time
- Run benchmarks before and after changes
- Compare results to identify regressions
- Use BenchmarkDotNet's result comparison tools

## Best Practices

### 1. Always Use Release Configuration
```bash
dotnet run -c Release
```
Debug builds include additional checks that skew results.

### 2. Close Other Applications
Minimize background processes to reduce noise in measurements.

### 3. Run Multiple Iterations
BenchmarkDotNet automatically runs multiple iterations and warmup cycles.

### 4. Use Memory Diagnoser
```csharp
[MemoryDiagnoser]
public class MyBenchmarks { ... }
```
Provides allocation and GC statistics.

### 5. Order Results
```csharp
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class MyBenchmarks { ... }
```
Makes results easier to interpret.

## Continuous Integration

### Running in CI/CD
```bash
# Run benchmarks and export results
dotnet run -c Release --project tests/Fast-Mapper.Benchmarks -- --exporters json

# Compare with baseline
dotnet run -c Release --project tests/Fast-Mapper.Benchmarks -- --filter "*" --baseline
```

### Performance Regression Detection
- Store benchmark results as artifacts
- Compare against previous runs
- Fail build if performance degrades beyond threshold

## Troubleshooting

### Issue: Benchmarks take too long
**Solution**: Use `--job short` for faster iterations during development:
```bash
dotnet run -c Release -- --job short
```

### Issue: Results are inconsistent
**Solution**: 
- Close background applications
- Disable CPU frequency scaling
- Run on a dedicated machine

### Issue: Out of memory
**Solution**: Run benchmark suites individually instead of all at once.

## Dependencies

- **BenchmarkDotNet** (v0.15.6+) - Benchmarking framework
- **Fast-Mapper.Core** - Library under test
- **Fast-Mapper.Sample** - Sample models for benchmarking

## Contributing

When adding new benchmarks:
1. Create a new benchmark class in the appropriate category
2. Use `[MemoryDiagnoser]` attribute
3. Include `[GlobalSetup]` for initialization
4. Add descriptive names with `Description` parameter
5. Update this README with new benchmarks

## Resources

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [Performance Best Practices](https://docs.microsoft.com/en-us/dotnet/framework/performance/)
- [.NET Performance Tips](https://docs.microsoft.com/en-us/dotnet/core/performance/)

