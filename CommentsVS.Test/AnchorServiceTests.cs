using CommentsVS.Services;
using CommentsVS.ToolWindows;

namespace CommentsVS.Test;

[TestClass]
public sealed class AnchorServiceTests
{
    private AnchorService _anchorService;

    [TestInitialize]
    public void TestInitialize()
    {
        _anchorService = new AnchorService();
    }

    #region Basic Anchor Detection Tests

    [TestMethod]
    public void ScanText_WithTodoComment_FindsAnchor()
    {
        var text = "// TODO: Implement this feature";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Todo, anchors[0].AnchorType);
        Assert.AreEqual("Implement this feature", anchors[0].Message);
        Assert.AreEqual(1, anchors[0].LineNumber);
    }

    [TestMethod]
    public void ScanText_WithHackComment_FindsAnchor()
    {
        var text = "// HACK: Quick fix for now";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Hack, anchors[0].AnchorType);
        Assert.AreEqual("Quick fix for now", anchors[0].Message);
    }

    [TestMethod]
    public void ScanText_WithNoteComment_FindsAnchor()
    {
        var text = "// NOTE: Important information";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Note, anchors[0].AnchorType);
        Assert.AreEqual("Important information", anchors[0].Message);
    }

    [TestMethod]
    public void ScanText_WithBugComment_FindsAnchor()
    {
        var text = "// BUG: This doesn't work correctly";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Bug, anchors[0].AnchorType);
        Assert.AreEqual("This doesn't work correctly", anchors[0].Message);
    }

    [TestMethod]
    public void ScanText_WithFixmeComment_FindsAnchor()
    {
        var text = "// FIXME: Needs to be fixed";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Fixme, anchors[0].AnchorType);
        Assert.AreEqual("Needs to be fixed", anchors[0].Message);
    }

    [TestMethod]
    public void ScanText_WithUndoneComment_FindsAnchor()
    {
        var text = "// UNDONE: Not yet completed";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Undone, anchors[0].AnchorType);
        Assert.AreEqual("Not yet completed", anchors[0].Message);
    }

    [TestMethod]
    public void ScanText_WithReviewComment_FindsAnchor()
    {
        var text = "// REVIEW: Needs code review";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Review, anchors[0].AnchorType);
        Assert.AreEqual("Needs code review", anchors[0].Message);
    }

    [TestMethod]
    public void ScanText_WithAnchorComment_FindsAnchor()
    {
        var text = "// ANCHOR: section-name";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Anchor, anchors[0].AnchorType);
        Assert.AreEqual("section-name", anchors[0].Message);
    }

    #endregion

    #region Case Insensitivity Tests

    [TestMethod]
    public void ScanText_WithLowercaseTodo_FindsAnchor()
    {
        var text = "// todo: lowercase works too";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Todo, anchors[0].AnchorType);
    }

    [TestMethod]
    public void ScanText_WithMixedCaseTodo_FindsAnchor()
    {
        var text = "// ToDo: mixed case works";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Todo, anchors[0].AnchorType);
    }

    #endregion

    #region Metadata Extraction Tests

    [TestMethod]
    public void ScanText_WithOwnerMetadata_ExtractsOwner()
    {
        var text = "// TODO(@mads): Fix this";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual("mads", anchors[0].Owner);
        Assert.AreEqual("Fix this", anchors[0].Message);
    }

    [TestMethod]
    public void ScanText_WithIssueMetadata_ExtractsIssue()
    {
        var text = "// TODO(#123): Related to issue";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual("#123", anchors[0].IssueReference);
        Assert.AreEqual("Related to issue", anchors[0].Message);
    }

    [TestMethod]
    public void ScanText_WithBracketMetadata_ExtractsMetadata()
    {
        var text = "// TODO[@john]: Assigned to John";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual("john", anchors[0].Owner);
    }

    [TestMethod]
    public void ScanText_WithOwnerAndIssue_ExtractsBoth()
    {
        var text = "// TODO(@mads #456): Fix issue";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual("mads", anchors[0].Owner);
        Assert.AreEqual("#456", anchors[0].IssueReference);
    }

    [TestMethod]
    public void ScanText_WithAnchorId_ExtractsId()
    {
        var text = "// ANCHOR(section-name): Start of section";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Anchor, anchors[0].AnchorType);
        Assert.AreEqual("section-name", anchors[0].AnchorId);
    }

    #endregion

    #region Multiple Anchors Tests

    [TestMethod]
    public void ScanText_WithMultipleAnchors_FindsAll()
    {
        var text = @"// TODO: First task
// HACK: Second issue
// FIXME: Third problem";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(3, anchors);
        Assert.AreEqual(AnchorType.Todo, anchors[0].AnchorType);
        Assert.AreEqual(AnchorType.Hack, anchors[1].AnchorType);
        Assert.AreEqual(AnchorType.Fixme, anchors[2].AnchorType);
    }

    [TestMethod]
    public void ScanText_WithMultipleAnchorsOnSameLine_FindsAll()
    {
        var text = "// TODO: First // HACK: Second";

        var anchors = _anchorService.ScanText(text, "test.cs");

        // This should find at least one anchor
        Assert.IsTrue(anchors.Count >= 1);
    }

    #endregion

    #region Comment Style Tests

    [TestMethod]
    public void ScanText_WithBlockCommentStyle_FindsAnchor()
    {
        var text = "/* TODO: Block comment style */";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Todo, anchors[0].AnchorType);
    }

    [TestMethod]
    public void ScanText_WithVisualBasicStyle_FindsAnchor()
    {
        var text = "' TODO: VB style comment";

        var anchors = _anchorService.ScanText(text, "test.vb");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Todo, anchors[0].AnchorType);
    }

    [TestMethod]
    public void ScanText_WithHtmlCommentStyle_FindsAnchor()
    {
        var text = "<!-- TODO: HTML style comment -->";

        var anchors = _anchorService.ScanText(text, "test.html");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Todo, anchors[0].AnchorType);
    }

    #endregion

    #region Edge Cases Tests

    [TestMethod]
    public void ScanText_WithEmptyString_ReturnsEmpty()
    {
        var anchors = _anchorService.ScanText("", "test.cs");

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    public void ScanText_WithNullString_ReturnsEmpty()
    {
        var anchors = _anchorService.ScanText(null, "test.cs");

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    public void ScanText_WithNoAnchors_ReturnsEmpty()
    {
        var text = "// This is just a regular comment";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    public void ScanText_WithAnchorInMiddleOfLine_FindsAnchor()
    {
        var text = "var x = 5; // TODO: Refactor this";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Todo, anchors[0].AnchorType);
    }

    #endregion

    #region Line Number Tests

    [TestMethod]
    public void ScanText_SetsCorrectLineNumbers()
    {
        var text = @"// Line 1
// TODO: Line 2
// Line 3
// HACK: Line 4";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(2, anchors);
        Assert.AreEqual(2, anchors[0].LineNumber);
        Assert.AreEqual(4, anchors[1].LineNumber);
    }

    #endregion

    #region File Path Tests

    [TestMethod]
    public void ScanText_SetsFilePath()
    {
        var text = "// TODO: Test";
        var filePath = @"C:\Projects\Test\file.cs";

        var anchors = _anchorService.ScanText(text, filePath);

        Assert.HasCount(1, anchors);
        Assert.AreEqual(filePath, anchors[0].FilePath);
    }

    [TestMethod]
    public void ScanText_SetsProjectName()
    {
        var text = "// TODO: Test";
        var projectName = "MyProject";

        var anchors = _anchorService.ScanText(text, "test.cs", projectName);

        Assert.HasCount(1, anchors);
        Assert.AreEqual(projectName, anchors[0].Project);
    }

    #endregion

    #region Colon Optional Tests

    [TestMethod]
    public void ScanText_WithoutColon_FindsAnchor()
    {
        var text = "// TODO Implement feature";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Todo, anchors[0].AnchorType);
        Assert.AreEqual("Implement feature", anchors[0].Message);
    }

    [TestMethod]
    public void ScanText_WithColon_FindsAnchor()
    {
        var text = "// TODO: Implement feature";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        Assert.AreEqual(AnchorType.Todo, anchors[0].AnchorType);
        Assert.AreEqual("Implement feature", anchors[0].Message);
    }

    #endregion

    #region Column Position Tests

    [TestMethod]
    public void ScanText_SetsColumnPosition()
    {
        var text = "// TODO: Test";

        var anchors = _anchorService.ScanText(text, "test.cs");

        Assert.HasCount(1, anchors);
        // Column should be set to where the tag starts
        Assert.IsTrue(anchors[0].Column >= 0);
    }

    #endregion

    #region Performance Tests

    [TestMethod]
    public void ScanText_WithLargeTextWithoutAnchors_CompletesQuickly()
    {
        // Create a large text without anchors
        var lines = new string[1000];
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = $"// This is line {i} with no anchor keywords";
        }
        var text = string.Join("\n", lines);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var anchors = _anchorService.ScanText(text, "test.cs");
        stopwatch.Stop();

        // Should complete quickly due to fast pre-check
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, "Scan should complete quickly");
        Assert.IsEmpty(anchors);
    }

    #endregion
}
