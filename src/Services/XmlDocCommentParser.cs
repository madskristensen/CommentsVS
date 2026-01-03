using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Text;

namespace CommentsVS.Services
{
    /// <summary>
    /// Represents a parsed XML documentation comment block.
    /// </summary>
    public sealed class XmlDocCommentBlock
    {
        /// <summary>
        /// Gets the span in the text buffer that contains this comment block.
        /// </summary>
        public Span Span { get; }

        /// <summary>
        /// Gets the starting line number (0-based).
        /// </summary>
        public int StartLine { get; }

        /// <summary>
        /// Gets the ending line number (0-based, inclusive).
        /// </summary>
        public int EndLine { get; }

        /// <summary>
        /// Gets the indentation (whitespace before the comment prefix) on the first line.
        /// </summary>
        public string Indentation { get; }

        /// <summary>
        /// Gets the raw XML content of the comment (without prefixes).
        /// </summary>
        public string XmlContent { get; }

        /// <summary>
        /// Gets the language comment style used for this block.
        /// </summary>
        public LanguageCommentStyle CommentStyle { get; }

        /// <summary>
        /// Gets whether this is a multi-line comment (/** */) vs single-line (///).
        /// </summary>
        public bool IsMultiLineStyle { get; }

        public XmlDocCommentBlock(
            Span span,
            int startLine,
            int endLine,
            string indentation,
            string xmlContent,
            LanguageCommentStyle commentStyle,
            bool isMultiLineStyle)
        {
            Span = span;
            StartLine = startLine;
            EndLine = endLine;
            Indentation = indentation;
            XmlContent = xmlContent;
            CommentStyle = commentStyle;
            IsMultiLineStyle = isMultiLineStyle;
        }
    }

    /// <summary>
    /// Parses XML documentation comments from text buffers.
    /// </summary>
    public sealed class XmlDocCommentParser
    {
        private readonly LanguageCommentStyle _commentStyle;

        public XmlDocCommentParser(LanguageCommentStyle commentStyle)
        {
            _commentStyle = commentStyle ?? throw new ArgumentNullException(nameof(commentStyle));
        }

        /// <summary>
        /// Finds all XML documentation comment blocks in the given text snapshot.
        /// </summary>
        /// <param name="snapshot">The text snapshot to search.</param>
        /// <returns>A collection of parsed comment blocks.</returns>
        public IReadOnlyList<XmlDocCommentBlock> FindAllCommentBlocks(ITextSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var blocks = new List<XmlDocCommentBlock>();
            int lineCount = snapshot.LineCount;
            int currentLine = 0;

            while (currentLine < lineCount)
            {
                var block = TryParseCommentBlockAt(snapshot, currentLine);
                if (block != null)
                {
                    blocks.Add(block);
                    currentLine = block.EndLine + 1;
                }
                else
                {
                    currentLine++;
                }
            }

            return blocks;
        }

        /// <summary>
        /// Finds the XML documentation comment block containing the given position, if any.
        /// </summary>
        /// <param name="snapshot">The text snapshot.</param>
        /// <param name="position">The position to check.</param>
        /// <returns>The comment block at the position, or null if none.</returns>
        public XmlDocCommentBlock FindCommentBlockAtPosition(ITextSnapshot snapshot, int position)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            if (position < 0 || position > snapshot.Length)
            {
                return null;
            }

            var line = snapshot.GetLineFromPosition(position);
            int lineNumber = line.LineNumber;

            // First, check if the current line is a doc comment line
            if (!IsDocCommentLine(line.GetText()))
            {
                return null;
            }

            // Search backwards to find the start of the comment block
            int startLine = lineNumber;
            while (startLine > 0)
            {
                var prevLine = snapshot.GetLineFromLineNumber(startLine - 1);
                if (!IsDocCommentLine(prevLine.GetText()))
                {
                    break;
                }
                startLine--;
            }

            return TryParseCommentBlockAt(snapshot, startLine);
        }

        /// <summary>
        /// Finds all comment blocks that intersect the given span.
        /// </summary>
        /// <param name="snapshot">The text snapshot.</param>
        /// <param name="span">The span to check for intersections.</param>
        /// <returns>Comment blocks that intersect the span.</returns>
        public IReadOnlyList<XmlDocCommentBlock> FindCommentBlocksInSpan(ITextSnapshot snapshot, Span span)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var blocks = new List<XmlDocCommentBlock>();

            int startLine = snapshot.GetLineFromPosition(span.Start).LineNumber;
            int endLine = snapshot.GetLineFromPosition(span.End).LineNumber;

            int currentLine = startLine;

            // Search backwards to find if startLine is in the middle of a comment block
            while (currentLine > 0)
            {
                var prevLine = snapshot.GetLineFromLineNumber(currentLine - 1);
                if (!IsDocCommentLine(prevLine.GetText()))
                {
                    break;
                }
                currentLine--;
            }

            // Now parse forward
            while (currentLine <= endLine)
            {
                var block = TryParseCommentBlockAt(snapshot, currentLine);
                if (block != null)
                {
                    blocks.Add(block);
                    currentLine = block.EndLine + 1;
                }
                else
                {
                    currentLine++;
                }
            }

            return blocks;
        }

        /// <summary>
        /// Attempts to parse a comment block starting at the given line.
        /// </summary>
        private XmlDocCommentBlock TryParseCommentBlockAt(ITextSnapshot snapshot, int startLine)
        {
            if (startLine < 0 || startLine >= snapshot.LineCount)
            {
                return null;
            }

            var firstLine = snapshot.GetLineFromLineNumber(startLine);
            string firstLineText = firstLine.GetText();

            // Try single-line doc comment style (///, ''')
            var singleLineBlock = TryParseSingleLineCommentBlock(snapshot, startLine, firstLineText);
            if (singleLineBlock != null)
            {
                return singleLineBlock;
            }

            // Try multi-line doc comment style (/** */)
            if (_commentStyle.SupportsMultiLineDoc)
            {
                var multiLineBlock = TryParseMultiLineCommentBlock(snapshot, startLine, firstLineText);
                if (multiLineBlock != null)
                {
                    return multiLineBlock;
                }
            }

            return null;
        }

        /// <summary>
        /// Parses a block of consecutive single-line doc comments (/// or ''').
        /// </summary>
        private XmlDocCommentBlock TryParseSingleLineCommentBlock(
            ITextSnapshot snapshot,
            int startLine,
            string firstLineText)
        {
            string prefix = _commentStyle.SingleLineDocPrefix;
            string trimmedFirst = firstLineText.TrimStart();

            if (!trimmedFirst.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            // Extract indentation
            string indentation = firstLineText.Substring(0, firstLineText.Length - trimmedFirst.Length);

            var xmlContentBuilder = new StringBuilder();
            int endLine = startLine;

            for (int i = startLine; i < snapshot.LineCount; i++)
            {
                var line = snapshot.GetLineFromLineNumber(i);
                string lineText = line.GetText();
                string trimmedLine = lineText.TrimStart();

                if (!trimmedLine.StartsWith(prefix, StringComparison.Ordinal))
                {
                    break;
                }

                // Extract content after the prefix
                string content = trimmedLine.Substring(prefix.Length);

                // Remove leading single space if present (standard formatting)
                if (content.Length > 0 && content[0] == ' ')
                {
                    content = content.Substring(1);
                }

                if (xmlContentBuilder.Length > 0)
                {
                    xmlContentBuilder.AppendLine();
                }

                xmlContentBuilder.Append(content.TrimEnd());
                endLine = i;
            }

            var firstSnapshotLine = snapshot.GetLineFromLineNumber(startLine);
            var lastSnapshotLine = snapshot.GetLineFromLineNumber(endLine);
            var span = new Span(firstSnapshotLine.Start.Position, lastSnapshotLine.End.Position - firstSnapshotLine.Start.Position);

            return new XmlDocCommentBlock(
                span,
                startLine,
                endLine,
                indentation,
                xmlContentBuilder.ToString(),
                _commentStyle,
                isMultiLineStyle: false);
        }

        /// <summary>
        /// Parses a multi-line doc comment block (/** */).
        /// </summary>
        private XmlDocCommentBlock TryParseMultiLineCommentBlock(
            ITextSnapshot snapshot,
            int startLine,
            string firstLineText)
        {
            string trimmedFirst = firstLineText.TrimStart();

            if (!trimmedFirst.StartsWith(_commentStyle.MultiLineDocStart, StringComparison.Ordinal))
            {
                return null;
            }

            string indentation = firstLineText.Substring(0, firstLineText.Length - trimmedFirst.Length);
            var xmlContentBuilder = new StringBuilder();
            int endLine = startLine;

            // Handle content on the opening line after /**
            string openingContent = trimmedFirst.Substring(_commentStyle.MultiLineDocStart.Length);

            // Check if single-line: /** content */
            int closeIndex = openingContent.IndexOf(_commentStyle.MultiLineDocEnd, StringComparison.Ordinal);
            if (closeIndex >= 0)
            {
                string content = openingContent.Substring(0, closeIndex).Trim();
                xmlContentBuilder.Append(content);

                var line = snapshot.GetLineFromLineNumber(startLine);
                var span = new Span(line.Start.Position, line.End.Position - line.Start.Position);

                return new XmlDocCommentBlock(
                    span,
                    startLine,
                    startLine,
                    indentation,
                    xmlContentBuilder.ToString(),
                    _commentStyle,
                    isMultiLineStyle: true);
            }

            // Multi-line: look for closing */
            if (!string.IsNullOrWhiteSpace(openingContent))
            {
                xmlContentBuilder.Append(openingContent.Trim());
            }

            for (int i = startLine + 1; i < snapshot.LineCount; i++)
            {
                var line = snapshot.GetLineFromLineNumber(i);
                string lineText = line.GetText();
                string trimmedLine = lineText.TrimStart();
                endLine = i;

                closeIndex = trimmedLine.IndexOf(_commentStyle.MultiLineDocEnd, StringComparison.Ordinal);
                if (closeIndex >= 0)
                {
                    // Found the end
                    string content = trimmedLine.Substring(0, closeIndex);
                    content = StripContinuationPrefix(content);

                    if (xmlContentBuilder.Length > 0 && !string.IsNullOrWhiteSpace(content))
                    {
                        xmlContentBuilder.AppendLine();
                    }

                    xmlContentBuilder.Append(content.Trim());
                    break;
                }

                // Middle line
                string middleContent = StripContinuationPrefix(trimmedLine).TrimEnd();

                if (xmlContentBuilder.Length > 0)
                {
                    xmlContentBuilder.AppendLine();
                }

                xmlContentBuilder.Append(middleContent);
            }

            var firstSnapshotLine = snapshot.GetLineFromLineNumber(startLine);
            var lastSnapshotLine = snapshot.GetLineFromLineNumber(endLine);
            var blockSpan = new Span(
                firstSnapshotLine.Start.Position,
                lastSnapshotLine.End.Position - firstSnapshotLine.Start.Position);

            return new XmlDocCommentBlock(
                blockSpan,
                startLine,
                endLine,
                indentation,
                xmlContentBuilder.ToString(),
                _commentStyle,
                isMultiLineStyle: true);
        }

        /// <summary>
        /// Removes the continuation prefix (e.g., " * ") from a line.
        /// </summary>
        private string StripContinuationPrefix(string line)
        {
            string continuation = _commentStyle.MultiLineContinuation;
            if (string.IsNullOrEmpty(continuation))
            {
                return line;
            }

            // Handle variations like "* ", " *", "*"
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("*", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(1);
                if (trimmed.Length > 0 && trimmed[0] == ' ')
                {
                    trimmed = trimmed.Substring(1);
                }
                return trimmed;
            }

            return line;
        }

        /// <summary>
        /// Checks if a line is a documentation comment line.
        /// </summary>
        private bool IsDocCommentLine(string lineText)
        {
            string trimmed = lineText.TrimStart();

            if (trimmed.StartsWith(_commentStyle.SingleLineDocPrefix, StringComparison.Ordinal))
            {
                return true;
            }

            if (_commentStyle.SupportsMultiLineDoc)
            {
                if (trimmed.StartsWith(_commentStyle.MultiLineDocStart, StringComparison.Ordinal) ||
                    trimmed.StartsWith("*", StringComparison.Ordinal) ||
                    trimmed.EndsWith(_commentStyle.MultiLineDocEnd, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
