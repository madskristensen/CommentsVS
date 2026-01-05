using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace CommentsVS.Services
{
    /// <summary>
    /// Represents a segment of rendered comment content.
    /// </summary>
    public class RenderedSegment(string text, RenderedSegmentType type = RenderedSegmentType.Text, string linkTarget = null)
    {
        public string Text { get; } = text;
        public RenderedSegmentType Type { get; } = type;
        public string LinkTarget { get; } = linkTarget;
    }

    public enum RenderedSegmentType
    {
        Text,
        Bold,
        Italic,
        Code,
        Link,
        ParamRef,
        TypeParamRef,
        Heading
    }

    /// <summary>
    /// Represents a rendered line of XML documentation.
    /// </summary>
    public class RenderedLine
    {
        public List<RenderedSegment> Segments { get; } = [];
        public bool IsBlank => Segments.Count == 0 || (Segments.Count == 1 && string.IsNullOrWhiteSpace(Segments[0].Text));
    }

    /// <summary>
    /// Represents a type of XML doc comment section.
    /// </summary>
    public enum CommentSectionType
    {
        Summary,
        TypeParam,
        Param,
        Returns,
        Value,
        Remarks,
        Example,
        Exception,
        SeeAlso,
        Other
    }


    /// <summary>
    /// Represents a distinct section of an XML doc comment (e.g., summary, param, returns).
    /// </summary>
    public class RenderedCommentSection(CommentSectionType type, string heading = null, string name = null)
    {
        public CommentSectionType Type { get; } = type;
        public string Heading { get; } = heading;
        public List<RenderedLine> Lines { get; } = [];

        /// <summary>
        /// For param/typeparam/exception sections, this is the name or type.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Index in Lines where list/complex content starts (-1 if none).
        /// Used to get prose-only content for collapsed summary view.
        /// </summary>
        public int ListContentStartIndex { get; set; } = -1;

        public bool IsEmpty => Lines.Count == 0 || Lines.All(l => l.IsBlank);

        /// <summary>
        /// Gets only the prose lines (before any list content) for collapsed view.
        /// </summary>
        public IEnumerable<RenderedLine> ProseLines
        {
            get
            {
                if (ListContentStartIndex < 0)
                {
                    return Lines;
                }
                return Lines.Take(ListContentStartIndex).Where(l => !l.IsBlank);
            }
        }
    }

    /// <summary>
    /// Represents a fully rendered XML documentation comment block.
    /// </summary>
    public class RenderedComment
    {
        public List<RenderedLine> Lines { get; } = [];
        public string Indentation { get; set; } = "";

        /// <summary>
        /// Gets the individual sections of the comment (summary, params, returns, etc.).
        /// </summary>
        public List<RenderedCommentSection> Sections { get; } = [];

        /// <summary>
        /// Gets the summary section, if present.
        /// </summary>
        public RenderedCommentSection Summary => Sections.FirstOrDefault(s => s.Type == CommentSectionType.Summary);

        /// <summary>
        /// Gets whether there are additional sections beyond the summary.
        /// </summary>
        public bool HasAdditionalSections => Sections.Any(s => s.Type != CommentSectionType.Summary && !s.IsEmpty);

        /// <summary>
        /// Gets all non-summary sections.
        /// </summary>
        public IEnumerable<RenderedCommentSection> AdditionalSections =>
            Sections.Where(s => s.Type != CommentSectionType.Summary && !s.IsEmpty);
    }

    /// <summary>
    /// Service to parse and render XML documentation comments into formatted text.
    /// </summary>
    public static class XmlDocCommentRenderer
    {
        private static readonly Regex _xmlTagRegex = new(
            @"<(?<closing>/)?(?<tag>\w+)(?<attrs>[^>]*)(?<selfclose>/)?>",
            RegexOptions.Compiled);

        /// <summary>
        /// Renders an XML documentation comment block into formatted segments.
        /// </summary>
        public static RenderedComment Render(XmlDocCommentBlock block)
        {
            var result = new RenderedComment { Indentation = block.Indentation };
            var xmlContent = block.XmlContent;

            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return result;
            }

            // Wrap in root element for parsing
            var wrappedXml = $"<root>{xmlContent}</root>";

            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(wrappedXml);

                foreach (System.Xml.XmlNode child in doc.DocumentElement.ChildNodes)
                {
                    RenderTopLevelNode(child, result);
                }

                // Also populate the legacy Lines collection from sections for backward compatibility
                PopulateLinesFromSections(result);
            }
            catch
            {
                // If XML parsing fails, just render as plain text
                var line = new RenderedLine();
                line.Segments.Add(new RenderedSegment(CleanText(xmlContent)));
                result.Lines.Add(line);

                // Also add as a summary section
                var summarySection = new RenderedCommentSection(CommentSectionType.Summary);
                var summaryLine = new RenderedLine();
                summaryLine.Segments.Add(new RenderedSegment(CleanText(xmlContent)));
                summarySection.Lines.Add(summaryLine);
                result.Sections.Add(summarySection);
            }

            return result;
        }

        private static void PopulateLinesFromSections(RenderedComment result)
        {
            var isFirst = true;
            RenderedCommentSection previousSection = null;

            foreach (RenderedCommentSection section in result.Sections)
            {
                if (section.IsEmpty)
                    continue;

                if (!isFirst)
                {
                    // Add extra padding between sections for better readability
                    // More space before major sections like Remarks, Example
                    var needsExtraPadding = section.Type == CommentSectionType.Remarks
                        || section.Type == CommentSectionType.Example
                        || section.Type == CommentSectionType.SeeAlso
                        || (previousSection != null && previousSection.Type == CommentSectionType.Summary);

                    result.Lines.Add(new RenderedLine()); // First blank line
                    if (needsExtraPadding)
                    {
                        result.Lines.Add(new RenderedLine()); // Extra blank line for visual separation
                    }
                }

                isFirst = false;

                // Add heading if present (but not for summary)
                if (!string.IsNullOrEmpty(section.Heading) && section.Type != CommentSectionType.Summary)
                {
                    var headingLine = new RenderedLine();
                    headingLine.Segments.Add(new RenderedSegment(section.Heading, RenderedSegmentType.Heading));
                    result.Lines.Add(headingLine);

                    // Add a small gap after the heading before content
                    if (section.Lines.Count > 0 && !section.Lines[0].IsBlank)
                    {
                        result.Lines.Add(new RenderedLine());
                    }
                }

                foreach (RenderedLine line in section.Lines)
                {
                    result.Lines.Add(line);
                }

                previousSection = section;
            }
        }

        private static void RenderTopLevelNode(System.Xml.XmlNode node, RenderedComment result)
        {
            if (node is System.Xml.XmlText textNode)
            {
                var text = CleanText(textNode.Value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Loose text outside any element - add to summary or create one
                    RenderedCommentSection summary = result.Sections.FirstOrDefault(s => s.Type == CommentSectionType.Summary);
                    if (summary == null)
                    {
                        summary = new RenderedCommentSection(CommentSectionType.Summary);
                        result.Sections.Insert(0, summary);
                    }
                    RenderedLine line = GetOrCreateCurrentLine(summary);
                    line.Segments.Add(new RenderedSegment(text));
                }
                return;
            }

            if (node is System.Xml.XmlWhitespace)
            {
                return;
            }

            if (node is not System.Xml.XmlElement element)
            {
                return;
            }

            var tagName = element.Name.ToLowerInvariant();

            switch (tagName)
            {
                case "summary":
                    RenderSectionElement(element, result, CommentSectionType.Summary, null, null);
                    break;

                case "remarks":
                    RenderSectionElement(element, result, CommentSectionType.Remarks, "Remarks:", null);
                    break;

                case "returns":
                    RenderSectionElement(element, result, CommentSectionType.Returns, "Returns:", null);
                    break;

                case "value":
                    RenderSectionElement(element, result, CommentSectionType.Value, "Value:", null);
                    break;

                case "example":
                    RenderSectionElement(element, result, CommentSectionType.Example, "Example:", null);
                    break;

                case "param":
                    var paramName = element.GetAttribute("name");
                    RenderSectionElement(element, result, CommentSectionType.Param, $"Parameter '{paramName}':", paramName);
                    break;

                case "typeparam":
                    var typeParamName = element.GetAttribute("name");
                    RenderSectionElement(element, result, CommentSectionType.TypeParam, $"Type parameter '{typeParamName}':", typeParamName);
                    break;

                case "exception":
                    var exceptionType = GetTypeNameFromCref(element.GetAttribute("cref"));
                    RenderSectionElement(element, result, CommentSectionType.Exception, $"Throws {exceptionType}:", exceptionType);
                    break;

                case "seealso":
                    RenderSeeAlsoSection(element, result);
                    break;

                default:
                    // Unknown top-level tag - add to summary or create one
                    RenderedCommentSection summary = result.Sections.FirstOrDefault(s => s.Type == CommentSectionType.Summary);
                    if (summary == null)
                    {
                        summary = new RenderedCommentSection(CommentSectionType.Summary);
                        result.Sections.Insert(0, summary);
                    }
                    RenderChildNodes(element, summary);
                    break;
            }
        }

        private static void RenderSectionElement(System.Xml.XmlElement element, RenderedComment result,
            CommentSectionType sectionType, string heading, string name)
        {
            var section = new RenderedCommentSection(sectionType, heading, name);
            RenderChildNodes(element, section);
            result.Sections.Add(section);
        }

        private static void RenderSeeAlsoSection(System.Xml.XmlElement element, RenderedComment result)
        {
            // Find or create the seealso section
            RenderedCommentSection seeAlsoSection = result.Sections.FirstOrDefault(s => s.Type == CommentSectionType.SeeAlso);
            if (seeAlsoSection == null)
            {
                seeAlsoSection = new RenderedCommentSection(CommentSectionType.SeeAlso, "See also:");
                result.Sections.Add(seeAlsoSection);
            }

            // Add the reference
            var cref = element.GetAttribute("cref");
            var href = element.GetAttribute("href");
            var displayText = element.InnerText;
            string linkTarget = null;

            if (!string.IsNullOrEmpty(cref))
            {
                linkTarget = cref;
                if (string.IsNullOrEmpty(displayText))
                {
                    displayText = GetTypeNameFromCref(cref);
                }
            }
            else if (!string.IsNullOrEmpty(href))
            {
                linkTarget = href;
                if (string.IsNullOrEmpty(displayText))
                {
                    displayText = href;
                }
            }

            if (!string.IsNullOrEmpty(displayText))
            {
                var line = new RenderedLine();
                line.Segments.Add(new RenderedSegment("• "));
                line.Segments.Add(new RenderedSegment(displayText, RenderedSegmentType.Link, linkTarget));
                seeAlsoSection.Lines.Add(line);
            }
        }

        private static void RenderNode(System.Xml.XmlNode node, RenderedCommentSection section)
        {
            if (node is System.Xml.XmlText textNode)
            {
                var text = CleanText(textNode.Value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    RenderedLine line = GetOrCreateCurrentLine(section);
                    line.Segments.Add(new RenderedSegment(text));
                }
                return;
            }

            // Handle significant whitespace (whitespace in mixed content) - add single space
            if (node is System.Xml.XmlSignificantWhitespace)
            {
                RenderedLine line = GetOrCreateCurrentLine(section);
                // Only add space if the last segment doesn't already end with space
                if (line.Segments.Count > 0)
                {
                    RenderedSegment lastSegment = line.Segments[line.Segments.Count - 1];
                    if (!lastSegment.Text.EndsWith(" ", StringComparison.Ordinal))
                    {
                        line.Segments.Add(new RenderedSegment(""));
                    }
                }
                return;
            }

            if (node is System.Xml.XmlWhitespace)
            {
                return;
            }

            if (node is not System.Xml.XmlElement element)
            {
                return;
            }

            var tagName = element.Name.ToLowerInvariant();

            switch (tagName)
            {
                case "see":
                    RenderSeeTag(element, section);
                    break;

                case "paramref":
                    RenderParamRef(element, section);
                    break;

                case "typeparamref":
                    RenderTypeParamRef(element, section);
                    break;

                case "c":
                    RenderInlineCode(element, section);
                    break;

                case "code":
                    RenderCodeBlock(element, section);
                    break;

                case "para":
                    section.Lines.Add(new RenderedLine()); // Add blank line before
                    RenderChildNodes(element, section);
                    section.Lines.Add(new RenderedLine()); // Add blank line after
                    break;

                case "list":
                    RenderList(element, section);
                    break;

                case "b":
                case "strong":
                    RenderBold(element, section);
                    break;

                case "i":
                case "em":
                    RenderItalic(element, section);
                    break;

                default:
                    // Unknown tag - just render children
                    RenderChildNodes(element, section);
                    break;
            }
        }

        private static void RenderChildNodes(System.Xml.XmlNode parent, RenderedCommentSection section)
        {
            foreach (System.Xml.XmlNode child in parent.ChildNodes)
            {
                RenderNode(child, section);
            }
        }

        private static void RenderSeeTag(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            var cref = element.GetAttribute("cref");
            var href = element.GetAttribute("href");
            var langword = element.GetAttribute("langword");

            var displayText = element.InnerText?.Trim();
            string linkTarget = null;

            if (!string.IsNullOrEmpty(cref))
            {
                linkTarget = cref;
                if (string.IsNullOrEmpty(displayText))
                {
                    displayText = GetTypeNameFromCref(cref);
                    // Fallback to full cref if extraction failed
                    if (string.IsNullOrEmpty(displayText))
                    {
                        displayText = cref;
                    }
                }
            }
            else if (!string.IsNullOrEmpty(href))
            {
                linkTarget = href;
                if (string.IsNullOrEmpty(displayText))
                {
                    displayText = href;
                }
            }
            else if (!string.IsNullOrEmpty(langword))
            {
                displayText = langword;
            }

            if (!string.IsNullOrEmpty(displayText))
            {
                RenderedLine line = GetOrCreateCurrentLine(section);
                line.Segments.Add(new RenderedSegment(displayText, RenderedSegmentType.Link, linkTarget));
            }
        }

        private static void RenderParamRef(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            var name = element.GetAttribute("name");
            if (!string.IsNullOrEmpty(name))
            {
                RenderedLine line = GetOrCreateCurrentLine(section);
                line.Segments.Add(new RenderedSegment(name, RenderedSegmentType.ParamRef));
            }
        }

        private static void RenderTypeParamRef(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            var name = element.GetAttribute("name");
            if (!string.IsNullOrEmpty(name))
            {
                RenderedLine line = GetOrCreateCurrentLine(section);
                line.Segments.Add(new RenderedSegment(name, RenderedSegmentType.TypeParamRef));
            }
        }

        private static void RenderInlineCode(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            var code = element.InnerText;
            if (!string.IsNullOrEmpty(code))
            {
                RenderedLine line = GetOrCreateCurrentLine(section);
                line.Segments.Add(new RenderedSegment(code, RenderedSegmentType.Code));
            }
        }

        private static void RenderCodeBlock(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            section.Lines.Add(new RenderedLine()); // Blank line before

            // Preserve the original formatting of code blocks
            var codeContent = element.InnerText;

            // Split by newlines while preserving empty lines
            var codeLines = codeContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // Find minimum indentation to normalize (skip empty lines)
            var minIndent = int.MaxValue;
            foreach (var codeLine in codeLines)
            {
                if (string.IsNullOrWhiteSpace(codeLine))
                    continue;
                var leadingSpaces = codeLine.Length - codeLine.TrimStart().Length;
                if (leadingSpaces < minIndent)
                    minIndent = leadingSpaces;
            }
            if (minIndent == int.MaxValue)
                minIndent = 0;

            foreach (var codeLine in codeLines)
            {
                var line = new RenderedLine();
                if (string.IsNullOrWhiteSpace(codeLine))
                {
                    // Preserve empty lines in code blocks
                    line.Segments.Add(new RenderedSegment(" ", RenderedSegmentType.Code));
                }
                else
                {
                    // Remove common leading indentation but preserve relative indentation
                    var normalizedLine = codeLine.Length > minIndent
                        ? codeLine.Substring(minIndent)
                        : codeLine.TrimStart();
                    line.Segments.Add(new RenderedSegment("    " + normalizedLine.TrimEnd(), RenderedSegmentType.Code));
                }
                section.Lines.Add(line);
            }

            section.Lines.Add(new RenderedLine()); // Blank line after
        }

        private static void RenderList(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            // Mark where list content starts (for collapsed view to exclude it)
            if (section.ListContentStartIndex < 0)
            {
                section.ListContentStartIndex = section.Lines.Count;
            }

            // Add blank line before list for visual separation (if there's preceding content)
            if (section.Lines.Count > 0 && !section.Lines[section.Lines.Count - 1].IsBlank)
            {
                section.Lines.Add(new RenderedLine());
            }

            var listType = element.GetAttribute("type");
            var itemNumber = 1;

            foreach (System.Xml.XmlNode child in element.ChildNodes)
            {
                if (child is System.Xml.XmlElement itemElement && itemElement.Name.ToLowerInvariant() == "item")
                {
                    var line = new RenderedLine();
                    var bullet = listType == "number" ? $"{itemNumber++}. " : "  • ";
                    line.Segments.Add(new RenderedSegment(bullet));

                    // Get term and description if present
                    XmlNode term = itemElement.SelectSingleNode("term");
                    XmlNode description = itemElement.SelectSingleNode("description");

                    if (term != null)
                    {
                        line.Segments.Add(new RenderedSegment(term.InnerText.Trim(), RenderedSegmentType.Bold));
                        if (description != null)
                        {
                            line.Segments.Add(new RenderedSegment(" – "));
                            line.Segments.Add(new RenderedSegment(description.InnerText.Trim()));
                        }
                    }
                    else
                    {
                        line.Segments.Add(new RenderedSegment(itemElement.InnerText.Trim()));
                    }

                    section.Lines.Add(line);
                }
            }

            // Add blank line after list for visual separation
            section.Lines.Add(new RenderedLine());
        }

        private static void RenderBold(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            RenderedLine line = GetOrCreateCurrentLine(section);
            line.Segments.Add(new RenderedSegment(element.InnerText, RenderedSegmentType.Bold));
        }

        private static void RenderItalic(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            RenderedLine line = GetOrCreateCurrentLine(section);
            line.Segments.Add(new RenderedSegment(element.InnerText, RenderedSegmentType.Italic));
        }

        private static RenderedLine GetOrCreateCurrentLine(RenderedCommentSection section)
        {
            if (section.Lines.Count == 0)
            {
                var line = new RenderedLine();
                section.Lines.Add(line);
                return line;
            }
            return section.Lines[section.Lines.Count - 1];
        }

        // Legacy method for backward compatibility
        private static RenderedLine GetOrCreateCurrentLine(RenderedComment result)
        {
            if (result.Lines.Count == 0)
            {
                var line = new RenderedLine();
                result.Lines.Add(line);
                return line;
            }
            return result.Lines[result.Lines.Count - 1];
        }

        private static string GetTypeNameFromCref(string cref)
        {
            if (string.IsNullOrEmpty(cref))
            {
                return "";
            }

            var result = cref;

            // Remove type prefix (T:, M:, P:, F:, E:)
            if (result.Length > 2 && result[1] == ':')
            {
                result = result.Substring(2);
            }

            // Remove parameter list for methods first (to handle nested types in params)
            var parenIndex = result.IndexOf('(');
            if (parenIndex >= 0)
            {
                result = result.Substring(0, parenIndex);
            }

            // Get just the type/member name (last part after .)
            var lastDot = result.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < result.Length - 1)
            {
                result = result.Substring(lastDot + 1);
            }

            return result;
        }

        private static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            // Remember if there was leading/trailing whitespace
            var hadLeadingSpace = char.IsWhiteSpace(text[0]);
            var hadTrailingSpace = char.IsWhiteSpace(text[text.Length - 1]);

            // Normalize whitespace (collapses multiple spaces/newlines to single space)
            text = Regex.Replace(text, @"\s+", " ");

            // Only trim if we're going to re-add the spaces we need
            var trimmed = text.Trim();

            if (trimmed.Length == 0)
            {
                // Pure whitespace - return single space if there was any whitespace
                return (hadLeadingSpace || hadTrailingSpace) ? " " : "";
            }

            // Re-add single leading/trailing space for inline element separation
            var result = trimmed;
            if (hadLeadingSpace)
            {
                result = " " + result;
            }
            if (hadTrailingSpace)
            {
                result += " ";
            }

            return result.Trim();
        }

        /// <summary>
        /// Extracts plain text summary from a comment block for compact collapsed display.
        /// Strips all XML tags and returns just the text content from the summary element.
        /// </summary>
        public static string GetStrippedSummary(XmlDocCommentBlock block)
        {
            if (block == null || string.IsNullOrWhiteSpace(block.XmlContent))
            {
                return string.Empty;
            }

            var xmlContent = block.XmlContent;
            var wrappedXml = $"<root>{xmlContent}</root>";

            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(wrappedXml);

                // Find the summary element
                var summaryNode = doc.DocumentElement.SelectSingleNode("summary");
                if (summaryNode == null)
                {
                    // No summary - try to extract any text content
                    return CleanText(doc.DocumentElement.InnerText);
                }

                // Extract text from summary, preserving inline elements but removing tags
                var summaryText = ExtractPlainText(summaryNode);
                return CleanText(summaryText);
            }
            catch
            {
                // If XML parsing fails, strip tags manually
                return StripXmlTags(xmlContent);
            }
        }

        /// <summary>
        /// Recursively extracts plain text from an XML node, preserving text content but removing tags.
        /// </summary>
        private static string ExtractPlainText(System.Xml.XmlNode node)
        {
            var sb = new StringBuilder();

            foreach (System.Xml.XmlNode child in node.ChildNodes)
            {
                if (child is System.Xml.XmlText textNode)
                {
                    sb.Append(textNode.Value);
                }
                else if (child is System.Xml.XmlElement element)
                {
                    // For certain elements, add text representation
                    var tagName = element.Name.ToLowerInvariant();
                    switch (tagName)
                    {
                        case "paramref":
                        case "typeparamref":
                            sb.Append(element.GetAttribute("name"));
                            break;
                        case "see":
                        case "seealso":
                            var cref = element.GetAttribute("cref");
                            sb.Append(GetTypeNameFromCref(cref));
                            break;
                        default:
                            // Recursively extract text from child elements
                            sb.Append(ExtractPlainText(element));
                            break;
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Fallback method to strip XML tags using regex when parsing fails.
        /// </summary>
        private static string StripXmlTags(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return string.Empty;
            }

            // Remove XML tags
            var text = Regex.Replace(xml, @"<[^>]+>", " ");
            // Clean up whitespace
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }
    }
}

