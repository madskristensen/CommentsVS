using System.Collections.Generic;
using System.ComponentModel.Composition;
using CommentsVS.Commands;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Tagging
{
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IOutliningRegionTag))]
    [ContentType(ContentTypes.CSharp)]
    [ContentType(ContentTypes.VisualBasic)]
    [ContentType(ContentTypes.FSharp)]
    [ContentType("TypeScript")]
    [ContentType("JavaScript")]
    [Order(Before = "default")]
    [Order(Before = "Structure")]
    [Name("XmlDocCommentOutliningTagger")]
    internal sealed class XmlDocCommentOutliningTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return buffer.Properties.GetOrCreateSingletonProperty(
                () => new XmlDocCommentOutliningTagger(buffer)) as ITagger<T>;
        }
    }

    /// <summary>
    /// Provides outlining regions for XML documentation comments. When rendered comments are enabled, outlining is
    /// disabled to avoid conflicts with the IntraTextAdornment that replaces the comment text.
    /// </summary>
    internal sealed class XmlDocCommentOutliningTagger : ITagger<IOutliningRegionTag>
    {
        private readonly ITextBuffer _buffer;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public XmlDocCommentOutliningTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;
            SetRenderingModeHelper.RenderedCommentsStateChanged += OnRenderedStateChanged;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            RaiseTagsChanged();
        }

        private void OnRenderedStateChanged(object sender, EventArgs e)
        {
            // Raise tags changed to update outlining regions for the new mode
            RaiseTagsChanged();
        }

        private void RaiseTagsChanged()
        {
            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {
                yield break;
            }

            RenderingMode renderingMode = General.Instance.CommentRenderingMode;

            // In Compact/Full mode, IntraTextAdornment handles display entirely
            // Don't create outlining regions to avoid background color artifacts
            if (renderingMode == RenderingMode.Compact || renderingMode == RenderingMode.Full)
            {
                yield break;
            }

            // Use cached blocks for performance - includes large file check
            IReadOnlyList<XmlDocCommentBlock> commentBlocks = XmlDocCommentParser.GetCachedCommentBlocks(_buffer);
            if (commentBlocks == null)
            {
                yield break;
            }

            ITextSnapshot snapshot = spans[0].Snapshot;
            var collapseByDefault = General.Instance.CollapseCommentsOnFileOpen;

            foreach (XmlDocCommentBlock block in commentBlocks)
            {
                // Adjust span to start after indentation so collapsed text
                // appears at the same column as the comment
                var adjustedStart = block.Span.Start + block.Indentation.Length;
                var adjustedSpan = new Span(adjustedStart, block.Span.End - adjustedStart);
                var blockSpan = new SnapshotSpan(snapshot, adjustedSpan);

                if (!spans.IntersectsWith(new NormalizedSnapshotSpanCollection(blockSpan)))
                {
                    continue;
                }

                // Off mode: show first line as-is (with comment prefix)
                ITextSnapshotLine firstLine = snapshot.GetLineFromLineNumber(block.StartLine);
                var collapsedText = firstLine.GetText().TrimStart();

                var tag = new OutliningRegionTag(
                    collapsedForm: collapsedText,
                    collapsedHintForm: block.XmlContent,
                    isDefaultCollapsed: collapseByDefault,
                    isImplementation: false);

                yield return new TagSpan<IOutliningRegionTag>(blockSpan, tag);
            }
        }
    }
}
