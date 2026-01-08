using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CommentsVS.ToolWindows;

namespace CommentsVS.Test;

/// <summary>
/// Performance tests for SolutionAnchorScanner file processing.
/// These tests measure and compare sequential vs parallel scanning approaches.
/// </summary>
[TestClass]
public sealed class SolutionScannerPerformanceTests
{
    private AnchorService _anchorService = null!;
    private List<(string FilePath, string Content, string ProjectName)> _testFiles = null!;

    [TestInitialize]
    public void Setup()
    {
        _anchorService = new AnchorService();
        _testFiles = new List<(string, string, string)>();

        // Generate 100 realistic test files with varying sizes and anchor densities
        for (int i = 0; i < 100; i++)
        {
            var content = GenerateTestFile(i);
            _testFiles.Add(($"TestFile{i}.cs", content, "TestProject"));
        }
    }

    private static string GenerateTestFile(int seed)
    {
        var lines = new List<string>();
        var random = new Random(seed);
        int lineCount = 200 + random.Next(800); // 200-1000 lines per file

        for (int i = 0; i < lineCount; i++)
        {
            int lineType = random.Next(100);
            if (lineType < 5) // 5% chance of TODO comment
            {
                lines.Add($"    // TODO: Implement feature {i}");
            }
            else if (lineType < 8) // 3% chance of other anchors
            {
                string[] anchors = { "HACK", "NOTE", "BUG", "FIXME", "REVIEW" };
                lines.Add($"    // {anchors[random.Next(anchors.Length)]}: Fix this at line {i}");
            }
            else if (lineType < 20) // 12% chance of regular comment
            {
                lines.Add($"    // Regular comment at line {i}");
            }
            else if (lineType < 40) // 20% blank lines
            {
                lines.Add("");
            }
            else // 60% code lines
            {
                lines.Add($"    var variable{i} = SomeMethod({i});");
            }
        }

        return string.Join("\r\n", lines);
    }

    [TestMethod]
    public void Baseline_SequentialScan_MeasurePerformance()
    {
        // Warmup
        foreach (var (filePath, content, projectName) in _testFiles.Take(5))
        {
            _anchorService.ScanText(content, filePath, projectName);
        }

        // Measure
        var sw = Stopwatch.StartNew();
        int totalAnchors = 0;

        foreach (var (filePath, content, projectName) in _testFiles)
        {
            var anchors = _anchorService.ScanText(content, filePath, projectName);
            totalAnchors += anchors.Count;
        }

        sw.Stop();

        Console.WriteLine($"=== SEQUENTIAL SCAN BASELINE ===");
        Console.WriteLine($"Files processed: {_testFiles.Count}");
        Console.WriteLine($"Total anchors found: {totalAnchors}");
        Console.WriteLine($"Elapsed time: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"Average per file: {sw.ElapsedMilliseconds / (double)_testFiles.Count:F2} ms");

        Assert.IsTrue(totalAnchors > 0, "Should find some anchors in test files");
    }

    [TestMethod]
    public void Optimized_ParallelScan_MeasurePerformance()
    {
        // Warmup
        foreach (var (filePath, content, projectName) in _testFiles.Take(5))
        {
            _anchorService.ScanText(content, filePath, projectName);
        }

        // Measure
        var sw = Stopwatch.StartNew();
        int totalAnchors = 0;
        object lockObj = new object();

        Parallel.ForEach(_testFiles, (file) =>
        {
            var anchors = _anchorService.ScanText(file.Content, file.FilePath, file.ProjectName);
            lock (lockObj)
            {
                totalAnchors += anchors.Count;
            }
        });

        sw.Stop();

        Console.WriteLine($"=== PARALLEL SCAN ===");
        Console.WriteLine($"Files processed: {_testFiles.Count}");
        Console.WriteLine($"Total anchors found: {totalAnchors}");
        Console.WriteLine($"Elapsed time: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"Average per file: {sw.ElapsedMilliseconds / (double)_testFiles.Count:F2} ms");
        Console.WriteLine($"Processor count: {Environment.ProcessorCount}");

        Assert.IsTrue(totalAnchors > 0, "Should find some anchors in test files");
    }

    [TestMethod]
    public void Optimized_ParallelScanWithInterlocked_MeasurePerformance()
    {
        // Warmup
        foreach (var (filePath, content, projectName) in _testFiles.Take(5))
        {
            _anchorService.ScanText(content, filePath, projectName);
        }

        // Measure
        var sw = Stopwatch.StartNew();
        int totalAnchors = 0;

        Parallel.ForEach(_testFiles, (file) =>
        {
            var anchors = _anchorService.ScanText(file.Content, file.FilePath, file.ProjectName);
            System.Threading.Interlocked.Add(ref totalAnchors, anchors.Count);
        });

        sw.Stop();

        Console.WriteLine($"=== PARALLEL SCAN WITH INTERLOCKED ===");
        Console.WriteLine($"Files processed: {_testFiles.Count}");
        Console.WriteLine($"Total anchors found: {totalAnchors}");
        Console.WriteLine($"Elapsed time: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"Average per file: {sw.ElapsedMilliseconds / (double)_testFiles.Count:F2} ms");
        Console.WriteLine($"Processor count: {Environment.ProcessorCount}");

        Assert.IsTrue(totalAnchors > 0, "Should find some anchors in test files");
    }

    [TestMethod]
    public void Compare_SequentialVsParallel_Performance()
    {
        // Warmup both paths
        foreach (var (filePath, content, projectName) in _testFiles.Take(5))
        {
            _anchorService.ScanText(content, filePath, projectName);
        }

        // Sequential measurement
        var swSequential = Stopwatch.StartNew();
        int sequentialAnchors = 0;

        foreach (var (filePath, content, projectName) in _testFiles)
        {
            var anchors = _anchorService.ScanText(content, filePath, projectName);
            sequentialAnchors += anchors.Count;
        }
        swSequential.Stop();

        // Parallel measurement
        var swParallel = Stopwatch.StartNew();
        int parallelAnchors = 0;

        Parallel.ForEach(_testFiles, (file) =>
        {
            var anchors = _anchorService.ScanText(file.Content, file.FilePath, file.ProjectName);
            System.Threading.Interlocked.Add(ref parallelAnchors, anchors.Count);
        });
        swParallel.Stop();

        // Results
        Console.WriteLine($"=== COMPARISON RESULTS ===");
        Console.WriteLine($"Files processed: {_testFiles.Count}");
        Console.WriteLine($"Sequential time: {swSequential.ElapsedMilliseconds} ms");
        Console.WriteLine($"Parallel time: {swParallel.ElapsedMilliseconds} ms");
        Console.WriteLine($"Speedup: {swSequential.ElapsedMilliseconds / (double)swParallel.ElapsedMilliseconds:F2}x");
        Console.WriteLine($"Processor count: {Environment.ProcessorCount}");

        // Verify correctness
        Assert.AreEqual(sequentialAnchors, parallelAnchors, "Both methods should find the same number of anchors");
    }
}
