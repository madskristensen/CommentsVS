using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        private static readonly ConcurrentDictionary<string, string> _gitDirCache = new(StringComparer.OrdinalIgnoreCase);

        private const string NoGitDirectorySentinel = "<none>";
        private static readonly GitRepositoryInfo NoRepositoryInfoSentinel = new(
            GitHostingProvider.Unknown,
            string.Empty,
            string.Empty,
            string.Empty);

        /// <summary>
        /// Cache of in-flight async operations to prevent duplicate reads.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Task<GitRepositoryInfo>> _pendingReads = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Clears all cached repository information.
        /// Call this when the solution closes or when Git configuration may have changed.
        /// </summary>
        public static void ClearCache()
        {
            _repoCache.Clear();
            _pendingReads.Clear();
            _gitDirCache.Clear();
        }

        /// <summary>
        /// Defines a pattern for matching an Azure DevOps remote URL.
        /// </summary>
        private sealed class RemoteUrlPattern(Regex regex, GitHostingProvider provider, string baseUrl, bool usesOrgProject = false)
        {
            public Regex Regex { get; } = regex;
            public GitHostingProvider Provider { get; } = provider;
            public string BaseUrl { get; } = baseUrl;
            public bool UsesOrgProject { get; } = usesOrgProject;
        }

        private static readonly Regex _scpRemoteUrlPattern = new(
            @"^(?<user>[^@]+)@(?<host>[^:]+):(?<path>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        /// Gets repository info for a file path asynchronously by finding the Git repository root and parsing the remote URL.
        /// Results are cached by git directory for performance.
        /// </summary>
        public static async Task<GitRepositoryInfo> GetRepositoryInfoAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            try
            {
                var gitDir = GetGitDirectoryCached(filePath);
                if (gitDir == null)
                {
                    return null;
                }

                // Check cache first
                if (_repoCache.TryGetValue(gitDir, out GitRepositoryInfo cachedInfo))
                {
                    return ReferenceEquals(cachedInfo, NoRepositoryInfoSentinel) ? null : cachedInfo;
                }

                // Check if there's already a pending read for this git directory
                if (_pendingReads.TryGetValue(gitDir, out Task<GitRepositoryInfo> pendingTask))
                {
                    return await pendingTask.ConfigureAwait(false);
                }

                // Create new task for reading
                Task<GitRepositoryInfo> readTask = ReadAndCacheRepositoryInfoAsync(gitDir);

                // Store the task to prevent duplicate reads
                if (_pendingReads.TryAdd(gitDir, readTask))
                {
                    try
                    {
                        return await readTask.ConfigureAwait(false);
                    }
                    finally
                    {
                        // Clean up the pending read
                        _pendingReads.TryRemove(gitDir, out _);
                    }
                }
                else
                {
                    // Another thread added it first, use that one
                    if (_pendingReads.TryGetValue(gitDir, out Task<GitRepositoryInfo> existingTask))
                    {
                        return await existingTask.ConfigureAwait(false);
                    }

                    // Fallback: just read it
                    return await readTask.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                ex.Log();
                return null;
            }
        }

        /// <summary>
        /// Tries to get cached repository info without blocking.
        /// Returns null if the info is not yet cached (call GetRepositoryInfoAsync to fetch it).
        /// Use this from synchronous contexts like ITagger.GetTags to avoid UI thread blocking.
        /// </summary>
        public static GitRepositoryInfo TryGetCachedRepositoryInfo(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            try
            {
                var gitDir = GetGitDirectoryCached(filePath);
                if (gitDir == null)
                {
                    return null;
                }

                _repoCache.TryGetValue(gitDir, out GitRepositoryInfo cachedInfo);
                return ReferenceEquals(cachedInfo, NoRepositoryInfoSentinel) ? null : cachedInfo;
            }
            catch (Exception ex)
            {
                ex.Log();
                return null;
            }
        }

        private static async Task<GitRepositoryInfo> ReadAndCacheRepositoryInfoAsync(string gitDir)
        {
            // Read remote URL on background thread
            var remoteUrl = await Task.Run(() => GetOriginRemoteUrlAsync(gitDir)).ConfigureAwait(false);

            if (string.IsNullOrEmpty(remoteUrl))
            {
                _repoCache[gitDir] = NoRepositoryInfoSentinel;
                return null;
            }

            GitRepositoryInfo repoInfo = ParseRemoteUrl(remoteUrl);

            // Cache even if null to avoid repeated lookups
            _repoCache[gitDir] = repoInfo ?? NoRepositoryInfoSentinel;

            return repoInfo;
        }

        private static string GetGitDirectoryCached(string filePath)
        {
            if (_gitDirCache.TryGetValue(filePath, out var cachedGitDir))
            {
                return string.Equals(cachedGitDir, NoGitDirectorySentinel, StringComparison.Ordinal)
                    ? null
                    : cachedGitDir;
            }

            var gitDir = FindGitDirectory(filePath);
            _gitDirCache[filePath] = gitDir ?? NoGitDirectorySentinel;
            return gitDir;
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

        private static async Task<string> GetOriginRemoteUrlAsync(string gitDir)
        {
            var configPath = Path.Combine(gitDir, "config");
            if (!File.Exists(configPath))
            {
                return null;
            }

            string[] lines;
            try
            {
                // .NET Framework 4.8 doesn't have ReadAllLinesAsync, read on background thread
                lines = await Task.Run(() => File.ReadAllLines(configPath)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ex.Log();
                return null;
            }

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
            if (string.IsNullOrWhiteSpace(remoteUrl))
            {
                return null;
            }

            if (TryParseAzureDevOpsRemoteUrl(remoteUrl, out GitRepositoryInfo repoInfo))
            {
                return repoInfo;
            }

            return TryParseStandardRemoteUrl(remoteUrl);
        }

        private static bool TryParseAzureDevOpsRemoteUrl(string remoteUrl, out GitRepositoryInfo repoInfo)
        {
            foreach (RemoteUrlPattern pattern in _remoteUrlPatterns)
            {
                Match match = pattern.Regex.Match(remoteUrl);
                if (match.Success)
                {
                    var owner = pattern.UsesOrgProject ? match.Groups["org"].Value : match.Groups["owner"].Value;
                    var repo = pattern.UsesOrgProject ? match.Groups["project"].Value : match.Groups["repo"].Value;

                    repoInfo = new GitRepositoryInfo(pattern.Provider, owner, repo, pattern.BaseUrl);
                    return true;
                }
            }

            repoInfo = null;
            return false;
        }

        private static GitRepositoryInfo TryParseStandardRemoteUrl(string remoteUrl)
        {
            if (!TryGetRemoteLocation(remoteUrl, out var host, out var baseUrl, out var pathSegments))
            {
                return null;
            }

            GitHostingProvider provider = GetProviderFromHost(host, pathSegments.Length);
            if (provider == GitHostingProvider.Unknown)
            {
                return null;
            }

            if (!TryGetOwnerAndRepository(provider, pathSegments, out var owner, out var repository))
            {
                return null;
            }

            return new GitRepositoryInfo(provider, owner, repository, baseUrl);
        }

        private static bool TryGetRemoteLocation(string remoteUrl, out string host, out string baseUrl, out string[] pathSegments)
        {
            if (Uri.TryCreate(remoteUrl, UriKind.Absolute, out Uri remoteUri) && !string.IsNullOrEmpty(remoteUri.Host))
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

            host = null;
            baseUrl = null;
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

        private static GitHostingProvider GetProviderFromHost(string host, int segmentCount)
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

            return segmentCount > 2
                ? GitHostingProvider.GitLab
                : GitHostingProvider.GitHub;
        }

        private static bool TryGetOwnerAndRepository(GitHostingProvider provider, string[] pathSegments, out string owner, out string repository)
        {
            owner = null;
            repository = null;

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
            return host?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
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
    }
}
