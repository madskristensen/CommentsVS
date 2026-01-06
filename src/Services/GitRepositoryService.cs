using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;

namespace CommentsVS.Services
{
    /// <summary>
    /// Service to detect Git repository information from a file path.
    /// </summary>
    public static class GitRepositoryService
    {
        /// <summary>
        /// Cache of repository info by git directory path.
        /// Using ConcurrentDictionary for thread-safety since multiple taggers may access simultaneously.
        /// </summary>
        private static readonly ConcurrentDictionary<string, GitRepositoryInfo> _repoCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Defines a pattern for matching a Git remote URL to a hosting provider.
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
        /// Gets repository info for a file path by finding the Git repository root and parsing the remote URL.
        /// Results are cached by git directory for performance.
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

                // Check cache first
                if (_repoCache.TryGetValue(gitDir, out GitRepositoryInfo cachedInfo))
                {
                    return cachedInfo;
                }

                // Parse and cache
                var remoteUrl = GetOriginRemoteUrl(gitDir);
                if (string.IsNullOrEmpty(remoteUrl))
                {
                    return null;
                }

                GitRepositoryInfo repoInfo = ParseRemoteUrl(remoteUrl);

                // Cache even if null to avoid repeated lookups
                _repoCache.TryAdd(gitDir, repoInfo);

                return repoInfo;
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
    }
}
