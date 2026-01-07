using System.Text.RegularExpressions;

namespace CommentsVS.Test;

[TestClass]
public sealed class PrefixCommentClassifierTests
{
    // Regex to match comment prefixes - same as in PrefixCommentClassifier
    private static readonly Regex _prefixRegex = new(
        @"(?<prefix>//|#|')\s*(?<marker>[!?*\->]|//)\s*(?<content>.*?)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    #region Alert Prefix (!)

    [TestMethod]
    public void WhenAlertPrefixThenMatchesCorrectly()
    {
        var text = "// ! This is an alert";
        Match match = _prefixRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("//", match.Groups["prefix"].Value);
        Assert.AreEqual("!", match.Groups["marker"].Value);
        Assert.AreEqual("This is an alert", match.Groups["content"].Value);
    }

    [TestMethod]
    public void WhenAlertPrefixWithNoSpaceThenMatchesCorrectly()
    {
        var text = "//! Critical alert";
        Match match = _prefixRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("!", match.Groups["marker"].Value);
        Assert.AreEqual("Critical alert", match.Groups["content"].Value);
    }

    #endregion

    #region Query Prefix (?)

    [TestMethod]
    public void WhenQueryPrefixThenMatchesCorrectly()
    {
        var text = "// ? Why is this returning null?";
        Match match = _prefixRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("?", match.Groups["marker"].Value);
        Assert.AreEqual("Why is this returning null?", match.Groups["content"].Value);
    }

    #endregion

    #region Important Prefix (*)

    [TestMethod]
    public void WhenImportantPrefixThenMatchesCorrectly()
    {
        var text = "// * Important: Call Initialize first";
        Match match = _prefixRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("*", match.Groups["marker"].Value);
        Assert.AreEqual("Important: Call Initialize first", match.Groups["content"].Value);
    }

    #endregion

    #region Strikethrough Prefix (//)

    [TestMethod]
    public void WhenStrikethroughPrefixThenMatchesCorrectly()
    {
        var text = "// // Old code kept for reference";
        Match match = _prefixRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("//", match.Groups["marker"].Value);
        Assert.AreEqual("Old code kept for reference", match.Groups["content"].Value);
    }

    #endregion

    #region Disabled Prefix (-)

    [TestMethod]
    public void WhenDisabledPrefixThenMatchesCorrectly()
    {
        var text = "// - Disabled pending review";
        Match match = _prefixRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("-", match.Groups["marker"].Value);
        Assert.AreEqual("Disabled pending review", match.Groups["content"].Value);
    }

    #endregion

    #region Quote Prefix (>)

    [TestMethod]
    public void WhenQuotePrefixThenMatchesCorrectly()
    {
        var text = "// > From the API docs: Returns -1 on failure";
        Match match = _prefixRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual(">", match.Groups["marker"].Value);
        Assert.AreEqual("From the API docs: Returns -1 on failure", match.Groups["content"].Value);
    }

    #endregion

    #region Regular Comments (No Match)

    [TestMethod]
    public void WhenRegularCommentThenNoMatch()
    {
        var text = "// Regular comment without prefix";
        Match match = _prefixRegex.Match(text);

        // Should not match because "R" is not a valid marker
        Assert.IsFalse(match.Success);
    }

    [TestMethod]
    public void WhenTodoCommentThenNoMatch()
    {
        var text = "// TODO: This should not match prefix highlighting";
        Match match = _prefixRegex.Match(text);

        // Should not match because "T" is not a valid marker
        Assert.IsFalse(match.Success);
    }

    #endregion

    #region Other Languages

    [TestMethod]
    public void WhenHashCommentWithAlertPrefixThenMatchesCorrectly()
    {
        var text = "# ! Python or PowerShell alert";
        Match match = _prefixRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("#", match.Groups["prefix"].Value);
        Assert.AreEqual("!", match.Groups["marker"].Value);
    }

    [TestMethod]
    public void WhenVbCommentWithQueryPrefixThenMatchesCorrectly()
    {
        var text = "' ? VB question comment";
        Match match = _prefixRegex.Match(text);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("'", match.Groups["prefix"].Value);
        Assert.AreEqual("?", match.Groups["marker"].Value);
    }

    #endregion

    #region Multiline

    [TestMethod]
    public void WhenMultipleLinesThenMatchesEach()
    {
        var text = @"// ! First alert
// ? Second query
// Regular comment
// * Third important";
        MatchCollection matches = _prefixRegex.Matches(text);

        Assert.AreEqual(3, matches.Count);
        Assert.AreEqual("!", matches[0].Groups["marker"].Value);
        Assert.AreEqual("?", matches[1].Groups["marker"].Value);
        Assert.AreEqual("*", matches[2].Groups["marker"].Value);
    }

    #endregion
}
