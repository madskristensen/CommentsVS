using CommentsVS.Services;
using CommentsVS.ToolWindows;

namespace CommentsVS.Test;

[TestClass]
public sealed class AnchorExporterTests
{
    private static AnchorItem CreateAnchor(
        AnchorType type = AnchorType.Todo,
        string message = "Fix this",
        string filePath = @"C:\src\Foo.cs",
        int line = 10,
        string project = "MyProject",
        string owner = null,
        string issue = null,
        string anchorId = null)
    {
        return new AnchorItem
        {
            AnchorType = type,
            Message = message,
            FilePath = filePath,
            LineNumber = line,
            Project = project,
            Owner = owner,
            IssueReference = issue,
            AnchorId = anchorId
        };
    }

    #region TSV

    [TestMethod]
    public void Export_Tsv_IncludesHeaderRow()
    {
        var result = AnchorExporter.Export([], AnchorExportFormat.Tsv);

        Assert.Contains("Type\tMessage\tFile\tPath\tLine\tProject\tOwner\tIssue\tAnchor ID", result);
    }

    [TestMethod]
    public void Export_Tsv_SingleAnchor_ProducesTabSeparatedRow()
    {
        AnchorItem anchor = CreateAnchor();

        var result = AnchorExporter.Export([anchor], AnchorExportFormat.Tsv);

        Assert.Contains("TODO\tFix this\tFoo.cs\tC:\\src\\Foo.cs\t10\tMyProject", result);
    }

    #endregion

    #region CSV

    [TestMethod]
    public void Export_Csv_IncludesHeaderRow()
    {
        var result = AnchorExporter.Export([], AnchorExportFormat.Csv);

        Assert.Contains("Type,Message,File,Path,Line,Project,Owner,Issue,Anchor ID", result);
    }

    [TestMethod]
    public void Export_Csv_FieldWithComma_IsQuoted()
    {
        AnchorItem anchor = CreateAnchor(message: "Fix this, and that");

        var result = AnchorExporter.Export([anchor], AnchorExportFormat.Csv);

        Assert.Contains("\"Fix this, and that\"", result);
    }

    [TestMethod]
    public void Export_Csv_FieldWithQuote_IsEscapedAndQuoted()
    {
        AnchorItem anchor = CreateAnchor(message: "Say \"hello\"");

        var result = AnchorExporter.Export([anchor], AnchorExportFormat.Csv);

        Assert.Contains("\"Say \"\"hello\"\"\"", result);
    }

    [TestMethod]
    public void Export_Csv_FieldWithNewline_IsQuoted()
    {
        AnchorItem anchor = CreateAnchor(message: "line1\nline2");

        var result = AnchorExporter.Export([anchor], AnchorExportFormat.Csv);

        Assert.Contains("\"line1\nline2\"", result);
    }

    [TestMethod]
    public void Export_Csv_FieldWithoutSpecialCharacters_IsNotQuoted()
    {
        AnchorItem anchor = CreateAnchor(message: "Plain message");

        var result = AnchorExporter.Export([anchor], AnchorExportFormat.Csv);

        Assert.Contains("Plain message", result);
        Assert.DoesNotContain("\"Plain message\"", result);
    }

    #endregion

    #region Markdown

    [TestMethod]
    public void Export_Markdown_IncludesTitleAndHeaderRow()
    {
        var result = AnchorExporter.Export([], AnchorExportFormat.Markdown);

        Assert.Contains("# Code Anchors", result);
        Assert.Contains("| Type | Message | File | Path | Line | Project | Owner | Issue | Anchor ID |", result);
    }

    [TestMethod]
    public void Export_Markdown_EscapesPipeCharactersInCells()
    {
        AnchorItem anchor = CreateAnchor(message: "a | b");

        var result = AnchorExporter.Export([anchor], AnchorExportFormat.Markdown);

        Assert.Contains("a \\| b", result);
    }

    [TestMethod]
    public void Export_Markdown_RemovesNewlinesFromCells()
    {
        AnchorItem anchor = CreateAnchor(message: "line1\nline2\rline3");

        var result = AnchorExporter.Export([anchor], AnchorExportFormat.Markdown);

        Assert.DoesNotContain("line1\nline2", result);
        Assert.Contains("line1 line2line3", result);
    }

    [TestMethod]
    public void Export_Markdown_SingleAnchor_UsesSingularSuffix()
    {
        AnchorItem anchor = CreateAnchor();

        var result = AnchorExporter.Export([anchor], AnchorExportFormat.Markdown);

        Assert.Contains("*Exported from Comment Studio — 1 anchor*", result);
    }

    [TestMethod]
    public void Export_Markdown_MultipleAnchors_UsesPluralSuffix()
    {
        List<AnchorItem> anchors = [CreateAnchor(), CreateAnchor()];

        var result = AnchorExporter.Export(anchors, AnchorExportFormat.Markdown);

        Assert.Contains("*Exported from Comment Studio — 2 anchors*", result);
    }

    #endregion

    #region JSON

    [TestMethod]
    public void Export_Json_IncludesCountAndAnchorsArray()
    {
        AnchorItem anchor = CreateAnchor(owner: "mads", issue: "#123", anchorId: "section-a");

        var result = AnchorExporter.Export([anchor], AnchorExportFormat.Json);

        Assert.Contains("\"count\": 1", result);
        Assert.Contains("\"type\": \"TODO\"", result);
        Assert.Contains("\"message\": \"Fix this\"", result);
        Assert.Contains("\"owner\": \"mads\"", result);
        Assert.Contains("\"issue\": \"#123\"", result);
        Assert.Contains("\"anchorId\": \"section-a\"", result);
    }

    [TestMethod]
    public void Export_Json_EscapesSpecialCharactersInStrings()
    {
        AnchorItem anchor = CreateAnchor(message: "Say \"hi\"\\ and\ttab\nnewline");

        var result = AnchorExporter.Export([anchor], AnchorExportFormat.Json);

        Assert.Contains("Say \\\"hi\\\"\\\\ and\\ttab\\nnewline", result);
    }

    [TestMethod]
    public void Export_Json_NullFields_SerializeAsJsonNull()
    {
        AnchorItem anchor = CreateAnchor(owner: null, issue: null, anchorId: null);

        var result = AnchorExporter.Export([anchor], AnchorExportFormat.Json);

        Assert.Contains("\"owner\": null", result);
        Assert.Contains("\"issue\": null", result);
        Assert.Contains("\"anchorId\": null", result);
    }

    [TestMethod]
    public void Export_Json_NoAnchors_EmptyArray()
    {
        var result = AnchorExporter.Export([], AnchorExportFormat.Json);

        Assert.Contains("\"count\": 0", result);
        Assert.Contains("\"anchors\": [", result);
    }

    #endregion

    #region Null input / format fallback

    [TestMethod]
    public void Export_NullAnchors_DoesNotThrowAndReturnsHeaderOnly()
    {
        var result = AnchorExporter.Export(null, AnchorExportFormat.Tsv);

        Assert.Contains("Type\tMessage\tFile\tPath\tLine\tProject\tOwner\tIssue\tAnchor ID", result);
    }

    [TestMethod]
    public void Export_UnknownFormat_FallsBackToTsv()
    {
        var result = AnchorExporter.Export([], (AnchorExportFormat)999);

        Assert.Contains("Type\tMessage\tFile\tPath\tLine\tProject\tOwner\tIssue\tAnchor ID", result);
    }

    #endregion

    #region File filters and extension mapping

    [TestMethod]
    [DataRow(AnchorExportFormat.Tsv, "*.tsv")]
    [DataRow(AnchorExportFormat.Csv, "*.csv")]
    [DataRow(AnchorExportFormat.Markdown, "*.md")]
    [DataRow(AnchorExportFormat.Json, "*.json")]
    public void GetFileFilter_KnownFormats_ContainsExpectedExtension(AnchorExportFormat format, string expectedExtension)
    {
        var result = AnchorExporter.GetFileFilter(format);

        Assert.Contains(expectedExtension, result);
    }

    [TestMethod]
    public void GetAllFormatsFileFilter_ContainsAllFormatExtensions()
    {
        var result = AnchorExporter.GetAllFormatsFileFilter();

        Assert.Contains("*.tsv", result);
        Assert.Contains("*.csv", result);
        Assert.Contains("*.md", result);
        Assert.Contains("*.json", result);
    }

    [TestMethod]
    [DataRow(".tsv", AnchorExportFormat.Tsv)]
    [DataRow(".CSV", AnchorExportFormat.Csv)]
    [DataRow(".md", AnchorExportFormat.Markdown)]
    [DataRow(".JSON", AnchorExportFormat.Json)]
    public void GetFormatFromExtension_KnownExtensions_ReturnsExpectedFormat(string extension, AnchorExportFormat expected)
    {
        AnchorExportFormat result = AnchorExporter.GetFormatFromExtension(extension);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void GetFormatFromExtension_UnknownExtension_FallsBackToTsv()
    {
        AnchorExportFormat result = AnchorExporter.GetFormatFromExtension(".xyz");

        Assert.AreEqual(AnchorExportFormat.Tsv, result);
    }

    [TestMethod]
    public void GetFormatFromExtension_NullExtension_FallsBackToTsv()
    {
        AnchorExportFormat result = AnchorExporter.GetFormatFromExtension(null);

        Assert.AreEqual(AnchorExportFormat.Tsv, result);
    }

    #endregion
}
