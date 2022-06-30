using System;
using BenchmarkDotNet;
using BenchmarkDotNet.Attributes;

namespace benchmarks
{
    internal class Benchmarks
    {
        [Benchmark]
        public PSResourceInfo Scenario1()
        {
            PSResourceInfo a = null;

            // Implement your benchmark here

            return a;
        }

        [Benchmark]
        public void Scenario2()
        {
            // Implement your benchmark here
        }
    }
}
