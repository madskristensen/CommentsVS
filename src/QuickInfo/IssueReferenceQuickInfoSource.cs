using System.Collections.Generic;
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
    [ContentType(SupportedContentTypes.Code)]
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
        private static readonly Regex _issueReferenceRegex = new(
            @"#(?<number>\d+)\b",
            RegexOptions.Compiled);

        public async Task<QuickInfoItem> GetQuickInfoItemAsync(
            IAsyncQuickInfoSession session,
            CancellationToken cancellationToken)
        {
            if (!General.Instance.EnableIssueLinks)
            {
                return null;
            }

            SnapshotPoint? triggerPoint = session.GetTriggerPoint(textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
            {
                return null;
            }

            // Get repo info asynchronously
            GitRepositoryInfo repoInfo = await GetRepoInfoAsync().ConfigureAwait(false);
            if (repoInfo == null)
            {
                return null;
            }

            ITextSnapshotLine line = triggerPoint.Value.GetContainingLine();
            var lineText = line.GetText();

            var positionInLine = triggerPoint.Value.Position - line.Start.Position;

            // Find the comment portion(s) in the line
            IEnumerable<(int Start, int Length)> commentSpans = CommentSpanHelper.FindCommentSpans(lineText);

            // Check if trigger point is within any comment span
            var isInComment = false;
            var commentText = lineText;
            var commentStartInLine = 0;

            foreach ((var start, var length) in commentSpans)
            {
                if (positionInLine >= start && positionInLine < start + length)
                {
                    isInComment = true;
                    commentText = lineText.Substring(start, length);
                    commentStartInLine = start;
                    break;
                }
            }

            if (!isInComment)
            {
                return null;
            }

            // Find issue references in the comment portion
            foreach (Match match in _issueReferenceRegex.Matches(commentText))
            {
                var matchStartInLine = commentStartInLine + match.Index;
                var matchEndInLine = matchStartInLine + match.Length;

                // Check if the trigger point is within this match (adjusted for comment start)
                if (positionInLine >= matchStartInLine && positionInLine <= matchEndInLine)
                {
                    if (int.TryParse(match.Groups["number"].Value, out var issueNumber))
                    {
                        var url = repoInfo.GetIssueUrl(issueNumber);
                        if (!string.IsNullOrEmpty(url))
                        {
                            var span = new SnapshotSpan(line.Start + matchStartInLine, match.Length);
                            ITrackingSpan trackingSpan = textBuffer.CurrentSnapshot.CreateTrackingSpan(
                                span, SpanTrackingMode.EdgeInclusive);

                            var providerName = GetProviderName(repoInfo.Provider);
                            var tooltip = $"{providerName} Issue #{issueNumber}\n{url}\n\nCtrl+Click to open";

                            return new QuickInfoItem(trackingSpan, tooltip);
                        }
                    }
                }
            }

            return null;
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

        private async Task<GitRepositoryInfo> GetRepoInfoAsync()
        {
            if (textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                return await GitRepositoryService.GetRepositoryInfoAsync(document.FilePath).ConfigureAwait(false);
            }

                return null;
            }

            public void Dispose()
        {
        }
    }
}
