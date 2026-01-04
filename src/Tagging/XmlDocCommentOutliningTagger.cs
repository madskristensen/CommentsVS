using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
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
    /// The regions are always present; the toggle command collapses/expands them.
    /// </summary>
    internal sealed class XmlDocCommentOutliningTagger : ITagger<IOutliningRegionTag>
    {
        private readonly ITextBuffer _buffer;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public XmlDocCommentOutliningTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            // Notify that tags may have changed when buffer content changes
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
            LanguageCommentStyle commentStyle = contentType.IsOfType("CSharp")
                ? LanguageCommentStyle.CSharp
                : LanguageCommentStyle.VisualBasic;

            var parser = new XmlDocCommentParser(commentStyle);
            IReadOnlyList<XmlDocCommentBlock> commentBlocks = parser.FindAllCommentBlocks(snapshot);

            // Check if we should auto-collapse on file open
            var collapseByDefault = General.Instance.CollapseCommentsOnFileOpen;

            foreach (XmlDocCommentBlock block in commentBlocks)
            {
                // Adjust span to start after indentation so the collapsed text
                // appears at the same column as the comment, not at column 0
                var adjustedStart = block.Span.Start + block.Indentation.Length;
                var adjustedSpan = new Span(adjustedStart, block.Span.End - adjustedStart);
                var blockSpan = new SnapshotSpan(snapshot, adjustedSpan);

                if (!spans.IntersectsWith(new NormalizedSnapshotSpanCollection(blockSpan)))
                {
                    continue;
                }

                // Get the collapsed text to display
                string collapsedText = GetCollapsedText(snapshot, block);

                var tag = new OutliningRegionTag(
                    collapsedForm: collapsedText,
                    collapsedHintForm: block.XmlContent,
                    isDefaultCollapsed: collapseByDefault,
                    isImplementation: false);

                yield return new TagSpan<IOutliningRegionTag>(blockSpan, tag);
            }
        }

        private static string GetCollapsedText(ITextSnapshot snapshot, XmlDocCommentBlock block)
        {
            const int maxLength = 85;

            // Start with the first line
            ITextSnapshotLine firstLine = snapshot.GetLineFromLineNumber(block.StartLine);
            string result = firstLine.GetText().TrimStart();

            // If first line is just "/// <summary>" and there are more lines, append content
            if (result.EndsWith("<summary>") && block.EndLine > block.StartLine)
            {
                // Get content from subsequent lines
                for (int i = block.StartLine + 1; i <= block.EndLine; i++)
                {
                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
                    string lineText = line.GetText().Trim();

                    // Strip comment prefix (///, ''', *)
                    if (lineText.StartsWith("///"))
                        lineText = lineText.Substring(3).Trim();
                    else if (lineText.StartsWith("'''"))
                        lineText = lineText.Substring(3).Trim();
                    else if (lineText.StartsWith("*"))
                        lineText = lineText.Substring(1).Trim();

                    // Stop if we hit the closing tag or another XML tag
                    if (lineText.StartsWith("</") || lineText.StartsWith("<"))
                        break;

                    if (!string.IsNullOrEmpty(lineText))
                    {
                        result += " " + lineText;
                    }

                    // Stop if we've collected enough
                    if (result.Length >= maxLength)
                        break;
                }
            }

            // Truncate and add ellipsis if too long
            if (result.Length > maxLength)
            {
                result = result.Substring(0, maxLength - 4) + " ...";
            }

            return result;
        }
    }
}
