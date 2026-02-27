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
    /// <summary>
    /// Provides outlining tags for XML documentation comments with collapse-by-default behavior.
    /// Only active when rendering mode is Off and CollapseCommentsOnFileOpen is enabled.
    /// </summary>
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IOutliningRegionTag))]
    [ContentType(SupportedContentTypes.CSharp)]
    [ContentType(SupportedContentTypes.VisualBasic)]
    [ContentType(SupportedContentTypes.FSharp)]
    [ContentType(SupportedContentTypes.CPlusPlus)]
    [ContentType(SupportedContentTypes.TypeScript)]
    [ContentType(SupportedContentTypes.JavaScript)]
    [ContentType(SupportedContentTypes.Razor)]
    [ContentType(SupportedContentTypes.Sql)]
    [ContentType(SupportedContentTypes.PowerShell)]
    internal sealed class XmlDocCommentOutliningTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (buffer == null)
            {
                return null;
            }

            return buffer.Properties.GetOrCreateSingletonProperty(
                typeof(XmlDocCommentOutliningTagger),
                () => new XmlDocCommentOutliningTagger(buffer)) as ITagger<T>;
        }
    }

    /// <summary>
    /// Creates outlining regions for XML documentation comments that are collapsed by default.
    /// This tagger only provides tags when:
    /// - Rendering mode is Off (not Compact or Full)
    /// - CollapseCommentsOnFileOpen setting is enabled
    /// </summary>
    internal sealed class XmlDocCommentOutliningTagger : ITagger<IOutliningRegionTag>, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private bool _disposed;
        private bool _shouldProvideOutliningTags;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public XmlDocCommentOutliningTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;

            // Cache initial settings state
            UpdateCachedSettings();

            // Listen for rendering mode changes to refresh tags
            SetRenderingModeHelper.RenderedCommentsStateChanged += OnRenderedStateChanged;

            // Listen for settings changes
            General.Saved += OnSettingsSaved;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            // Only raise tags changed if the feature is active
            if (!ShouldProvideOutliningTags())
            {
                return;
            }

            foreach (ITextChange change in e.Changes)
            {
                ITextSnapshotLine startLine = e.After.GetLineFromPosition(change.NewPosition);
                ITextSnapshotLine endLine = e.After.GetLineFromPosition(change.NewEnd);

                var start = startLine.Start.Position;
                var length = endLine.End.Position - start;

                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                    new SnapshotSpan(e.After, start, length)));
            }
        }

        private void OnRenderedStateChanged(object sender, EventArgs e)
        {
            // Rendering mode changed, update cache and refresh all tags
            UpdateCachedSettings();
            RaiseTagsChangedForEntireBuffer();
        }

        private void OnSettingsSaved(General options)
        {
            // Settings changed, update cache and refresh all tags
            UpdateCachedSettings();
            RaiseTagsChangedForEntireBuffer();
        }

        private void UpdateCachedSettings()
        {
            General settings = General.Instance;
            _shouldProvideOutliningTags = settings.CommentRenderingMode == RenderingMode.Off
                && settings.CollapseCommentsOnFileOpen;
        }

        private void RaiseTagsChangedForEntireBuffer()
        {
            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            if (snapshot.Length > 0)
            {
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                    new SnapshotSpan(snapshot, 0, snapshot.Length)));
            }
        }

        /// <summary>
        /// Determines whether this tagger should provide outlining tags.
        /// Only active when rendering mode is Off AND collapse by default is enabled.
        /// Uses cached value updated on settings/rendering mode changes.
        /// </summary>
        private bool ShouldProvideOutliningTags() => _shouldProvideOutliningTags;

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            // Early exit if feature is not active
            if (!ShouldProvideOutliningTags())
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

            // Get cached comment blocks
            IReadOnlyList<XmlDocCommentBlock> commentBlocks = XmlDocCommentParser.GetCachedCommentBlocks(_buffer);
            if (commentBlocks == null || commentBlocks.Count == 0)
            {
                yield break;
            }

            // Find comment blocks that intersect with the requested spans
            foreach (XmlDocCommentBlock block in commentBlocks)
            {
                // Only create outlining regions for multi-line comments
                if (block.EndLine <= block.StartLine)
                {
                    continue;
                }

                // Adjust span to start at the comment prefix (skip indentation)
                // The block.Span starts at position 0 of the line, but the outlining
                // region should start at the /// or ''' prefix
                var indentLength = block.Indentation?.Length ?? 0;
                var adjustedStart = block.Span.Start + indentLength;
                var adjustedLength = block.Span.Length - indentLength;

                if (adjustedLength <= 0 || adjustedStart >= snapshot.Length)
                {
                    continue;
                }

                var blockSpan = new SnapshotSpan(snapshot, adjustedStart, adjustedLength);

                // Check if this block intersects any of the requested spans
                var intersects = false;
                foreach (SnapshotSpan span in spans)
                {
                    if (blockSpan.IntersectsWith(span))
                    {
                        intersects = true;
                        break;
                    }
                }

                if (!intersects)
                {
                    continue;
                }

                // Create the collapsed form text (first line preview)
                var collapsedForm = GetCollapsedFormText(snapshot, block);

                // Create outlining region tag with isDefaultCollapsed = true
                // Use lazy hint form to avoid string allocation until tooltip is shown
                var tag = new OutliningRegionTag(
                    isDefaultCollapsed: true,
                    isImplementation: false,
                    collapsedForm: collapsedForm,
                    collapsedHintForm: new LazyHintForm(blockSpan));

                yield return new TagSpan<IOutliningRegionTag>(blockSpan, tag);
            }
        }

        /// <summary>
        /// Gets the text to display when the region is collapsed.
        /// Shows the first line of the comment as a preview.
        /// </summary>
        private static string GetCollapsedFormText(ITextSnapshot snapshot, XmlDocCommentBlock block)
        {
            ITextSnapshotLine firstLine = snapshot.GetLineFromLineNumber(block.StartLine);
            var lineText = firstLine.GetText().Trim();

            // Truncate if too long
            const int maxLength = 60;
            if (lineText.Length > maxLength)
            {
                lineText = lineText.Substring(0, maxLength) + "...";
            }

            return lineText;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _buffer.Changed -= OnBufferChanged;
            SetRenderingModeHelper.RenderedCommentsStateChanged -= OnRenderedStateChanged;
            General.Saved -= OnSettingsSaved;
        }

        /// <summary>
        /// Lazy wrapper for collapsed hint form text.
        /// Defers string allocation until ToString() is called (when tooltip is shown).
        /// </summary>
        private sealed class LazyHintForm
        {
            private readonly SnapshotSpan _span;
            private string _text;

            public LazyHintForm(SnapshotSpan span) => _span = span;

            public override string ToString() => _text ??= _span.GetText();
        }
    }
}
