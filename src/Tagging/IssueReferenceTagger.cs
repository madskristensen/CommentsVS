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
                foreach ((int Start, int Length) commentSpan in FindCommentSpans(text))
                {
                    var commentText = text.Substring(commentSpan.Start, commentSpan.Length);

                    foreach (Match match in _issueReferenceRegex.Matches(commentText))
                    {
                        if (int.TryParse(match.Groups["number"].Value, out var issueNumber))
                        {
                            var url = _repoInfo.GetIssueUrl(issueNumber);
                            if (!string.IsNullOrEmpty(url))
                            {
                                // Adjust match position to be relative to the full span
                                var matchStartInSpan = commentSpan.Start + match.Index;
                                var tagSpan = new SnapshotSpan(span.Snapshot, span.Start + matchStartInSpan, match.Length);
                                yield return new TagSpan<IUrlTag>(tagSpan, new UrlTag(new Uri(url)));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Finds all comment spans in the given text (both full-line and inline comments).
        /// </summary>
        private static IEnumerable<(int Start, int Length)> FindCommentSpans(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            // Check if entire line is a comment (starts with comment prefix)
            if (LanguageCommentStyle.IsCommentLine(text))
            {
                yield return (0, text.Length);
                yield break;
            }

            // Look for inline single-line comments (//)
            var inlineCommentIndex = text.IndexOf("//");
            if (inlineCommentIndex >= 0)
            {
                // Make sure it's not inside a string literal
                if (!IsInsideStringLiteral(text, inlineCommentIndex))
                {
                    yield return (inlineCommentIndex, text.Length - inlineCommentIndex);
                }
            }
        }

        /// <summary>
        /// Checks if a position is inside a string literal.
        /// Simple heuristic: count quotes before the position.
        /// </summary>
        private static bool IsInsideStringLiteral(string text, int position)
        {
            var quoteCount = 0;
            var inVerbatim = false;

            for (var i = 0; i < position; i++)
            {
                if (text[i] == '@' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    inVerbatim = true;
                    quoteCount++;
                    i++; // Skip the quote
                    continue;
                }

                if (text[i] == '"')
                {
                    // Check if it's escaped (not in verbatim)
                    if (!inVerbatim && i > 0 && text[i - 1] == '\\')
                    {
                        // Count consecutive backslashes
                        var backslashCount = 0;
                        for (var j = i - 1; j >= 0 && text[j] == '\\'; j--)
                        {
                            backslashCount++;
                        }
                        // If odd number of backslashes, the quote is escaped
                        if (backslashCount % 2 == 1)
                        {
                            continue;
                        }
                    }

                    quoteCount++;

                    // If we were in verbatim and hit a quote, check for double-quote escape
                    if (inVerbatim && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        i++; // Skip the second quote in ""
                        continue;
                    }

                    if (quoteCount % 2 == 0)
                    {
                        inVerbatim = false;
                    }
                }
            }

            // Odd quote count means we're inside a string
            return quoteCount % 2 == 1;
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
