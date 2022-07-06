// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BenchmarkDotNet.Running;

namespace Benchmarks
{
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            var summaryV2Remote = BenchmarkRunner.Run<BenchmarksV2RemoteRepo>();
            var summaryV3Remote = BenchmarkRunner.Run<BenchmarksV3RemoteRepo>();
            var summaryV2Local = BenchmarkRunner.Run<BenchmarksV2LocalRepo>();
            var summaryV3Local = BenchmarkRunner.Run<BenchmarksV3LocalRepo>();
        }
    }
}