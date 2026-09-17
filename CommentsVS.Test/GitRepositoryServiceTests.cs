using CommentsVS.Services;

namespace CommentsVS.Test;

/// <summary>
/// Tests for GitRepositoryService URL parsing logic.
/// Calls the real internal GitRepositoryService.ParseRemoteUrl (exposed via InternalsVisibleTo)
/// instead of a hand-copied mirror, so regressions in the production regex/parsing are caught.
/// </summary>
[TestClass]
public sealed class GitRepositoryServiceTests
{
    #region GitHub URL Parsing

    [TestMethod]
    public void ParseRemoteUrl_GitHubHttps_ReturnsCorrectInfo()
    {
        var url = "https://github.com/owner/repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
        Assert.AreEqual("https://github.com", result.BaseUrl);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitHubHttpsWithGitExtension_ReturnsCorrectInfo()
    {
        var url = "https://github.com/owner/repo.git";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitHubSsh_ReturnsCorrectInfo()
    {
        var url = "git@github.com:owner/repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitHubSshWithGitExtension_ReturnsCorrectInfo()
    {
        var url = "git@github.com:owner/repo.git";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitHubOrganization_ReturnsCorrectInfo()
    {
        var url = "https://github.com/Microsoft/vscode";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual("Microsoft", result.Owner);
        Assert.AreEqual("vscode", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitHubEnterpriseHttps_ReturnsCorrectInfo()
    {
        var url = "https://github.contoso.com/owner/repo.git";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
        Assert.AreEqual("https://github.contoso.com", result.BaseUrl);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitHubEnterpriseSsh_ReturnsCorrectInfo()
    {
        var url = "git@github.contoso.com:owner/repo.git";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
        Assert.AreEqual("https://github.contoso.com", result.BaseUrl);
    }

    #endregion

    #region GitLab URL Parsing

    [TestMethod]
    public void ParseRemoteUrl_GitLabHttps_ReturnsCorrectInfo()
    {
        var url = "https://gitlab.com/owner/repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitLab, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
        Assert.AreEqual("https://gitlab.com", result.BaseUrl);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitLabSsh_ReturnsCorrectInfo()
    {
        var url = "git@gitlab.com:owner/repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitLab, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitLabEnterpriseHttps_ReturnsCorrectInfo()
    {
        var url = "https://gitlab.contoso.com/group/subgroup/repo.git";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitLab, result.Provider);
        Assert.AreEqual("group/subgroup", result.Owner);
        Assert.AreEqual("repo", result.Repository);
        Assert.AreEqual("https://gitlab.contoso.com", result.BaseUrl);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitLabEnterpriseSsh_ReturnsCorrectInfo()
    {
        var url = "git@gitlab.contoso.com:group/subgroup/repo.git";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitLab, result.Provider);
        Assert.AreEqual("group/subgroup", result.Owner);
        Assert.AreEqual("repo", result.Repository);
        Assert.AreEqual("https://gitlab.contoso.com", result.BaseUrl);
    }

    #endregion

    #region Bitbucket URL Parsing

    [TestMethod]
    public void ParseRemoteUrl_BitbucketHttps_ReturnsCorrectInfo()
    {
        var url = "https://bitbucket.org/owner/repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.Bitbucket, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
        Assert.AreEqual("https://bitbucket.org", result.BaseUrl);
    }

    [TestMethod]
    public void ParseRemoteUrl_BitbucketSsh_ReturnsCorrectInfo()
    {
        var url = "git@bitbucket.org:owner/repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.Bitbucket, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
    }

    #endregion

    #region Azure DevOps URL Parsing

    [TestMethod]
    public void ParseRemoteUrl_AzureDevOpsNewFormat_ReturnsCorrectInfo()
    {
        var url = "https://dev.azure.com/org/project/_git/repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.AzureDevOps, result.Provider);
        Assert.AreEqual("org", result.Owner);
        Assert.AreEqual("project", result.Repository);
        Assert.AreEqual("https://dev.azure.com", result.BaseUrl);
    }

    [TestMethod]
    public void ParseRemoteUrl_AzureDevOpsSsh_ReturnsCorrectInfo()
    {
        var url = "git@ssh.dev.azure.com:v3/org/project/repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.AzureDevOps, result.Provider);
        Assert.AreEqual("org", result.Owner);
        Assert.AreEqual("project", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_AzureDevOpsOldFormat_ReturnsCorrectInfo()
    {
        var url = "https://myorg.visualstudio.com/myproject/_git/myrepo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.AzureDevOps, result.Provider);
        Assert.AreEqual("myorg", result.Owner);
        Assert.AreEqual("myproject", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_SelfHostedAzureDevOps_ReturnsCorrectInfo()
    {
        // Self-hosted Azure DevOps URL with _git pattern on a custom server
        // Format: /{collection}/{project}/_git/{repo} - we extract collection as owner, project as repository
        var url = "https://tfs.example.com/DefaultCollection/MyProject/_git/MyRepo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.AzureDevOps, result.Provider);
        Assert.AreEqual("DefaultCollection", result.Owner);
        Assert.AreEqual("MyProject", result.Repository);
        Assert.AreEqual("https://tfs.example.com", result.BaseUrl);
    }

    [TestMethod]
    public void ParseRemoteUrl_SelfHostedAzureDevOps_IssueUrl()
    {
        // Ensure work item URL is generated correctly for self-hosted Azure DevOps
        var url = "https://tfs.company.local/org/project/_git/repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.AzureDevOps, result.Provider);

        // Work item URL should use org/project (which maps to Owner/Repository)
        var issueUrl = result.GetIssueUrl(123);
        Assert.AreEqual("https://tfs.company.local/org/project/_workitems/edit/123", issueUrl);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void ParseRemoteUrl_UnknownHostSingleSegment_ReturnsNull()
    {
        var url = "https://example.com/repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseRemoteUrl_LocalPath_ReturnsNull()
    {
        var url = @"C:\repos\myrepo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseRemoteUrl_EmptyString_ReturnsNull()
    {
        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl("");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseRemoteUrl_NullString_ReturnsNull()
    {
        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseRemoteUrl_MalformedUrl_ReturnsNull()
    {
        var url = "not-a-url";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitHubWithTrailingSlash_ReturnsCorrectInfo()
    {
        var url = "https://github.com/owner/repo/";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        // Should still match the owner/repo
        Assert.IsNotNull(result);
        Assert.AreEqual("owner", result.Owner);
    }

    [TestMethod]
    public void ParseRemoteUrl_CaseInsensitive_ReturnsCorrectProvider()
    {
        var url = "HTTPS://GITHUB.COM/Owner/Repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
    }

    [TestMethod]
    public void ParseRemoteUrl_UnknownHostTwoSegments_AssumesGitHubStyle()
    {
        var url = "https://code.contoso.com/owner/repo.git";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
        Assert.AreEqual("https://code.contoso.com", result.BaseUrl);
    }

    #endregion

    #region Special Characters in Names

    [TestMethod]
    public void ParseRemoteUrl_OwnerWithHyphen_ReturnsCorrectInfo()
    {
        var url = "https://github.com/my-org/my-repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual("my-org", result.Owner);
        Assert.AreEqual("my-repo", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_OwnerWithUnderscore_ReturnsCorrectInfo()
    {
        var url = "https://github.com/my_org/my_repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual("my_org", result.Owner);
        Assert.AreEqual("my_repo", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_NumericOwner_ReturnsCorrectInfo()
    {
        var url = "https://github.com/123org/456repo";

        GitRepositoryInfo? result = GitRepositoryService.ParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual("123org", result.Owner);
        Assert.AreEqual("456repo", result.Repository);
    }

    #endregion
}
