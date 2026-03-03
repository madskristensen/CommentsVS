using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
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
        private readonly IReadOnlyList<string> _anchorTags;
        private readonly HashSet<string> _customTags;
        private readonly Regex _anchorRegex;
        private readonly BufferedTagChangeNotifier _changeNotifier;
        private bool _disposed;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public CommentTagOverviewTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;
            _changeNotifier = new BufferedTagChangeNotifier(args => TagsChanged?.Invoke(this, args));

            // Get file path and cache anchor tags/regex for this file (from .editorconfig or Options)
            var filePath = buffer.GetFileName();
            _anchorTags = EditorConfigSettings.GetAllAnchorTags(filePath);
            _customTags = EditorConfigSettings.GetCustomAnchorTags(filePath);
            _anchorRegex = EditorConfigSettings.GetAnchorClassificationRegex(filePath);
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            _changeNotifier.Queue(e);
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
                var text = span.GetText();

                // Fast pre-check: skip regex if no anchor keywords are present
                var hasAnyAnchor = false;
                foreach (var keyword in _anchorTags)
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

                var lineStart = span.Start.Position;

                foreach ((int Start, int Length) commentSpan in CommentSpanHelper.FindCommentSpans(text))
                {
                    var commentText = text.Substring(commentSpan.Start, commentSpan.Length);

                    // Skip comment spans that don't contain anchor keywords
                    var hasAnchorInComment = false;
                    foreach (var keyword in _anchorTags)
                    {
                        if (commentText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            hasAnchorInComment = true;
                            break;
                        }
                    }

                    if (!hasAnchorInComment)
                    {
                        continue;
                    }

                    foreach (Match match in _anchorRegex.Matches(commentText))
                    {
                        Group tagGroup = match.Groups["tag"];
                        if (!tagGroup.Success)
                        {
                            continue;
                        }

                        var tag = tagGroup.Value.TrimEnd(':').ToUpperInvariant();
                        var formatName = GetOverviewMarkFormatName(tag);

                        if (formatName == null)
                        {
                            continue;
                        }

                        // Extend span to include the optional tag prefix (e.g., @ in @TODO)
                        Group pfxGroup = match.Groups["tagprefix"];
                        var spanStart = pfxGroup.Success ? pfxGroup.Index : tagGroup.Index;
                        var spanLength = (tagGroup.Index + tagGroup.Length) - spanStart;
                        var tagSpan = new SnapshotSpan(snapshot, lineStart + commentSpan.Start + spanStart, spanLength);

                        yield return new TagSpan<OverviewMarkTag>(tagSpan, new OverviewMarkTag(formatName));
                    }
                }
            }
        }

        /// <summary>
        /// Gets the overview mark format name for a given tag keyword.
        /// </summary>
        private string GetOverviewMarkFormatName(string tag)
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

        private string CheckCustomTag(string tag)
        {
            // Check if it's a custom tag (from .editorconfig or Options page)
            if (_customTags.Contains(tag))
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
            _changeNotifier.Dispose();
        }
    }
}
