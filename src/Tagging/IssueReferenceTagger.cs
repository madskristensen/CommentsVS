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
    [ContentType("code")]
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
    internal sealed class IssueReferenceTagger : ITagger<IUrlTag>
    {
        private readonly ITextBuffer _buffer;
        private GitRepositoryInfo _repoInfo;
        private bool _repoInfoInitialized;

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

            // Initialize repo info lazily
            if (!_repoInfoInitialized)
            {
                InitializeRepoInfo(spans[0].Snapshot);
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

                // Check if this line is a comment
                if (!LanguageCommentStyle.IsCommentLine(text))
                {
                    continue;
                }

                foreach (Match match in _issueReferenceRegex.Matches(text))
                {
                    if (int.TryParse(match.Groups["number"].Value, out var issueNumber))
                    {
                        var url = _repoInfo.GetIssueUrl(issueNumber);
                        if (!string.IsNullOrEmpty(url))
                        {
                            var tagSpan = new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length);
                            yield return new TagSpan<IUrlTag>(tagSpan, new UrlTag(new Uri(url)));
                        }
                    }
                }
            }
        }

        private void InitializeRepoInfo(ITextSnapshot snapshot)
        {
            _repoInfoInitialized = true;

            if (_buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                _repoInfo = GitRepositoryService.GetRepositoryInfo(document.FilePath);
            }
        }
    }
}
