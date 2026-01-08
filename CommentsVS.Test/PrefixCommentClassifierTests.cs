using System.Text.RegularExpressions;

namespace CommentsVS.Test;

[TestClass]
public sealed class PrefixCommentClassifierTests
{
    // Regex patterns for different comment styles - same as in PrefixCommentClassifier
    private static readonly Regex _cStyleRegex = new(
        @"(?<prefix>//)\s*(?<marker>[!?*\->]|//)\s*(?<content>.*?)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex _hashStyleRegex = new(
        @"(?<prefix>#)\s*(?<marker>[!?*\->])\s*(?<content>.*?)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex _vbStyleRegex = new(
        @"(?<prefix>')\s*(?<marker>[!?*\->])\s*(?<content>.*?)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex _sqlStyleRegex = new(
        @"(?<prefix>--)\s*(?<marker>[!?*\->])\s*(?<content>.*?)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    #region C-Style Comments (//)

    [TestMethod]
    public void WhenCStyleAlertPrefixThenMatchesCorrectly()
    {
        var text = "// ! This is an alert";
        Match match = _cStyleRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("//", match.Groups["prefix"].Value);
        Assert.AreEqual("!", match.Groups["marker"].Value);
        Assert.AreEqual("This is an alert", match.Groups["content"].Value);
    }

    [TestMethod]
    public void WhenCStyleAlertPrefixWithNoSpaceThenMatchesCorrectly()
    {
        var text = "//! Critical alert";
        Match match = _cStyleRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("!", match.Groups["marker"].Value);
        Assert.AreEqual("Critical alert", match.Groups["content"].Value);
    }

    [TestMethod]
    public void WhenCStyleQueryPrefixThenMatchesCorrectly()
    {
        var text = "// ? Why is this returning null?";
        Match match = _cStyleRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("?", match.Groups["marker"].Value);
        Assert.AreEqual("Why is this returning null?", match.Groups["content"].Value);
    }

    [TestMethod]
    public void WhenCStyleImportantPrefixThenMatchesCorrectly()
    {
        var text = "// * Important: Call Initialize first";
        Match match = _cStyleRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("*", match.Groups["marker"].Value);
        Assert.AreEqual("Important: Call Initialize first", match.Groups["content"].Value);
    }

    [TestMethod]
    public void WhenCStyleStrikethroughPrefixThenMatchesCorrectly()
    {
        var text = "// // Old code kept for reference";
        Match match = _cStyleRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("//", match.Groups["marker"].Value);
        Assert.AreEqual("Old code kept for reference", match.Groups["content"].Value);
    }

    [TestMethod]
    public void WhenCStyleDisabledPrefixThenMatchesCorrectly()
    {
        var text = "// - Disabled pending review";
        Match match = _cStyleRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("-", match.Groups["marker"].Value);
        Assert.AreEqual("Disabled pending review", match.Groups["content"].Value);
    }

    [TestMethod]
    public void WhenCStyleQuotePrefixThenMatchesCorrectly()
    {
        var text = "// > From the API docs: Returns -1 on failure";
        Match match = _cStyleRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual(">", match.Groups["marker"].Value);
        Assert.AreEqual("From the API docs: Returns -1 on failure", match.Groups["content"].Value);
    }

    [TestMethod]
    public void WhenCStyleRegularCommentThenNoMatch()
    {
        var text = "// Regular comment without prefix";
        Match match = _cStyleRegex.Match(text);

        Assert.IsFalse(match.Success);
    }

    [TestMethod]
    public void WhenCStyleMultipleLinesThenMatchesEach()
    {
        var text = @"// ! First alert
// ? Second query
// Regular comment
// * Third important";
        MatchCollection matches = _cStyleRegex.Matches(text);

        Assert.HasCount(3, matches);
        Assert.AreEqual("!", matches[0].Groups["marker"].Value);
        Assert.AreEqual("?", matches[1].Groups["marker"].Value);
        Assert.AreEqual("*", matches[2].Groups["marker"].Value);
    }

    #endregion

    #region Issue #20 - Char Literals

    [TestMethod]
    public void WhenCharLiteralWithSpecialCharacterThenCStyleRegexNoMatch()
    {
        // Issue #20: C-style regex should NOT match '*' inside character literals
        var text = "if (nextChar == '*' && PeekChar() == '/')";
        Match match = _cStyleRegex.Match(text);

        // C-style regex only looks for // prefix, not '
        Assert.IsFalse(match.Success);
    }

    #endregion

    #region Hash-Style Comments (#)

    [TestMethod]
    public void WhenHashStyleAlertPrefixThenMatchesCorrectly()
    {
        var text = "# ! PowerShell alert";
        Match match = _hashStyleRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("#", match.Groups["prefix"].Value);
        Assert.AreEqual("!", match.Groups["marker"].Value);
    }

    #endregion

    #region VB-Style Comments (')

    [TestMethod]
    public void WhenVbStyleQueryPrefixThenMatchesCorrectly()
    {
        var text = "' ? VB question comment";
        Match match = _vbStyleRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("'", match.Groups["prefix"].Value);
        Assert.AreEqual("?", match.Groups["marker"].Value);
    }

    #endregion

    #region SQL-Style Comments (--)

    [TestMethod]
    public void WhenSqlStyleAlertPrefixThenMatchesCorrectly()
    {
        var text = "-- ! SQL alert comment";
        Match match = _sqlStyleRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("--", match.Groups["prefix"].Value);
        Assert.AreEqual("!", match.Groups["marker"].Value);
    }

    [TestMethod]
    public void WhenSqlStyleImportantPrefixThenMatchesCorrectly()
    {
        var text = "-- * Important SQL note";
        Match match = _sqlStyleRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("*", match.Groups["marker"].Value);
    }

    #endregion
}
