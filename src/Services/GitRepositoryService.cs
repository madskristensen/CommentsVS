using System.IO;
using System.Text.RegularExpressions;

namespace CommentsVS.Services
{
    /// <summary>
    /// Represents information about a Git repository's hosting provider.
    /// </summary>
    public sealed class GitRepositoryInfo(GitHostingProvider provider, string owner, string repository, string baseUrl)
    {
        public GitHostingProvider Provider { get; } = provider;
        public string Owner { get; } = owner;
        public string Repository { get; } = repository;
        public string BaseUrl { get; } = baseUrl;

        /// <summary>
        /// Gets the URL for an issue/work item number.
        /// </summary>
        public string GetIssueUrl(int issueNumber)
        {
            return Provider switch
            {
                GitHostingProvider.GitHub => $"{BaseUrl}/{Owner}/{Repository}/issues/{issueNumber}",
                GitHostingProvider.GitLab => $"{BaseUrl}/{Owner}/{Repository}/-/issues/{issueNumber}",
                GitHostingProvider.Bitbucket => $"{BaseUrl}/{Owner}/{Repository}/issues/{issueNumber}",
                GitHostingProvider.AzureDevOps => $"{BaseUrl}/{Owner}/{Repository}/_workitems/edit/{issueNumber}",// Azure DevOps uses work items which are org-wide
                                                                                                                  // Format: https://dev.azure.com/{org}/{project}/_workitems/edit/{id}
                _ => null,
            };
        }
    }

    /// <summary>
    /// Supported Git hosting providers.
    /// </summary>
    public enum GitHostingProvider
    {
        Unknown,
        GitHub,
        GitLab,
        Bitbucket,
        AzureDevOps
    }

    /// <summary>
    /// Service to detect Git repository information from a file path.
    /// </summary>
    public static class GitRepositoryService
    {
        // Regex to parse remote URLs
        private static readonly Regex _gitHubHttpsRegex = new(
            @"https?://github\.com/(?<owner>[^/]+)/(?<repo>[^/\.]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _gitHubSshRegex = new(
            @"git@github\.com:(?<owner>[^/]+)/(?<repo>[^/\.]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _gitLabHttpsRegex = new(
            @"https?://gitlab\.com/(?<owner>[^/]+)/(?<repo>[^/\.]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _gitLabSshRegex = new(
            @"git@gitlab\.com:(?<owner>[^/]+)/(?<repo>[^/\.]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _bitbucketHttpsRegex = new(
            @"https?://bitbucket\.org/(?<owner>[^/]+)/(?<repo>[^/\.]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _bitbucketSshRegex = new(
            @"git@bitbucket\.org:(?<owner>[^/]+)/(?<repo>[^/\.]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _azureDevOpsHttpsRegex = new(
            @"https?://dev\.azure\.com/(?<org>[^/]+)/(?<project>[^/]+)/_git/(?<repo>[^/\.]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _azureDevOpsSshRegex = new(
            @"git@ssh\.dev\.azure\.com:v3/(?<org>[^/]+)/(?<project>[^/]+)/(?<repo>[^/\.]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _azureDevOpsOldHttpsRegex = new(
            @"https?://(?<org>[^\.]+)\.visualstudio\.com/(?<project>[^/]+)/_git/(?<repo>[^/\.]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Gets repository info for a file path by finding the Git repository root and parsing the remote URL.
        /// </summary>
        public static GitRepositoryInfo GetRepositoryInfo(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            try
            {
                var gitDir = FindGitDirectory(filePath);
                if (gitDir == null)
                {
                    return null;
                }

                var remoteUrl = GetOriginRemoteUrl(gitDir);
                if (string.IsNullOrEmpty(remoteUrl))
                {
                    return null;
                }

                return ParseRemoteUrl(remoteUrl);
            }
            catch
            {
                return null;
            }
        }

        private static string FindGitDirectory(string startPath)
        {
            var directory = Path.GetDirectoryName(startPath);

            while (!string.IsNullOrEmpty(directory))
            {
                var gitDir = Path.Combine(directory, ".git");
                if (Directory.Exists(gitDir))
                {
                    return gitDir;
                }

                // Check for .git file (worktrees/submodules)
                if (File.Exists(gitDir))
                {
                    return gitDir;
                }

                directory = Path.GetDirectoryName(directory);
            }

            return null;
        }

        private static string GetOriginRemoteUrl(string gitDir)
        {
            var configPath = Path.Combine(gitDir, "config");
            if (!File.Exists(configPath))
            {
                return null;
            }

            var lines = File.ReadAllLines(configPath);
            var inOriginSection = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("["))
                {
                    inOriginSection = trimmed.Equals("[remote \"origin\"]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (inOriginSection && trimmed.StartsWith("url", StringComparison.OrdinalIgnoreCase))
                {
                    var equalsIndex = trimmed.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        return trimmed.Substring(equalsIndex + 1).Trim();
                    }
                }
            }

            return null;
        }

        private static GitRepositoryInfo ParseRemoteUrl(string remoteUrl)
        {
            // GitHub
            Match match = _gitHubHttpsRegex.Match(remoteUrl);
            if (match.Success)
            {
                return new GitRepositoryInfo(
                    GitHostingProvider.GitHub,
                    match.Groups["owner"].Value,
                    match.Groups["repo"].Value,
                    "https://github.com");
            }

            match = _gitHubSshRegex.Match(remoteUrl);
            if (match.Success)
            {
                return new GitRepositoryInfo(
                    GitHostingProvider.GitHub,
                    match.Groups["owner"].Value,
                    match.Groups["repo"].Value,
                    "https://github.com");
            }

            // GitLab
            match = _gitLabHttpsRegex.Match(remoteUrl);
            if (match.Success)
            {
                return new GitRepositoryInfo(
                    GitHostingProvider.GitLab,
                    match.Groups["owner"].Value,
                    match.Groups["repo"].Value,
                    "https://gitlab.com");
            }

            match = _gitLabSshRegex.Match(remoteUrl);
            if (match.Success)
            {
                return new GitRepositoryInfo(
                    GitHostingProvider.GitLab,
                    match.Groups["owner"].Value,
                    match.Groups["repo"].Value,
                    "https://gitlab.com");
            }

            // Bitbucket
            match = _bitbucketHttpsRegex.Match(remoteUrl);
            if (match.Success)
            {
                return new GitRepositoryInfo(
                    GitHostingProvider.Bitbucket,
                    match.Groups["owner"].Value,
                    match.Groups["repo"].Value,
                    "https://bitbucket.org");
            }

            match = _bitbucketSshRegex.Match(remoteUrl);
            if (match.Success)
            {
                return new GitRepositoryInfo(
                    GitHostingProvider.Bitbucket,
                    match.Groups["owner"].Value,
                    match.Groups["repo"].Value,
                    "https://bitbucket.org");
            }

            // Azure DevOps (new format)
            match = _azureDevOpsHttpsRegex.Match(remoteUrl);
            if (match.Success)
            {
                return new GitRepositoryInfo(
                    GitHostingProvider.AzureDevOps,
                    match.Groups["org"].Value,
                    match.Groups["project"].Value,
                    "https://dev.azure.com");
            }

            match = _azureDevOpsSshRegex.Match(remoteUrl);
            if (match.Success)
            {
                return new GitRepositoryInfo(
                    GitHostingProvider.AzureDevOps,
                    match.Groups["org"].Value,
                    match.Groups["project"].Value,
                    "https://dev.azure.com");
            }

            // Azure DevOps (old visualstudio.com format)
            match = _azureDevOpsOldHttpsRegex.Match(remoteUrl);
            if (match.Success)
            {
                return new GitRepositoryInfo(
                    GitHostingProvider.AzureDevOps,
                    match.Groups["org"].Value,
                    match.Groups["project"].Value,
                    $"https://dev.azure.com");
            }

            return null;
        }
    }
}
