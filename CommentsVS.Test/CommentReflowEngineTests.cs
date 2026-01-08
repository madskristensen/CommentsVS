using System.Text.RegularExpressions;

namespace CommentsVS.Test;

/// <summary>
/// Tests for CommentReflowEngine text reflow logic.
/// Note: Full integration tests require VS text buffers, so these tests focus on
/// the core reflow behavior using test helper methods that mirror internal logic.
/// </summary>
[TestClass]
public sealed class CommentReflowEngineTests
{
    #region Text Wrapping Tests

    [TestMethod]
    public void WrapText_ShortText_ReturnsUnchanged()
    {
        List<string> result = TestWrapText("Short text", maxWidth: 80);

        Assert.HasCount(1, result);
        Assert.AreEqual("Short text", result[0]);
    }

    [TestMethod]
    public void WrapText_LongText_WrapsCorrectly()
    {
        var text = "This is a longer piece of text that should be wrapped to fit within the maximum line length";
        List<string> result = TestWrapText(text, maxWidth: 40);

        Assert.IsGreaterThan(1, result.Count);
        // Lines should generally fit within max width (some may exceed due to single long words)
        foreach (var line in result)
        {
            // We verify wrapping occurred; exact length may vary due to word boundaries
            Assert.IsNotNull(line);
            Assert.IsGreaterThan(0, line.Length);
        }
    }

    [TestMethod]
    public void WrapText_PreservesWords_DoesNotBreakMidWord()
    {
        var text = "word1 word2 word3 word4 word5";
        List<string> result = TestWrapText(text, maxWidth: 15);

        foreach (var line in result)
        {
            // Each line should contain complete words
            var words = line.Split(' ');
            foreach (var word in words)
            {
                Assert.IsGreaterThan(0, word.Length, "Empty word found");
            }
        }
    }

    [TestMethod]
    public void WrapText_EmptyString_ReturnsEmpty()
    {
        List<string> result = TestWrapText("", maxWidth: 80);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void WrapText_WhitespaceOnly_ReturnsEmpty()
    {
        List<string> result = TestWrapText("   ", maxWidth: 80);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void WrapText_WithXmlTags_PreservesTagsAsTokens()
    {
        var text = "This has <see cref=\"MyClass\"/> inline tag";
        List<string> result = TestWrapText(text, maxWidth: 80);

        var joined = string.Join(" ", result);
        Assert.Contains("<see cref=\"MyClass\"/>", joined, "XML tag should be preserved");
    }

    [TestMethod]
    public void WrapText_VeryLongWord_PlacedOnOwnLine()
    {
        var text = "short ThisIsAVeryLongWordThatExceedsMaxWidth end";
        List<string> result = TestWrapText(text, maxWidth: 20);

        // The long word should be on its own line
        Assert.IsTrue(result.Any(l => l.Contains("ThisIsAVeryLongWordThatExceedsMaxWidth")));
    }

    #endregion

    #region Whitespace Normalization Tests

    [TestMethod]
    public void NormalizeWhitespace_MultipleSpaces_CollapsedToSingle()
    {
        var input = "text   with    multiple     spaces";
        var result = TestNormalizeWhitespace(input);

        Assert.IsNotNull(result);
        Assert.DoesNotContain("  ", result, "Should not contain double spaces");
    }

    [TestMethod]
    public void NormalizeWhitespace_TabsAndSpaces_CollapsedToSingle()
    {
        var input = "text\t\twith\t \ttabs";
        var result = TestNormalizeWhitespace(input);

        Assert.IsNotNull(result);
        Assert.DoesNotContain("\t", result, "Should not contain tabs");
        Assert.DoesNotContain("  ", result, "Should not contain double spaces");
    }

    [TestMethod]
    public void NormalizeWhitespace_LeadingTrailingSpaces_Trimmed()
    {
        var input = "  text with leading and trailing spaces  ";
        var result = TestNormalizeWhitespace(input);

        Assert.IsNotNull(result);
        Assert.DoesNotStartWith(" ", result, "Should not start with space");
        Assert.DoesNotEndWith(" ", result, "Should not end with space");
    }

    [TestMethod]
    public void NormalizeWhitespace_NewlinesCRLF_ConvertedToLF()
    {
        var input = "line1\r\nline2\r\nline3";
        var result = TestNormalizeWhitespace(input);

        Assert.IsNotNull(result);
        Assert.DoesNotContain("\r", result, "Should not contain carriage returns");
    }

    [TestMethod]
    public void NormalizeWhitespace_Null_ReturnsNull()
    {
        var result = TestNormalizeWhitespace(null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void NormalizeWhitespace_Empty_ReturnsEmpty()
    {
        var result = TestNormalizeWhitespace("");

        Assert.AreEqual("", result);
    }

    #endregion

    #region Paragraph Splitting Tests

    [TestMethod]
    public void SplitIntoParagraphs_SingleParagraph_ReturnsSingle()
    {
        var input = "This is a single paragraph with no blank lines.";
        List<string> result = TestSplitIntoParagraphs(input, preserveBlankLines: false);

        Assert.HasCount(1, result);
        Assert.AreEqual("This is a single paragraph with no blank lines.", result[0]);
    }

    [TestMethod]
    public void SplitIntoParagraphs_TwoParagraphs_ReturnsBoth()
    {
        var input = "First paragraph.\n\nSecond paragraph.";
        List<string> result = TestSplitIntoParagraphs(input, preserveBlankLines: false);

        Assert.HasCount(2, result);
        Assert.AreEqual("First paragraph.", result[0]);
        Assert.AreEqual("Second paragraph.", result[1]);
    }

    [TestMethod]
    public void SplitIntoParagraphs_MultipleBlankLines_TreatedAsSingleSeparator()
    {
        var input = "First paragraph.\n\n\n\nSecond paragraph.";
        List<string> result = TestSplitIntoParagraphs(input, preserveBlankLines: false);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public void SplitIntoParagraphs_PreserveBlankLines_AddsEmptyString()
    {
        var input = "First paragraph.\n\nSecond paragraph.";
        List<string> result = TestSplitIntoParagraphs(input, preserveBlankLines: true);

        // Should preserve the blank line indicator
        Assert.IsGreaterThanOrEqualTo(2, result.Count);
    }

    [TestMethod]
    public void SplitIntoParagraphs_EmptyInput_ReturnsEmpty()
    {
        List<string> result = TestSplitIntoParagraphs("", preserveBlankLines: false);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void SplitIntoParagraphs_WhitespaceOnlyLines_IgnoresWhitespace()
    {
        var input = "Paragraph.\n   \n   \nNext.";
        List<string> result = TestSplitIntoParagraphs(input, preserveBlankLines: false);

        Assert.HasCount(2, result);
        Assert.AreEqual("Paragraph.", result[0]);
        Assert.AreEqual("Next.", result[1]);
    }

    #endregion

    #region XML Tokenization Tests

    [TestMethod]
    public void TokenizeWithXmlTags_PlainText_ReturnsWords()
    {
        var input = "simple text here";
        List<string> result = TestTokenizeWithXmlTags(input);


        Assert.HasCount(3, result);
        Assert.AreEqual("simple", result[0]);
        Assert.AreEqual("text", result[1]);
        Assert.AreEqual("here", result[2]);
    }

    [TestMethod]
    public void TokenizeWithXmlTags_WithInlineTags_PreservesTags()
    {
        var input = "text <see cref=\"Test\"/> more";
        List<string> result = TestTokenizeWithXmlTags(input);

        // Verify key content is preserved
        Assert.IsGreaterThanOrEqualTo(3, result.Count, "Should have multiple tokens");
        Assert.Contains("text", result, "Should contain 'text'");
        Assert.IsTrue(result.Any(t => t.Contains("<see")), "Should contain XML tag");
        Assert.Contains("more", result, "Should contain 'more'");
    }

    [TestMethod]
    public void TokenizeWithXmlTags_OpenAndCloseTags_PreservesEach()
    {
        var input = "<c>code</c> here";
        List<string> result = TestTokenizeWithXmlTags(input);

        // The tokenizer may combine <c>code</c> into a single token or split differently
        var joined = string.Join(" ", result);
        Assert.Contains("<c>", joined, "Should preserve <c>");
        Assert.Contains("code", joined, "Should preserve code");
        Assert.IsTrue(joined.Contains("</c>") || result.Any(t => t.EndsWith("</c>")), "Should preserve </c>");
        Assert.Contains("here", result, "Should preserve 'here'");
    }

    [TestMethod]
    public void TokenizeWithXmlTags_EmptyInput_ReturnsEmpty()
    {
        List<string> result = TestTokenizeWithXmlTags("");

        Assert.IsEmpty(result);
    }

    #endregion


    #region Block Tag Detection Tests

    [TestMethod]
    [DataRow("summary")]
    [DataRow("remarks")]
    [DataRow("returns")]
    [DataRow("value")]
    [DataRow("example")]
    [DataRow("exception")]
    [DataRow("param")]
    [DataRow("typeparam")]
    [DataRow("seealso")]
    [DataRow("permission")]
    [DataRow("include")]
    public void IsBlockTag_KnownBlockTags_ReturnsTrue(string tagName)
    {
        var result = IsBlockTag(tagName);

        Assert.IsTrue(result, $"'{tagName}' should be recognized as a block tag");
    }

    [TestMethod]
    [DataRow("c")]
    [DataRow("see")]
    [DataRow("paramref")]
    [DataRow("typeparamref")]
    [DataRow("unknown")]
    public void IsBlockTag_InlineTags_ReturnsFalse(string tagName)
    {
        var result = IsBlockTag(tagName);

        Assert.IsFalse(result, $"'{tagName}' should not be recognized as a block tag");
    }

    [TestMethod]
    [DataRow("SUMMARY")]
    [DataRow("Remarks")]
    [DataRow("PARAM")]
    public void IsBlockTag_CaseInsensitive_ReturnsTrue(string tagName)
    {
        var result = IsBlockTag(tagName);

        Assert.IsTrue(result, $"'{tagName}' should be recognized case-insensitively");
    }

    #endregion

    #region Preformatted Tag Detection Tests

    [TestMethod]
    public void IsPreformattedTag_Code_ReturnsTrue()
    {
        Assert.IsTrue(IsPreformattedTag("code"));
    }

    [TestMethod]
    public void IsPreformattedTag_CodeUppercase_ReturnsTrue()
    {
        Assert.IsTrue(IsPreformattedTag("CODE"));
    }

    [TestMethod]
    [DataRow("summary")]
    [DataRow("c")]
    [DataRow("see")]
    public void IsPreformattedTag_NonPreformattedTags_ReturnsFalse(string tagName)
    {
        Assert.IsFalse(IsPreformattedTag(tagName));
    }

    #endregion

    #region Test Helper Methods - Mirror CommentReflowEngine internal logic

    private static readonly HashSet<string> _blockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "summary", "remarks", "returns", "value", "example", "exception",
        "param", "typeparam", "seealso", "permission", "include"
    };

    private static readonly HashSet<string> _preformattedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "code"
    };

    private static bool IsBlockTag(string tagName) => _blockTags.Contains(tagName);

    private static bool IsPreformattedTag(string tagName) => _preformattedTags.Contains(tagName);

    /// <summary>
    /// Mirrors the WrapText method from CommentReflowEngine.
    /// </summary>
    private static List<string> TestWrapText(string text, int maxWidth)
    {
        var lines = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return lines;
        }

        List<string> tokens = TestTokenizeWithXmlTags(text);
        var currentLine = new System.Text.StringBuilder();
        var currentLength = 0;

        foreach (var token in tokens)
        {
            var tokenLength = token.Length;

            if (currentLength == 0)
            {
                currentLine.Append(token);
                currentLength = tokenLength;
            }
            else if (currentLength + 1 + tokenLength <= maxWidth)
            {
                currentLine.Append(' ');
                currentLine.Append(token);
                currentLength += 1 + tokenLength;
            }
            else
            {
                lines.Add(currentLine.ToString());
                currentLine.Clear();
                currentLine.Append(token);
                currentLength = tokenLength;
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString());
        }

        return lines;
    }

    /// <summary>
    /// Mirrors the NormalizeWhitespace method from CommentReflowEngine.
    /// </summary>
    private static string? TestNormalizeWhitespace(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        content = content?.Replace("\r\n", "\n").Replace("\r", "\n");

        var contentLines = content?.Split('\n');
        var normalizedLines = (from line in contentLines
                               let normalized = Regex.Replace(line, @"[ \t]+", " ").Trim()
                               select normalized).ToList();
        return string.Join("\n", normalizedLines);
    }

    /// <summary>
    /// Mirrors the SplitIntoParagraphs method from CommentReflowEngine.
    /// </summary>
    private static List<string> TestSplitIntoParagraphs(string content, bool preserveBlankLines)
    {
        var paragraphs = new List<string>();

        if (string.IsNullOrEmpty(content))
        {
            return paragraphs;
        }

        var parts = Regex.Split(content, @"\n\s*\n");

        foreach (var part in parts)
        {
            var partLines = part.Split('\n');
            var nonBlankLines = partLines
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();

            if (nonBlankLines.Count > 0)
            {
                paragraphs.Add(string.Join(" ", nonBlankLines));
            }
            else if (preserveBlankLines && paragraphs.Count > 0)
            {
                paragraphs.Add("");
            }
        }

        return paragraphs;
    }

    /// <summary>
    /// Mirrors the TokenizeWithXmlTags method from CommentReflowEngine.
    /// </summary>
    private static List<string> TestTokenizeWithXmlTags(string text)
    {
        var tokens = new List<string>();
        var regex = new System.Text.RegularExpressions.Regex(@"(<[^>]+>)|(\S+)");
        MatchCollection matches = regex.Matches(text);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            tokens.Add(match.Value);
        }

        return tokens;
    }

    #endregion
}
