using CommentsVS.Services;

namespace CommentsVS.Test;

[TestClass]
public sealed class LinkAnchorParserTests
{
    #region Basic File Links

    [TestMethod]
    public void Parse_BasicFilePath_ReturnsCorrectPath()
    {
        string text = "// LINK: path/to/file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("path/to/file.cs", results[0].FilePath);
        Assert.IsFalse(results[0].HasLineNumber);
        Assert.IsFalse(results[0].HasAnchor);
    }

    [TestMethod]
    public void Parse_RelativePath_ReturnsCorrectPath()
    {
        string text = "// LINK: ./relative/path/file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("./relative/path/file.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_ParentRelativePath_ReturnsCorrectPath()
    {
        string text = "// LINK: ../sibling/folder/file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("../sibling/folder/file.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_FilePathWithSpaces_ReturnsCorrectPath()
    {
        string text = "// LINK: images/Add group calendar.png";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("images/Add group calendar.png", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_FilePathWithSpacesAndLineNumber_ReturnsCorrectPath()
    {
        string text = "// LINK: path/My File.cs:45";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("path/My File.cs", results[0].FilePath);
        Assert.AreEqual(45, results[0].LineNumber);
    }

    [TestMethod]
    public void Parse_FilePathWithSpacesAndAnchor_ReturnsCorrectPath()
    {
        string text = "// LINK: docs/User Guide.md#getting-started";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("docs/User Guide.md", results[0].FilePath);
        Assert.AreEqual("getting-started", results[0].AnchorName);
    }

    [TestMethod]
    public void Parse_WithoutColon_ReturnsCorrectPath()
    {
        string text = "// LINK path/to/file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("path/to/file.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_SolutionRelativePath_ReturnsCorrectPath()
    {
        string text = "// LINK: /solution/root/file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("/solution/root/file.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_TildeRelativePath_ReturnsCorrectPath()
    {
        string text = "// LINK: ~/solution/root/file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("~/solution/root/file.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_ProjectRelativePath_ReturnsCorrectPath()
    {
        string text = "// LINK: @/project/root/file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("@/project/root/file.cs", results[0].FilePath);
    }

    #endregion

    #region Line Number Links

    [TestMethod]
    public void Parse_FileWithLineNumber_ReturnsCorrectLineNumber()
    {
        string text = "// LINK: Services/UserService.cs:45";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("Services/UserService.cs", results[0].FilePath);
        Assert.IsTrue(results[0].HasLineNumber);
        Assert.AreEqual(45, results[0].LineNumber);
        Assert.IsFalse(results[0].HasLineRange);
    }

    [TestMethod]
    public void Parse_FileWithLineRange_ReturnsCorrectRange()
    {
        string text = "// LINK: Database/Schema.sql:100-150";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("Database/Schema.sql", results[0].FilePath);
        Assert.IsTrue(results[0].HasLineRange);
        Assert.AreEqual(100, results[0].LineNumber);
        Assert.AreEqual(150, results[0].EndLineNumber);
    }

    #endregion

    #region Anchor Links

    [TestMethod]
    public void Parse_FileWithAnchor_ReturnsCorrectAnchor()
    {
        string text = "// LINK: Services/UserService.cs#validate-input";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("Services/UserService.cs", results[0].FilePath);
        Assert.IsTrue(results[0].HasAnchor);
        Assert.AreEqual("validate-input", results[0].AnchorName);
    }

    [TestMethod]
    public void Parse_LocalAnchor_ReturnsLocalAnchor()
    {
        string text = "// LINK: #local-anchor";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.IsTrue(results[0].IsLocalAnchor);
        Assert.AreEqual("local-anchor", results[0].AnchorName);
        Assert.IsNull(results[0].FilePath);
    }

    [TestMethod]
    public void Parse_FileWithLineAndAnchor_ReturnsBoth()
    {
        string text = "// LINK: ./file.cs:50#section-name";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("./file.cs", results[0].FilePath);
        Assert.AreEqual(50, results[0].LineNumber);
        Assert.AreEqual("section-name", results[0].AnchorName);
    }

    #endregion

    #region Case Insensitivity

    [TestMethod]
    public void Parse_LowercaseLink_ParsesCorrectly()
    {
        string text = "// link: path/to/file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("path/to/file.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_MixedCaseLink_ParsesCorrectly()
    {
        string text = "// Link: path/to/file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("path/to/file.cs", results[0].FilePath);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        string text = "";

        var results = LinkAnchorParser.Parse(text);

        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void Parse_NullString_ReturnsEmptyList()
    {
        string text = null;

        var results = LinkAnchorParser.Parse(text);

        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void Parse_NoLinkKeyword_ReturnsEmptyList()
    {
        string text = "// This is just a comment about a file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void Parse_MultipleLinks_ReturnsAll()
    {
        // Multiple LINKs on separate lines (most common case)
        string text = "// LINK: file1.cs\n// LINK: file2.cs:10";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(2, results);
        Assert.AreEqual("file1.cs", results[0].FilePath);
        Assert.AreEqual("file2.cs", results[1].FilePath);
        Assert.AreEqual(10, results[1].LineNumber);
    }

    #endregion

    #region ContainsLinkAnchor

    [TestMethod]
    public void ContainsLinkAnchor_WithLink_ReturnsTrue()
    {
        string text = "// LINK: path/to/file.cs";

        bool result = LinkAnchorParser.ContainsLinkAnchor(text);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ContainsLinkAnchor_WithoutLink_ReturnsFalse()
    {
        string text = "// This is just a comment";

        bool result = LinkAnchorParser.ContainsLinkAnchor(text);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ContainsLinkAnchor_EmptyString_ReturnsFalse()
    {
        string text = "";

        bool result = LinkAnchorParser.ContainsLinkAnchor(text);

        Assert.IsFalse(result);
    }

    #endregion

    #region GetLinkAtPosition


    [TestMethod]
    public void GetLinkAtPosition_AtLinkKeyword_ReturnsNull()
    {
        // Clicking on "LINK:" prefix should NOT return a link (only target is clickable)
        string text = "// LINK: file.cs";
        int position = 3; // At 'L' in LINK

        var result = LinkAnchorParser.GetLinkAtPosition(text, position);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetLinkAtPosition_AtFilePath_ReturnsLink()
    {
        string text = "// LINK: file.cs";
        int position = 10; // In the middle of "file.cs"

        var result = LinkAnchorParser.GetLinkAtPosition(text, position);

        Assert.IsNotNull(result);
        Assert.AreEqual("file.cs", result.FilePath);
    }

    [TestMethod]
    public void GetLinkAtPosition_OutsideLink_ReturnsNull()
    {
        string text = "// Some text LINK: file.cs";
        int position = 5; // In "Some text"

        var result = LinkAnchorParser.GetLinkAtPosition(text, position);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetLinkAtPosition_InvalidPosition_ReturnsNull()
    {
        string text = "// LINK: file.cs";

        var result = LinkAnchorParser.GetLinkAtPosition(text, -1);
        Assert.IsNull(result);

        result = LinkAnchorParser.GetLinkAtPosition(text, 100);
        Assert.IsNull(result);
    }

    #endregion

    #region Position and Length

    [TestMethod]
    public void Parse_SetsCorrectStartIndex()
    {
        string text = "// LINK: file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual(3, results[0].StartIndex); // "// " is 3 chars
    }

    [TestMethod]
    public void Parse_SetsCorrectLength()
    {
        string text = "// LINK: file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("LINK: file.cs".Length, results[0].Length);
    }

    [TestMethod]
    public void Parse_SetsCorrectTargetStartIndex()
    {
        string text = "// LINK: file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        // "// LINK: " is 9 chars, target starts after that
        Assert.AreEqual(9, results[0].TargetStartIndex);
    }

    [TestMethod]
    public void Parse_SetsCorrectTargetLength()
    {
        string text = "// LINK: file.cs";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        // Only "file.cs" is the target
        Assert.AreEqual("file.cs".Length, results[0].TargetLength);
    }

    [TestMethod]
    public void Parse_TargetIncludesLineNumberAndAnchor()
    {
        string text = "// LINK: file.cs:42#section";

        var results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        // Target includes "file.cs:42#section"
        Assert.AreEqual("file.cs:42#section".Length, results[0].TargetLength);
    }

    [TestMethod]
    public void GetLinkAtPosition_OnPrefix_ReturnsNull()
    {
        string text = "// LINK: file.cs";

        // Position 5 is within "LINK:" prefix
        var result = LinkAnchorParser.GetLinkAtPosition(text, 5);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetLinkAtPosition_OnTarget_ReturnsLink()
    {
        string text = "// LINK: file.cs";

        // Position 10 is within "file.cs" target
        var result = LinkAnchorParser.GetLinkAtPosition(text, 10);

        Assert.IsNotNull(result);
        Assert.AreEqual("file.cs", result.FilePath);
    }

    #endregion
}
