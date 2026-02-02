using CommentsVS.Services;
using static CommentsVS.Services.RenderedSegment;

namespace CommentsVS.Test;

[TestClass]
public sealed class XmlDocCommentRendererTests
{
    private const string NoSummaryPlaceholder = "(No summary provided)";

    #region Markdown Bold Tests

    [TestMethod]
    public void ProcessMarkdownInText_WithBold_DoubleAsterisk_CreatesBoldSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("This is **bold** text");

        Assert.HasCount(3, segments, "Expected 3 segments: before, bold, after");
        Assert.AreEqual("This is ", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[0].Type);
        Assert.AreEqual("bold", segments[1].Text);
        Assert.AreEqual(RenderedSegmentType.Bold, segments[1].Type);
        Assert.AreEqual(" text", segments[2].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[2].Type);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithBold_DoubleUnderscore_CreatesBoldSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("This is __bold__ text");

        Assert.HasCount(3, segments, "Expected 3 segments: before, bold, after");
        Assert.AreEqual("This is ", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[0].Type);
        Assert.AreEqual("bold", segments[1].Text);
        Assert.AreEqual(RenderedSegmentType.Bold, segments[1].Type);
        Assert.AreEqual(" text", segments[2].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[2].Type);
    }

    #endregion

    #region Markdown Italic Tests

    [TestMethod]
    public void ProcessMarkdownInText_WithItalic_SingleAsterisk_CreatesItalicSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("This is *italic* text");

        Assert.HasCount(3, segments, "Expected 3 segments: before, italic, after");
        Assert.AreEqual("This is ", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[0].Type);
        Assert.AreEqual("italic", segments[1].Text);
        Assert.AreEqual(RenderedSegmentType.Italic, segments[1].Type);
        Assert.AreEqual(" text", segments[2].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[2].Type);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithItalic_InSentence_CreatesItalicSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("Represents a user with *basic* contact information.");

        RenderedSegment italicSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.Italic);
        Assert.IsNotNull(italicSegment, "Expected an italic segment for *basic*");
        Assert.AreEqual("basic", italicSegment.Text);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithItalic_ReturnsItalicSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("with *basic* contact");

        Assert.HasCount(3, segments, "Expected 3 segments: before, italic, after");
        Assert.AreEqual("with ", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[0].Type);
        Assert.AreEqual("basic", segments[1].Text);
        Assert.AreEqual(RenderedSegmentType.Italic, segments[1].Type);
        Assert.AreEqual(" contact", segments[2].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[2].Type);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithItalic_SingleUnderscore_CreatesItalicSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("This is _italic_ text");

        Assert.HasCount(3, segments, "Expected 3 segments: before, italic, after");
        Assert.AreEqual("This is ", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[0].Type);
        Assert.AreEqual("italic", segments[1].Text);
        Assert.AreEqual(RenderedSegmentType.Italic, segments[1].Type);
        Assert.AreEqual(" text", segments[2].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[2].Type);
    }

    #endregion

    #region Markdown Code Tests

    [TestMethod]
    public void ProcessMarkdownInText_WithInlineCode_CreatesCodeSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("Use the `GetValue` method");

        Assert.HasCount(3, segments, "Expected 3 segments: before, code, after");
        Assert.AreEqual("Use the ", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[0].Type);
        Assert.AreEqual("GetValue", segments[1].Text);
        Assert.AreEqual(RenderedSegmentType.Code, segments[1].Type);
        Assert.AreEqual(" method", segments[2].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[2].Type);
    }

    [TestMethod]
    public void ProcessMarkdownInText_BoldInsideCode_CodeTakesPrecedence()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("Use `**not bold**` for emphasis");

        RenderedSegment codeSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.Code);
        Assert.IsNotNull(codeSegment, "Expected a code segment");
        Assert.AreEqual("**not bold**", codeSegment.Text);

        RenderedSegment boldSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.Bold);
        Assert.IsNull(boldSegment, "Should not have a bold segment when ** is inside code");
    }

    #endregion

    #region Markdown Strikethrough Tests

    [TestMethod]
    public void ProcessMarkdownInText_WithStrikethrough_CreatesStrikethroughSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("This is ~~removed~~ text");

        Assert.HasCount(3, segments, "Expected 3 segments: before, strikethrough, after");
        Assert.AreEqual("This is ", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[0].Type);
        Assert.AreEqual("removed", segments[1].Text);
        Assert.AreEqual(RenderedSegmentType.Strikethrough, segments[1].Type);
        Assert.AreEqual(" text", segments[2].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[2].Type);
    }

    #endregion

    #region Multiple Markdown Formats Tests

    [TestMethod]
    public void ProcessMarkdownInText_WithMultipleFormats_CreatesCorrectSegments()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("This is **bold** and *italic* and `code`");

        RenderedSegment boldSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.Bold);
        RenderedSegment italicSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.Italic);
        RenderedSegment codeSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.Code);

        Assert.IsNotNull(boldSegment, "Expected a bold segment");
        Assert.AreEqual("bold", boldSegment.Text);

        Assert.IsNotNull(italicSegment, "Expected an italic segment");
        Assert.AreEqual("italic", italicSegment.Text);

        Assert.IsNotNull(codeSegment, "Expected a code segment");
        Assert.AreEqual("code", codeSegment.Text);
    }

    #endregion

    #region Plain Text Tests

    [TestMethod]
    public void ProcessMarkdownInText_WithNoMarkdown_CreatesTextSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("This is plain text");

        Assert.HasCount(1, segments);
        Assert.AreEqual("This is plain text", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[0].Type);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithEmptyString_ReturnsEmptyList()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("");

        Assert.IsEmpty(segments);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithNull_ReturnsEmptyList()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText(null!);

        Assert.IsEmpty(segments);
    }

    #endregion

    #region Markdown Link Tests

    [TestMethod]
    public void ProcessMarkdownInText_WithLink_CreatesLinkSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("See the [API docs](https://example.com/api) for details");

        Assert.HasCount(3, segments, "Expected 3 segments: before, link, after");
        Assert.AreEqual("See the ", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[0].Type);
        Assert.AreEqual("API docs", segments[1].Text);
        Assert.AreEqual(RenderedSegmentType.Link, segments[1].Type);
        Assert.AreEqual("https://example.com/api", segments[1].LinkTarget);
        Assert.AreEqual(" for details", segments[2].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[2].Type);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithAutoLink_CreatesLinkSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("Visit <https://example.com> for more info");

        Assert.HasCount(3, segments, "Expected 3 segments: before, link, after");
        Assert.AreEqual("Visit ", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[0].Type);
        Assert.AreEqual("https://example.com", segments[1].Text);
        Assert.AreEqual(RenderedSegmentType.Link, segments[1].Type);
        Assert.AreEqual("https://example.com", segments[1].LinkTarget);
        Assert.AreEqual(" for more info", segments[2].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[2].Type);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithLinkInMiddle_CreatesLinkSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("See the [docs](https://example.com) here");

        RenderedSegment linkSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.Link);
        Assert.IsNotNull(linkSegment, "Expected a link segment");
        Assert.AreEqual("docs", linkSegment.Text);
        Assert.AreEqual("https://example.com", linkSegment.LinkTarget);
    }

    #endregion

    #region Edge Case Tests

    [TestMethod]
    public void ProcessMarkdownInText_WithBoldAtStart_CreatesBoldSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("**bold** at start");

        Assert.HasCount(2, segments);
        Assert.AreEqual("bold", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Bold, segments[0].Type);
        Assert.AreEqual(" at start", segments[1].Text);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithBoldAtEnd_CreatesBoldSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("ends with **bold**");

        Assert.HasCount(2, segments);
        Assert.AreEqual("ends with ", segments[0].Text);
        Assert.AreEqual("bold", segments[1].Text);
        Assert.AreEqual(RenderedSegmentType.Bold, segments[1].Type);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithOnlyBold_CreatesBoldSegment()
    {
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("**bold**");

        Assert.HasCount(1, segments);
        Assert.AreEqual("bold", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Bold, segments[0].Type);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithNestedFormatting_HandlesCorrectly()
    {
        // Markdown doesn't support nested formatting like **_text_**
        // but we should handle it gracefully
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("Text with **bold text** here");

        RenderedSegment boldSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.Bold);
        Assert.IsNotNull(boldSegment);
        Assert.AreEqual("bold text", boldSegment.Text);
    }

    #endregion

    #region Issue Reference Tests

    [TestMethod]
    public void ProcessMarkdownInText_WithIssueReference_WithoutRepoInfo_TreatsAsPlainText()
    {
        // Without repo info, issue references should be treated as plain text
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("See issue #123 for details");

        // All should be plain text since no repo info provided
        Assert.IsTrue(segments.All(s => s.Type == RenderedSegmentType.Text), "Without repo info, issue refs should be plain text");
        var joined = string.Join("", segments.Select(s => s.Text));
        Assert.Contains("#123", joined, "Original text should be preserved");
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithIssueReference_WithRepoInfo_CreatesIssueReferenceSegment()
    {
        // Create a mock repo info for GitHub
        var repoInfo = new GitRepositoryInfo(GitHostingProvider.GitHub, "testowner", "testrepo", "https://github.com");

        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("See issue #123 for details", repoInfo);

        RenderedSegment issueSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.IssueReference);
        Assert.IsNotNull(issueSegment, "Should have an IssueReference segment");
        Assert.AreEqual("#123", issueSegment.Text, "Issue reference text should be preserved");
        Assert.AreEqual("https://github.com/testowner/testrepo/issues/123", issueSegment.LinkTarget, "Should have correct issue URL");
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithIssueReference_AtStartOfText_CreatesIssueReferenceSegment()
    {
        var repoInfo = new GitRepositoryInfo(GitHostingProvider.GitHub, "owner", "repo", "https://github.com");

        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("#42 is the issue", repoInfo);

        RenderedSegment issueSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.IssueReference);
        Assert.IsNotNull(issueSegment, "Should have an IssueReference segment");
        Assert.AreEqual("#42", issueSegment.Text);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithIssueReference_AfterParenthesis_CreatesIssueReferenceSegment()
    {
        var repoInfo = new GitRepositoryInfo(GitHostingProvider.GitHub, "owner", "repo", "https://github.com");

        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("Fixed bug (#99)", repoInfo);

        RenderedSegment issueSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.IssueReference);
        Assert.IsNotNull(issueSegment, "Should have an IssueReference segment");
        Assert.AreEqual("#99", issueSegment.Text);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithMultipleIssueReferences_CreatesMultipleSegments()
    {
        var repoInfo = new GitRepositoryInfo(GitHostingProvider.GitHub, "owner", "repo", "https://github.com");

        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("See #10 and #20", repoInfo);

        List<RenderedSegment> issueSegments = [.. segments.Where(s => s.Type == RenderedSegmentType.IssueReference)];
        Assert.HasCount(2, issueSegments, "Should have two IssueReference segments");
        Assert.AreEqual("#10", issueSegments[0].Text);
        Assert.AreEqual("#20", issueSegments[1].Text);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithIssueReferenceAndMarkdown_HandlesBoth()
    {
        var repoInfo = new GitRepositoryInfo(GitHostingProvider.GitHub, "owner", "repo", "https://github.com");

        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("Fix for **bug** #123", repoInfo);

        Assert.IsTrue(segments.Any(s => s.Type == RenderedSegmentType.Bold), "Should have bold segment");
        Assert.IsTrue(segments.Any(s => s.Type == RenderedSegmentType.IssueReference), "Should have issue reference segment");
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithHashtagInCode_NotTreatedAsIssueReference()
    {
        var repoInfo = new GitRepositoryInfo(GitHostingProvider.GitHub, "owner", "repo", "https://github.com");

        // Code blocks have higher priority than issue references
        List<RenderedSegment> segments = XmlDocCommentRenderer.ProcessMarkdownInText("Use `#123` in code", repoInfo);

        // The #123 inside code should not be an issue reference
        Assert.IsFalse(segments.Any(s => s.Type == RenderedSegmentType.IssueReference && s.Text == "#123"),
            "Issue refs inside code blocks should not be converted");
    }

    #endregion

    #region Missing Summary Tests

    [TestMethod]
    public void GetStrippedSummaryFromXml_WithoutSummary_ReturnsPlaceholder()
    {
        var xml = "<remarks>Only remarks.</remarks>";

        var result = XmlDocCommentRenderer.GetStrippedSummaryFromXml(xml);

        Assert.AreEqual(NoSummaryPlaceholder, result);
    }

    [TestMethod]
    public void RenderXmlContent_WithoutSummary_AddsPlaceholderSummarySection()
    {
        var xml = "<remarks>Only remarks.</remarks>";

        RenderedComment result = XmlDocCommentRenderer.RenderXmlContent(xml);

        Assert.IsNotNull(result.Summary, "Summary section should be created when missing.");
        Assert.IsFalse(result.Summary.IsEmpty, "Summary section should contain placeholder text.");
        Assert.IsTrue(result.Summary.Lines.SelectMany(l => l.Segments)
            .Any(s => s.Text == NoSummaryPlaceholder),
            "Summary should include the placeholder text.");
    }

    [TestMethod]
    public void GetStrippedSummaryFromXml_WithEmptySummary_ReturnsPlaceholder()
    {
        var xml = "<summary>   </summary><returns>value</returns>";

        var result = XmlDocCommentRenderer.GetStrippedSummaryFromXml(xml);

        Assert.AreEqual(NoSummaryPlaceholder, result);
    }

    [TestMethod]
    public void RenderXmlContent_WithEmptySummary_AddsPlaceholderSummarySection()
    {
        var xml = "<summary>   </summary><returns>value</returns>";

        RenderedComment result = XmlDocCommentRenderer.RenderXmlContent(xml);

        Assert.IsNotNull(result.Summary, "Summary section should exist.");
        Assert.IsFalse(result.Summary.IsEmpty, "Summary section should contain placeholder when empty.");
        Assert.IsTrue(result.Summary.Lines.SelectMany(l => l.Segments)
            .Any(s => s.Text == NoSummaryPlaceholder));
    }

    #endregion

    #region Inheritdoc Tests

    [TestMethod]
    public void GetStrippedSummaryFromXml_WithInheritdoc_ReturnsInheritedMessage()
    {
        var result = XmlDocCommentRenderer.GetStrippedSummaryFromXml("<inheritdoc/>");

        Assert.AreEqual("(Documentation inherited)", result);
    }

    [TestMethod]
    public void GetStrippedSummaryFromXml_WithInheritdocAndCref_ReturnsInheritedMessageWithTypeName()
    {
        var result = XmlDocCommentRenderer.GetStrippedSummaryFromXml("<inheritdoc cref=\"IDisposable.Dispose\"/>");

        Assert.AreEqual("(Documentation inherited from Dispose)", result);
    }

    [TestMethod]
    public void GetStrippedSummaryFromXml_WithInheritdocFullCref_ReturnsInheritedMessageWithTypeName()
    {
        var result = XmlDocCommentRenderer.GetStrippedSummaryFromXml("<inheritdoc cref=\"T:System.IDisposable\"/>");

        Assert.AreEqual("(Documentation inherited from IDisposable)", result);
    }

    [TestMethod]
    public void RenderXmlContent_WithInheritdoc_CreatesSummarySection()
    {
        RenderedComment result = XmlDocCommentRenderer.RenderXmlContent("<inheritdoc/>");

        Assert.IsNotNull(result.Summary, "Should have a summary section");
        Assert.IsFalse(result.Summary.IsEmpty, "Summary section should not be empty");
        Assert.IsTrue(result.Summary.Lines.Any(l => l.Segments.Any(s => s.Text.Contains("Documentation inherited"))),
            "Should contain inherited documentation message");
    }

    [TestMethod]
    public void RenderXmlContent_WithInheritdocAndCref_IncludesTypeNameInMessage()
    {
        RenderedComment result = XmlDocCommentRenderer.RenderXmlContent("<inheritdoc cref=\"ICloneable.Clone\"/>");

        Assert.IsNotNull(result.Summary, "Should have a summary section");
        List<RenderedSegment> allSegments = [.. result.Summary.Lines.SelectMany(l => l.Segments)];
        Assert.IsTrue(allSegments.Any(s => s.Type == RenderedSegmentType.Code && s.Text == "Clone"),
            "Should have code segment with type name");
    }

    [TestMethod]
    public void RenderXmlContent_WithInheritdoc_UsesItalicForMessage()
    {
        RenderedComment result = XmlDocCommentRenderer.RenderXmlContent("<inheritdoc/>");

        List<RenderedSegment> allSegments = [.. result.Summary.Lines.SelectMany(l => l.Segments)];
        Assert.IsTrue(allSegments.Any(s => s.Type == RenderedSegmentType.Italic),
            "Inherited documentation message should use italic styling");
    }

    #endregion

    #region Remarks with XML Tags Tests

    [TestMethod]
    public void RenderXmlContent_RemarksWithXmlTagsOnMultipleLines_PreservesLineBreaks()
    {
        // This is the exact scenario from issue #31
        var xml = """
            <summary>Test summary</summary>
            <remarks>
            Line 1 with <c>SomeType</c> reference.
            Line 2 with <c>AnotherType</c> reference.
            </remarks>
            """;

        RenderedComment result = XmlDocCommentRenderer.RenderXmlContent(xml);

        RenderedCommentSection remarksSection = result.Sections.FirstOrDefault(s => s.Type == CommentSectionType.Remarks);
        Assert.IsNotNull(remarksSection, "Should have a remarks section");

        // Filter out blank lines to count content lines
        List<RenderedLine> contentLines = [.. remarksSection.Lines.Where(l => !l.IsBlank)];
        Assert.IsGreaterThanOrEqualTo(2, contentLines.Count, $"Expected at least 2 content lines in remarks, got {contentLines.Count}");

        // Verify both lines have code segments
        var linesWithCode = contentLines.Where(l => l.Segments.Any(s => s.Type == RenderedSegmentType.Code)).ToList();
        Assert.IsGreaterThanOrEqualTo(2, linesWithCode.Count, $"Expected at least 2 lines with code segments, got {linesWithCode.Count}");
    }

    [TestMethod]
    public void RenderXmlContent_RemarksWithMixedContent_PreservesStructure()
    {
        var xml = """
            <remarks>
            First paragraph with <c>Code1</c>.
            Second paragraph with <see cref="Type"/> reference.
            </remarks>
            """;

        RenderedComment result = XmlDocCommentRenderer.RenderXmlContent(xml);

        RenderedCommentSection remarksSection = result.Sections.FirstOrDefault(s => s.Type == CommentSectionType.Remarks);
        Assert.IsNotNull(remarksSection, "Should have a remarks section");

        // Verify content is not collapsed to a single line
        List<RenderedLine> contentLines = [.. remarksSection.Lines.Where(l => !l.IsBlank)];
        Assert.IsGreaterThanOrEqualTo(2, contentLines.Count, $"Content should span multiple lines, got {contentLines.Count}");
    }

    [TestMethod]
    public void RenderXmlContent_RemarksWithInlineCode_RendersCodeSegments()
    {
        var xml = """
            <remarks>
            Use <c>MyMethod</c> for processing.
            </remarks>
            """;

        RenderedComment result = XmlDocCommentRenderer.RenderXmlContent(xml);

        RenderedCommentSection remarksSection = result.Sections.FirstOrDefault(s => s.Type == CommentSectionType.Remarks);
        Assert.IsNotNull(remarksSection, "Should have a remarks section");

        List<RenderedSegment> allSegments = [.. remarksSection.Lines.SelectMany(l => l.Segments)];
        RenderedSegment codeSegment = allSegments.FirstOrDefault(s => s.Type == RenderedSegmentType.Code);
        Assert.IsNotNull(codeSegment, "Should have a code segment");
        Assert.AreEqual("MyMethod", codeSegment.Text);
    }

    #endregion
}
