using CommentsVS.Services;

namespace CommentsVS.Test;

[TestClass]
public sealed class XmlDocCommentRendererTests
{
    [TestMethod]
    public void Render_WithMarkdownBold_DoubleAsterisk_CreatesBoldSegment()
    {
        var block = CreateCommentBlock("<summary>This is **bold** text</summary>");

        RenderedComment result = XmlDocCommentRenderer.Render(block);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Sections.Count > 0);
        var summary = result.Summary;
        Assert.IsNotNull(summary);

        // Find the bold segment
        var boldSegment = summary.Lines
            .SelectMany(l => l.Segments)
            .FirstOrDefault(s => s.Type == RenderedSegmentType.Bold);

        Assert.IsNotNull(boldSegment, "Expected a bold segment");
        Assert.AreEqual("bold", boldSegment.Text);
    }

    [TestMethod]
    public void Render_WithMarkdownBold_DoubleUnderscore_CreatesBoldSegment()
    {
        var block = CreateCommentBlock("<summary>This is __bold__ text</summary>");

        RenderedComment result = XmlDocCommentRenderer.Render(block);

        Assert.IsNotNull(result);
        var summary = result.Summary;
        Assert.IsNotNull(summary);

        var boldSegment = summary.Lines
            .SelectMany(l => l.Segments)
            .FirstOrDefault(s => s.Type == RenderedSegmentType.Bold);

        Assert.IsNotNull(boldSegment, "Expected a bold segment");
        Assert.AreEqual("bold", boldSegment.Text);
    }

    [TestMethod]
    public void Render_WithMarkdownItalic_SingleAsterisk_CreatesItalicSegment()
    {
        var block = CreateCommentBlock("<summary>This is *italic* text</summary>");

        RenderedComment result = XmlDocCommentRenderer.Render(block);

        Assert.IsNotNull(result);
        var summary = result.Summary;
        Assert.IsNotNull(summary);

        var italicSegment = summary.Lines
            .SelectMany(l => l.Segments)
            .FirstOrDefault(s => s.Type == RenderedSegmentType.Italic);

        Assert.IsNotNull(italicSegment, "Expected an italic segment");
        Assert.AreEqual("italic", italicSegment.Text);
    }

    [TestMethod]
    public void Render_WithMarkdownItalic_InSentence_CreatesItalicSegment()
    {
        // This tests the exact scenario from the user's screenshot
        var block = CreateCommentBlock("<summary>Represents a user with *basic* contact information.</summary>");

        RenderedComment result = XmlDocCommentRenderer.Render(block);

        Assert.IsNotNull(result);
        var summary = result.Summary;
        Assert.IsNotNull(summary);

        var italicSegment = summary.Lines
            .SelectMany(l => l.Segments)
            .FirstOrDefault(s => s.Type == RenderedSegmentType.Italic);

        Assert.IsNotNull(italicSegment, "Expected an italic segment for *basic*");
        Assert.AreEqual("basic", italicSegment.Text);
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithItalic_ReturnsItalicSegment()
    {
        // Direct test of the markdown processing method
        var segments = XmlDocCommentRenderer.ProcessMarkdownInText("with *basic* contact");

        Assert.AreEqual(3, segments.Count, "Expected 3 segments: before, italic, after");
        Assert.AreEqual("with ", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[0].Type);
        Assert.AreEqual("basic", segments[1].Text);
        Assert.AreEqual(RenderedSegmentType.Italic, segments[1].Type);
        Assert.AreEqual(" contact", segments[2].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[2].Type);
    }

    [TestMethod]
    public void Render_WithMarkdownItalic_SingleUnderscore_CreatesItalicSegment()
    {
        var block = CreateCommentBlock("<summary>This is _italic_ text</summary>");

        RenderedComment result = XmlDocCommentRenderer.Render(block);

        Assert.IsNotNull(result);
        var summary = result.Summary;
        Assert.IsNotNull(summary);

        var italicSegment = summary.Lines
            .SelectMany(l => l.Segments)
            .FirstOrDefault(s => s.Type == RenderedSegmentType.Italic);

        Assert.IsNotNull(italicSegment, "Expected an italic segment");
        Assert.AreEqual("italic", italicSegment.Text);
    }

    [TestMethod]
    public void Render_WithMarkdownInlineCode_CreatesCodeSegment()
    {
        var block = CreateCommentBlock("<summary>Use the `GetValue` method</summary>");

        RenderedComment result = XmlDocCommentRenderer.Render(block);

        Assert.IsNotNull(result);
        var summary = result.Summary;
        Assert.IsNotNull(summary);

        var codeSegment = summary.Lines
            .SelectMany(l => l.Segments)
            .FirstOrDefault(s => s.Type == RenderedSegmentType.Code);

        Assert.IsNotNull(codeSegment, "Expected a code segment");
        Assert.AreEqual("GetValue", codeSegment.Text);
    }

    [TestMethod]
    public void Render_WithMarkdownStrikethrough_CreatesStrikethroughSegment()
    {
        var block = CreateCommentBlock("<summary>This is ~~removed~~ text</summary>");

        RenderedComment result = XmlDocCommentRenderer.Render(block);

        Assert.IsNotNull(result);
        var summary = result.Summary;
        Assert.IsNotNull(summary);

        var strikethroughSegment = summary.Lines
            .SelectMany(l => l.Segments)
            .FirstOrDefault(s => s.Type == RenderedSegmentType.Strikethrough);

        Assert.IsNotNull(strikethroughSegment, "Expected a strikethrough segment");
        Assert.AreEqual("removed", strikethroughSegment.Text);
    }

    [TestMethod]
    public void Render_WithMultipleMarkdownFormats_CreatesCorrectSegments()
    {
        var block = CreateCommentBlock("<summary>This is **bold** and *italic* and `code`</summary>");

        RenderedComment result = XmlDocCommentRenderer.Render(block);

        Assert.IsNotNull(result);
        var summary = result.Summary;
        Assert.IsNotNull(summary);

        var segments = summary.Lines.SelectMany(l => l.Segments).ToList();

        var boldSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.Bold);
        var italicSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.Italic);
        var codeSegment = segments.FirstOrDefault(s => s.Type == RenderedSegmentType.Code);

        Assert.IsNotNull(boldSegment, "Expected a bold segment");
        Assert.AreEqual("bold", boldSegment.Text);

        Assert.IsNotNull(italicSegment, "Expected an italic segment");
        Assert.AreEqual("italic", italicSegment.Text);

        Assert.IsNotNull(codeSegment, "Expected a code segment");
        Assert.AreEqual("code", codeSegment.Text);
    }

    [TestMethod]
    public void Render_WithXmlBoldTag_CreatesBoldSegment()
    {
        var block = CreateCommentBlock("<summary>This is <b>bold</b> text</summary>");

        RenderedComment result = XmlDocCommentRenderer.Render(block);

        Assert.IsNotNull(result);
        var summary = result.Summary;
        Assert.IsNotNull(summary);

        var boldSegment = summary.Lines
            .SelectMany(l => l.Segments)
            .FirstOrDefault(s => s.Type == RenderedSegmentType.Bold);

        Assert.IsNotNull(boldSegment, "Expected a bold segment from <b> tag");
        Assert.AreEqual("bold", boldSegment.Text);
    }

    [TestMethod]
    public void Render_WithXmlItalicTag_CreatesItalicSegment()
    {
        var block = CreateCommentBlock("<summary>This is <i>italic</i> text</summary>");

        RenderedComment result = XmlDocCommentRenderer.Render(block);

        Assert.IsNotNull(result);
        var summary = result.Summary;
        Assert.IsNotNull(summary);

        var italicSegment = summary.Lines
            .SelectMany(l => l.Segments)
            .FirstOrDefault(s => s.Type == RenderedSegmentType.Italic);

        Assert.IsNotNull(italicSegment, "Expected an italic segment from <i> tag");
        Assert.AreEqual("italic", italicSegment.Text);
    }

    [TestMethod]
    public void Render_WithNoMarkdown_CreatesTextSegment()
    {
        var block = CreateCommentBlock("<summary>This is plain text</summary>");

        RenderedComment result = XmlDocCommentRenderer.Render(block);

        Assert.IsNotNull(result);
        var summary = result.Summary;
        Assert.IsNotNull(summary);

        var segments = summary.Lines.SelectMany(l => l.Segments).ToList();
        Assert.IsTrue(segments.Count > 0);
        Assert.IsTrue(segments.All(s => s.Type == RenderedSegmentType.Text));
    }

    [TestMethod]
    public void Render_BoldDoesNotMatchInsideCode_CodeTakesPrecedence()
    {
        var block = CreateCommentBlock("<summary>Use `**not bold**` for emphasis</summary>");

        RenderedComment result = XmlDocCommentRenderer.Render(block);

        Assert.IsNotNull(result);
        var summary = result.Summary;
        Assert.IsNotNull(summary);

        // The **not bold** should be inside code, not rendered as bold
        var codeSegment = summary.Lines
            .SelectMany(l => l.Segments)
            .FirstOrDefault(s => s.Type == RenderedSegmentType.Code);

        Assert.IsNotNull(codeSegment, "Expected a code segment");
        Assert.AreEqual("**not bold**", codeSegment.Text);

        // There should be no bold segment
        var boldSegment = summary.Lines
            .SelectMany(l => l.Segments)
            .FirstOrDefault(s => s.Type == RenderedSegmentType.Bold);

        Assert.IsNull(boldSegment, "Should not have a bold segment when ** is inside code");
    }

    [TestMethod]
    public void ProcessMarkdownInText_WithLink_CreatesLinkSegment()
    {
        var segments = XmlDocCommentRenderer.ProcessMarkdownInText("See the [API docs](https://example.com/api) for details");

        Assert.AreEqual(3, segments.Count, "Expected 3 segments: before, link, after");
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
        var segments = XmlDocCommentRenderer.ProcessMarkdownInText("Visit <https://example.com> for more info");

        Assert.AreEqual(3, segments.Count, "Expected 3 segments: before, link, after");
        Assert.AreEqual("Visit ", segments[0].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[0].Type);
        Assert.AreEqual("https://example.com", segments[1].Text);
        Assert.AreEqual(RenderedSegmentType.Link, segments[1].Type);
        Assert.AreEqual("https://example.com", segments[1].LinkTarget);
        Assert.AreEqual(" for more info", segments[2].Text);
        Assert.AreEqual(RenderedSegmentType.Text, segments[2].Type);
    }

    [TestMethod]
    public void Render_WithMarkdownLink_CreatesLinkSegment()
    {
        var block = CreateCommentBlock("<summary>See the [docs](https://example.com) here</summary>");

        RenderedComment result = XmlDocCommentRenderer.Render(block);

        Assert.IsNotNull(result);
        var summary = result.Summary;
        Assert.IsNotNull(summary);

        var linkSegment = summary.Lines
            .SelectMany(l => l.Segments)
            .FirstOrDefault(s => s.Type == RenderedSegmentType.Link);

        Assert.IsNotNull(linkSegment, "Expected a link segment");
        Assert.AreEqual("docs", linkSegment.Text);
        Assert.AreEqual("https://example.com", linkSegment.LinkTarget);
    }

    private static XmlDocCommentBlock CreateCommentBlock(string xmlContent)
    {
        var commentStyle = LanguageCommentStyle.GetForContentType("CSharp");
        return new XmlDocCommentBlock(
            span: new Microsoft.VisualStudio.Text.Span(0, xmlContent.Length),
            startLine: 0,
            endLine: 0,
            indentation: "",
            xmlContent: xmlContent,
            commentStyle: commentStyle,
            isMultiLineStyle: false);
    }
}
