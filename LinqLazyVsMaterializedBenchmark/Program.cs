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

namespace LinqLazyVsMaterializedBenchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new ManualConfig()
                .AddJob(Job.ShortRun)
                .AddExporter(MarkdownExporter.GitHub)
                .AddDiagnoser(MemoryDiagnoser.Default)
                .AddDiagnoser(ThreadingDiagnoser.Default)
                .AddLogger(ConsoleLogger.Default)
                .AddColumnProvider(DefaultColumnProviders.Instance);

            BenchmarkRunner.Run<Benchmark1>(config);
        }
    }

    // 大きいデータに対して LINQ クエリを実行し，最初の 10 件だけ使う場合の Benchmark
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

        [Benchmark]
        public void LazySelectWhere()
        {
            var top10Ids = new List<int>();

            var linqResults = _source
                .WhereLazy(x => x.Name == "taro")
                .SelectLazy(x => x.Id);

            int count = 0;
            foreach (var id in linqResults)
            {
                top10Ids.Add(id);
                if (++count >= 10) break;
            }

            top10Ids.Consume(_consumer);
        }

        [Benchmark]
        public void MaterializedSelectWhere()
        {
            var top10Ids = new List<int>();

            var linqResults = _source
                .WhereToArray(x => x.Name == "taro")
                .SelectToArray(x => x.Id);

            int count = 0;
            foreach (var id in linqResults)
            {
                top10Ids.Add(id);
                if (++count >= 10) break;
            }

            top10Ids.Consume(_consumer);
        }

        private static IEnumerable<TestData> GenerateTestData()
        {
            Random random = new Random();
            var names = new [] {"foo", "bar", "hanako", "taro", "jiro", "kyoko"};
            int id = 0;

            while (true)
                yield return new TestData(++id, names[random.Next(0, names.Length - 1)]);
        }

        private record TestData(int Id, string Name);
    }

    public static class SimpleLazyEvalLinq
    {
        public static IEnumerable<TResult> SelectLazy<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> resultSelector)
        {
            foreach (var item in source) yield return resultSelector(item);
        }

        public static IEnumerable<T> WhereLazy<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            foreach (var item in source) if (predicate(item)) yield return item;
        }
    }

    public static class LinqToArray
    {
        public static TResult[] SelectToArray<TSource, TResult>(this TSource[] source, Func<TSource, TResult> resultSelector)
        {
            var results = new TResult[source.Length];
            for (var i = 0; i < source.Length; i++) results[i] = resultSelector(source[i]);
            return results;
        }

        public static T[] WhereToArray<T>(this T[] source, Func<T, bool> predicate)
        {
            var results = new T[source.Length];
            var i = 0;
            for (var j = 0; j < source.Length; j++) if (predicate(source[j])) results[i++] = source[j];

            Array.Resize<T>(ref results, i);
            return results;
        }
    }
}
