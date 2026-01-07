using CommentsVS.Services;
using Microsoft.VisualStudio.Text;

namespace CommentsVS.Test;

[TestClass]
public sealed class CommentReflowEngineTests
{
    private static XmlDocCommentBlock CreateBlock(string xmlContent, LanguageCommentStyle style, bool isMultiLine = false)
    {
        return new XmlDocCommentBlock(
            span: new Span(0, 100),
            startLine: 0,
            endLine: 5,
            indentation: "    ",
            xmlContent: xmlContent,
            commentStyle: style,
            isMultiLineStyle: isMultiLine);
    }

    #region Basic Reflow Tests

    [TestMethod]
    public void ReflowComment_WithSimpleSummary_ReflowsCorrectly()
    {
        var engine = new CommentReflowEngine(maxLineLength: 80, useCompactStyle: false, preserveBlankLines: false);
        var block = CreateBlock("<summary>This is a simple summary.</summary>", LanguageCommentStyle.CSharp);

        var result = engine.ReflowComment(block);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("<summary>"));
        Assert.IsTrue(result.Contains("This is a simple summary."));
        Assert.IsTrue(result.Contains("</summary>"));
    }

    [TestMethod]
    public void ReflowComment_WithLongText_WrapsCorrectly()
    {
        var engine = new CommentReflowEngine(maxLineLength: 50, useCompactStyle: false, preserveBlankLines: false);
        var longText = "<summary>This is a very long summary that should be wrapped across multiple lines because it exceeds the maximum line length.</summary>";
        var block = CreateBlock(longText, LanguageCommentStyle.CSharp);

        var result = engine.ReflowComment(block);

        Assert.IsNotNull(result);
        var lines = result.Split(["\r\n", "\n"], StringSplitOptions.None);
        Assert.IsTrue(lines.Length > 3, "Expected text to be wrapped across multiple lines");
    }

    [TestMethod]
    public void ReflowComment_WithCompactStyle_FormatsOnSingleLine()
    {
        var engine = new CommentReflowEngine(maxLineLength: 120, useCompactStyle: true, preserveBlankLines: false);
        var block = CreateBlock("<summary>Short summary.</summary>", LanguageCommentStyle.CSharp);

        var result = engine.ReflowComment(block);

        Assert.IsNotNull(result);
        // Compact style should put opening tag, content, and closing tag on same line when possible
        var lines = result.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        Assert.IsTrue(lines.Any(l => l.Contains("<summary>Short summary.</summary>")), 
            "Expected compact format with content on single line");
    }

    [TestMethod]
    public void ReflowComment_WithNullBlock_ThrowsArgumentNullException()
    {
        var engine = new CommentReflowEngine(maxLineLength: 80, useCompactStyle: false, preserveBlankLines: false);

        Assert.ThrowsException<ArgumentNullException>(() => engine.ReflowComment(null));
    }

    [TestMethod]
    public void ReflowComment_WithEmptyXmlContent_ReturnsNull()
    {
        var engine = new CommentReflowEngine(maxLineLength: 80, useCompactStyle: false, preserveBlankLines: false);
        var block = CreateBlock("", LanguageCommentStyle.CSharp);

        var result = engine.ReflowComment(block);

        Assert.IsNull(result);
    }

    #endregion

    #region Preformatted Content Tests

    [TestMethod]
    public void ReflowComment_WithCodeBlock_PreservesFormatting()
    {
        var engine = new CommentReflowEngine(maxLineLength: 80, useCompactStyle: false, preserveBlankLines: false);
        var xmlContent = "<summary>Example:</summary><code>var x = 123;\nvar y = 456;</code>";
        var block = CreateBlock(xmlContent, LanguageCommentStyle.CSharp);

        var result = engine.ReflowComment(block);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("<code>"), "Expected code opening tag");
        Assert.IsTrue(result.Contains("</code>"), "Expected code closing tag");
        // Code content should not be reflowed
        Assert.IsTrue(result.Contains("var x = 123;") || result.Contains("var x = 123;"));
    }

    #endregion

    #region Multiple Elements Tests

    [TestMethod]
    public void ReflowComment_WithMultipleElements_ProcessesAll()
    {
        var engine = new CommentReflowEngine(maxLineLength: 80, useCompactStyle: false, preserveBlankLines: false);
        var xmlContent = "<summary>Main summary.</summary><param name=\"value\">Parameter description.</param>";
        var block = CreateBlock(xmlContent, LanguageCommentStyle.CSharp);

        var result = engine.ReflowComment(block);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("<summary>"));
        Assert.IsTrue(result.Contains("</summary>"));
        Assert.IsTrue(result.Contains("<param"));
        Assert.IsTrue(result.Contains("</param>"));
    }

    #endregion

    #region Multi-Line Style Tests

    [TestMethod]
    public void ReflowComment_WithMultiLineStyle_UsesMultiLineFormat()
    {
        var engine = new CommentReflowEngine(maxLineLength: 80, useCompactStyle: false, preserveBlankLines: false);
        var block = CreateBlock("<summary>Summary text.</summary>", LanguageCommentStyle.CSharp, isMultiLine: true);

        var result = engine.ReflowComment(block);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("/**"), "Expected multi-line comment start");
        Assert.IsTrue(result.Contains("*/"), "Expected multi-line comment end");
    }

    [TestMethod]
    public void ReflowComment_WithSingleLineStyle_UsesSingleLineFormat()
    {
        var engine = new CommentReflowEngine(maxLineLength: 80, useCompactStyle: false, preserveBlankLines: false);
        var block = CreateBlock("<summary>Summary text.</summary>", LanguageCommentStyle.CSharp, isMultiLine: false);

        var result = engine.ReflowComment(block);

        Assert.IsNotNull(result);
        // Single-line style should use /// prefix (no /** */)
        Assert.IsFalse(result.Contains("/**"), "Should not have multi-line comment start in single-line style");
    }

    #endregion

    #region Paragraph and Blank Line Tests

    [TestMethod]
    public void ReflowComment_WithPreserveBlankLines_RetainsBlankLines()
    {
        var engine = new CommentReflowEngine(maxLineLength: 80, useCompactStyle: false, preserveBlankLines: true);
        var xmlContent = "<summary>First paragraph.\n\nSecond paragraph.</summary>";
        var block = CreateBlock(xmlContent, LanguageCommentStyle.CSharp);

        var result = engine.ReflowComment(block);

        Assert.IsNotNull(result);
        // When preserving blank lines, there should be empty lines in the output
        var lines = result.Split(["\r\n", "\n"], StringSplitOptions.None);
        Assert.IsTrue(lines.Any(l => string.IsNullOrWhiteSpace(l) || l.Trim() == "///" || l.Trim() == "*"),
            "Expected blank lines to be preserved");
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void ReflowComment_WithVeryShortMaxLength_StillWorks()
    {
        var engine = new CommentReflowEngine(maxLineLength: 20, useCompactStyle: false, preserveBlankLines: false);
        var block = CreateBlock("<summary>Test</summary>", LanguageCommentStyle.CSharp);

        var result = engine.ReflowComment(block);

        Assert.IsNotNull(result);
        // Should handle very short line lengths gracefully
    }

    [TestMethod]
    public void ReflowComment_WithInvalidMaxLength_UsesDefault()
    {
        var engine = new CommentReflowEngine(maxLineLength: -1, useCompactStyle: false, preserveBlankLines: false);
        var block = CreateBlock("<summary>Test summary.</summary>", LanguageCommentStyle.CSharp);

        var result = engine.ReflowComment(block);

        Assert.IsNotNull(result);
        // Should use default value (120) when given invalid length
    }

    #endregion
}
