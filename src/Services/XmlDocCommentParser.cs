using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.Text;

namespace CommentsVS.Services
{
    /// <summary>
    /// Represents a parsed XML documentation comment block.
    /// </summary>
    public sealed class XmlDocCommentBlock(
        Span span,
        int startLine,
        int endLine,
        string indentation,
        string xmlContent,
        LanguageCommentStyle commentStyle,
        bool isMultiLineStyle)
    {
        /// <summary>
        /// Gets the span in the text buffer that contains this comment block.
        /// </summary>
        public Span Span { get; } = span;

        /// <summary>
        /// Gets the starting line number (0-based).
        /// </summary>
        public int StartLine { get; } = startLine;

        /// <summary>
        /// Gets the ending line number (0-based, inclusive).
        /// </summary>
        public int EndLine { get; } = endLine;

        /// <summary>
        /// Gets the indentation (whitespace before the comment prefix) on the first line.
        /// </summary>
        public string Indentation { get; } = indentation;

        /// <summary>
        /// Gets the raw XML content of the comment (without prefixes).
        /// </summary>
        public string XmlContent { get; } = xmlContent;

        /// <summary>
        /// Gets the language comment style used for this block.
        /// </summary>
        public LanguageCommentStyle CommentStyle { get; } = commentStyle;

        /// <summary>
        /// Gets whether this is a multi-line comment (/** */) vs single-line (///).
        /// </summary>
        public bool IsMultiLineStyle { get; } = isMultiLineStyle;
    }

    /// <summary>
    /// Parses XML documentation comments from text buffers.
    /// </summary>
    public sealed class XmlDocCommentParser(LanguageCommentStyle commentStyle)
    {
        private readonly LanguageCommentStyle _commentStyle = commentStyle ?? throw new ArgumentNullException(nameof(commentStyle));

        /// <summary>
        /// Gets cached comment blocks for a text buffer, parsing only if the cache is stale.
        /// This is the preferred method for taggers and other components that need comment blocks.
        /// Returns null if the file is too large to process.
        /// </summary>
        /// <param name="buffer">The text buffer.</param>
        /// <returns>Cached comment blocks, or null if file is too large or unsupported.</returns>
        public static IReadOnlyList<XmlDocCommentBlock> GetCachedCommentBlocks(ITextBuffer buffer)
        {
            if (buffer == null)
            {
                return null;
            }

            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);
            if (commentStyle == null)
            {
                return null;
            }

            var cache = XmlDocCommentCache.GetOrCreate(buffer);
            return cache?.GetCommentBlocks(snapshot, commentStyle);
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
            var lineCount = snapshot.LineCount;
            var currentLine = 0;

            while (currentLine < lineCount)
            {
                XmlDocCommentBlock block = TryParseCommentBlockAt(snapshot, currentLine);
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

            ITextSnapshotLine line = snapshot.GetLineFromPosition(position);
            var lineNumber = line.LineNumber;

            // First, check if the current line is a doc comment line
            if (!IsDocCommentLine(line.GetText()))
            {
                return null;
            }

            // Search backwards to find the start of the comment block
            var startLine = lineNumber;
            while (startLine > 0)
            {
                ITextSnapshotLine prevLine = snapshot.GetLineFromLineNumber(startLine - 1);
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

            var startLine = snapshot.GetLineFromPosition(span.Start).LineNumber;
            var endLine = snapshot.GetLineFromPosition(span.End).LineNumber;

            var currentLine = startLine;

            // Search backwards to find if startLine is in the middle of a comment block
            while (currentLine > 0)
            {
                ITextSnapshotLine prevLine = snapshot.GetLineFromLineNumber(currentLine - 1);
                if (!IsDocCommentLine(prevLine.GetText()))
                {
                    break;
                }
                currentLine--;
            }

            // Now parse forward
            while (currentLine <= endLine)
            {
                XmlDocCommentBlock block = TryParseCommentBlockAt(snapshot, currentLine);
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

            ITextSnapshotLine firstLine = snapshot.GetLineFromLineNumber(startLine);
            var firstLineText = firstLine.GetText();

            // Try single-line doc comment style (///, ''')
            XmlDocCommentBlock singleLineBlock = TryParseSingleLineCommentBlock(snapshot, startLine, firstLineText);
            if (singleLineBlock != null)
            {
                return singleLineBlock;
            }

            // Try multi-line doc comment style (/** */)
            if (_commentStyle.SupportsMultiLineDoc)
            {
                XmlDocCommentBlock multiLineBlock = TryParseMultiLineCommentBlock(snapshot, startLine, firstLineText);
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
            var prefix = _commentStyle.SingleLineDocPrefix;
            var trimmedFirst = firstLineText.TrimStart();

            if (!trimmedFirst.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            // Extract indentation
            var indentation = firstLineText.Substring(0, firstLineText.Length - trimmedFirst.Length);

            var xmlContentBuilder = new StringBuilder();
            var endLine = startLine;

            for (var i = startLine; i < snapshot.LineCount; i++)
            {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
                var lineText = line.GetText();
                var trimmedLine = lineText.TrimStart();

                if (!trimmedLine.StartsWith(prefix, StringComparison.Ordinal))
                {
                    break;
                }

                // Extract content after the prefix
                var content = trimmedLine.Substring(prefix.Length);

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

            ITextSnapshotLine firstSnapshotLine = snapshot.GetLineFromLineNumber(startLine);
            ITextSnapshotLine lastSnapshotLine = snapshot.GetLineFromLineNumber(endLine);

            // Use End position (not EndIncludingLineBreak) to avoid affecting the next line
            var spanEnd = lastSnapshotLine.End.Position;
            var span = new Span(firstSnapshotLine.Start.Position, spanEnd - firstSnapshotLine.Start.Position);

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
            var trimmedFirst = firstLineText.TrimStart();

            if (!trimmedFirst.StartsWith(_commentStyle.MultiLineDocStart, StringComparison.Ordinal))
            {
                return null;
            }

            var indentation = firstLineText.Substring(0, firstLineText.Length - trimmedFirst.Length);
            var xmlContentBuilder = new StringBuilder();
            var endLine = startLine;

            // Handle content on the opening line after /**
            var openingContent = trimmedFirst.Substring(_commentStyle.MultiLineDocStart.Length);

            // Check if single-line: /** content */
            var closeIndex = openingContent.IndexOf(_commentStyle.MultiLineDocEnd, StringComparison.Ordinal);
            if (closeIndex >= 0)
            {
                var content = openingContent.Substring(0, closeIndex).Trim();
                xmlContentBuilder.Append(content);

                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(startLine);
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

            for (var i = startLine + 1; i < snapshot.LineCount; i++)
            {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
                var lineText = line.GetText();
                var trimmedLine = lineText.TrimStart();
                endLine = i;

                closeIndex = trimmedLine.IndexOf(_commentStyle.MultiLineDocEnd, StringComparison.Ordinal);
                if (closeIndex >= 0)
                {
                    // Found the end
                    var content = trimmedLine.Substring(0, closeIndex);
                    content = StripContinuationPrefix(content);

                    if (xmlContentBuilder.Length > 0 && !string.IsNullOrWhiteSpace(content))
                    {
                        xmlContentBuilder.AppendLine();
                    }

                    xmlContentBuilder.Append(content.Trim());
                    break;
                }

                // Middle line
                var middleContent = StripContinuationPrefix(trimmedLine).TrimEnd();

                if (xmlContentBuilder.Length > 0)
                {
                    xmlContentBuilder.AppendLine();
                }

                xmlContentBuilder.Append(middleContent);
            }

            ITextSnapshotLine firstSnapshotLine = snapshot.GetLineFromLineNumber(startLine);
            ITextSnapshotLine lastSnapshotLine = snapshot.GetLineFromLineNumber(endLine);
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
            var continuation = _commentStyle.MultiLineContinuation;
            if (string.IsNullOrEmpty(continuation))
            {
                return line;
            }

            // Handle variations like "* ", " *", "*"
            var trimmed = line.TrimStart();
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
            var trimmed = lineText.TrimStart();

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

        #region String-based parsing (VS-independent, for benchmarking)

        /// <summary>
        /// Finds all XML documentation comment blocks in the given lines.
        /// This overload is VS-independent and can be used for benchmarking.
        /// </summary>
        /// <param name="lines">The lines of text to parse.</param>
        /// <returns>A collection of parsed comment blocks.</returns>
        public IReadOnlyList<XmlDocCommentBlock> FindAllCommentBlocks(IReadOnlyList<string> lines)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));

            var blocks = new List<XmlDocCommentBlock>();
            var currentLine = 0;

            while (currentLine < lines.Count)
            {
                XmlDocCommentBlock block = TryParseCommentBlockAt(lines, currentLine);
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
        /// Attempts to parse a comment block starting at the given line (string-based).
        /// </summary>
        private XmlDocCommentBlock TryParseCommentBlockAt(IReadOnlyList<string> lines, int startLine)
        {
            if (startLine < 0 || startLine >= lines.Count)
            {
                return null;
            }

            var firstLineText = lines[startLine];

            // Try single-line doc comment style (///, ''')
            XmlDocCommentBlock singleLineBlock = TryParseSingleLineCommentBlock(lines, startLine, firstLineText);
            if (singleLineBlock != null)
            {
                return singleLineBlock;
            }

            // Try multi-line doc comment style (/** */)
            if (_commentStyle.SupportsMultiLineDoc)
            {
                XmlDocCommentBlock multiLineBlock = TryParseMultiLineCommentBlock(lines, startLine, firstLineText);
                if (multiLineBlock != null)
                {
                    return multiLineBlock;
                }
            }

            return null;
        }

        /// <summary>
        /// Parses a block of consecutive single-line doc comments (string-based).
        /// </summary>
        private XmlDocCommentBlock TryParseSingleLineCommentBlock(
            IReadOnlyList<string> lines,
            int startLine,
            string firstLineText)
        {
            var prefix = _commentStyle.SingleLineDocPrefix;

            // Fast path: check if line starts with prefix without allocating trimmed string
            var leadingWhitespace = GetLeadingWhitespaceLength(firstLineText);
            if (!StartsWithAtOffset(firstLineText, leadingWhitespace, prefix))
            {
                return null;
            }

            var indentation = firstLineText.Substring(0, leadingWhitespace);
            var xmlContentBuilder = new StringBuilder();
            var endLine = startLine;
            var spanLength = 0;

            for (var i = startLine; i < lines.Count; i++)
            {
                var lineText = lines[i];
                var lineLeadingWs = GetLeadingWhitespaceLength(lineText);

                if (!StartsWithAtOffset(lineText, lineLeadingWs, prefix))
                {
                    break;
                }

                // Calculate content start (after prefix and optional single space)
                var contentStart = lineLeadingWs + prefix.Length;
                if (contentStart < lineText.Length && lineText[contentStart] == ' ')
                {
                    contentStart++;
                }

                // Calculate content end (before trailing whitespace)
                var contentEnd = lineText.Length;
                while (contentEnd > contentStart && char.IsWhiteSpace(lineText[contentEnd - 1]))
                {
                    contentEnd--;
                }

                if (xmlContentBuilder.Length > 0)
                {
                    xmlContentBuilder.AppendLine();
                }

                if (contentEnd > contentStart)
                {
                    xmlContentBuilder.Append(lineText, contentStart, contentEnd - contentStart);
                }

                spanLength += lineText.Length;
                if (i > startLine)
                {
                    spanLength += Environment.NewLine.Length;
                }

                endLine = i;
            }

            // Calculate span start position only once at the end
            var spanStart = 0;
            var newLineLen = Environment.NewLine.Length;
            for (var i = 0; i < startLine; i++)
            {
                spanStart += lines[i].Length + newLineLen;
            }

            var span = new Span(spanStart, spanLength);

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
        /// Gets the number of leading whitespace characters in the string.
        /// </summary>
        private static int GetLeadingWhitespaceLength(string s)
        {
            var i = 0;
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }

            return i;
        }

        /// <summary>
        /// Checks if the string contains the specified value starting at the given offset.
        /// </summary>
        private static bool StartsWithAtOffset(string s, int offset, string value)
        {
            if (offset + value.Length > s.Length)
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                if (s[offset + i] != value[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Parses a multi-line doc comment block (string-based).
        /// </summary>
        private XmlDocCommentBlock TryParseMultiLineCommentBlock(
            IReadOnlyList<string> lines,
            int startLine,
            string firstLineText)
        {
            var trimmedFirst = firstLineText.TrimStart();

            if (!trimmedFirst.StartsWith(_commentStyle.MultiLineDocStart, StringComparison.Ordinal))
            {
                return null;
            }

            var indentation = firstLineText.Substring(0, firstLineText.Length - trimmedFirst.Length);
            var xmlContentBuilder = new StringBuilder();
            var endLine = startLine;
            var spanStart = 0;

            // Calculate span start
            for (var i = 0; i < startLine; i++)
            {
                spanStart += lines[i].Length + Environment.NewLine.Length;
            }

            var openingContent = trimmedFirst.Substring(_commentStyle.MultiLineDocStart.Length);
            var closeIndex = openingContent.IndexOf(_commentStyle.MultiLineDocEnd, StringComparison.Ordinal);

            if (closeIndex >= 0)
            {
                // Single-line: /** content */
                var content = openingContent.Substring(0, closeIndex).Trim();
                xmlContentBuilder.Append(content);

                var span = new Span(spanStart, lines[startLine].Length);
                return new XmlDocCommentBlock(
                    span,
                    startLine,
                    startLine,
                    indentation,
                    xmlContentBuilder.ToString(),
                    _commentStyle,
                    isMultiLineStyle: true);
            }

            if (!string.IsNullOrWhiteSpace(openingContent))
            {
                xmlContentBuilder.Append(openingContent.Trim());
            }

            for (var i = startLine + 1; i < lines.Count; i++)
            {
                var lineText = lines[i];
                var trimmedLine = lineText.TrimStart();
                endLine = i;

                closeIndex = trimmedLine.IndexOf(_commentStyle.MultiLineDocEnd, StringComparison.Ordinal);
                if (closeIndex >= 0)
                {
                    var content = trimmedLine.Substring(0, closeIndex);
                    content = StripContinuationPrefix(content);

                    if (xmlContentBuilder.Length > 0 && !string.IsNullOrWhiteSpace(content))
                    {
                        xmlContentBuilder.AppendLine();
                    }

                    xmlContentBuilder.Append(content.Trim());
                    break;
                }

                var middleContent = StripContinuationPrefix(trimmedLine).TrimEnd();

                if (xmlContentBuilder.Length > 0)
                {
                    xmlContentBuilder.AppendLine();
                }

                xmlContentBuilder.Append(middleContent);
            }

            // Calculate span end
            var spanEnd = spanStart;
            for (var i = startLine; i <= endLine; i++)
            {
                spanEnd += lines[i].Length;
                if (i < endLine)
                {
                    spanEnd += Environment.NewLine.Length;
                }
            }

            return new XmlDocCommentBlock(
                new Span(spanStart, spanEnd - spanStart),
                startLine,
                endLine,
                indentation,
                xmlContentBuilder.ToString(),
                _commentStyle,
                isMultiLineStyle: true);
        }

        #endregion
    }
}
