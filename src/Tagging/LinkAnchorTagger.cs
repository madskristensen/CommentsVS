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
        private bool _disposed;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public LinkAnchorTagger(ITextBuffer buffer, IClassificationTypeRegistryService registry)
        {
            _buffer = buffer;
            _linkClassificationType = registry.GetClassificationType(LinkAnchorClassificationDefinition.LinkAnchorClassificationType);
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
                var text = span.GetText();

                // Fast pre-check: skip if no LINK keyword
                if (text.IndexOf("LINK", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                // Check if this line is a comment
                if (!LanguageCommentStyle.IsCommentLine(text))
                {
                    continue;
                }


                IReadOnlyList<LinkAnchorInfo> links = LinkAnchorParser.Parse(text);
                foreach (LinkAnchorInfo link in links)
                {
                    // Only underline the target portion (path/anchor), not the "LINK:" prefix
                    var tagSpan = new SnapshotSpan(span.Snapshot, span.Start + link.TargetStartIndex, link.TargetLength);
                    yield return new TagSpan<IClassificationTag>(tagSpan, new ClassificationTag(_linkClassificationType));
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
