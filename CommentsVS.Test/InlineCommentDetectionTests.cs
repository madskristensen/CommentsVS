using CommentsVS.Services;

namespace CommentsVS.Test;

/// <summary>
/// Tests for inline comment detection functionality.
/// These tests ensure that comment tags and issue references work in both full-line and inline comments.
/// </summary>
[TestClass]
public sealed class InlineCommentDetectionTests
{
    #region FindCommentSpans Tests

    [TestMethod]
    public void FindCommentSpans_FullLineComment_ReturnsFullLine()
    {
        var text = "// TODO: fix this";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.HasCount(1, spans);
        Assert.AreEqual(0, spans[0].Start);
        Assert.AreEqual(text.Length, spans[0].Length);
    }

    [TestMethod]
    public void FindCommentSpans_InlineComment_ReturnsCorrectSpan()
    {
        var text = "var x = 5; // TODO: fix this";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.HasCount(1, spans);
        Assert.AreEqual(11, spans[0].Start, "Comment should start at position 11");
        Assert.AreEqual(17, spans[0].Length, "Comment length should be 17 characters (from // to end)");
    }

    [TestMethod]
    public void FindCommentSpans_InlineCommentAfterCode_ReturnsCorrectSpan()
    {
        var text = "var repoInfo = new GitRepositoryInfo(...); // test #123";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.HasCount(1, spans);
        Assert.IsGreaterThan(0, spans[0].Start, "Comment should start after code");
        Assert.StartsWith("//", text.Substring(spans[0].Start), "Should start with //");
    }

    [TestMethod]
    public void FindCommentSpans_NoComment_ReturnsEmpty()
    {
        var text = "var x = 5;";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.IsEmpty(spans);
    }

    [TestMethod]
    public void FindCommentSpans_CommentWithSpaces_ReturnsCorrectSpan()
    {
        var text = "    // TODO: indented comment";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.HasCount(1, spans);
        Assert.AreEqual(0, spans[0].Start);
        Assert.AreEqual(text.Length, spans[0].Length);
    }

    #endregion

    #region IsInsideStringLiteral Tests

    [TestMethod]
    public void IsInsideStringLiteral_InsideRegularString_ReturnsTrue()
    {
        var text = "var s = \"test string\";";
        var position = 12; // Inside "test string"

        var result = TestHelpers.IsInsideStringLiteral(text, position);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsInsideStringLiteral_OutsideString_ReturnsFalse()
    {
        var text = "var s = \"test\"; // comment";
        var position = 20; // In the comment

        var result = TestHelpers.IsInsideStringLiteral(text, position);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsInsideStringLiteral_VerbatimString_ReturnsTrue()
    {
        var text = "var path = @\"C:\\temp\\\\file.txt\";";
        var position = 18; // Inside the verbatim string

        var result = TestHelpers.IsInsideStringLiteral(text, position);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsInsideStringLiteral_EscapedQuote_ReturnsTrue()
    {
        var text = "var s = \"Hello \\\"World\\\"\";";
        var position = 15; // Inside the string after escaped quote

        var result = TestHelpers.IsInsideStringLiteral(text, position);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsInsideStringLiteral_DoubleQuoteInVerbatim_ReturnsTrue()
    {
        var text = "var s = @\"path\\\\file\";";
        var position = 15; // Inside the verbatim string

        var result = TestHelpers.IsInsideStringLiteral(text, position);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsInsideStringLiteral_AfterVerbatimString_ReturnsFalse()
    {
        var text = "var path = @\"C:\\temp\"; // comment";
        var position = 25; // In the comment

        var result = TestHelpers.IsInsideStringLiteral(text, position);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsInsideStringLiteral_BeforeString_ReturnsFalse()
    {
        var text = "var s = \"test\";";
        var position = 5; // Before the string starts

        var result = TestHelpers.IsInsideStringLiteral(text, position);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsInsideStringLiteral_MultipleStrings_FirstString_ReturnsTrue()
    {
        var text = "var s1 = \"first\"; var s2 = \"second\";";
        var position = 12; // Inside "first"

        var result = TestHelpers.IsInsideStringLiteral(text, position);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsInsideStringLiteral_MultipleStrings_SecondString_ReturnsTrue()
    {
        var text = "var s1 = \"first\"; var s2 = \"second\";";
        var position = 30; // Inside "second"

        var result = TestHelpers.IsInsideStringLiteral(text, position);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsInsideStringLiteral_MultipleStrings_BetweenStrings_ReturnsFalse()
    {
        var text = "var s1 = \"first\"; var s2 = \"second\";";
        var position = 20; // Between the two strings

        var result = TestHelpers.IsInsideStringLiteral(text, position);

        Assert.IsFalse(result);
    }

    #endregion

    #region Integration Tests - Comment in String

    [TestMethod]
    public void FindCommentSpans_CommentAfterString_DetectsIt()
    {
        // Use a string without // in it
        var text = "var url = \"example.com\"; // Real comment";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.HasCount(1, spans);
        Assert.IsGreaterThanOrEqualTo(25, spans[0].Start, $"Should detect comment after position 25");
    }

    [TestMethod]
    public void FindCommentSpans_SlashesInVerbatimString_IgnoresThem()
    {
        // This test case actually has // inside the string, which our algorithm correctly ignores
        // because it checks IsInsideStringLiteral. There's no actual comment here!
        var text = "var path = @\"C:\\\\temp\\\\//file.txt\";";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        // No comment should be found because // is inside the string
        Assert.IsEmpty(spans, "Should not detect // inside verbatim string as a comment");
    }

    [TestMethod]
    public void FindCommentSpans_RealComment_AfterCode_DetectsIt()
    {
        // Use real code without URLs
        var text = "var s = \"test string\"; // TODO: add validation";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.HasCount(1, spans);
        var commentText = text.Substring(spans[0].Start, spans[0].Length);
        Assert.Contains("TODO", commentText);
        Assert.DoesNotContain("test string", commentText);
    }

    [TestMethod]
    public void FindCommentSpans_UrlLikeTokenWithoutComment_ReturnsEmpty()
    {
        var text = "var endpoint = http://example.com/api";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.IsEmpty(spans);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void FindCommentSpans_EmptyString_ReturnsEmpty()
    {
        var text = "";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.IsEmpty(spans);
    }

    [TestMethod]
    public void FindCommentSpans_OnlyWhitespace_ReturnsEmpty()
    {
        var text = "    ";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.IsEmpty(spans);
    }

    [TestMethod]
    public void FindCommentSpans_CommentAtEndOfLine_ReturnsCorrectSpan()
    {
        var text = "var x = 5; //";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.HasCount(1, spans);
        Assert.AreEqual(11, spans[0].Start);
        Assert.AreEqual(2, spans[0].Length); // Just the "//"
    }

    [TestMethod]
    public void IsInsideStringLiteral_ConsecutiveBackslashes_HandlesProperly()
    {
        var text = "var s = \"path\\\\\\\\file\";";
        var position = 15; // Inside the string after backslashes

        var result = TestHelpers.IsInsideStringLiteral(text, position);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsInsideStringLiteral_OddNumberOfBackslashesBeforeQuote_EscapesQuote()
    {
        var text = "var s = \"test\\\"quote\";";
        var position = 16; // After the escaped quote, still in string

        var result = TestHelpers.IsInsideStringLiteral(text, position);

        Assert.IsTrue(result);
    }

    #endregion

    #region Real-World Scenarios

    [TestMethod]
    public void FindCommentSpans_RealCodeWithIssueReference_DetectsComment()
    {
        // Note: URLs contain :// which our simple // finder would miss. That's actually correct behavior!
        var text = "var repoInfo = GitRepositoryInfo.Parse(url); // test #123";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.HasCount(1, spans);
        var commentText = text.Substring(spans[0].Start, spans[0].Length);
        Assert.Contains("#123", commentText);
    }

    [TestMethod]
    public void FindCommentSpans_CodeWithTodoTag_DetectsComment()
    {
        var text = "return null; // TODO: implement this feature";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.HasCount(1, spans);
        var commentText = text.Substring(spans[0].Start, spans[0].Length);
        Assert.Contains("TODO", commentText);
    }

    [TestMethod]
    public void FindCommentSpans_ComplexLineWithMetadata_DetectsComment()
    {
        var text = "var x = Calculate(param1, param2); // TODO(@john)[#456]: optimize algorithm";

        var spans = TestHelpers.FindCommentSpans(text).ToList();

        Assert.HasCount(1, spans);
        var commentText = text.Substring(spans[0].Start, spans[0].Length);
        Assert.Contains("TODO", commentText);
        Assert.Contains("@john", commentText);
        Assert.Contains("#456", commentText);
    }

    #endregion
}

/// <summary>
/// Helper class to expose private methods for testing.
/// In a real implementation, these would be extracted to a testable utility class
/// or made internal with InternalsVisibleTo.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Mirror of the FindCommentSpans implementation from IssueReferenceTagger.
    /// </summary>
    public static IEnumerable<(int Start, int Length)> FindCommentSpans(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        // Check if entire line is a comment (starts with comment prefix)
        if (LanguageCommentStyle.IsCommentLine(text))
        {
            yield return (0, text.Length);
            yield break;
        }

        // Look for inline single-line comments (//), skipping URL-like tokens such as http://
        var searchIndex = 0;
        while (searchIndex < text.Length)
        {
            var inlineCommentIndex = text.IndexOf("//", searchIndex, StringComparison.Ordinal);
            if (inlineCommentIndex < 0)
            {
                break;
            }

            if (inlineCommentIndex > 0 && text[inlineCommentIndex - 1] == ':')
            {
                searchIndex = inlineCommentIndex + 2;
                continue;
            }

            // Make sure it's not inside a string literal
            if (!IsInsideStringLiteral(text, inlineCommentIndex))
            {
                yield return (inlineCommentIndex, text.Length - inlineCommentIndex);
                yield break;
            }

            searchIndex = inlineCommentIndex + 2;
        }
    }

    /// <summary>
    /// Mirror of the IsInsideStringLiteral implementation from IssueReferenceTagger.
    /// </summary>
    public static bool IsInsideStringLiteral(string text, int position)
    {
        var quoteCount = 0;
        var inVerbatim = false;

        for (var i = 0; i < position; i++)
        {
            if (text[i] == '@' && i + 1 < text.Length && text[i + 1] == '"')
            {
                inVerbatim = true;
                quoteCount++;
                i++; // Skip the quote
                continue;
            }

            if (text[i] == '"')
            {
                // Check if it's escaped (not in verbatim)
                if (!inVerbatim && i > 0 && text[i - 1] == '\\')
                {
                    // Count consecutive backslashes
                    var backslashCount = 0;
                    for (var j = i - 1; j >= 0 && text[j] == '\\'; j--)
                    {
                        backslashCount++;
                    }
                    // If odd number of backslashes, the quote is escaped
                    if (backslashCount % 2 == 1)
                    {
                        continue;
                    }
                }

                quoteCount++;

                // If we were in verbatim and hit a quote, check for double-quote escape
                if (inVerbatim && i + 1 < text.Length && text[i + 1] == '"')
                {
                    i++; // Skip the second quote in ""
                    continue;
                }

                if (quoteCount % 2 == 0)
                {
                    inVerbatim = false;
                }
            }
        }

        // Odd quote count means we're inside a string
        return quoteCount % 2 == 1;
    }
}
