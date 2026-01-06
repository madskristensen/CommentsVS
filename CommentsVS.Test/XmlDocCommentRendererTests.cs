using CommentsVS.Services;

namespace CommentsVS.Test;

[TestClass]
public sealed class XmlDocCommentRendererTests
{
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
}
