using System;
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
    [ContentType("CSharp")]
    [ContentType("Basic")]
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
    /// Provides outlining regions for XML documentation comments.
    /// When rendered comments are enabled, outlining is disabled to avoid conflicts
    /// with the IntraTextAdornment that replaces the comment text.
    /// </summary>
    internal sealed class XmlDocCommentOutliningTagger : ITagger<IOutliningRegionTag>
    {
        private readonly ITextBuffer _buffer;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public XmlDocCommentOutliningTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;
            ToggleRenderedCommentsCommand.RenderedCommentsStateChanged += OnRenderedStateChanged;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            RaiseTagsChanged();
        }

        private void OnRenderedStateChanged(object sender, EventArgs e)
        {
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

            ITextSnapshot snapshot = spans[0].Snapshot;
            IContentType contentType = snapshot.ContentType;
            var commentStyle = LanguageCommentStyle.GetForContentType(contentType);

            if (commentStyle == null)
            {
                yield break;
            }

            var parser = new XmlDocCommentParser(commentStyle);
            IReadOnlyList<XmlDocCommentBlock> commentBlocks = parser.FindAllCommentBlocks(snapshot);

            var renderingMode = General.Instance.CommentRenderingMode;
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

                string collapsedText;
                bool isDefaultCollapsed;

                if (renderingMode == RenderingMode.Compact || renderingMode == RenderingMode.Full)
                {
                    // In Compact/Full mode: use stripped summary as collapsed text
                    // Regions are collapsed by default to show the rendered adornment
                    collapsedText = XmlDocCommentRenderer.GetStrippedSummary(block);
                    if (string.IsNullOrWhiteSpace(collapsedText))
                    {
                        collapsedText = "...";
                    }
                    // Truncate long summaries for the collapsed form
                    if (collapsedText.Length > 80)
                    {
                        collapsedText = collapsedText.Substring(0, 77) + "...";
                    }
                    isDefaultCollapsed = true; // Always collapsed by default in rendered modes
                }
                else
                {
                    // Off mode: show first line as-is (with comment prefix)
                    ITextSnapshotLine firstLine = snapshot.GetLineFromLineNumber(block.StartLine);
                    collapsedText = firstLine.GetText().TrimStart();
                    isDefaultCollapsed = collapseByDefault;
                }

                var tag = new OutliningRegionTag(
                    collapsedForm: collapsedText,
                    collapsedHintForm: block.XmlContent,
                    isDefaultCollapsed: isDefaultCollapsed,
                    isImplementation: false);

                yield return new TagSpan<IOutliningRegionTag>(blockSpan, tag);
            }
        }
    }
}
