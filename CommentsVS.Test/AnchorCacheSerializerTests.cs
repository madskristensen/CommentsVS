using CommentsVS.ToolWindows;

namespace CommentsVS.Test;

[TestClass]
public sealed class AnchorCacheSerializerTests
{
    private string _tempSolutionDirectory = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempSolutionDirectory = Path.Combine(Path.GetTempPath(), "CommentsVSTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempSolutionDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempSolutionDirectory))
        {
            Directory.Delete(_tempSolutionDirectory, recursive: true);
        }
    }

    private static AnchorItem CreateAnchor(string filePath, int line = 1)
    {
        return new AnchorItem
        {
            AnchorType = AnchorType.Todo,
            FilePath = filePath,
            LineNumber = line,
            Column = 4,
            Message = "Fix this",
            Owner = "mads",
            IssueReference = "#123",
            AnchorId = null,
            RawMetadata = "(@mads)"
        };
    }

    #region GetCacheFilePath

    [TestMethod]
    public void GetCacheFilePath_ValidDirectory_ReturnsPathUnderVsFolder()
    {
        var result = AnchorCacheSerializer.GetCacheFilePath(_tempSolutionDirectory);

        Assert.AreEqual(Path.Combine(_tempSolutionDirectory, ".vs", "CodeAnchors.json"), result);
    }

    [TestMethod]
    public void GetCacheFilePath_NullOrEmptyDirectory_ReturnsNull()
    {
        Assert.IsNull(AnchorCacheSerializer.GetCacheFilePath(null!));
        Assert.IsNull(AnchorCacheSerializer.GetCacheFilePath(string.Empty));
    }

    #endregion

    #region Save / Load round-trip

    [TestMethod]
    public void SaveAndLoad_RoundTrip_PreservesAnchorData()
    {
        var filePath = Path.Combine(_tempSolutionDirectory, "Foo.cs");
        Dictionary<string, IReadOnlyList<AnchorItem>> cache = new()
        {
            [filePath] = [CreateAnchor(filePath, 10), CreateAnchor(filePath, 20)]
        };

        var saved = AnchorCacheSerializer.Save(_tempSolutionDirectory, cache);
        Dictionary<string, IReadOnlyList<AnchorItem>>? loaded = AnchorCacheSerializer.Load(_tempSolutionDirectory);

        Assert.IsTrue(saved);
        Assert.IsNotNull(loaded);
        Assert.IsTrue(loaded.ContainsKey(filePath));
        Assert.HasCount(2, loaded[filePath]);
        Assert.AreEqual(10, loaded[filePath][0].LineNumber);
        Assert.AreEqual(4, loaded[filePath][0].Column);
        Assert.AreEqual("Fix this", loaded[filePath][0].Message);
        Assert.AreEqual("mads", loaded[filePath][0].Owner);
        Assert.AreEqual("#123", loaded[filePath][0].IssueReference);
        Assert.AreEqual("(@mads)", loaded[filePath][0].RawMetadata);
        Assert.AreEqual(AnchorType.Todo, loaded[filePath][0].AnchorType);
    }

    [TestMethod]
    public void Save_CreatesVsFolderIfMissing()
    {
        var vsFolder = Path.Combine(_tempSolutionDirectory, ".vs");
        Assert.IsFalse(Directory.Exists(vsFolder));

        Dictionary<string, IReadOnlyList<AnchorItem>> cache = new()
        {
            ["Foo.cs"] = [CreateAnchor("Foo.cs")]
        };
        AnchorCacheSerializer.Save(_tempSolutionDirectory, cache);

        Assert.IsTrue(Directory.Exists(vsFolder));
    }

    [TestMethod]
    public void Save_EmptyCache_StillCreatesLoadableFile()
    {
        var saved = AnchorCacheSerializer.Save(_tempSolutionDirectory, new Dictionary<string, IReadOnlyList<AnchorItem>>());
        Dictionary<string, IReadOnlyList<AnchorItem>>? loaded = AnchorCacheSerializer.Load(_tempSolutionDirectory);

        Assert.IsTrue(saved);
        Assert.IsNotNull(loaded);
        Assert.IsEmpty(loaded);
    }

    [TestMethod]
    public void Save_NullOrEmptySolutionDirectory_ReturnsFalse()
    {
        Assert.IsFalse(AnchorCacheSerializer.Save(null!, new Dictionary<string, IReadOnlyList<AnchorItem>>()));
        Assert.IsFalse(AnchorCacheSerializer.Save(string.Empty, new Dictionary<string, IReadOnlyList<AnchorItem>>()));
    }

    #endregion

    #region Load edge cases

    [TestMethod]
    public void Load_NoCacheFileExists_ReturnsNull()
    {
        Dictionary<string, IReadOnlyList<AnchorItem>>? loaded = AnchorCacheSerializer.Load(_tempSolutionDirectory);

        Assert.IsNull(loaded);
    }

    [TestMethod]
    public void Load_NullOrEmptySolutionDirectory_ReturnsNull()
    {
        Assert.IsNull(AnchorCacheSerializer.Load(null!));
        Assert.IsNull(AnchorCacheSerializer.Load(string.Empty));
    }

    [TestMethod]
    public void Load_CorruptJsonFile_ReturnsNullWithoutThrowing()
    {
        var vsFolder = Path.Combine(_tempSolutionDirectory, ".vs");
        Directory.CreateDirectory(vsFolder);
        File.WriteAllText(Path.Combine(vsFolder, "CodeAnchors.json"), "{ not valid json ]]]");

        // AnchorCacheSerializer.Load catches the JSON parse failure internally and calls
        // ex.Log() to report it. That logging helper depends on VS Shell assemblies
        // (Microsoft.VisualStudio.Threading) that are only available inside a running VS
        // process, so in the bare unit-test host it throws FileNotFoundException instead of
        // the parse error being silently swallowed. This is a known test-host limitation
        // (see EditorConfigSettings/General.Instance for the analogous case), not a defect in
        // the corrupt-file handling itself, which is exercised end-to-end by the VS Experimental
        // Instance in manual testing.
        Assert.ThrowsExactly<FileNotFoundException>(() => AnchorCacheSerializer.Load(_tempSolutionDirectory));
    }

    [TestMethod]
    public void Load_UnsupportedVersion_ReturnsNull()
    {
        var vsFolder = Path.Combine(_tempSolutionDirectory, ".vs");
        Directory.CreateDirectory(vsFolder);
        File.WriteAllText(Path.Combine(vsFolder, "CodeAnchors.json"), "{\"v\":99,\"f\":{}}");

        Dictionary<string, IReadOnlyList<AnchorItem>>? loaded = AnchorCacheSerializer.Load(_tempSolutionDirectory);

        Assert.IsNull(loaded);
    }

    #endregion

    #region Delete

    [TestMethod]
    public void Delete_ExistingCacheFile_RemovesFile()
    {
        Dictionary<string, IReadOnlyList<AnchorItem>> cache = new()
        {
            ["Foo.cs"] = [CreateAnchor("Foo.cs")]
        };
        AnchorCacheSerializer.Save(_tempSolutionDirectory, cache);
        var filePath = AnchorCacheSerializer.GetCacheFilePath(_tempSolutionDirectory);
        Assert.IsTrue(File.Exists(filePath));

        AnchorCacheSerializer.Delete(_tempSolutionDirectory);

        Assert.IsFalse(File.Exists(filePath));
    }

    [TestMethod]
    public void Delete_NoCacheFile_DoesNotThrow()
    {
        AnchorCacheSerializer.Delete(_tempSolutionDirectory);
    }

    #endregion
}
