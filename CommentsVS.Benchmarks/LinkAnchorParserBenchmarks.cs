using BenchmarkDotNet.Attributes;
using CommentsVS.Services;

namespace CommentsVS.Benchmarks;

/// <summary>
/// Benchmarks for LinkAnchorParser - tests LINK: reference parsing performance.
/// </summary>
[MemoryDiagnoser]
public class LinkAnchorParserBenchmarks
{
    private string _noLinkLine = null!;
    private string _simpleLinkLine = null!;
    private string _complexLinkLine = null!;
    private string _multipleLinkLine = null!;
    private string[] _fileWithLinks = null!;
    private string[] _fileWithoutLinks = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Line without any LINK references (common case - should be fast)
        _noLinkLine = "    /// This is a regular comment line without any special references.";

        // Simple link reference
        _simpleLinkLine = "    // See LINK: Services/UserService.cs for implementation details.";

        // Complex link with line number and anchor
        _complexLinkLine = "    // LINK: Database/Schema.sql:45-67#create-tables and LINK: ./config.json:10#connection-string";

        // Multiple links on one line
        _multipleLinkLine = "    // Related: LINK: Models/User.cs LINK: Models/Role.cs:20 LINK: #local-anchor LINK: ../tests/UserTests.cs:100-150#setup";

        // Simulated file with scattered links
        _fileWithLinks = GenerateFileWithLinks(totalLines: 500, linkLines: 50);

        // File without any links (baseline)
        _fileWithoutLinks = GenerateFileWithoutLinks(totalLines: 500);
    }

    private static string[] GenerateFileWithLinks(int totalLines, int linkLines)
    {
        var lines = new string[totalLines];
        var linkInterval = totalLines / linkLines;

        for (var i = 0; i < totalLines; i++)
        {
            if (i % linkInterval == 0 && i > 0)
            {
                lines[i] = $"    // See LINK: Services/Service{i}.cs:10-20#method-{i} for details";
            }
            else
            {
                lines[i] = $"    public void Method{i}() {{ /* implementation */ }}";
            }
        }

        return lines;
    }

    private static string[] GenerateFileWithoutLinks(int totalLines)
    {
        var lines = new string[totalLines];
        for (var i = 0; i < totalLines; i++)
        {
            lines[i] = $"    /// This is documentation line {i} with no special syntax.";
        }
        return lines;
    }

    [Benchmark(Description = "Parse line without LINK (fast path)")]
    public IReadOnlyList<LinkAnchorInfo> ParseNoLinkLine()
    {
        return LinkAnchorParser.Parse(_noLinkLine);
    }

    [Benchmark(Description = "Parse simple LINK reference")]
    public IReadOnlyList<LinkAnchorInfo> ParseSimpleLinkLine()
    {
        return LinkAnchorParser.Parse(_simpleLinkLine);
    }

    [Benchmark(Description = "Parse complex LINK with line range and anchor")]
    public IReadOnlyList<LinkAnchorInfo> ParseComplexLinkLine()
    {
        return LinkAnchorParser.Parse(_complexLinkLine);
    }

    [Benchmark(Description = "Parse multiple LINK references in one line")]
    public IReadOnlyList<LinkAnchorInfo> ParseMultipleLinkLine()
    {
        return LinkAnchorParser.Parse(_multipleLinkLine);
    }

    [Benchmark(Description = "Parse 500-line file with 50 links")]
    public int ParseFileWithLinks()
    {
        var count = 0;
        foreach (var line in _fileWithLinks)
        {
            count += LinkAnchorParser.Parse(line).Count;
        }
        return count;
    }

    [Benchmark(Description = "Parse 500-line file without links (fast path)")]
    public int ParseFileWithoutLinks()
    {
        var count = 0;
        foreach (var line in _fileWithoutLinks)
        {
            count += LinkAnchorParser.Parse(line).Count;
        }
        return count;
    }
}
