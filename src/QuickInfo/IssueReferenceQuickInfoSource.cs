using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.QuickInfo
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name("IssueReferenceQuickInfo")]
    [ContentType("code")]
    [Order(Before = "Default Quick Info Presenter")]
    internal sealed class IssueReferenceQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(
                () => new IssueReferenceQuickInfoSource(textBuffer));
        }
    }

    /// <summary>
    /// Provides hover tooltips for issue references (#123) showing the full URL.
    /// </summary>
    internal sealed class IssueReferenceQuickInfoSource(ITextBuffer textBuffer) : IAsyncQuickInfoSource
    {
        private GitRepositoryInfo _repoInfo;
        private bool _repoInfoInitialized;

        private static readonly Regex _issueReferenceRegex = new(
            @"#(?<number>\d+)\b",
            RegexOptions.Compiled);

        public Task<QuickInfoItem> GetQuickInfoItemAsync(
            IAsyncQuickInfoSession session,
            CancellationToken cancellationToken)
        {
            if (!General.Instance.EnableIssueLinks)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            SnapshotPoint? triggerPoint = session.GetTriggerPoint(textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            // Initialize repo info lazily
            if (!_repoInfoInitialized)
            {
                InitializeRepoInfo();
            }

            if (_repoInfo == null)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            ITextSnapshotLine line = triggerPoint.Value.GetContainingLine();
            var lineText = line.GetText();

            // Check if this line is a comment
            if (!LanguageCommentStyle.IsCommentLine(lineText))
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            var positionInLine = triggerPoint.Value.Position - line.Start.Position;

            // Find issue references in the line
            foreach (Match match in _issueReferenceRegex.Matches(lineText))
            {
                // Check if the trigger point is within this match
                if (positionInLine >= match.Index && positionInLine <= match.Index + match.Length)
                {
                    if (int.TryParse(match.Groups["number"].Value, out var issueNumber))
                    {
                        var url = _repoInfo.GetIssueUrl(issueNumber);
                        if (!string.IsNullOrEmpty(url))
                        {
                            var span = new SnapshotSpan(line.Start + match.Index, match.Length);
                            ITrackingSpan trackingSpan = textBuffer.CurrentSnapshot.CreateTrackingSpan(
                                span, SpanTrackingMode.EdgeInclusive);

                            var providerName = GetProviderName(_repoInfo.Provider);
                            var tooltip = $"{providerName} Issue #{issueNumber}\n{url}\n\nCtrl+Click to open";

                            return Task.FromResult(new QuickInfoItem(trackingSpan, tooltip));
                        }
                    }
                }
            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        private static string GetProviderName(GitHostingProvider provider)
        {
            return provider switch
            {
                GitHostingProvider.GitHub => "GitHub",
                GitHostingProvider.GitLab => "GitLab",
                GitHostingProvider.Bitbucket => "Bitbucket",
                GitHostingProvider.AzureDevOps => "Azure DevOps Work Item",
                _ => "Issue",
            };
        }

        private void InitializeRepoInfo()
        {
            _repoInfoInitialized = true;

            if (textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                _repoInfo = GitRepositoryService.GetRepositoryInfo(document.FilePath);
            }
        }

        public void Dispose()
        {
        }
    }
}
