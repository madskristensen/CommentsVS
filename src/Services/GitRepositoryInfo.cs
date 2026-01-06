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
                GitHostingProvider.AzureDevOps => $"{BaseUrl}/{Owner}/{Repository}/_workitems/edit/{issueNumber}",
                _ => null,
            };
        }
    }
}
