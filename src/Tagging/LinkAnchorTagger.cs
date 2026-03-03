using System.Collections.Generic;
using System.ComponentModel.Composition;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Tagging
{
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(IClassificationTag))]
    [ContentType(SupportedContentTypes.Code)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class LinkAnchorTaggerProvider : IViewTaggerProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry { get; set; }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
            {
                return null;
            }

            return buffer.Properties.GetOrCreateSingletonProperty(
                () => new LinkAnchorTagger(buffer, ClassificationRegistry)) as ITagger<T>;
        }
    }

    /// <summary>
    /// Creates classification tags for LINK anchors in comments to render them with underline styling.
    /// </summary>
    internal sealed class LinkAnchorTagger : ITagger<IClassificationTag>, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private readonly IClassificationType _linkClassificationType;
        private readonly CommentLineParseCache _lineParseCache;
        private readonly BufferedTagChangeNotifier _changeNotifier;
        private bool _disposed;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public LinkAnchorTagger(ITextBuffer buffer, IClassificationTypeRegistryService registry)
        {
            _buffer = buffer;
            _linkClassificationType = registry.GetClassificationType(LinkAnchorClassificationDefinition.LinkAnchorClassificationType);
            _lineParseCache = CommentLineParseCache.GetOrCreate(buffer);
            _changeNotifier = new BufferedTagChangeNotifier(args => TagsChanged?.Invoke(this, args));
            _buffer.Changed += OnBufferChanged;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            _changeNotifier.Queue(e);
        }

        public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_linkClassificationType == null)
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

            foreach (SnapshotSpan span in spans)
            {
                var startLineNumber = span.Start.GetContainingLine().LineNumber;
                var endLineNumber = span.End.GetContainingLine().LineNumber;

                for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
                {
                    ITextSnapshotLine line = span.Snapshot.GetLineFromLineNumber(lineNumber);
                    ParsedCommentLineData lineData = _lineParseCache.GetLineData(span.Snapshot, lineNumber);

                    if (lineData.Links.Count == 0)
                    {
                        continue;
                    }

                    foreach (LinkAnchorInfo link in lineData.Links)
                    {
                        var tagStart = line.Start.Position + link.TargetStartIndex;
                        var tagSpan = new SnapshotSpan(span.Snapshot, tagStart, link.TargetLength);

                        if (tagSpan.IntersectsWith(span))
                        {
                            yield return new TagSpan<IClassificationTag>(tagSpan, new ClassificationTag(_linkClassificationType));
                        }
                    }
                }
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
            _changeNotifier.Dispose();
        }
    }

    /// <summary>
    /// Defines the classification type for LINK anchors.
    /// </summary>
    internal static class LinkAnchorClassificationDefinition
    {
        public const string LinkAnchorClassificationType = "CommentsVS.LinkAnchor";

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(LinkAnchorClassificationType)]
        internal static ClassificationTypeDefinition LinkAnchorType = null;
    }

    /// <summary>
    /// Defines the format for LINK anchor text (underlined, like hyperlinks).
    /// </summary>
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = LinkAnchorClassificationDefinition.LinkAnchorClassificationType)]
    [Name(LinkAnchorClassificationDefinition.LinkAnchorClassificationType)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class LinkAnchorFormatDefinition : ClassificationFormatDefinition
    {
        public LinkAnchorFormatDefinition()
        {
            DisplayName = "Comment Link Anchor";
            TextDecorations = System.Windows.TextDecorations.Underline;
            // SteelBlue is visible in both light and dark themes
            ForegroundColor = System.Windows.Media.Colors.SteelBlue;
        }
    }
}
