using BenchmarkDotNet.Running;

namespace benchmarks
{
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<Benchmarks>();
        }
    }
}