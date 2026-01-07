using CommentsVS.Services;

namespace CommentsVS.Test;

[TestClass]
public sealed class FilePathResolverTests
{
    private const string CurrentFile = @"C:\Projects\MyProject\src\Services\UserService.cs";
    private const string SolutionDir = @"C:\Projects\MyProject";
    private const string ProjectDir = @"C:\Projects\MyProject\src";

    #region Relative Path Tests

    [TestMethod]
    public void Resolve_WithRelativePath_ResolvesFromCurrentFile()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve("./Logger.cs");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.EndsWith("Logger.cs"));
        Assert.IsTrue(result.Contains(@"Services"));
    }

    [TestMethod]
    public void Resolve_WithParentRelativePath_ResolvesCorrectly()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve("../Models/User.cs");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.EndsWith("User.cs"));
        Assert.IsTrue(result.Contains(@"Models"));
    }

    [TestMethod]
    public void Resolve_WithPlainRelativePath_ResolvesFromCurrentFile()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve("Logger.cs");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.EndsWith("Logger.cs"));
        Assert.IsTrue(result.Contains(@"Services"));
    }

    #endregion

    #region Solution-Relative Path Tests

    [TestMethod]
    public void Resolve_WithSolutionRelativePath_SlashPrefix_ResolvesFromSolution()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve("/README.md");

        Assert.IsNotNull(result);
        Assert.AreEqual(Path.Combine(SolutionDir, "README.md"), result);
    }

    [TestMethod]
    public void Resolve_WithSolutionRelativePath_TildePrefix_ResolvesFromSolution()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve("~/docs/guide.md");

        Assert.IsNotNull(result);
        Assert.AreEqual(Path.Combine(SolutionDir, "docs", "guide.md"), result);
    }

    [TestMethod]
    public void Resolve_WithSolutionRelativePath_NoSolutionDir_ReturnsNull()
    {
        var resolver = new FilePathResolver(CurrentFile, solutionDirectory: null, ProjectDir);

        var result = resolver.Resolve("/README.md");

        Assert.IsNull(result);
    }

    #endregion

    #region Project-Relative Path Tests

    [TestMethod]
    public void Resolve_WithProjectRelativePath_ResolvesFromProject()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve("@/Models/User.cs");

        Assert.IsNotNull(result);
        Assert.AreEqual(Path.Combine(ProjectDir, "Models", "User.cs"), result);
    }

    [TestMethod]
    public void Resolve_WithProjectRelativePath_NoProjectDir_ReturnsNull()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, projectDirectory: null);

        var result = resolver.Resolve("@/Models/User.cs");

        Assert.IsNull(result);
    }

    #endregion

    #region Path Normalization Tests

    [TestMethod]
    public void Resolve_WithForwardSlashes_ConvertsToSystemSeparator()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve("Models/User.cs");

        Assert.IsNotNull(result);
        // Should work regardless of path separator
        Assert.IsTrue(result.Contains("User.cs"));
    }

    [TestMethod]
    public void Resolve_WithBackslashes_WorksCorrectly()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve(@"Models\User.cs");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("User.cs"));
    }

    [TestMethod]
    public void Resolve_WithMixedSlashes_NormalizesCorrectly()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve("Models/SubFolder\\User.cs");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("User.cs"));
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void Resolve_WithNullPath_ReturnsNull()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve(null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Resolve_WithEmptyPath_ReturnsNull()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve("");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Resolve_WithWhitespacePath_ReturnsNull()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve("   ");

        Assert.IsNull(result);
    }

    #endregion

    #region TryResolve Tests

    [TestMethod]
    public void TryResolve_WithExistingFile_ReturnsTrueAndPath()
    {
        // Create a temporary file to test with
        var tempFile = Path.GetTempFileName();
        try
        {
            var tempDir = Path.GetDirectoryName(tempFile);
            var fileName = Path.GetFileName(tempFile);
            var currentFile = Path.Combine(tempDir, "current.cs");

            var resolver = new FilePathResolver(currentFile, tempDir, tempDir);

            var result = resolver.TryResolve(fileName, out var resolvedPath);

            Assert.IsTrue(result);
            Assert.AreEqual(tempFile, resolvedPath);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void TryResolve_WithNonExistingFile_ReturnsFalse()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.TryResolve("NonExistent.cs", out var resolvedPath);

        Assert.IsFalse(result);
    }

    #endregion

    #region FileExists Tests

    [TestMethod]
    public void FileExists_WithExistingFile_ReturnsTrue()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var result = FilePathResolver.FileExists(tempFile);

            Assert.IsTrue(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void FileExists_WithNonExistingFile_ReturnsFalse()
    {
        var result = FilePathResolver.FileExists(@"C:\NonExistent\File.cs");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void FileExists_WithNullPath_ReturnsFalse()
    {
        var result = FilePathResolver.FileExists(null);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void FileExists_WithEmptyPath_ReturnsFalse()
    {
        var result = FilePathResolver.FileExists("");

        Assert.IsFalse(result);
    }

    #endregion

    #region Property Tests

    [TestMethod]
    public void CurrentFileDirectory_ReturnsCorrectDirectory()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.CurrentFileDirectory;

        Assert.AreEqual(@"C:\Projects\MyProject\src\Services", result);
    }

    [TestMethod]
    public void SolutionDirectory_ReturnsProvidedValue()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.SolutionDirectory;

        Assert.AreEqual(SolutionDir, result);
    }

    [TestMethod]
    public void ProjectDirectory_ReturnsProvidedValue()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.ProjectDirectory;

        Assert.AreEqual(ProjectDir, result);
    }

    #endregion

    #region Complex Path Tests

    [TestMethod]
    public void Resolve_WithDeepNestedPath_ResolvesCorrectly()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve("../Models/DTOs/UserDTO.cs");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("UserDTO.cs"));
    }

    [TestMethod]
    public void Resolve_WithMultipleParentReferences_ResolvesCorrectly()
    {
        var resolver = new FilePathResolver(CurrentFile, SolutionDir, ProjectDir);

        var result = resolver.Resolve("../../docs/README.md");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("README.md"));
    }

    #endregion
}
