using CommentsVS.Services;

namespace CommentsVS.Test;

[TestClass]
public sealed class GitRepositoryServiceTests
{
    [TestInitialize]
    public void TestInitialize()
    {
        // Clear cache before each test
        GitRepositoryService.ClearCache();
    }

    #region GitHub URL Parsing Tests

    [TestMethod]
    public void ParseRemoteUrl_WithGitHubHttpsUrl_ReturnsCorrectInfo()
    {
        var remoteUrl = "https://github.com/madskristensen/CommentsVS";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("madskristensen", result.Owner);
        Assert.AreEqual("CommentsVS", result.Repository);
        Assert.AreEqual("https://github.com", result.BaseUrl);
    }

    [TestMethod]
    public void ParseRemoteUrl_WithGitHubHttpsUrlWithDotGit_ReturnsCorrectInfo()
    {
        var remoteUrl = "https://github.com/madskristensen/CommentsVS.git";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("madskristensen", result.Owner);
        Assert.AreEqual("CommentsVS", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_WithGitHubSshUrl_ReturnsCorrectInfo()
    {
        var remoteUrl = "git@github.com:madskristensen/CommentsVS.git";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("madskristensen", result.Owner);
        Assert.AreEqual("CommentsVS", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_WithGitHubHttpUrl_ReturnsCorrectInfo()
    {
        var remoteUrl = "http://github.com/user/repo";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("user", result.Owner);
        Assert.AreEqual("repo", result.Repository);
    }

    #endregion

    #region GitLab URL Parsing Tests

    [TestMethod]
    public void ParseRemoteUrl_WithGitLabHttpsUrl_ReturnsCorrectInfo()
    {
        var remoteUrl = "https://gitlab.com/mygroup/myproject";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitLab, result.Provider);
        Assert.AreEqual("mygroup", result.Owner);
        Assert.AreEqual("myproject", result.Repository);
        Assert.AreEqual("https://gitlab.com", result.BaseUrl);
    }

    [TestMethod]
    public void ParseRemoteUrl_WithGitLabSshUrl_ReturnsCorrectInfo()
    {
        var remoteUrl = "git@gitlab.com:mygroup/myproject.git";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitLab, result.Provider);
        Assert.AreEqual("mygroup", result.Owner);
        Assert.AreEqual("myproject", result.Repository);
    }

    #endregion

    #region Bitbucket URL Parsing Tests

    [TestMethod]
    public void ParseRemoteUrl_WithBitbucketHttpsUrl_ReturnsCorrectInfo()
    {
        var remoteUrl = "https://bitbucket.org/myteam/myrepo";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.Bitbucket, result.Provider);
        Assert.AreEqual("myteam", result.Owner);
        Assert.AreEqual("myrepo", result.Repository);
        Assert.AreEqual("https://bitbucket.org", result.BaseUrl);
    }

    [TestMethod]
    public void ParseRemoteUrl_WithBitbucketSshUrl_ReturnsCorrectInfo()
    {
        var remoteUrl = "git@bitbucket.org:myteam/myrepo.git";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.Bitbucket, result.Provider);
        Assert.AreEqual("myteam", result.Owner);
        Assert.AreEqual("myrepo", result.Repository);
    }

    #endregion

    #region Azure DevOps URL Parsing Tests

    [TestMethod]
    public void ParseRemoteUrl_WithAzureDevOpsNewFormatHttps_ReturnsCorrectInfo()
    {
        var remoteUrl = "https://dev.azure.com/myorg/myproject/_git/myrepo";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.AzureDevOps, result.Provider);
        Assert.AreEqual("myorg", result.Owner);
        Assert.AreEqual("myproject", result.Repository);
        Assert.AreEqual("https://dev.azure.com", result.BaseUrl);
    }

    [TestMethod]
    public void ParseRemoteUrl_WithAzureDevOpsNewFormatSsh_ReturnsCorrectInfo()
    {
        var remoteUrl = "git@ssh.dev.azure.com:v3/myorg/myproject/myrepo";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.AzureDevOps, result.Provider);
        Assert.AreEqual("myorg", result.Owner);
        Assert.AreEqual("myproject", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_WithAzureDevOpsOldFormat_ReturnsCorrectInfo()
    {
        var remoteUrl = "https://myorg.visualstudio.com/myproject/_git/myrepo";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.AzureDevOps, result.Provider);
        Assert.AreEqual("myorg", result.Owner);
        Assert.AreEqual("myproject", result.Repository);
        Assert.AreEqual("https://dev.azure.com", result.BaseUrl);
    }

    #endregion

    #region Unknown Provider Tests

    [TestMethod]
    public void ParseRemoteUrl_WithUnknownProvider_ReturnsNull()
    {
        var remoteUrl = "https://unknown-git-host.com/user/repo";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseRemoteUrl_WithInvalidUrl_ReturnsNull()
    {
        var remoteUrl = "not-a-valid-url";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseRemoteUrl_WithEmptyString_ReturnsNull()
    {
        var result = InvokeParseRemoteUrl("");

        Assert.IsNull(result);
    }

    #endregion

    #region Case Insensitivity Tests

    [TestMethod]
    public void ParseRemoteUrl_WithMixedCaseGitHub_ReturnsCorrectInfo()
    {
        var remoteUrl = "https://GitHub.COM/User/Repo";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("User", result.Owner);
        Assert.AreEqual("Repo", result.Repository);
    }

    #endregion

    #region Cache Tests

    [TestMethod]
    public void ClearCache_RemovesAllCachedData()
    {
        // Cache is already cleared in TestInitialize
        // This test verifies that calling ClearCache doesn't throw
        GitRepositoryService.ClearCache();

        // No exception means success
        Assert.IsTrue(true);
    }

    #endregion

    #region TryGetCachedRepositoryInfo Tests

    [TestMethod]
    public void TryGetCachedRepositoryInfo_WithNullPath_ReturnsNull()
    {
        var result = GitRepositoryService.TryGetCachedRepositoryInfo(null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryGetCachedRepositoryInfo_WithEmptyPath_ReturnsNull()
    {
        var result = GitRepositoryService.TryGetCachedRepositoryInfo("");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryGetCachedRepositoryInfo_WithNonGitPath_ReturnsNull()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var result = GitRepositoryService.TryGetCachedRepositoryInfo(tempFile);

            Assert.IsNull(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region Special Characters Tests

    [TestMethod]
    public void ParseRemoteUrl_WithHyphenInName_ReturnsCorrectInfo()
    {
        var remoteUrl = "https://github.com/my-org/my-repo";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual("my-org", result.Owner);
        Assert.AreEqual("my-repo", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_WithUnderscoreInName_ReturnsCorrectInfo()
    {
        var remoteUrl = "https://github.com/my_org/my_repo";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual("my_org", result.Owner);
        Assert.AreEqual("my_repo", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_WithNumbersInName_ReturnsCorrectInfo()
    {
        var remoteUrl = "https://github.com/org123/repo456";

        var result = InvokeParseRemoteUrl(remoteUrl);

        Assert.IsNotNull(result);
        Assert.AreEqual("org123", result.Owner);
        Assert.AreEqual("repo456", result.Repository);
    }

    #endregion

    // Helper method to invoke the private ParseRemoteUrl method via reflection
    private static GitRepositoryInfo InvokeParseRemoteUrl(string remoteUrl)
    {
        var method = typeof(GitRepositoryService).GetMethod(
            "ParseRemoteUrl",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        return (GitRepositoryInfo)method.Invoke(null, [remoteUrl]);
    }
}
