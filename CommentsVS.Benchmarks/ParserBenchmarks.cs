using BenchmarkDotNet.Attributes;
using CommentsVS.Services;

namespace CommentsVS.Benchmarks;

/// <summary>
/// Benchmarks for XmlDocCommentParser - tests comment block parsing performance.
/// </summary>
[MemoryDiagnoser]
public class ParserBenchmarks
{
    private XmlDocCommentParser _csharpParser = null!;
    private string[] _smallFile = null!;
    private string[] _mediumFile = null!;
    private string[] _largeFile = null!;
    private string[] _denseCommentsFile = null!;

    [GlobalSetup]
    public void Setup()
    {
        _csharpParser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);

        // Small file: ~50 lines with 2 doc comments
        _smallFile = GenerateFile(commentCount: 2, linesPerComment: 5, codeLinesPerComment: 15);

        // Medium file: ~500 lines with 20 doc comments
        _mediumFile = GenerateFile(commentCount: 20, linesPerComment: 8, codeLinesPerComment: 15);

        // Large file: ~2000 lines with 80 doc comments
        _largeFile = GenerateFile(commentCount: 80, linesPerComment: 8, codeLinesPerComment: 15);

        // Dense comments: many small comments close together
        _denseCommentsFile = GenerateFile(commentCount: 100, linesPerComment: 3, codeLinesPerComment: 3);
    }

    private static string[] GenerateFile(int commentCount, int linesPerComment, int codeLinesPerComment)
    {
        var lines = new List<string>
        {
            "using System;",
            "using System.Collections.Generic;",
            "",
            "namespace TestNamespace",
            "{"
        };

        for (var i = 0; i < commentCount; i++)
        {
            // Add code lines
            for (var j = 0; j < codeLinesPerComment; j++)
            {
                lines.Add($"    public int Property{i}_{j} {{ get; set; }}");
            }

            // Add doc comment
            lines.Add("    /// <summary>");
            for (var j = 0; j < linesPerComment - 2; j++)
            {
                lines.Add($"    /// This is line {j} of the documentation for method {i}.");
            }
            lines.Add("    /// </summary>");

            // Add param tags for some methods
            if (i % 3 == 0)
            {
                lines.Add("    /// <param name=\"value\">The input value to process.</param>");
                lines.Add("    /// <returns>The processed result.</returns>");
            }

            lines.Add($"    public void Method{i}(int value) {{ }}");
            lines.Add("");
        }

        lines.Add("}");
        return [.. lines];
    }

    [Benchmark(Description = "Parse small file (~50 lines, 2 comments)")]
    public IReadOnlyList<XmlDocCommentBlock> ParseSmallFile()
    {
        return _csharpParser.FindAllCommentBlocks(_smallFile);
    }

    [Benchmark(Description = "Parse medium file (~500 lines, 20 comments)")]
    public IReadOnlyList<XmlDocCommentBlock> ParseMediumFile()
    {
        return _csharpParser.FindAllCommentBlocks(_mediumFile);
    }

    [Benchmark(Description = "Parse large file (~2000 lines, 80 comments)")]
    public IReadOnlyList<XmlDocCommentBlock> ParseLargeFile()
    {
        return _csharpParser.FindAllCommentBlocks(_largeFile);
    }

    [Benchmark(Description = "Parse dense comments file (100 small comments)")]
    public IReadOnlyList<XmlDocCommentBlock> ParseDenseCommentsFile()
    {
        return _csharpParser.FindAllCommentBlocks(_denseCommentsFile);
    }
}
