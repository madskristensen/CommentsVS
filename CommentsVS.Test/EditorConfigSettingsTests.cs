using System.Collections.Generic;
using System.Text.RegularExpressions;
using CommentsVS.Services;

namespace CommentsVS.Test;

/// <summary>
/// Tests for EditorConfigSettings's pure regex-building methods (internal, exposed via InternalsVisibleTo).
/// These exercise the real production algorithms directly, without touching .editorconfig or General.Instance.
/// </summary>
[TestClass]
public sealed class EditorConfigSettingsTests
{
    [TestMethod]
    [DataRow("false")]
    [DataRow("off")]
    [DataRow("no")]
    [DataRow("0")]
    public void ParseEnabledValue_DisabledValues_ReturnFalse(string value)
    {
        Assert.IsFalse(EditorConfigSettings.ParseEnabledValue(value));
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("on")]
    [DataRow("yes")]
    [DataRow("1")]
    [DataRow("")]
    [DataRow("invalid")]
    public void ParseEnabledValue_OtherValues_ReturnTrue(string value)
    {
        Assert.IsTrue(EditorConfigSettings.ParseEnabledValue(value));
    }

    [TestMethod]
    public void IsEnabled_FileExtensionSection_ExcludesMatchingFilesOnly()
    {
        string directory = CreateEditorConfigTestDirectory(
            """
            root = true

            [*.ps1]
            commentsvs_enabled = false
            """);

        try
        {
            string powerShellFile = Path.Combine(directory, "build.ps1");
            string csharpFile = Path.Combine(directory, "Program.cs");
            File.WriteAllText(powerShellFile, "# test");
            File.WriteAllText(csharpFile, "// test");
            EditorConfigSettings.ClearCaches();

            Assert.IsFalse(EditorConfigSettings.IsEnabled(powerShellFile));
            Assert.IsTrue(EditorConfigSettings.IsEnabled(csharpFile));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            EditorConfigSettings.ClearCaches();
        }
    }

    [TestMethod]
    public void IsEnabled_ProjectRootSection_ExcludesNestedFiles()
    {
        string directory = CreateEditorConfigTestDirectory(
            """
            root = true

            [*]
            commentsvs_enabled = false
            """);

        try
        {
            string nestedDirectory = Path.Combine(directory, "Features");
            Directory.CreateDirectory(nestedDirectory);
            string filePath = Path.Combine(nestedDirectory, "Feature.cs");
            File.WriteAllText(filePath, "// test");
            EditorConfigSettings.ClearCaches();

            Assert.IsFalse(EditorConfigSettings.IsEnabled(filePath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            EditorConfigSettings.ClearCaches();
        }
    }

    private static string CreateEditorConfigTestDirectory(string editorConfig)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CommentsVS.Test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ".editorconfig"), editorConfig);
        return directory;
    }

    [TestMethod]
    public void BuildAnchorKeywordsPattern_EmptyCustomTags_ReturnsBuiltInPattern()
    {
        var result = EditorConfigSettings.BuildAnchorKeywordsPattern([]);

        Assert.AreEqual(EditorConfigSettings.BuiltInAnchorPattern, result);
    }

    [TestMethod]
    public void BuildAnchorKeywordsPattern_WithCustomTags_AppendsEscapedTags()
    {
        var result = EditorConfigSettings.BuildAnchorKeywordsPattern(new HashSet<string> { "PERF" });

        Assert.AreEqual(EditorConfigSettings.BuiltInAnchorPattern + "|PERF", result);
    }

    [TestMethod]
    public void BuildAnchorKeywordsPattern_TagWithRegexSpecialChars_IsEscaped()
    {
        var result = EditorConfigSettings.BuildAnchorKeywordsPattern(new HashSet<string> { "C++" });

        Assert.Contains(@"C\+\+", result);
    }

    [TestMethod]
    public void BuildPrefixPattern_Null_ReturnsNull()
    {
        Assert.IsNull(EditorConfigSettings.BuildPrefixPattern(null));
    }

    [TestMethod]
    public void BuildPrefixPattern_EmptyString_ReturnsNull()
    {
        Assert.IsNull(EditorConfigSettings.BuildPrefixPattern(""));
    }

    [TestMethod]
    public void BuildPrefixPattern_Whitespace_ReturnsNull()
    {
        Assert.IsNull(EditorConfigSettings.BuildPrefixPattern("   "));
    }

    [TestMethod]
    public void BuildPrefixPattern_SingleChar_ReturnsCharacterClass()
    {
        var result = EditorConfigSettings.BuildPrefixPattern("@");

        Assert.AreEqual("[@]", result);
    }

    [TestMethod]
    public void BuildPrefixPattern_MultipleChars_ReturnsCharacterClassWithAll()
    {
        var result = EditorConfigSettings.BuildPrefixPattern("@,$");

        Assert.IsTrue(result.Contains('@'));
        Assert.IsTrue(result.Contains('$'));
    }

    [TestMethod]
    public void BuildPrefixPattern_MultiCharacterEntries_AreIgnored()
    {
        // Only single-character prefixes are valid; multi-char entries are dropped
        var result = EditorConfigSettings.BuildPrefixPattern("ab,@");

        Assert.AreEqual("[@]", result);
    }

    [TestMethod]
    public void BuildPrefixPattern_OnlyMultiCharacterEntries_ReturnsNull()
    {
        Assert.IsNull(EditorConfigSettings.BuildPrefixPattern("ab,cd"));
    }

    [TestMethod]
    public void BuildPrefixFragment_NullPattern_ReturnsEmptyString()
    {
        Assert.AreEqual("", EditorConfigSettings.BuildPrefixFragment(null));
    }

    [TestMethod]
    public void BuildPrefixFragment_NonNullPattern_WrapsInOptionalGroup()
    {
        var result = EditorConfigSettings.BuildPrefixFragment("[@]");

        Assert.AreEqual(@"(?<tagprefix>[@])?\s*", result);
    }

    [TestMethod]
    public void BuildAnchorClassificationRegex_NoPrefix_MatchesTagAtCommentStart()
    {
        Regex regex = EditorConfigSettings.BuildAnchorClassificationRegex(EditorConfigSettings.BuiltInAnchorPattern, null);

        Match match = regex.Match("// TODO: fix this");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("TODO", match.Groups["tag"].Value);
    }

    [TestMethod]
    public void BuildAnchorClassificationRegex_WithPrefix_MatchesTagWithPrefixChar()
    {
        Regex regex = EditorConfigSettings.BuildAnchorClassificationRegex(EditorConfigSettings.BuiltInAnchorPattern, "[@]");

        Match match = regex.Match("// @TODO: fix this");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("@", match.Groups["tagprefix"].Value);
        Assert.AreEqual("TODO", match.Groups["tag"].Value);
    }

    [TestMethod]
    public void BuildAnchorClassificationRegex_WithPrefix_StillMatchesWithoutPrefixChar()
    {
        Regex regex = EditorConfigSettings.BuildAnchorClassificationRegex(EditorConfigSettings.BuiltInAnchorPattern, "[@]");

        Match match = regex.Match("// TODO: fix this");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("TODO", match.Groups["tag"].Value);
    }

    [TestMethod]
    public void BuildAnchorWithMetadataRegex_MatchesParenMetadata()
    {
        Regex regex = EditorConfigSettings.BuildAnchorWithMetadataRegex(EditorConfigSettings.BuiltInAnchorPattern);

        Match match = regex.Match("TODO(@mads): fix");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("(@mads)", match.Groups["metadata"].Value);
    }

    [TestMethod]
    public void BuildAnchorServiceRegex_NoPrefix_CapturesPrefixTagAndMessage()
    {
        Regex regex = EditorConfigSettings.BuildAnchorServiceRegex(EditorConfigSettings.BuiltInAnchorPattern, null);

        Match match = regex.Match("// BUG: this crashes");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("//", match.Groups["prefix"].Value);
        Assert.AreEqual("BUG", match.Groups["tag"].Value);
        Assert.AreEqual("this crashes", match.Groups["message"].Value.Trim());
    }

    [TestMethod]
    public void BuildAnchorServiceRegex_WithPrefix_MatchesTagWithPrefixChar()
    {
        Regex regex = EditorConfigSettings.BuildAnchorServiceRegex(EditorConfigSettings.BuiltInAnchorPattern, "[@]");

        Match match = regex.Match("// @TODO: fix this");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("@", match.Groups["tagprefix"].Value);
        Assert.AreEqual("TODO", match.Groups["tag"].Value);
    }

    [TestMethod]
    public void BuildAnchorServiceRegex_WithMetadata_CapturesMetadataGroup()
    {
        Regex regex = EditorConfigSettings.BuildAnchorServiceRegex(EditorConfigSettings.BuiltInAnchorPattern, null);

        Match match = regex.Match("// TODO(@mads): review this");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("(@mads)", match.Groups["metadata"].Value);
    }
}
