using CommentsVS.Services;

namespace CommentsVS.Test;

[TestClass]
public sealed class GitRepositoryInfoTests
{
    [TestMethod]
    public void GetIssueUrl_GitHub_ReturnsCorrectUrl()
    {
        var repoInfo = new GitRepositoryInfo(
            GitHostingProvider.GitHub,
            "owner",
            "repo",
            "https://github.com");

        var result = repoInfo.GetIssueUrl(123);

        Assert.AreEqual("https://github.com/owner/repo/issues/123", result);
    }

    [TestMethod]
    public void GetIssueUrl_GitLab_ReturnsCorrectUrl()
    {
        var repoInfo = new GitRepositoryInfo(
            GitHostingProvider.GitLab,
            "owner",
            "repo",
            "https://gitlab.com");

        var result = repoInfo.GetIssueUrl(456);

        Assert.AreEqual("https://gitlab.com/owner/repo/-/issues/456", result);
    }

    [TestMethod]
    public void GetIssueUrl_Bitbucket_ReturnsCorrectUrl()
    {
        var repoInfo = new GitRepositoryInfo(
            GitHostingProvider.Bitbucket,
            "owner",
            "repo",
            "https://bitbucket.org");

        var result = repoInfo.GetIssueUrl(789);

        Assert.AreEqual("https://bitbucket.org/owner/repo/issues/789", result);
    }

    [TestMethod]
    public void GetIssueUrl_AzureDevOps_ReturnsCorrectUrl()
    {
        var repoInfo = new GitRepositoryInfo(
            GitHostingProvider.AzureDevOps,
            "org",
            "project",
            "https://dev.azure.com");

        var result = repoInfo.GetIssueUrl(101);

        Assert.AreEqual("https://dev.azure.com/org/project/_workitems/edit/101", result);
    }

    [TestMethod]
    public void GetIssueUrl_UnknownProvider_ReturnsNull()
    {
        var repoInfo = new GitRepositoryInfo(
            GitHostingProvider.Unknown,
            "owner",
            "repo",
            "https://example.com");

        var result = repoInfo.GetIssueUrl(123);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var repoInfo = new GitRepositoryInfo(
            GitHostingProvider.GitHub,
            "testowner",
            "testrepo",
            "https://github.com");

        Assert.AreEqual(GitHostingProvider.GitHub, repoInfo.Provider);
        Assert.AreEqual("testowner", repoInfo.Owner);
        Assert.AreEqual("testrepo", repoInfo.Repository);
        Assert.AreEqual("https://github.com", repoInfo.BaseUrl);
    }
}
