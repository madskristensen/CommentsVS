using CommentsVS.Services;

namespace CommentsVS.Test;

/// <summary>
/// Tests for FilePathResolver path resolution logic.
/// These tests focus on the path resolution algorithm without requiring VS dependencies.
/// </summary>
[TestClass]
public sealed class FilePathResolverTests
{
    private const string _testCurrentFile = @"C:\Projects\MyApp\src\Services\UserService.cs";
    private const string _testSolutionDir = @"C:\Projects\MyApp";
    private const string _testProjectDir = @"C:\Projects\MyApp\src";

    #region Relative Path Resolution

    [TestMethod]
    public void Resolve_SimplePath_ResolvedFromCurrentFileDirectory()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("OtherService.cs");

        Assert.AreEqual(@"C:\Projects\MyApp\src\Services\OtherService.cs", result);
    }

    [TestMethod]
    public void Resolve_DotSlashPath_ResolvedFromCurrentFileDirectory()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("./OtherService.cs");

        Assert.AreEqual(@"C:\Projects\MyApp\src\Services\OtherService.cs", result);
    }

    [TestMethod]
    public void Resolve_ParentRelativePath_NavigatesUp()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("../Models/User.cs");

        Assert.AreEqual(@"C:\Projects\MyApp\src\Models\User.cs", result);
    }

    [TestMethod]
    public void Resolve_MultipleParentRelativePath_NavigatesMultipleLevels()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("../../tests/UserTests.cs");

        Assert.AreEqual(@"C:\Projects\MyApp\tests\UserTests.cs", result);
    }

    [TestMethod]
    public void Resolve_SubdirectoryPath_ResolvesCorrectly()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("Handlers/UserHandler.cs");

        Assert.AreEqual(@"C:\Projects\MyApp\src\Services\Handlers\UserHandler.cs", result);
    }

    #endregion

    #region Solution-Relative Path Resolution

    [TestMethod]
    public void Resolve_TildeSlashPath_ResolvedFromSolutionRoot()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("~/docs/readme.md");

        Assert.AreEqual(@"C:\Projects\MyApp\docs\readme.md", result);
    }

    [TestMethod]
    public void Resolve_LeadingSlashPath_ResolvedFromSolutionRoot()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("/src/Program.cs");

        Assert.AreEqual(@"C:\Projects\MyApp\src\Program.cs", result);
    }

    [TestMethod]
    public void Resolve_SolutionRelativeWithBackslash_ResolvedFromSolutionRoot()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("~\\config\\appsettings.json");

        Assert.AreEqual(@"C:\Projects\MyApp\config\appsettings.json", result);
    }

    [TestMethod]
    public void Resolve_SolutionRelative_NoSolutionDir_ReturnsNull()
    {
        var resolver = new FilePathResolver(_testCurrentFile, solutionDirectory: null, _testProjectDir);

        var result = resolver.Resolve("~/readme.md");

        Assert.IsNull(result);
    }

    #endregion

    #region Project-Relative Path Resolution

    [TestMethod]
    public void Resolve_AtSlashPath_ResolvedFromProjectRoot()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("@/Models/User.cs");

        Assert.AreEqual(@"C:\Projects\MyApp\src\Models\User.cs", result);
    }

    [TestMethod]
    public void Resolve_ProjectRelativeWithBackslash_ResolvedFromProjectRoot()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("@\\Services\\UserService.cs");

        Assert.AreEqual(@"C:\Projects\MyApp\src\Services\UserService.cs", result);
    }

    [TestMethod]
    public void Resolve_ProjectRelative_NoProjectDir_ReturnsNull()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, projectDirectory: null);

        var result = resolver.Resolve("@/Models/User.cs");

        Assert.IsNull(result);
    }

    #endregion

    #region Path Normalization

    [TestMethod]
    public void Resolve_ForwardSlashes_ConvertedToBackslashes()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("Models/User.cs");

        Assert.IsTrue(result?.Contains('\\') ?? false, "Should use Windows path separators");
        Assert.IsFalse(result?.Contains('/') ?? true, "Should not contain forward slashes");
    }

    [TestMethod]
    public void Resolve_MixedSlashes_HandledCorrectly()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("Models/Sub\\User.cs");

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Contains('/'), "Should not contain forward slashes");
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void Resolve_NullPath_ReturnsNull()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve(null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Resolve_EmptyPath_ReturnsNull()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Resolve_WhitespacePath_ReturnsNull()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("   ");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Resolve_NoCurrentFile_RelativePathReturnsNull()
    {
        var resolver = new FilePathResolver(currentFilePath: null, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("file.cs");

        Assert.IsNull(result);
    }

    #endregion

    #region TryResolve Tests

    [TestMethod]
    public void TryResolve_ExistingFile_ReturnsTrueAndPath()
    {
        // Create a temporary file for this test
        var tempDir = Path.Combine(Path.GetTempPath(), "FilePathResolverTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test.txt");
        File.WriteAllText(tempFile, "test content");

        try
        {
            var currentFile = Path.Combine(tempDir, "current.cs");
            var resolver = new FilePathResolver(currentFile, tempDir, tempDir);

            var success = resolver.TryResolve("test.txt", out var resolvedPath);

            Assert.IsTrue(success);
            Assert.AreEqual(tempFile, resolvedPath);
        }
        finally
        {
            // Cleanup
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [TestMethod]
    public void TryResolve_NonExistingFile_ReturnsFalse()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var success = resolver.TryResolve("nonexistent_file_abc123.xyz", out var resolvedPath);

        Assert.IsFalse(success);
    }

    #endregion

    #region FileExists Tests

    [TestMethod]
    public void FileExists_ExistingFile_ReturnsTrue()
    {
        // Use a file that we know exists
        var existingFile = typeof(FilePathResolverTests).Assembly.Location;

        var result = FilePathResolver.FileExists(existingFile);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void FileExists_NonExistingFile_ReturnsFalse()
    {
        var result = FilePathResolver.FileExists(@"C:\NonExistent\path\file.xyz");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void FileExists_NullPath_ReturnsFalse()
    {
        var result = FilePathResolver.FileExists(null);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void FileExists_EmptyPath_ReturnsFalse()
    {
        var result = FilePathResolver.FileExists("");

        Assert.IsFalse(result);
    }

    #endregion

    #region Complex Path Scenarios

    [TestMethod]
    public void Resolve_PathWithSpaces_HandledCorrectly()
    {
        var currentFile = @"C:\My Projects\App Name\src\file.cs";
        var solutionDir = @"C:\My Projects\App Name";
        var resolver = new FilePathResolver(currentFile, solutionDir, solutionDir);

        var result = resolver.Resolve("Models/User Model.cs");

        Assert.AreEqual(@"C:\My Projects\App Name\src\Models\User Model.cs", result);
    }

    [TestMethod]
    public void Resolve_DeepNestedPath_HandledCorrectly()
    {
        var resolver = new FilePathResolver(_testCurrentFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("../../../external/lib/helper.cs");

        Assert.IsNotNull(result);
        Assert.EndsWith("helper.cs", result);
    }

    [TestMethod]
    public void Resolve_SolutionRootFile_FromDeepPath()
    {
        var deepFile = @"C:\Projects\MyApp\src\Features\Users\Handlers\GetUser.cs";
        var resolver = new FilePathResolver(deepFile, _testSolutionDir, _testProjectDir);

        var result = resolver.Resolve("~/README.md");

        Assert.AreEqual(@"C:\Projects\MyApp\README.md", result);
    }

    #endregion
}
