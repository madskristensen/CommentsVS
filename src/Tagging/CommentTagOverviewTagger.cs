using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using CommentsVS.Classification;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Tagging
{
    /// <summary>
    /// Provides overview mark taggers for comment tags (TODO, HACK, etc.) in the vertical scrollbar.
    /// </summary>
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(OverviewMarkTag))]
    [ContentType(SupportedContentTypes.Code)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class CommentTagOverviewTaggerProvider : IViewTaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
            {
                return null;
            }

            return buffer.Properties.GetOrCreateSingletonProperty(
                typeof(CommentTagOverviewTagger),
                () => new CommentTagOverviewTagger(buffer)) as ITagger<T>;
        }
    }

    /// <summary>
    /// Creates overview marks (scrollbar markers) for comment tags like TODO, HACK, NOTE, etc.
    /// </summary>
    internal sealed class CommentTagOverviewTagger : ITagger<OverviewMarkTag>, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private bool _disposed;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public CommentTagOverviewTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            // Raise tags changed for modified lines
            foreach (ITextChange change in e.Changes)
            {
                ITextSnapshotLine startLine = e.After.GetLineFromPosition(change.NewPosition);
                ITextSnapshotLine endLine = e.After.GetLineFromPosition(change.NewEnd);

                int start = startLine.Start.Position;
                int length = endLine.End.Position - start;

                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                    new SnapshotSpan(e.After, start, length)));
            }
        }

        public IEnumerable<ITagSpan<OverviewMarkTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            // Check if scrollbar markers are enabled
            if (!General.Instance.EnableScrollbarMarkers)
            {
                yield break;
            }

            // Also check if comment tag highlighting is enabled (respect the main setting)
            if (!General.Instance.EnableCommentTagHighlighting)
            {
                yield break;
            }

            if (spans.Count == 0)
            {
                yield break;
            }

            ITextSnapshot snapshot = spans[0].Snapshot;

            // Skip large files for performance
            if (snapshot.Length > Constants.MaxFileSize)
            {
                yield break;
            }

            foreach (SnapshotSpan span in spans)
            {
                string text = span.GetText();

                // Fast pre-check: skip regex if no anchor keywords are present
                bool hasAnyAnchor = false;
                foreach (string keyword in Constants.GetAllAnchorKeywords())
                {
                    if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasAnyAnchor = true;
                        break;
                    }
                }

                if (!hasAnyAnchor)
                {
                    continue;
                }

                int lineStart = span.Start.Position;

                foreach (System.Text.RegularExpressions.Match match in CommentPatterns.AnchorClassificationRegex.Matches(text))
                {
                    System.Text.RegularExpressions.Group tagGroup = match.Groups["tag"];
                    if (!tagGroup.Success)
                    {
                        continue;
                    }

                    string tag = tagGroup.Value.TrimEnd(':').ToUpperInvariant();
                    string formatName = GetOverviewMarkFormatName(tag);

                    if (formatName == null)
                    {
                        continue;
                    }

                    var tagSpan = new SnapshotSpan(snapshot, lineStart + tagGroup.Index, tagGroup.Length);

                    yield return new TagSpan<OverviewMarkTag>(tagSpan, new OverviewMarkTag(formatName));
                }
            }
        }

        /// <summary>
        /// Gets the overview mark format name for a given tag keyword.
        /// </summary>
        private static string GetOverviewMarkFormatName(string tag)
        {
            return tag switch
            {
                "TODO" => OverviewMarkFormatNames.Todo,
                "HACK" => OverviewMarkFormatNames.Hack,
                "NOTE" => OverviewMarkFormatNames.Note,
                "BUG" => OverviewMarkFormatNames.Bug,
                "FIXME" => OverviewMarkFormatNames.Fixme,
                "UNDONE" => OverviewMarkFormatNames.Undone,
                "REVIEW" => OverviewMarkFormatNames.Review,
                "ANCHOR" => OverviewMarkFormatNames.Anchor,
                _ => CheckCustomTag(tag)
            };
        }

        private static string CheckCustomTag(string tag)
        {
            // Check if it's a custom tag
            if (General.Instance.GetCustomTagsSet().Contains(tag))
            {
                return OverviewMarkFormatNames.Custom;
            }

            return null;
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
