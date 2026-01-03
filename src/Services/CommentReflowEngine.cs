using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CommentsVS.Services
{
    /// <summary>
    /// Represents a parsed XML element from a doc comment.
    /// </summary>
    internal sealed class XmlElement
    {
        public string TagName { get; set; }
        public string OpenTag { get; set; }
        public string Content { get; set; }
        public string CloseTag { get; set; }
    }

    /// <summary>
    /// Reflows XML documentation comments to fit within a specified line length.
    /// </summary>
    public sealed class CommentReflowEngine
    {
        private readonly int _maxLineLength;
        private readonly bool _useCompactStyle;
        private readonly bool _preserveBlankLines;

        // XML tags that should typically stay on their own line or preserve formatting
        private static readonly HashSet<string> BlockTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "summary", "remarks", "returns", "value", "example", "exception",
            "param", "typeparam", "seealso", "permission", "include"
        };

        // Tags whose content should not be reflowed (preformatted)
        private static readonly HashSet<string> PreformattedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "code"
        };

        // Regex to parse XML elements
        private static readonly Regex ElementRegex = new Regex(
            @"<(\w+)([^>]*)>(.*?)</\1>|<(\w+)([^/>]*)/?>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        public CommentReflowEngine(int maxLineLength, bool useCompactStyle, bool preserveBlankLines)
        {
            _maxLineLength = maxLineLength > 0 ? maxLineLength : 120;
            _useCompactStyle = useCompactStyle;
            _preserveBlankLines = preserveBlankLines;
        }

        /// <summary>
        /// Reflows a comment block and returns the new text to replace the original.
        /// </summary>
        public string ReflowComment(XmlDocCommentBlock block)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));

            string xmlContent = block.XmlContent;
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return null;
            }

            // Parse XML elements using regex
            var elements = ParseXmlElements(xmlContent);
            if (elements.Count == 0)
            {
                return null;
            }

            var reflowedLines = new List<string>();
            string prefix = block.IsMultiLineStyle
                ? block.CommentStyle.MultiLineContinuation?.TrimEnd() ?? " *"
                : block.CommentStyle.SingleLineDocPrefix;

            string linePrefix = block.Indentation + prefix + " ";
            int availableWidth = _maxLineLength - linePrefix.Length;

            if (availableWidth < 20)
            {
                availableWidth = 20;
            }

            foreach (var element in elements)
            {
                ReflowElement(element, reflowedLines, linePrefix, availableWidth);
            }

            return BuildFinalComment(block, reflowedLines);
        }

        /// <summary>
        /// Parses XML elements from content using regex.
        /// </summary>
        private List<XmlElement> ParseXmlElements(string content)
        {
            var elements = new List<XmlElement>();
            var matches = ElementRegex.Matches(content);

            foreach (Match match in matches)
            {
                if (match.Groups[1].Success)
                {
                    // Full element: <tag attrs>content</tag>
                    elements.Add(new XmlElement
                    {
                        TagName = match.Groups[1].Value,
                        OpenTag = $"<{match.Groups[1].Value}{match.Groups[2].Value}>",
                        Content = match.Groups[3].Value,
                        CloseTag = $"</{match.Groups[1].Value}>"
                    });
                }
                else if (match.Groups[4].Success)
                {
                    // Self-closing or empty: <tag attrs/>
                    elements.Add(new XmlElement
                    {
                        TagName = match.Groups[4].Value,
                        OpenTag = match.Value,
                        Content = "",
                        CloseTag = ""
                    });
                }
            }

            return elements;
        }

        /// <summary>
        /// Reflows a single XML element.
        /// </summary>
        private void ReflowElement(XmlElement element, List<string> lines, string linePrefix, int availableWidth)
        {
            bool isBlockTag = BlockTags.Contains(element.TagName);
            bool isPreformatted = PreformattedTags.Contains(element.TagName);

            // Self-closing tags
            if (string.IsNullOrEmpty(element.CloseTag))
            {
                lines.Add(linePrefix + element.OpenTag);
                return;
            }

            if (isPreformatted)
            {
                AddPreformattedElement(element, lines, linePrefix);
                return;
            }

            string innerContent = NormalizeWhitespace(element.Content);

            // Check if content fits on a single line (compact style)
            if (_useCompactStyle && isBlockTag)
            {
                string singleLine = $"{element.OpenTag}{innerContent}{element.CloseTag}";
                if (singleLine.Length <= availableWidth && 
                    !innerContent.Contains("\n") && 
                    !ContainsBlankLineSeparator(innerContent))
                {
                    lines.Add(linePrefix + singleLine);
                    return;
                }
            }

            // Multi-line format
            lines.Add(linePrefix + element.OpenTag);

            var paragraphs = SplitIntoParagraphs(innerContent);

            foreach (var paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    if (_preserveBlankLines)
                    {
                        lines.Add(linePrefix.TrimEnd());
                    }
                    continue;
                }

                var wrappedLines = WrapText(paragraph.Trim(), availableWidth);
                foreach (var wrappedLine in wrappedLines)
                {
                    lines.Add(linePrefix + wrappedLine);
                }
            }

            lines.Add(linePrefix + element.CloseTag);
        }

        /// <summary>
        /// Adds a preformatted element (like code) without reflowing.
        /// </summary>
        private void AddPreformattedElement(XmlElement element, List<string> lines, string linePrefix)
        {
            lines.Add(linePrefix + element.OpenTag);

            var contentLines = element.Content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in contentLines)
            {
                lines.Add(linePrefix + line);
            }

            lines.Add(linePrefix + element.CloseTag);
        }

        /// <summary>
        /// Normalizes whitespace in content.
        /// </summary>
        private string NormalizeWhitespace(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return content;
            }

            content = content.Replace("\r\n", "\n").Replace("\r", "\n");

            var contentLines = content.Split('\n');
            var normalizedLines = new List<string>();

            foreach (var line in contentLines)
            {
                string normalized = Regex.Replace(line, @"[ \t]+", " ").Trim();
                normalizedLines.Add(normalized);
            }

            return string.Join("\n", normalizedLines);
        }

        private static bool ContainsBlankLineSeparator(string content)
        {
            return content.Contains("\n\n") || Regex.IsMatch(content, @"\n\s*\n");
        }

        private List<string> SplitIntoParagraphs(string content)
        {
            var paragraphs = new List<string>();

            if (string.IsNullOrEmpty(content))
            {
                return paragraphs;
            }

            var parts = Regex.Split(content, @"\n\s*\n");

            foreach (var part in parts)
            {
                var partLines = part.Split('\n');
                var nonBlankLines = partLines
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .ToList();

                if (nonBlankLines.Count > 0)
                {
                    paragraphs.Add(string.Join(" ", nonBlankLines));
                }
                else if (_preserveBlankLines && paragraphs.Count > 0)
                {
                    paragraphs.Add("");
                }
            }

            return paragraphs;
        }

        /// <summary>
        /// Wraps text to fit within the specified width.
        /// </summary>
        private List<string> WrapText(string text, int maxWidth)
        {
            var lines = new List<string>();

            if (string.IsNullOrWhiteSpace(text))
            {
                return lines;
            }

            var tokens = TokenizeWithXmlTags(text);
            var currentLine = new StringBuilder();
            int currentLength = 0;

            foreach (var token in tokens)
            {
                int tokenLength = token.Length;

                if (currentLength == 0)
                {
                    currentLine.Append(token);
                    currentLength = tokenLength;
                }
                else if (currentLength + 1 + tokenLength <= maxWidth)
                {
                    currentLine.Append(' ');
                    currentLine.Append(token);
                    currentLength += 1 + tokenLength;
                }
                else
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                    currentLine.Append(token);
                    currentLength = tokenLength;
                }
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
            }

            return lines;
        }

        private static List<string> TokenizeWithXmlTags(string text)
        {
            var tokens = new List<string>();
            var regex = new Regex(@"(<[^>]+>)|(\S+)");
            var matches = regex.Matches(text);

            foreach (Match match in matches)
            {
                tokens.Add(match.Value);
            }

            return tokens;
        }

        private string BuildFinalComment(XmlDocCommentBlock block, List<string> lines)
        {
            if (lines.Count == 0)
            {
                return null;
            }

            var sb = new StringBuilder();

            if (block.IsMultiLineStyle)
            {
                sb.Append(block.Indentation);
                sb.AppendLine(block.CommentStyle.MultiLineDocStart);

                foreach (var line in lines)
                {
                    sb.AppendLine(line);
                }


                sb.Append(block.Indentation);
                sb.Append(" ");
                sb.Append(block.CommentStyle.MultiLineDocEnd);
            }
            else
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    sb.Append(lines[i]);
                    if (i < lines.Count - 1)
                    {
                        sb.AppendLine();
                    }
                }
            }

            return sb.ToString();
        }
    }
}
