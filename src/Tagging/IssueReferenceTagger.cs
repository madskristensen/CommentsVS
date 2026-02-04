using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Tagging
{
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(IUrlTag))]
    [ContentType(SupportedContentTypes.Code)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class IssueReferenceTaggerProvider : IViewTaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
            {
                return null;
            }

            return buffer.Properties.GetOrCreateSingletonProperty(
                () => new IssueReferenceTagger(buffer)) as ITagger<T>;
        }
    }


    /// <summary>
    /// Creates clickable links for issue references like #123 in comments.
    /// </summary>
    internal sealed class IssueReferenceTagger : ITagger<IUrlTag>, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private GitRepositoryInfo _repoInfo;
        private bool _repoInfoInitialized;
        private readonly string _filePath;
        private bool _disposed;

        // Match #123 pattern (issue/PR number) - must be preceded by whitespace or start of line
        // and followed by word boundary
        private static readonly Regex _issueReferenceRegex = new(
            @"(?<=^|[\s\(\[\{])#(?<number>\d+)\b",
            RegexOptions.Compiled);

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IssueReferenceTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;

            // Get file path and trigger async initialization
            if (_buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                _filePath = document.FilePath;
                // Fire and forget - triggers async fetch, results will be available on subsequent GetTags calls
                InitializeRepoInfoAsync().FireAndForget();
            }
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            foreach (ITextChange change in e.Changes)
            {
                ITextSnapshotLine line = e.After.GetLineFromPosition(change.NewPosition);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                    new SnapshotSpan(e.After, line.Start, line.Length)));
            }
        }

        public IEnumerable<ITagSpan<IUrlTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (!General.Instance.EnableIssueLinks)
            {
                yield break;
            }

            if (spans.Count == 0)
            {
                yield break;
            }

            // Skip large files for performance
            if (spans[0].Snapshot.Length > Constants.MaxFileSize)
            {
                yield break;
            }

            // Try to get cached repo info (non-blocking)
            if (!_repoInfoInitialized)
            {
                _repoInfo = GitRepositoryService.TryGetCachedRepositoryInfo(_filePath);
                if (_repoInfo != null)
                {
                    _repoInfoInitialized = true;
                }
            }

            if (_repoInfo == null)
            {
                yield break;
            }

            foreach (SnapshotSpan span in spans)
            {
                var text = span.GetText();

                // Quick check - skip if no # in the text
                if (!text.Contains("#"))
                {
                    continue;
                }

                // Find all comment portions in the text (both full-line and inline comments)
                foreach ((int Start, int Length) commentSpan in CommentSpanHelper.FindCommentSpans(text))
                {
                    // Use Regex.Match with startAt and check bounds instead of Substring allocation
                    var commentEnd = commentSpan.Start + commentSpan.Length;
                    Match match = _issueReferenceRegex.Match(text, commentSpan.Start, commentSpan.Length);

                    while (match.Success && match.Index < commentEnd)
                    {
                        if (int.TryParse(match.Groups["number"].Value, out var issueNumber))
                        {
                            var url = _repoInfo.GetIssueUrl(issueNumber);
                            if (!string.IsNullOrEmpty(url))
                            {
                                // match.Index is already relative to the full text
                                var tagSpan = new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length);
                                yield return new TagSpan<IUrlTag>(tagSpan, new UrlTag(new Uri(url)));
                            }
                        }

                        match = match.NextMatch();
                    }
                }
            }
        }

        private async Task InitializeRepoInfoAsync()
        {
            if (string.IsNullOrEmpty(_filePath))
            {
                return;
            }

            _repoInfo = await GitRepositoryService.GetRepositoryInfoAsync(_filePath).ConfigureAwait(false);
            _repoInfoInitialized = true;

            // Trigger re-tagging now that repo info is available
            if (_repoInfo != null)
            {
                ITextSnapshot snapshot = _buffer.CurrentSnapshot;
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                    new SnapshotSpan(snapshot, 0, snapshot.Length)));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _buffer.Changed -= OnBufferChanged;
        }
    }
}
