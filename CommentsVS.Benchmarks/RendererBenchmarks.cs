using BenchmarkDotNet.Attributes;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using static CommentsVS.Services.RenderedSegment;

namespace CommentsVS.Benchmarks;

/// <summary>
/// Benchmarks for XmlDocCommentRenderer - tests XML doc comment rendering performance.
/// </summary>
[MemoryDiagnoser]
public class RendererBenchmarks
{
    private XmlDocCommentBlock _simpleComment = null!;
    private XmlDocCommentBlock _complexComment = null!;
    private XmlDocCommentBlock _markdownComment = null!;
    private XmlDocCommentBlock _listComment = null!;
    private GitRepositoryInfo _repoInfo = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Simple summary-only comment
        _simpleComment = new XmlDocCommentBlock(
            new Span(0, 100),
            startLine: 0,
            endLine: 2,
            indentation: "    ",
            xmlContent: "<summary>Gets or sets the name of the user.</summary>",
            commentStyle: LanguageCommentStyle.CSharp,
            isMultiLineStyle: false);

        // Complex comment with multiple sections
        _complexComment = new XmlDocCommentBlock(
            new Span(0, 500),
            startLine: 0,
            endLine: 15,
            indentation: "    ",
            xmlContent: """
                <summary>
                Processes the input data and returns a transformed result.
                This method handles various edge cases and provides robust error handling.
                </summary>
                <typeparam name="T">The type of input elements.</typeparam>
                <param name="input">The input collection to process.</param>
                <param name="options">Configuration options for the processing.</param>
                <returns>A new collection containing the transformed elements.</returns>
                <exception cref="ArgumentNullException">Thrown when input is null.</exception>
                <exception cref="InvalidOperationException">Thrown when processing fails.</exception>
                <remarks>
                This method is thread-safe and can be called concurrently.
                Performance may vary based on the size of the input collection.
                </remarks>
                <example>
                var result = processor.Process(items, new Options { Parallel = true });
                </example>
                """,
            commentStyle: LanguageCommentStyle.CSharp,
            isMultiLineStyle: false);

        // Comment with markdown-style formatting
        _markdownComment = new XmlDocCommentBlock(
            new Span(0, 300),
            startLine: 0,
            endLine: 8,
            indentation: "    ",
            xmlContent: """
                <summary>
                Connects to the **database** using the specified _connection string_.
                See <see cref="DatabaseConfig"/> for configuration options.
                Use <c>ConnectionString</c> property to specify the connection.
                This fixes issue #123 and addresses GH-456.
                </summary>
                <param name="connectionString">The `connection string` to use.</param>
                <returns><c>true</c> if connection succeeded; otherwise <c>false</c>.</returns>
                """,
            commentStyle: LanguageCommentStyle.CSharp,
            isMultiLineStyle: false);

        // Comment with list content
        _listComment = new XmlDocCommentBlock(
            new Span(0, 400),
            startLine: 0,
            endLine: 12,
            indentation: "    ",
            xmlContent: """
                <summary>
                Validates the input according to the following rules:
                <list type="bullet">
                <item><description>Input must not be null or empty</description></item>
                <item><description>Length must be between 1 and 100 characters</description></item>
                <item><description>Must contain only alphanumeric characters</description></item>
                </list>
                </summary>
                <param name="input">The string to validate.</param>
                <returns>
                <c>true</c> if all validation rules pass; otherwise <c>false</c>.
                </returns>
                """,
            commentStyle: LanguageCommentStyle.CSharp,
            isMultiLineStyle: false);

        // Setup repo info for issue reference resolution
        _repoInfo = new GitRepositoryInfo(
            GitHostingProvider.GitHub,
            owner: "microsoft",
            repository: "dotnet",
            baseUrl: "https://github.com");
    }

    [Benchmark(Description = "Render simple summary comment")]
    public RenderedComment RenderSimpleComment()
    {
        return XmlDocCommentRenderer.Render(_simpleComment);
    }

    [Benchmark(Description = "Render complex multi-section comment")]
    public RenderedComment RenderComplexComment()
    {
        return XmlDocCommentRenderer.Render(_complexComment);
    }

    [Benchmark(Description = "Render comment with markdown formatting")]
    public RenderedComment RenderMarkdownComment()
    {
        return XmlDocCommentRenderer.Render(_markdownComment, _repoInfo);
    }

    [Benchmark(Description = "Render comment with list content")]
    public RenderedComment RenderListComment()
    {
        return XmlDocCommentRenderer.Render(_listComment);
    }

    [Benchmark(Description = "GetStrippedSummary - simple")]
    public string GetStrippedSummarySimple()
    {
        return XmlDocCommentRenderer.GetStrippedSummary(_simpleComment);
    }

    [Benchmark(Description = "GetStrippedSummary - complex")]
    public string GetStrippedSummaryComplex()
    {
        return XmlDocCommentRenderer.GetStrippedSummary(_complexComment);
    }

    [Benchmark(Description = "ProcessMarkdownInText with issue refs")]
    public List<RenderedSegment> ProcessMarkdownWithIssueRefs()
    {
        return XmlDocCommentRenderer.ProcessMarkdownInText(
            "This fixes **critical** bug #123 and addresses issue GH-456. See `Config` class.",
            _repoInfo);
    }

    [Benchmark(Description = "ProcessMarkdownInText plain text")]
    public List<RenderedSegment> ProcessMarkdownPlainText()
    {
        return XmlDocCommentRenderer.ProcessMarkdownInText(
            "This is a plain text summary without any special formatting or references.",
            null);
    }
}
