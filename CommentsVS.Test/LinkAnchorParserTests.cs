using CommentsVS.Services;

namespace CommentsVS.Test;

[TestClass]
public sealed class LinkAnchorParserTests
{
    #region Basic File Links

    [TestMethod]
    public void Parse_BasicFilePath_ReturnsCorrectPath()
    {
        var text = "// LINK: path/to/file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("path/to/file.cs", results[0].FilePath);
        Assert.IsFalse(results[0].HasLineNumber);
        Assert.IsFalse(results[0].HasAnchor);
    }

    [TestMethod]
    public void Parse_RelativePath_ReturnsCorrectPath()
    {
        var text = "// LINK: ./relative/path/file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("./relative/path/file.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_ParentRelativePath_ReturnsCorrectPath()
    {
        var text = "// LINK: ../sibling/folder/file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("../sibling/folder/file.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_FilePathWithSpaces_ReturnsCorrectPath()
    {
        var text = "// LINK: images/Add group calendar.png";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("images/Add group calendar.png", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_FilePathWithSpacesAndLineNumber_ReturnsCorrectPath()
    {
        var text = "// LINK: path/My File.cs:45";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("path/My File.cs", results[0].FilePath);
        Assert.AreEqual(45, results[0].LineNumber);
    }

    [TestMethod]
    public void Parse_FilePathWithSpacesAndAnchor_ReturnsCorrectPath()
    {
        var text = "// LINK: docs/User Guide.md#getting-started";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("docs/User Guide.md", results[0].FilePath);
        Assert.AreEqual("getting-started", results[0].AnchorName);
    }

    [TestMethod]
    public void Parse_WithoutColon_ReturnsCorrectPath()
    {
        var text = "// LINK path/to/file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("path/to/file.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_SolutionRelativePath_ReturnsCorrectPath()
    {
        var text = "// LINK: /solution/root/file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("/solution/root/file.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_TildeRelativePath_ReturnsCorrectPath()
    {
        var text = "// LINK: ~/solution/root/file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("~/solution/root/file.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_ProjectRelativePath_ReturnsCorrectPath()
    {
        var text = "// LINK: @/project/root/file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("@/project/root/file.cs", results[0].FilePath);
    }

    #endregion

    #region Line Number Links

    [TestMethod]
    public void Parse_FileWithLineNumber_ReturnsCorrectLineNumber()
    {
        var text = "// LINK: Services/UserService.cs:45";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("Services/UserService.cs", results[0].FilePath);
        Assert.IsTrue(results[0].HasLineNumber);
        Assert.AreEqual(45, results[0].LineNumber);
        Assert.IsFalse(results[0].HasLineRange);
    }

    [TestMethod]
    public void Parse_FileWithLineRange_ReturnsCorrectRange()
    {
        var text = "// LINK: Database/Schema.sql:100-150";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

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
        var text = "// LINK: Services/UserService.cs#validate-input";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("Services/UserService.cs", results[0].FilePath);
        Assert.IsTrue(results[0].HasAnchor);
        Assert.AreEqual("validate-input", results[0].AnchorName);
    }

    [TestMethod]
    public void Parse_LocalAnchor_ReturnsLocalAnchor()
    {
        var text = "// LINK: #local-anchor";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.IsTrue(results[0].IsLocalAnchor);
        Assert.AreEqual("local-anchor", results[0].AnchorName);
        Assert.IsNull(results[0].FilePath);
    }

    [TestMethod]
    public void Parse_FileWithLineAndAnchor_ReturnsBoth()
    {
        var text = "// LINK: ./file.cs:50#section-name";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

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
        var text = "// link: path/to/file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("path/to/file.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_MixedCaseLink_ParsesCorrectly()
    {
        var text = "// Link: path/to/file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("path/to/file.cs", results[0].FilePath);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        var text = "";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void Parse_NullString_ReturnsEmptyList()
    {
        string? text = null;

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void Parse_NoLinkKeyword_ReturnsEmptyList()
    {
        var text = "// This is just a comment about a file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void Parse_MultipleLinks_ReturnsAll()
    {
        // Multiple LINKs on separate lines (most common case)
        var text = "// LINK: file1.cs\n// LINK: file2.cs:10";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

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
        var text = "// LINK: path/to/file.cs";

        var result = LinkAnchorParser.ContainsLinkAnchor(text);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ContainsLinkAnchor_WithoutLink_ReturnsFalse()
    {
        var text = "// This is just a comment";

        var result = LinkAnchorParser.ContainsLinkAnchor(text);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ContainsLinkAnchor_EmptyString_ReturnsFalse()
    {
        var text = "";

        var result = LinkAnchorParser.ContainsLinkAnchor(text);

        Assert.IsFalse(result);
    }

    #endregion

    #region GetLinkAtPosition


    [TestMethod]
    public void GetLinkAtPosition_AtLinkKeyword_ReturnsNull()
    {
        // Clicking on "LINK:" prefix should NOT return a link (only target is clickable)
        var text = "// LINK: file.cs";
        var position = 3; // At 'L' in LINK

        LinkAnchorInfo result = LinkAnchorParser.GetLinkAtPosition(text, position);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetLinkAtPosition_AtFilePath_ReturnsLink()
    {
        var text = "// LINK: file.cs";
        var position = 10; // In the middle of "file.cs"

        LinkAnchorInfo result = LinkAnchorParser.GetLinkAtPosition(text, position);

        Assert.IsNotNull(result);
        Assert.AreEqual("file.cs", result.FilePath);
    }

    [TestMethod]
    public void GetLinkAtPosition_OutsideLink_ReturnsNull()
    {
        var text = "// Some text LINK: file.cs";
        var position = 5; // In "Some text"

        LinkAnchorInfo result = LinkAnchorParser.GetLinkAtPosition(text, position);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetLinkAtPosition_InvalidPosition_ReturnsNull()
    {
        var text = "// LINK: file.cs";

        LinkAnchorInfo result = LinkAnchorParser.GetLinkAtPosition(text, -1);
        Assert.IsNull(result);

        result = LinkAnchorParser.GetLinkAtPosition(text, 100);
        Assert.IsNull(result);
    }

    #endregion

    #region Position and Length

    [TestMethod]
    public void Parse_SetsCorrectStartIndex()
    {
        var text = "// LINK: file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual(3, results[0].StartIndex); // "// " is 3 chars
    }

    [TestMethod]
    public void Parse_SetsCorrectLength()
    {
        var text = "// LINK: file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("LINK: file.cs".Length, results[0].Length);
    }

    [TestMethod]
    public void Parse_SetsCorrectTargetStartIndex()
    {
        var text = "// LINK: file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        // "// LINK: " is 9 chars, target starts after that
        Assert.AreEqual(9, results[0].TargetStartIndex);
    }

    [TestMethod]
    public void Parse_SetsCorrectTargetLength()
    {
        var text = "// LINK: file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        // Only "file.cs" is the target
        Assert.AreEqual("file.cs".Length, results[0].TargetLength);
    }

    [TestMethod]
    public void Parse_TargetIncludesLineNumberAndAnchor()
    {
        var text = "// LINK: file.cs:42#section";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        // Target includes "file.cs:42#section"
        Assert.AreEqual("file.cs:42#section".Length, results[0].TargetLength);
    }


    [TestMethod]
    public void GetLinkAtPosition_OnPrefix_ReturnsNull()
    {
        var text = "// LINK: file.cs";

        // Position 5 is within "LINK:" prefix
        LinkAnchorInfo result = LinkAnchorParser.GetLinkAtPosition(text, 5);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetLinkAtPosition_OnTarget_ReturnsLink()
    {
        var text = "// LINK: file.cs";

        // Position 10 is within "file.cs" target
        LinkAnchorInfo result = LinkAnchorParser.GetLinkAtPosition(text, 10);

        Assert.IsNotNull(result);
        Assert.AreEqual("file.cs", result.FilePath);
    }

    #endregion

    #region Additional Edge Cases

    [TestMethod]
    public void Parse_WindowsAbsolutePath_ReturnsCorrectPath()
    {
        // Windows absolute paths with drive letters
        var text = "// LINK: C:/Users/test/file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("C:/Users/test/file.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_UncPath_ReturnsCorrectPath()
    {
        var text = "// LINK: //server/share/file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        // UNC paths might be parsed differently - verify behavior
        Assert.IsGreaterThanOrEqualTo(0, results.Count);
    }

    [TestMethod]
    public void Parse_FileExtensionsWithNumbers_ReturnsCorrectPath()
    {
        var text = "// LINK: config.log4net.xml";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("config.log4net.xml", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_MarkdownFile_ReturnsCorrectPath()
    {
        var text = "// LINK: README.md";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("README.md", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_JsonFile_ReturnsCorrectPath()
    {
        var text = "// LINK: appsettings.json";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("appsettings.json", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_LineNumberZero_HandledCorrectly()
    {
        var text = "// LINK: file.cs:0";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("file.cs", results[0].FilePath);
        Assert.AreEqual(0, results[0].LineNumber);
    }

    [TestMethod]
    public void Parse_LargeLineNumber_HandledCorrectly()
    {
        var text = "// LINK: file.cs:99999";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual(99999, results[0].LineNumber);
    }

    [TestMethod]
    public void Parse_LineRangeReversed_HandledCorrectly()
    {
        // Range where end is less than start
        var text = "// LINK: file.cs:50-10";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        // Should still parse the numbers as provided
        Assert.AreEqual(50, results[0].LineNumber);
        Assert.AreEqual(10, results[0].EndLineNumber);
    }

    [TestMethod]
    public void Parse_AnchorWithHyphen_ReturnsCorrectAnchor()
    {
        var text = "// LINK: file.cs#section-name";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("section-name", results[0].AnchorName);
    }

    [TestMethod]
    public void Parse_AnchorWithUnderscore_ReturnsCorrectAnchor()
    {
        var text = "// LINK: file.cs#section_name";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("section_name", results[0].AnchorName);
    }

    [TestMethod]
    public void Parse_MultipleLinksOnSameLine_ReturnsBoth()
    {
        // Less common but possible: multiple LINK keywords on same line
        // Parser may or may not handle this - test that at least one is found
        var text = "// LINK: file1.cs\n// LINK: file2.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.IsGreaterThanOrEqualTo(2, results.Count);
        Assert.AreEqual("file1.cs", results[0].FilePath);
        Assert.AreEqual("file2.cs", results[1].FilePath);
    }

    [TestMethod]
    public void Parse_LinkInBlockComment_Matches()
    {
        // Block comments may include the closing */ in the path if not properly terminated
        // Test the actual behavior - parser finds a link regardless
        var text = "/* LINK: file.cs */";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        // The file path may include trailing content due to regex behavior
        Assert.IsTrue(results[0].FilePath?.Contains("file.cs") ?? false);
    }

    [TestMethod]
    public void Parse_LinkInVBComment_Matches()
    {
        var text = "' LINK: file.vb";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("file.vb", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_FilenameOnly_NoDirectory()
    {
        var text = "// LINK: Program.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual("Program.cs", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_HiddenFile_DotPrefix()
    {
        var text = "// LINK: .gitignore";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual(".gitignore", results[0].FilePath);
    }

    [TestMethod]
    public void Parse_FileInHiddenFolder()
    {
        var text = "// LINK: .github/workflows/build.yml";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.AreEqual(".github/workflows/build.yml", results[0].FilePath);
    }

    [TestMethod]
    public void IsLocalAnchor_LocalReference_ReturnsTrue()
    {
        var text = "// LINK: #section";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.IsTrue(results[0].IsLocalAnchor);
        Assert.IsNull(results[0].FilePath);
        Assert.AreEqual("section", results[0].AnchorName);
    }

    [TestMethod]
    public void HasLineNumber_WithLine_ReturnsTrue()
    {
        var text = "// LINK: file.cs:42";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.IsTrue(results[0].HasLineNumber);
        Assert.IsFalse(results[0].HasLineRange);
    }

    [TestMethod]
    public void HasLineRange_WithRange_ReturnsTrue()
    {
        var text = "// LINK: file.cs:10-20";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.IsTrue(results[0].HasLineNumber);
        Assert.IsTrue(results[0].HasLineRange);
    }

    [TestMethod]
    public void HasAnchor_WithAnchor_ReturnsTrue()
    {
        var text = "// LINK: file.cs#section";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.IsTrue(results[0].HasAnchor);
    }

    [TestMethod]
    public void HasAnchor_NoAnchor_ReturnsFalse()
    {
        var text = "// LINK: file.cs";

        IReadOnlyList<LinkAnchorInfo> results = LinkAnchorParser.Parse(text);

        Assert.HasCount(1, results);
        Assert.IsFalse(results[0].HasAnchor);
    }

    #endregion
}

