// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BenchmarkDotNet.Running;

namespace benchmarks
{
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            var summaryV2 = BenchmarkRunner.Run<BenchmarksV2>();
            var summaryV3 = BenchmarkRunner.Run<BenchmarksV3>();
        }
    }
}