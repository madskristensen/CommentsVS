using CommentsVS.Services;

namespace CommentsVS.Test;

[TestClass]
public sealed class WorkspacePathFilterTests
{
    [TestMethod]
    public void IsFileWithinRoot_FileInsideRoot_ReturnsTrue()
    {
        var result = WorkspacePathFilter.IsFileWithinRoot(
            @"C:\Repo\src\Feature\File.cs",
            @"C:\Repo");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsFileWithinRoot_FileOutsideRoot_ReturnsFalse()
    {
        var result = WorkspacePathFilter.IsFileWithinRoot(
            @"C:\OtherRepo\src\File.cs",
            @"C:\Repo");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsFileWithinRoot_PrefixButDifferentRoot_ReturnsFalse()
    {
        var result = WorkspacePathFilter.IsFileWithinRoot(
            @"C:\Repo2\src\File.cs",
            @"C:\Repo");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsFileWithinRoot_NullOrEmptyInput_ReturnsFalse()
    {
        Assert.IsFalse(WorkspacePathFilter.IsFileWithinRoot(null, @"C:\Repo"));
        Assert.IsFalse(WorkspacePathFilter.IsFileWithinRoot(@"C:\Repo\a.cs", null));
        Assert.IsFalse(WorkspacePathFilter.IsFileWithinRoot(string.Empty, @"C:\Repo"));
        Assert.IsFalse(WorkspacePathFilter.IsFileWithinRoot(@"C:\Repo\a.cs", string.Empty));
    }
}
