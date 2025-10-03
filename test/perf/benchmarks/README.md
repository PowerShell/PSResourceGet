# Micro Benchmarks

This folder contains micro benchmarks that test the performance of PSResourceGet.

## Quick Start

You can run the benchmarks directly using `dotnet run` in this directory:

1. To run the benchmarks in Interactive Mode, where you will be asked which benchmark(s) to run:

   ```bash
   dotnet run -c Release -f net8.0
   ```

1. To list all available benchmarks ([read more](https://github.com/dotnet/performance/blob/main/docs/benchmarkdotnet.md#Listing-the-Benchmarks)):

   ```bash
   dotnet run -c Release -f net8.0 --list [flat/tree]
   ```

1. To filter the benchmarks using a glob pattern applied to `namespace.typeName.methodName` ([read more](https://github.com/dotnet/performance/blob/main/docs/benchmarkdotnet.md#Filtering-the-Benchmarks)]):

   ```bash
   dotnet run -c Release -f net8.0 --filter *script* --list flat
   ```

1. To profile the benchmarked code and produce an ETW Trace file ([read more](https://github.com/dotnet/performance/blob/main/docs/benchmarkdotnet.md#Profiling))

   ```bash
   dotnet run -c Release -f net8.0 --filter *script* --profiler ETW
   ```

## References

- [Getting started with BenchmarkDotNet](https://benchmarkdotnet.org/articles/guides/getting-started.html)
- [Micro-benchmark Design Guidelines](https://github.com/dotnet/performance/blob/main/docs/microbenchmark-design-guidelines.md)
- [Adam SITNIK: Powerful benchmarking in .NET](https://www.youtube.com/watch?v=pdcrSG4tOLI&t=351s)
