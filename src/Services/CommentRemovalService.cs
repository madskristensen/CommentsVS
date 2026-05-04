using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CommentsVS.Options;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace CommentsVS.Services
{
    /// <summary>
    /// Service for removing comments from source code files.
    /// Provides shared logic for various comment removal commands.
    /// </summary>
    internal static class CommentRemovalService
    {
        /// <summary>
        /// Gets classification spans for the given classification name from the entire document.
        /// </summary>
        public static IEnumerable<IMappingSpan> GetClassificationSpans(IWpfTextView view, string classificationName)
        {
            if (view == null)
            {
                return [];
            }

            if (Package.GetGlobalService(typeof(SComponentModel)) is not IComponentModel componentModel)
            {
                return [];
            }

            var snapshot = new SnapshotSpan(view.TextBuffer.CurrentSnapshot, 0, view.TextBuffer.CurrentSnapshot.Length);

            // Use view tag aggregator for reliable classification retrieval
            IViewTagAggregatorFactoryService serviceViewTag = componentModel.GetService<IViewTagAggregatorFactoryService>();
            ITagAggregator<IClassificationTag> classifierViewTag = serviceViewTag.CreateTagAggregator<IClassificationTag>(view);

            return [.. classifierViewTag.GetTags(snapshot)
                .Where(s => s.Tag.ClassificationType.Classification.IndexOf(classificationName, StringComparison.OrdinalIgnoreCase) > -1)
                .Select(s => s.Span)
                .Reverse()];
        }

        /// <summary>
        /// Gets classification spans for the given classification name from the current selection.
        /// </summary>
        public static IEnumerable<IMappingSpan> GetSelectionClassificationSpans(IWpfTextView view, string classificationName)
        {
            if (view == null || view.Selection.IsEmpty)
            {
                return [];
            }

            if (Package.GetGlobalService(typeof(SComponentModel)) is not IComponentModel componentModel)
            {
                return [];
            }

            IBufferTagAggregatorFactoryService service = componentModel.GetService<IBufferTagAggregatorFactoryService>();
            ITagAggregator<IClassificationTag> classifier = service.CreateTagAggregator<IClassificationTag>(view.TextBuffer);

            List<IMappingSpan> mappingSpans = [.. classifier.GetTags(view.Selection.SelectedSpans)
                .Where(s => s.Tag.ClassificationType.Classification.IndexOf(classificationName, StringComparison.OrdinalIgnoreCase) > -1)
                .Select(s => s.Span)
                .Reverse()];

            if (mappingSpans.Count > 0)
            {
                return mappingSpans;
            }

            // Fallback to view tag aggregator
            IViewTagAggregatorFactoryService serviceViewTag = componentModel.GetService<IViewTagAggregatorFactoryService>();
            ITagAggregator<IClassificationTag> classifierViewTag = serviceViewTag.CreateTagAggregator<IClassificationTag>(view);

            return [.. classifierViewTag.GetTags(view.Selection.SelectedSpans)
                .Where(s => s.Tag.ClassificationType.Classification.IndexOf(classificationName, StringComparison.OrdinalIgnoreCase) > -1)
                .Select(s => s.Span)
                .Reverse()];
        }

        /// <summary>
        /// Determines if a line is effectively empty after comment removal.
        /// </summary>
        public static bool IsLineEmpty(ITextSnapshotLine line)
        {
            var text = line.GetText().Trim();

            return string.IsNullOrWhiteSpace(text)
                   || text == "<!--"
                   || text == "-->"
                   || text == "<%%>"
                   || text == "<%"
                   || text == "%>"
                   || Regex.IsMatch(text, @"^<!--(\s+)?-->$");
        }

        /// <summary>
        /// Determines if a line is an XML documentation comment.
        /// </summary>
        public static bool IsXmlDocComment(ITextSnapshotLine line)
        {
            var text = line.GetText().TrimStart();
            var contentType = line.Snapshot.TextBuffer.ContentType.TypeName;

            // C# and F# use ///
            if ((contentType.Contains("CSharp") || contentType.Contains("FSharp")) && text.StartsWith("///"))
            {
                return true;
            }

            // VB uses '''
            if (contentType.Contains("Basic") && text.StartsWith("'''"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines if a line contains an anchor comment (TODO, HACK, NOTE, BUG, FIXME, UNDONE, REVIEW, ANCHOR, or custom tags).
        /// Anchor comments must have the keyword immediately after the comment prefix (e.g., "// TODO:" not "// Note that...").
        /// </summary>
        public static bool ContainsAnchorComment(ITextSnapshotLine line)
        {
            var text = line.GetText();
            var customTags = General.Instance?.CustomTags ?? string.Empty;
            var keywords = CommentPatterns.BuiltInAnchorKeywordsPattern;

            if (!string.IsNullOrWhiteSpace(customTags))
            {
                var customTagList = customTags.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
                if (customTagList.Length > 0)
                {
                    keywords += "|" + string.Join("|", customTagList.Select(tag => Regex.Escape(tag.ToUpperInvariant())));
                }
            }

            // Match anchor keywords that appear immediately after a comment prefix.
            // Uppercase keywords may be bare; lowercase/mixed-case keywords require : or !.
            var anchorRegex = new Regex(
                $@"(//|/\*|\*|'|<!--)\s*(?:\b(?:{keywords})\b[:!]?|\b(?i:{keywords})\b[:!])");

            return anchorRegex.IsMatch(text);
        }

        /// <summary>
        /// Removes all comments from the buffer.
        /// </summary>
        public static void RemoveComments(
            IWpfTextView view,
            IEnumerable<IMappingSpan> mappingSpans,
            bool preserveXmlDoc = false,
            bool preserveAnchorComments = false)
        {
            List<IMappingSpan> spans = [.. mappingSpans];
            if (spans.Count == 0)
            {
                return;
            }

            ITextSnapshot snapshot = view.TextBuffer.CurrentSnapshot;

            // Collect line numbers to delete entirely
            HashSet<int> linesToDelete = [];

            // Collect spans to replace with whitespace
            Dictionary<int, (int Length, string Replacement)> spansToReplace = [];

            // Track which spans affect each line (to determine if line becomes empty)
            Dictionary<int, List<Span>> lineSpans = [];

            // First pass: collect all changes
            foreach (IMappingSpan mappingSpan in spans)
            {
                SnapshotPoint? startPoint = mappingSpan.Start.GetPoint(view.TextBuffer, PositionAffinity.Predecessor);
                SnapshotPoint? endPoint = mappingSpan.End.GetPoint(view.TextBuffer, PositionAffinity.Successor);

                if (!startPoint.HasValue || !endPoint.HasValue)
                {
                    continue;
                }

                var span = new Span(startPoint.Value, endPoint.Value - startPoint.Value);

                // Get only the lines that this specific span covers
                var startLineNumber = snapshot.GetLineNumberFromPosition(span.Start);
                var endLineNumber = snapshot.GetLineNumberFromPosition(span.End > 0 ? span.End - 1 : span.End);
                List<ITextSnapshotLine> lines = [];
                for (var i = startLineNumber; i <= endLineNumber; i++)
                {
                    lines.Add(snapshot.GetLineFromLineNumber(i));
                }

                // Check if this span should be preserved
                var shouldPreserve = lines.Any(line =>
                    (preserveXmlDoc && IsXmlDocComment(line)) ||
                    (preserveAnchorComments && ContainsAnchorComment(line)));

                if (shouldPreserve)
                {
                    continue;
                }

                foreach (ITextSnapshotLine line in lines)
                {
                    if (IsXmlDocComment(line))
                    {
                        // XML doc comments: delete entire line
                        linesToDelete.Add(line.LineNumber);
                    }
                    else
                    {
                        // Track span for this line
                        if (!lineSpans.ContainsKey(line.LineNumber))
                        {
                            lineSpans[line.LineNumber] = [];
                        }

                        lineSpans[line.LineNumber].Add(span);

                        if (!spansToReplace.ContainsKey(span.Start))
                        {
                            var mappingText = snapshot.GetText(span.Start, span.Length);
                            var empty = Regex.Replace(mappingText, @"[\S]+", string.Empty);
                            spansToReplace[span.Start] = (span.Length, empty);
                        }
                    }
                }
            }

            // Determine which lines will be empty after span replacements
            foreach (KeyValuePair<int, List<Span>> kvp in lineSpans)
            {
                if (linesToDelete.Contains(kvp.Key))
                {
                    continue;
                }

                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(kvp.Key);

                if (WillLineBeEmpty(line, kvp.Value))
                {
                    linesToDelete.Add(kvp.Key);
                }
            }

            // Single edit: apply all changes in reverse order
            using ITextEdit edit = view.TextBuffer.CreateEdit();

            // Delete lines (in reverse order)
            foreach (var lineNumber in linesToDelete.OrderByDescending(n => n))
            {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNumber);
                edit.Delete(line.Start, line.LengthIncludingLineBreak);
            }

            // Replace spans with whitespace (in reverse order), skipping lines being deleted
            foreach (var start in spansToReplace.Keys.OrderByDescending(s => s))
            {
                ITextSnapshotLine spanLine = snapshot.GetLineFromPosition(start);
                if (linesToDelete.Contains(spanLine.LineNumber))
                {
                    continue;
                }

                (var length, var replacement) = spansToReplace[start];
                edit.Replace(start, length, replacement);
            }

            edit.Apply();
        }

        /// <summary>
        /// Determines if a line will be empty after removing the specified spans.
        /// </summary>
        private static bool WillLineBeEmpty(ITextSnapshotLine line, List<Span> spansToRemove)
        {
            var lineText = line.GetText();
            var lineStart = line.Start.Position;

            // Build string of characters that will remain (not covered by any span)
            var remaining = new System.Text.StringBuilder();

            for (var i = 0; i < lineText.Length; i++)
            {
                var absolutePos = lineStart + i;
                var coveredBySpan = spansToRemove.Any(s => absolutePos >= s.Start && absolutePos < s.Start + s.Length);

                if (!coveredBySpan)
                {
                    remaining.Append(lineText[i]);
                }
            }

            var remainingText = remaining.ToString().Trim();

            return string.IsNullOrWhiteSpace(remainingText)
                   || remainingText == "<!--"
                   || remainingText == "-->"
                   || remainingText == "<%%>"
                   || remainingText == "<%"
                   || remainingText == "%>"
                   || Regex.IsMatch(remainingText, @"^<!--(\s+)?-->$");
        }

        /// <summary>
        /// Removes only XML documentation comments from the buffer.
        /// </summary>
        public static void RemoveXmlDocComments(IWpfTextView view, IEnumerable<IMappingSpan> mappingSpans)
        {
            List<IMappingSpan> spans = [.. mappingSpans];
            if (spans.Count == 0)
            {
                return;
            }

            List<int> affectedLines = [];

            foreach (IMappingSpan mappingSpan in spans)
            {
                SnapshotPoint? startPoint = mappingSpan.Start.GetPoint(view.TextBuffer, PositionAffinity.Predecessor);
                SnapshotPoint? endPoint = mappingSpan.End.GetPoint(view.TextBuffer, PositionAffinity.Successor);

                if (!startPoint.HasValue || !endPoint.HasValue)
                {
                    continue;
                }

                var span = new Span(startPoint.Value, endPoint.Value - startPoint.Value);
                ITextSnapshotLine line = view.TextBuffer.CurrentSnapshot.Lines.FirstOrDefault(l => l.Extent.IntersectsWith(span));

                if (line != null && !affectedLines.Contains(line.LineNumber))
                {
                    affectedLines.Add(line.LineNumber);
                }
            }

            using (ITextEdit edit = view.TextBuffer.CreateEdit())
            {
                foreach (var lineNumber in affectedLines.OrderByDescending(n => n))
                {
                    ITextSnapshotLine line = view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber);
                    edit.Delete(line.Start, line.LengthIncludingLineBreak);
                }

                edit.Apply();
            }
        }

        /// <summary>
        /// Removes only anchor comments (TODO, HACK, NOTE, BUG, FIXME, UNDONE, REVIEW, ANCHOR, or custom tags) from the buffer.
        /// Deletes entire comment lines that contain anchor keywords.
        /// </summary>
        public static void RemoveAnchorComments(IWpfTextView view, IEnumerable<IMappingSpan> mappingSpans)
        {
            List<IMappingSpan> spans = [.. mappingSpans];
            HashSet<int> affectedLines = [];

            // First pass: identify all lines containing anchor comments
            foreach (IMappingSpan mappingSpan in spans)
            {
                SnapshotPoint? startPoint = mappingSpan.Start.GetPoint(view.TextBuffer, PositionAffinity.Predecessor);
                SnapshotPoint? endPoint = mappingSpan.End.GetPoint(view.TextBuffer, PositionAffinity.Successor);

                if (!startPoint.HasValue || !endPoint.HasValue)
                {
                    continue;
                }

                var span = new Span(startPoint.Value, endPoint.Value - startPoint.Value);
                IEnumerable<ITextSnapshotLine> lines = view.TextBuffer.CurrentSnapshot.Lines.Where(l => l.Extent.IntersectsWith(span));

                foreach (ITextSnapshotLine line in lines)
                {
                    if (ContainsAnchorComment(line))
                    {
                        affectedLines.Add(line.LineNumber);
                    }
                }
            }

            if (affectedLines.Count == 0)
            {
                return;
            }

            // Second pass: delete entire lines that contain task comments
            using ITextEdit edit = view.TextBuffer.CreateEdit();

            foreach (var lineNumber in affectedLines.OrderByDescending(n => n))
            {
                if (lineNumber >= view.TextBuffer.CurrentSnapshot.LineCount)
                {
                    continue;
                }

                ITextSnapshotLine line = view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber);
                edit.Delete(line.Start, line.LengthIncludingLineBreak);
            }

            edit.Apply();
        }

        /// <summary>
        /// Removes #region and #endregion directives from the buffer.
        /// </summary>
        public static void RemoveRegions(IWpfTextView view)
        {
            using ITextEdit edit = view.TextBuffer.CreateEdit();

            foreach (ITextSnapshotLine line in view.TextBuffer.CurrentSnapshot.Lines.Reverse())
            {
                if (line.Extent.IsEmpty)
                {
                    continue;
                }

                var text = line.GetText()
                    .TrimStart('/', '*')
                    .Replace("<!--", string.Empty)
                    .TrimStart()
                    .ToLowerInvariant();

                if (text.StartsWith("#region") || text.StartsWith("#endregion") || text.StartsWith("#end region"))
                {
                    // Strip next line if empty
                    if (view.TextBuffer.CurrentSnapshot.LineCount > line.LineNumber + 1)
                    {
                        ITextSnapshotLine next = view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(line.LineNumber + 1);

                        if (IsLineEmpty(next))
                        {
                            edit.Delete(next.Start, next.LengthIncludingLineBreak);
                        }
                    }

                    edit.Delete(line.Start, line.LengthIncludingLineBreak);
                }
            }

            edit.Apply();
        }
    }
}
