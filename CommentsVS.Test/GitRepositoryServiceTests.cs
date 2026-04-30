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
    private static readonly Regex _scpRemoteUrlPattern = new(@"^(?<user>[^@]+)@(?<host>[^:]+):(?<path>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

    [TestMethod]
    public void ParseRemoteUrl_GitHubEnterpriseHttps_ReturnsCorrectInfo()
    {
        var url = "https://github.contoso.com/owner/repo.git";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

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

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

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

    [TestMethod]
    public void ParseRemoteUrl_GitLabEnterpriseHttps_ReturnsCorrectInfo()
    {
        var url = "https://gitlab.contoso.com/group/subgroup/repo.git";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

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

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

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

    [TestMethod]
    public void ParseRemoteUrl_SelfHostedAzureDevOps_ReturnsCorrectInfo()
    {
        // Self-hosted Azure DevOps URL with _git pattern on a custom server
        // Format: /{collection}/{project}/_git/{repo} - we extract collection as owner, project as repository
        var url = "https://tfs.example.com/DefaultCollection/MyProject/_git/MyRepo";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

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

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

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

    [TestMethod]
    public void ParseRemoteUrl_UnknownHostTwoSegments_AssumesGitHubStyle()
    {
        var url = "https://code.contoso.com/owner/repo.git";

        GitRepositoryInfo? result = TestParseRemoteUrl(url);

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
    /// Pattern definition matching the Azure DevOps parsing rules in GitRepositoryService.
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
        if (string.IsNullOrWhiteSpace(remoteUrl))
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

        if (!TryGetRemoteLocation(remoteUrl, out var host, out var baseUrl, out var pathSegments))
        {
            return null;
        }

        GitHostingProvider provider = GetProviderFromHost(host, pathSegments);
        if (provider == GitHostingProvider.Unknown)
        {
            return null;
        }

        if (!TryGetOwnerAndRepository(provider, pathSegments, out var ownerName, out var repositoryName))
        {
            return null;
        }

        return new GitRepositoryInfo(provider, ownerName, repositoryName, baseUrl);

    }

    private static bool TryGetRemoteLocation(string remoteUrl, out string host, out string baseUrl, out string[] pathSegments)
    {
        if (Uri.TryCreate(remoteUrl, UriKind.Absolute, out Uri? remoteUri) && !string.IsNullOrEmpty(remoteUri.Host))
        {
            host = remoteUri.Host;
            baseUrl = remoteUri.GetLeftPart(UriPartial.Authority);
            pathSegments = GetPathSegments(remoteUri.AbsolutePath);
            return pathSegments.Length >= 2;
        }

        Match sshMatch = _scpRemoteUrlPattern.Match(remoteUrl);
        if (sshMatch.Success)
        {
            host = sshMatch.Groups["host"].Value;
            baseUrl = $"https://{host}";
            pathSegments = GetPathSegments(sshMatch.Groups["path"].Value);
            return pathSegments.Length >= 2;
        }

        host = string.Empty;
        baseUrl = string.Empty;
        pathSegments = [];
        return false;
    }

    private static string[] GetPathSegments(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return [];
        }

        return repositoryPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
    }

    private static GitHostingProvider GetProviderFromHost(string host, string[] pathSegments)
    {
        if (ContainsHostKeyword(host, "gitlab"))
        {
            return GitHostingProvider.GitLab;
        }

        if (ContainsHostKeyword(host, "github"))
        {
            return GitHostingProvider.GitHub;
        }

        if (ContainsHostKeyword(host, "bitbucket"))
        {
            return GitHostingProvider.Bitbucket;
        }

        // Check for Azure DevOps by host keywords or URL pattern
        if (ContainsHostKeyword(host, "azure") ||
            ContainsHostKeyword(host, "visualstudio") ||
            ContainsGitSegment(pathSegments))
        {
            return GitHostingProvider.AzureDevOps;
        }

        return pathSegments.Length > 2
            ? GitHostingProvider.GitLab
            : GitHostingProvider.GitHub;
    }

    private static bool ContainsGitSegment(string[] pathSegments)
    {
        // Azure DevOps URLs contain a "_git" segment (e.g., /{project}/_git/{repo})
        foreach (var segment in pathSegments)
        {
            if (string.Equals(segment, "_git", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetOwnerAndRepository(GitHostingProvider provider, string[] pathSegments, out string owner, out string repository)
    {
        owner = string.Empty;
        repository = string.Empty;

        if (pathSegments.Length < 2)
        {
            return false;
        }

        repository = TrimGitSuffix(pathSegments[pathSegments.Length - 1]);
        if (string.IsNullOrWhiteSpace(repository))
        {
            return false;
        }

        switch (provider)
        {
            case GitHostingProvider.AzureDevOps:
                // Azure DevOps: /{org}/{project}/_git/{repo} or /{collection}/{project}/_git/{repo}
                // Find the _git segment and extract org as owner, project as repository
                var gitIndex = Array.FindIndex(pathSegments, s => string.Equals(s, "_git", StringComparison.OrdinalIgnoreCase));
                if (gitIndex < 2 || gitIndex >= pathSegments.Length - 1)
                {
                    return false;
                }

                // Owner is the organization/collection (first segment before project)
                // Repository is the project (segment immediately before _git)
                owner = string.Join("/", pathSegments, 0, gitIndex - 1);
                repository = pathSegments[gitIndex - 1];
                return !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repository);

            case GitHostingProvider.GitLab:
                owner = string.Join("/", pathSegments, 0, pathSegments.Length - 1);
                return !string.IsNullOrWhiteSpace(owner);

            case GitHostingProvider.GitHub:
            case GitHostingProvider.Bitbucket:
                if (pathSegments.Length != 2)
                {
                    return false;
                }

                owner = pathSegments[0];
                return !string.IsNullOrWhiteSpace(owner);

            default:
                return false;
        }
    }

    private static bool ContainsHostKeyword(string host, string keyword)
    {
        return host.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string TrimGitSuffix(string repositoryName)
    {
        if (string.IsNullOrEmpty(repositoryName))
        {
            return repositoryName;
        }

        return repositoryName.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? repositoryName.Substring(0, repositoryName.Length - 4)
            : repositoryName;
    }

    #endregion
}
