using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using CommentsVS.Benchmarks;

// Run all benchmarks when no args provided, or filter by args
if (args.Length == 0)
{
    Console.WriteLine("CommentsVS Benchmarks");
    Console.WriteLine("=====================");
    Console.WriteLine();
    Console.WriteLine("Available benchmark classes:");
    Console.WriteLine("  1. ParserBenchmarks      - XmlDocCommentParser performance");
    Console.WriteLine("  2. RendererBenchmarks    - XmlDocCommentRenderer performance");
    Console.WriteLine("  3. LinkAnchorParserBenchmarks - LINK: reference parsing");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run -c Release                    # Run all benchmarks");
    Console.WriteLine("  dotnet run -c Release -- --filter *Parser*   # Filter by name");
    Console.WriteLine("  dotnet run -c Release -- --list flat     # List all benchmarks");
    Console.WriteLine();

    // Run all benchmarks
    var config = DefaultConfig.Instance;
    BenchmarkRunner.Run([
        typeof(ParserBenchmarks),
        typeof(RendererBenchmarks),
        typeof(LinkAnchorParserBenchmarks)
    ], config);
}
else
{
    // Use BenchmarkSwitcher for command-line filtering
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
