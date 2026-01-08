using System.Text.RegularExpressions;
using CommentsVS.ToolWindows;

namespace CommentsVS.Test;

/// <summary>
/// Tests for AnchorService regex matching and metadata parsing logic.
/// Uses mirrored regex patterns since CommentPatterns is internal.
/// </summary>
[TestClass]
public sealed class AnchorServiceTests
{
    #region Mirrored Regex Patterns from CommentPatterns (internal class)

    /// <summary>
    /// Mirror of CommentPatterns.AnchorKeywordsPattern
    /// </summary>
    private const string _anchorKeywordsPattern = "TODO|HACK|NOTE|BUG|FIXME|UNDONE|REVIEW|ANCHOR";

    /// <summary>
    /// Mirror of CommentPatterns.AnchorServiceRegex
    /// </summary>
    private static readonly Regex _anchorServiceRegex = new(
        @"(?<prefix>//|/\*|'|<!--)\s*(?<tag>\b(?:" + _anchorKeywordsPattern + @")\b)\s*(?<metadata>(?:\([^)]*\)|\[[^\]]*\]))?\s*:?\s*(?<message>.*?)(?:\*/|-->|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Mirror of CommentPatterns.CommentTagRegex
    /// </summary>
    private static readonly Regex _commentTagRegex = new(
        @"\b(?<tag>" + _anchorKeywordsPattern + @"|LINK)\b:?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Mirror of Constants.AnchorKeywords
    /// </summary>
    private static readonly string[] _anchorKeywords =
        ["TODO", "HACK", "NOTE", "BUG", "FIXME", "UNDONE", "REVIEW", "ANCHOR"];

    #endregion

    #region AnchorType Parsing Tests

    [TestMethod]
    [DataRow("TODO", AnchorType.Todo)]
    [DataRow("HACK", AnchorType.Hack)]
    [DataRow("NOTE", AnchorType.Note)]
    [DataRow("BUG", AnchorType.Bug)]
    [DataRow("FIXME", AnchorType.Fixme)]
    [DataRow("UNDONE", AnchorType.Undone)]
    [DataRow("REVIEW", AnchorType.Review)]
    [DataRow("ANCHOR", AnchorType.Anchor)]
    public void Parse_ValidAnchorType_ReturnsCorrectType(string value, AnchorType expected)
    {
        AnchorType? result = AnchorTypeExtensions.Parse(value);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("todo")]
    [DataRow("Todo")]
    [DataRow("ToDo")]
    public void Parse_CaseInsensitive_ReturnsCorrectType(string value)
    {
        AnchorType? result = AnchorTypeExtensions.Parse(value);

        Assert.AreEqual(AnchorType.Todo, result);
    }

    [TestMethod]
    public void Parse_UnknownType_ReturnsNull()
    {
        AnchorType? result = AnchorTypeExtensions.Parse("UNKNOWN");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Parse_EmptyString_ReturnsNull()
    {
        AnchorType? result = AnchorTypeExtensions.Parse("");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Parse_NullString_ReturnsNull()
    {
        AnchorType? result = AnchorTypeExtensions.Parse(null);

        Assert.IsNull(result);
    }

    #endregion

    #region AnchorType Display Name Tests

    [TestMethod]
    [DataRow(AnchorType.Todo, "TODO")]
    [DataRow(AnchorType.Hack, "HACK")]
    [DataRow(AnchorType.Note, "NOTE")]
    [DataRow(AnchorType.Bug, "BUG")]
    [DataRow(AnchorType.Fixme, "FIXME")]
    [DataRow(AnchorType.Undone, "UNDONE")]
    [DataRow(AnchorType.Review, "REVIEW")]
    [DataRow(AnchorType.Anchor, "ANCHOR")]
    public void GetDisplayName_ReturnsUppercaseName(AnchorType anchorType, string expected)
    {
        var result = anchorType.GetDisplayName();

        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Anchor Regex Matching Tests

    [TestMethod]
    public void AnchorServiceRegex_MatchesTodo_ReturnsCorrectGroups()
    {
        var text = "// TODO: Fix this issue";

        Match match = _anchorServiceRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("//", match.Groups["prefix"].Value);
        Assert.AreEqual("TODO", match.Groups["tag"].Value);
        Assert.AreEqual("Fix this issue", match.Groups["message"].Value.Trim());
    }

    [TestMethod]
    public void AnchorServiceRegex_MatchesHack_ReturnsCorrectGroups()
    {
        var text = "// HACK: Temporary workaround";

        Match match = _anchorServiceRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("HACK", match.Groups["tag"].Value);
        Assert.AreEqual("Temporary workaround", match.Groups["message"].Value.Trim());
    }

    [TestMethod]
    public void AnchorServiceRegex_MatchesWithMetadata_CapturesMetadata()
    {
        var text = "// TODO(@mads): Review this code";

        Match match = _anchorServiceRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("TODO", match.Groups["tag"].Value);
        Assert.IsTrue(match.Groups["metadata"].Success);
        Assert.AreEqual("(@mads)", match.Groups["metadata"].Value);
    }

    [TestMethod]
    public void AnchorServiceRegex_MatchesWithBracketMetadata_CapturesMetadata()
    {
        var text = "// BUG[#123]: This causes a crash";

        Match match = _anchorServiceRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("BUG", match.Groups["tag"].Value);
        Assert.AreEqual("[#123]", match.Groups["metadata"].Value);
    }

    [TestMethod]
    public void AnchorServiceRegex_CaseInsensitive_MatchesLowercase()
    {
        var text = "// todo: lowercase anchor";

        Match match = _anchorServiceRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("todo", match.Groups["tag"].Value);
    }

    [TestMethod]
    public void AnchorServiceRegex_BlockComment_Matches()
    {
        var text = "/* TODO: Block comment anchor */";

        Match match = _anchorServiceRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("/*", match.Groups["prefix"].Value);
        Assert.AreEqual("TODO", match.Groups["tag"].Value);
    }

    [TestMethod]
    public void AnchorServiceRegex_VBComment_Matches()
    {
        var text = "' TODO: VB comment anchor";

        Match match = _anchorServiceRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("'", match.Groups["prefix"].Value);
        Assert.AreEqual("TODO", match.Groups["tag"].Value);
    }

    [TestMethod]
    public void AnchorServiceRegex_HtmlComment_Matches()
    {
        var text = "<!-- TODO: HTML comment anchor -->";

        Match match = _anchorServiceRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("<!--", match.Groups["prefix"].Value);
        Assert.AreEqual("TODO", match.Groups["tag"].Value);
    }

    [TestMethod]
    public void AnchorServiceRegex_NoComment_DoesNotMatch()
    {
        var text = "var todo = \"TODO\";"; // String literal, not a comment

        Match match = _anchorServiceRegex.Match(text);

        Assert.IsFalse(match.Success);
    }

    [TestMethod]
    public void AnchorServiceRegex_AnchorWithId_CapturesMetadata()
    {
        var text = "// ANCHOR(my-section): Section header";

        Match match = _anchorServiceRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("ANCHOR", match.Groups["tag"].Value);
        Assert.AreEqual("(my-section)", match.Groups["metadata"].Value);
    }

    #endregion

    #region Metadata Parsing Tests

    [TestMethod]
    public void ParseMetadata_WithOwner_ExtractsOwner()
    {
        var metadata = "(@mads)";

        (var owner, var issue, var anchorId) = TestParseMetadata(metadata, AnchorType.Todo);

        Assert.AreEqual("mads", owner);
        Assert.IsNull(issue);
        Assert.IsNull(anchorId);
    }

    [TestMethod]
    public void ParseMetadata_WithIssue_ExtractsIssue()
    {
        var metadata = "[#123]";

        (var owner, var issue, var anchorId) = TestParseMetadata(metadata, AnchorType.Bug);

        Assert.IsNull(owner);
        Assert.AreEqual("#123", issue);
        Assert.IsNull(anchorId);
    }

    [TestMethod]
    public void ParseMetadata_WithOwnerAndIssue_ExtractsBoth()
    {
        var metadata = "(@mads #456)";

        (var owner, var issue, var anchorId) = TestParseMetadata(metadata, AnchorType.Todo);

        Assert.AreEqual("mads", owner);
        Assert.AreEqual("#456", issue);
    }

    [TestMethod]
    public void ParseMetadata_AnchorType_ExtractsAnchorId()
    {
        var metadata = "(my-anchor-id)";

        (var owner, var issue, var anchorId) = TestParseMetadata(metadata, AnchorType.Anchor);

        Assert.IsNull(owner);
        Assert.IsNull(issue);
        Assert.AreEqual("my-anchor-id", anchorId);
    }

    [TestMethod]
    public void ParseMetadata_AnchorTypeWithBrackets_ExtractsAnchorId()
    {
        var metadata = "[section-name]";

        (var owner, var issue, var anchorId) = TestParseMetadata(metadata, AnchorType.Anchor);

        Assert.IsNull(owner);
        Assert.IsNull(issue);
        Assert.AreEqual("section-name", anchorId);
    }

    [TestMethod]
    public void ParseMetadata_Empty_ReturnsNulls()
    {
        (var owner, var issue, var anchorId) = TestParseMetadata("", AnchorType.Todo);

        Assert.IsNull(owner);
        Assert.IsNull(issue);
        Assert.IsNull(anchorId);
    }

    [TestMethod]
    public void ParseMetadata_Null_ReturnsNulls()
    {
        (var owner, var issue, var anchorId) = TestParseMetadata(null, AnchorType.Todo);

        Assert.IsNull(owner);
        Assert.IsNull(issue);
        Assert.IsNull(anchorId);
    }


    #endregion

    #region Comment Tag Regex Tests

    [TestMethod]
    [DataRow("TODO")]
    [DataRow("HACK")]
    [DataRow("NOTE")]
    [DataRow("BUG")]
    [DataRow("FIXME")]
    [DataRow("UNDONE")]
    [DataRow("REVIEW")]
    [DataRow("ANCHOR")]
    [DataRow("LINK")]
    public void CommentTagRegex_MatchesAllKeywords(string keyword)
    {
        var text = $"// {keyword}: message";

        Match match = _commentTagRegex.Match(text);

        Assert.IsTrue(match.Success, $"Should match {keyword}");
        Assert.AreEqual(keyword, match.Groups["tag"].Value);
    }

    [TestMethod]
    public void CommentTagRegex_MatchesWithoutColon()
    {
        var text = "// TODO message";

        Match match = _commentTagRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("TODO", match.Groups["tag"].Value);
    }

    [TestMethod]
    public void CommentTagRegex_CaseInsensitive()
    {
        var text = "// todo: lowercase";

        Match match = _commentTagRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("todo", match.Groups["tag"].Value);
    }

    #endregion

    #region Anchor Keywords Constants Tests

    [TestMethod]
    public void AnchorKeywords_ContainsAllExpectedKeywords()
    {
        var expectedKeywords = new[] { "TODO", "HACK", "NOTE", "BUG", "FIXME", "UNDONE", "REVIEW", "ANCHOR" };

        foreach (var keyword in expectedKeywords)
        {
            Assert.IsTrue(
                _anchorKeywords.Contains(keyword, StringComparer.OrdinalIgnoreCase),
                $"AnchorKeywords should contain {keyword}");
        }
    }

    #endregion

    #region Test Helper Methods - Mirror AnchorService logic

    private static readonly Regex _ownerRegex = new(
        @"@(\w+)",
        RegexOptions.Compiled);

    private static readonly Regex _issueRegex = new(
        @"#(\d+)",
        RegexOptions.Compiled);

    /// <summary>
    /// Mirrors the ParseMetadata method from AnchorService.
    /// </summary>
    private static (string? owner, string? issueReference, string? anchorId) TestParseMetadata(
        string? rawMetadata, AnchorType anchorType)
    {
        if (string.IsNullOrEmpty(rawMetadata))
        {
            return (null, null, null);
        }

        string? owner = null;
        string? issueReference = null;
        string? anchorId = null;

        // Extract owner (@username)
        Match ownerMatch = _ownerRegex.Match(rawMetadata);
        if (ownerMatch.Success)
        {
            owner = ownerMatch.Groups[1].Value;
        }

        // Extract issue reference (#123)
        Match issueMatch = _issueRegex.Match(rawMetadata);
        if (issueMatch.Success)
        {
            issueReference = "#" + issueMatch.Groups[1].Value;
        }

        // For ANCHOR type, the metadata content is the anchor ID
        if (anchorType == AnchorType.Anchor)
        {
            // Strip parentheses/brackets and use as anchor ID
            var content = rawMetadata?.Trim('(', ')', '[', ']');
            if (!string.IsNullOrWhiteSpace(content) && owner == null && issueReference == null)
            {
                anchorId = content;
            }
        }

        return (owner, issueReference, anchorId);
    }

    #endregion
}
