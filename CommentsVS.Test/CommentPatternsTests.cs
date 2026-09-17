using System.Text.RegularExpressions;
using CommentsVS.Services;

namespace CommentsVS.Test;

/// <summary>
/// Tests for CommentPatterns's pure regex-building methods (internal, exposed via InternalsVisibleTo).
/// These exercise the real production algorithms directly, without touching General.Instance.
/// </summary>
[TestClass]
public sealed class CommentPatternsTests
{
    [TestMethod]
    public void BuildAnchorKeywordsPattern_NullCustomTags_ReturnsBuiltInPattern()
    {
        var result = CommentPatterns.BuildAnchorKeywordsPattern(null);

        Assert.AreEqual(CommentPatterns.BuiltInAnchorKeywordsPattern, result);
    }

    [TestMethod]
    public void BuildAnchorKeywordsPattern_EmptyCustomTags_ReturnsBuiltInPattern()
    {
        var result = CommentPatterns.BuildAnchorKeywordsPattern("");

        Assert.AreEqual(CommentPatterns.BuiltInAnchorKeywordsPattern, result);
    }

    [TestMethod]
    public void BuildAnchorKeywordsPattern_WhitespaceCustomTags_ReturnsBuiltInPattern()
    {
        var result = CommentPatterns.BuildAnchorKeywordsPattern("   ");

        Assert.AreEqual(CommentPatterns.BuiltInAnchorKeywordsPattern, result);
    }

    [TestMethod]
    public void BuildAnchorKeywordsPattern_SingleCustomTag_AppendsToBuiltIn()
    {
        var result = CommentPatterns.BuildAnchorKeywordsPattern("PERF");

        Assert.AreEqual(CommentPatterns.BuiltInAnchorKeywordsPattern + "|PERF", result);
    }

    [TestMethod]
    public void BuildAnchorKeywordsPattern_MultipleCustomTags_AppendsAllUppercased()
    {
        var result = CommentPatterns.BuildAnchorKeywordsPattern("perf, security");

        Assert.Contains("PERF", result);
        Assert.Contains("SECURITY", result);
    }

    [TestMethod]
    public void BuildAnchorKeywordsPattern_DuplicateCustomTags_Deduplicated()
    {
        var result = CommentPatterns.BuildAnchorKeywordsPattern("PERF,perf,Perf");

        var occurrences = Regex.Matches(result, "PERF").Count;
        Assert.AreEqual(1, occurrences);
    }

    [TestMethod]
    public void BuildAnchorKeywordsPattern_TagWithRegexSpecialChars_IsEscaped()
    {
        var result = CommentPatterns.BuildAnchorKeywordsPattern("C++");

        // Regex.Escape("C++") == "C\+\+"
        Assert.Contains(@"C\+\+", result);
    }

    [TestMethod]
    public void BuildRegexPatterns_ClassificationRegex_MatchesTagAtCommentStart()
    {
        (Regex classification, _, _, _) = CommentPatterns.BuildRegexPatterns(CommentPatterns.BuiltInAnchorKeywordsPattern);

        Match match = classification.Match("// TODO: fix this");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("TODO", match.Groups["tag"].Value);
    }

    [TestMethod]
    public void BuildRegexPatterns_ClassificationRegex_DoesNotMatchMidSentence()
    {
        (Regex classification, _, _, _) = CommentPatterns.BuildRegexPatterns(CommentPatterns.BuiltInAnchorKeywordsPattern);

        Match match = classification.Match("// a straightforward bug fix");

        Assert.IsFalse(match.Success);
    }

    [TestMethod]
    public void BuildRegexPatterns_WithMetadataRegex_CapturesMetadataGroup()
    {
        (_, Regex withMetadata, _, _) = CommentPatterns.BuildRegexPatterns(CommentPatterns.BuiltInAnchorKeywordsPattern);

        Match match = withMetadata.Match("TODO(@mads): fix");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("(@mads)", match.Groups["metadata"].Value);
    }

    [TestMethod]
    public void BuildRegexPatterns_ServiceRegex_CapturesPrefixTagAndMessage()
    {
        (_, _, Regex service, _) = CommentPatterns.BuildRegexPatterns(CommentPatterns.BuiltInAnchorKeywordsPattern);

        Match match = service.Match("// BUG: this crashes");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("//", match.Groups["prefix"].Value);
        Assert.AreEqual("BUG", match.Groups["tag"].Value);
        Assert.AreEqual("this crashes", match.Groups["message"].Value.Trim());
    }

    [TestMethod]
    public void BuildRegexPatterns_MetadataParseRegex_MatchesParenMetadata()
    {
        (_, _, _, Regex metadataParse) = CommentPatterns.BuildRegexPatterns(CommentPatterns.BuiltInAnchorKeywordsPattern);

        Match match = metadataParse.Match("TODO(@mads): fix");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("TODO", match.Groups["tag"].Value);
        Assert.AreEqual("@mads", match.Groups["metaParen"].Value);
    }

    [TestMethod]
    public void BuildRegexPatterns_MetadataParseRegex_MatchesBracketMetadata()
    {
        (_, _, _, Regex metadataParse) = CommentPatterns.BuildRegexPatterns(CommentPatterns.BuiltInAnchorKeywordsPattern);

        Match match = metadataParse.Match("BUG[#123]: crash");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("BUG", match.Groups["tag"].Value);
        Assert.AreEqual("#123", match.Groups["metaBracket"].Value);
    }

    [TestMethod]
    public void BuildRegexPatterns_WithCustomTags_ClassificationMatchesCustomTag()
    {
        var keywords = CommentPatterns.BuildAnchorKeywordsPattern("PERF");
        (Regex classification, _, _, _) = CommentPatterns.BuildRegexPatterns(keywords);

        Match match = classification.Match("// PERF: slow query");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("PERF", match.Groups["tag"].Value);
    }

    [TestMethod]
    public void CommentTagRegex_MatchesLinkKeyword()
    {
        Match match = CommentPatterns.CommentTagRegex.Match("// LINK: see also");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("LINK", match.Groups["tag"].Value);
    }

    [TestMethod]
    public void CommentLineRegex_MatchesCStyleCommentPrefix()
    {
        Assert.IsTrue(CommentPatterns.CommentLineRegex.IsMatch("  // a comment"));
        Assert.IsTrue(CommentPatterns.CommentLineRegex.IsMatch("/* block */"));
        Assert.IsTrue(CommentPatterns.CommentLineRegex.IsMatch("' vb comment"));
    }

    [TestMethod]
    public void CommentLineRegex_DoesNotMatchCode()
    {
        Assert.IsFalse(CommentPatterns.CommentLineRegex.IsMatch("var x = 1;"));
    }
}
