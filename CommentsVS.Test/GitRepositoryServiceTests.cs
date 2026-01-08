using System.Text.RegularExpressions;
using CommentsVS.Services;

namespace CommentsVS.Test;

/// <summary>
/// Tests for GitRepositoryService URL parsing logic.
/// Since ParseRemoteUrl is internal, we test using helper methods that mirror the regex patterns.
/// </summary>
[TestClass]
public sealed class GitRepositoryServiceTests
{
    #region GitHub URL Parsing

    [TestMethod]
    public void ParseRemoteUrl_GitHubHttps_ReturnsCorrectInfo()
    {
        var url = "https://github.com/owner/repo";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

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

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitHubSsh_ReturnsCorrectInfo()
    {
        var url = "git@github.com:owner/repo";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitHubSshWithGitExtension_ReturnsCorrectInfo()
    {
        var url = "git@github.com:owner/repo.git";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitHubOrganization_ReturnsCorrectInfo()
    {
        var url = "https://github.com/Microsoft/vscode";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual("Microsoft", result.Owner);
        Assert.AreEqual("vscode", result.Repository);
    }

    #endregion

    #region GitLab URL Parsing

    [TestMethod]
    public void ParseRemoteUrl_GitLabHttps_ReturnsCorrectInfo()
    {
        var url = "https://gitlab.com/owner/repo";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

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

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitLab, result.Provider);
        Assert.AreEqual("owner", result.Owner);
        Assert.AreEqual("repo", result.Repository);
    }

    #endregion

    #region Bitbucket URL Parsing

    [TestMethod]
    public void ParseRemoteUrl_BitbucketHttps_ReturnsCorrectInfo()
    {
        var url = "https://bitbucket.org/owner/repo";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

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

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

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

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

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

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.AzureDevOps, result.Provider);
        Assert.AreEqual("org", result.Owner);
        Assert.AreEqual("project", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_AzureDevOpsOldFormat_ReturnsCorrectInfo()
    {
        var url = "https://myorg.visualstudio.com/myproject/_git/myrepo";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.AzureDevOps, result.Provider);
        Assert.AreEqual("myorg", result.Owner);
        Assert.AreEqual("myproject", result.Repository);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void ParseRemoteUrl_UnknownProvider_ReturnsNull()
    {
        var url = "https://example.com/owner/repo";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseRemoteUrl_LocalPath_ReturnsNull()
    {
        var url = @"C:\repos\myrepo";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseRemoteUrl_EmptyString_ReturnsNull()
    {
        GitRepositoryInfo? result = TestParseRemoteUrl("");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseRemoteUrl_NullString_ReturnsNull()
    {
        GitRepositoryInfo? result = TestParseRemoteUrl(null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseRemoteUrl_MalformedUrl_ReturnsNull()
    {
        var url = "not-a-url";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseRemoteUrl_GitHubWithTrailingSlash_ReturnsCorrectInfo()
    {
        var url = "https://github.com/owner/repo/";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        // Should still match the owner/repo
        Assert.IsNotNull(result);
        Assert.AreEqual("owner", result.Owner);
    }

    [TestMethod]
    public void ParseRemoteUrl_CaseInsensitive_ReturnsCorrectProvider()
    {
        var url = "HTTPS://GITHUB.COM/Owner/Repo";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual(GitHostingProvider.GitHub, result.Provider);
    }

    #endregion

    #region Special Characters in Names

    [TestMethod]
    public void ParseRemoteUrl_OwnerWithHyphen_ReturnsCorrectInfo()
    {
        var url = "https://github.com/my-org/my-repo";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual("my-org", result.Owner);
        Assert.AreEqual("my-repo", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_OwnerWithUnderscore_ReturnsCorrectInfo()
    {
        var url = "https://github.com/my_org/my_repo";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual("my_org", result.Owner);
        Assert.AreEqual("my_repo", result.Repository);
    }

    [TestMethod]
    public void ParseRemoteUrl_NumericOwner_ReturnsCorrectInfo()
    {
        var url = "https://github.com/123org/456repo";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

        Assert.IsNotNull(result);
        Assert.AreEqual("123org", result.Owner);
        Assert.AreEqual("456repo", result.Repository);
    }

    #endregion

    #region Test Helper - Mirrors GitRepositoryService.ParseRemoteUrl logic

    /// <summary>
    /// Pattern definition matching GitRepositoryService._remoteUrlPatterns
    /// </summary>
    private sealed class RemoteUrlPattern(Regex regex, GitHostingProvider provider, string baseUrl, bool usesOrgProject = false)
    {
        public Regex Regex { get; } = regex;
        public GitHostingProvider Provider { get; } = provider;
        public string BaseUrl { get; } = baseUrl;
        public bool UsesOrgProject { get; } = usesOrgProject;
    }

    private static readonly RemoteUrlPattern[] _remoteUrlPatterns =
    [
        // GitHub
        new(new(@"https?://github\.com/(?<owner>[^/]+)/(?<repo>[^/\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            GitHostingProvider.GitHub, "https://github.com"),
        new(new(@"git@github\.com:(?<owner>[^/]+)/(?<repo>[^/\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            GitHostingProvider.GitHub, "https://github.com"),

        // GitLab
        new(new(@"https?://gitlab\.com/(?<owner>[^/]+)/(?<repo>[^/\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            GitHostingProvider.GitLab, "https://gitlab.com"),
        new(new(@"git@gitlab\.com:(?<owner>[^/]+)/(?<repo>[^/\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            GitHostingProvider.GitLab, "https://gitlab.com"),

        // Bitbucket
        new(new(@"https?://bitbucket\.org/(?<owner>[^/]+)/(?<repo>[^/\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            GitHostingProvider.Bitbucket, "https://bitbucket.org"),
        new(new(@"git@bitbucket\.org:(?<owner>[^/]+)/(?<repo>[^/\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            GitHostingProvider.Bitbucket, "https://bitbucket.org"),

        // Azure DevOps (new format)
        new(new(@"https?://dev\.azure\.com/(?<org>[^/]+)/(?<project>[^/]+)/_git/(?<repo>[^/\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            GitHostingProvider.AzureDevOps, "https://dev.azure.com", usesOrgProject: true),
        new(new(@"git@ssh\.dev\.azure\.com:v3/(?<org>[^/]+)/(?<project>[^/]+)/(?<repo>[^/\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            GitHostingProvider.AzureDevOps, "https://dev.azure.com", usesOrgProject: true),

        // Azure DevOps (old visualstudio.com format)
        new(new(@"https?://(?<org>[^\.]+)\.visualstudio\.com/(?<project>[^/]+)/_git/(?<repo>[^/\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            GitHostingProvider.AzureDevOps, "https://dev.azure.com", usesOrgProject: true),
    ];

    /// <summary>
    /// Mirrors the ParseRemoteUrl method from GitRepositoryService.
    /// </summary>
    private static GitRepositoryInfo? TestParseRemoteUrl(string? remoteUrl)
    {
        if (string.IsNullOrEmpty(remoteUrl))
        {
            return null;
        }

        foreach (RemoteUrlPattern pattern in _remoteUrlPatterns)
        {
            Match match = pattern.Regex.Match(remoteUrl);
            if (match.Success)
            {
                var owner = pattern.UsesOrgProject ? match.Groups["org"].Value : match.Groups["owner"].Value;
                var repo = pattern.UsesOrgProject ? match.Groups["project"].Value : match.Groups["repo"].Value;

                return new GitRepositoryInfo(pattern.Provider, owner, repo, pattern.BaseUrl);
            }
        }

        return null;
    }

    #endregion
}
