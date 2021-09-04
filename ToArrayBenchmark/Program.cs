using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;

namespace ToArrayBenchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new ManualConfig()
                .AddJob(Job.ShortRun)
                .AddExporter(MarkdownExporter.GitHub)
                .AddDiagnoser(MemoryDiagnoser.Default)
                .AddLogger(ConsoleLogger.Default)
                .AddColumnProvider(DefaultColumnProviders.Instance);

            BenchmarkRunner.Run<Benchmark1>(config);
        }
    }

    public class Benchmark1
    {
        private TestData[] _source;

        private readonly Consumer _consumer = new();

        [GlobalSetup]
        public void Setup()
        {
            // 10万件のテストデータを用意
            _source = GenerateTestData().Take(100000).ToArray();
        }

        // materialize しない
        [Benchmark(Baseline = true)]
        public void WithoutMaterialize()
        {
            var results = _source
                .Where(x => x.Name == "taro")
                .Select(x => (x.Id, x.Name.ToUpper()))
                .Where(x => x.Id > 100)
                .Select(x => x.Id);

            // 2 回 results を使う
            results.Consume(_consumer);
            results.Consume(_consumer);
        }

        // 適正な materialize
        [Benchmark]
        public void MaterializeOnce()
        {
            var results = _source
                .Where(x => x.Name == "taro")
                .Select(x => (x.Id, x.Name.ToUpper()))
                .Where(x => x.Id > 100)
                .Select(x => x.Id)
                .ToArray();

            // 2 回 results を使う
            results.Consume(_consumer);
            results.Consume(_consumer);
        }

        // materialize の濫用
        [Benchmark]
        public void MaterializeMany()
        {
            var results = _source
                .ToArray()
                .Where(x => x.Name == "taro")
                .ToArray()
                .Select(x => (x.Id, x.Name.ToUpper()))
                .ToArray()
                .Where(x => x.Id > 100)
                .ToArray()
                .Select(x => x.Id)
                .ToArray();

            // 2 回 results を使う
            results.Consume(_consumer);
            results.Consume(_consumer);
        }

        private static IEnumerable<TestData> GenerateTestData()
        {
            Random random = new Random();
            var names = new[] { "foo", "bar", "hanako", "taro", "jiro", "kyoko" };
            int id = 0;

            while (true)
                yield return new TestData(++id, names[random.Next(0, names.Length - 1)]);
        }

        private record TestData(int Id, string Name);
    }
}
