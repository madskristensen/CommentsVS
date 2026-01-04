using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace CommentsVS.Services
{
    /// <summary>
    /// Represents a segment of rendered comment content.
    /// </summary>
    public class RenderedSegment
    {
        public string Text { get; }
        public RenderedSegmentType Type { get; }
        public string LinkTarget { get; }

        public RenderedSegment(string text, RenderedSegmentType type = RenderedSegmentType.Text, string linkTarget = null)
        {
            Text = text;
            Type = type;
            LinkTarget = linkTarget;
        }
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
        public List<RenderedSegment> Segments { get; } = new List<RenderedSegment>();
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
    public class RenderedCommentSection
    {
        public CommentSectionType Type { get; }
        public string Heading { get; }
        public List<RenderedLine> Lines { get; } = new List<RenderedLine>();

        /// <summary>
        /// For param/typeparam/exception sections, this is the name or type.
        /// </summary>
        public string Name { get; }

        public RenderedCommentSection(CommentSectionType type, string heading = null, string name = null)
        {
            Type = type;
            Heading = heading;
            Name = name;
        }

        public bool IsEmpty => Lines.Count == 0 || Lines.All(l => l.IsBlank);
    }

    /// <summary>
    /// Represents a fully rendered XML documentation comment block.
    /// </summary>
    public class RenderedComment
    {
        public List<RenderedLine> Lines { get; } = new List<RenderedLine>();
        public string Indentation { get; set; } = "";

        /// <summary>
        /// Gets the individual sections of the comment (summary, params, returns, etc.).
        /// </summary>
        public List<RenderedCommentSection> Sections { get; } = new List<RenderedCommentSection>();

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
        private static readonly Regex XmlTagRegex = new Regex(
            @"<(?<closing>/)?(?<tag>\w+)(?<attrs>[^>]*)(?<selfclose>/)?>",
            RegexOptions.Compiled);

        /// <summary>
        /// Renders an XML documentation comment block into formatted segments.
        /// </summary>
        public static RenderedComment Render(XmlDocCommentBlock block)
        {
            var result = new RenderedComment { Indentation = block.Indentation };
            string xmlContent = block.XmlContent;

            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return result;
            }

            // Wrap in root element for parsing
            string wrappedXml = $"<root>{xmlContent}</root>";

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
            bool first = true;
            foreach (var section in result.Sections)
            {
                if (section.IsEmpty)
                    continue;

                if (!first)
                {
                    result.Lines.Add(new RenderedLine()); // blank line between sections
                }
                first = false;

                // Add heading if present (but not for summary)
                if (!string.IsNullOrEmpty(section.Heading) && section.Type != CommentSectionType.Summary)
                {
                    var headingLine = new RenderedLine();
                    headingLine.Segments.Add(new RenderedSegment(section.Heading, RenderedSegmentType.Heading));
                    result.Lines.Add(headingLine);
                }

                foreach (var line in section.Lines)
                {
                    result.Lines.Add(line);
                }
            }
        }

        private static void RenderTopLevelNode(System.Xml.XmlNode node, RenderedComment result)
        {
            var textNode = node as System.Xml.XmlText;
            if (textNode != null)
            {
                string text = CleanText(textNode.Value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Loose text outside any element - add to summary or create one
                    var summary = result.Sections.FirstOrDefault(s => s.Type == CommentSectionType.Summary);
                    if (summary == null)
                    {
                        summary = new RenderedCommentSection(CommentSectionType.Summary);
                        result.Sections.Insert(0, summary);
                    }
                    var line = GetOrCreateCurrentLine(summary);
                    line.Segments.Add(new RenderedSegment(text));
                }
                return;
            }

            if (node is System.Xml.XmlWhitespace)
            {
                return;
            }

            var element = node as System.Xml.XmlElement;
            if (element == null)
            {
                return;
            }

            string tagName = element.Name.ToLowerInvariant();

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
                    string paramName = element.GetAttribute("name");
                    RenderSectionElement(element, result, CommentSectionType.Param, $"Parameter '{paramName}':", paramName);
                    break;

                case "typeparam":
                    string typeParamName = element.GetAttribute("name");
                    RenderSectionElement(element, result, CommentSectionType.TypeParam, $"Type parameter '{typeParamName}':", typeParamName);
                    break;

                case "exception":
                    string exceptionType = GetTypeNameFromCref(element.GetAttribute("cref"));
                    RenderSectionElement(element, result, CommentSectionType.Exception, $"Throws {exceptionType}:", exceptionType);
                    break;

                case "seealso":
                    RenderSeeAlsoSection(element, result);
                    break;

                default:
                    // Unknown top-level tag - add to summary or create one
                    var summary = result.Sections.FirstOrDefault(s => s.Type == CommentSectionType.Summary);
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
            var seeAlsoSection = result.Sections.FirstOrDefault(s => s.Type == CommentSectionType.SeeAlso);
            if (seeAlsoSection == null)
            {
                seeAlsoSection = new RenderedCommentSection(CommentSectionType.SeeAlso, "See also:");
                result.Sections.Add(seeAlsoSection);
            }

            // Add the reference
            string cref = element.GetAttribute("cref");
            string href = element.GetAttribute("href");
            string displayText = element.InnerText;
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
            var textNode = node as System.Xml.XmlText;
            if (textNode != null)
            {
                string text = CleanText(textNode.Value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var line = GetOrCreateCurrentLine(section);
                    line.Segments.Add(new RenderedSegment(text));
                }
                return;
            }

            if (node is System.Xml.XmlWhitespace)
            {
                return;
            }

            var element = node as System.Xml.XmlElement;
            if (element == null)
            {
                return;
            }

            string tagName = element.Name.ToLowerInvariant();

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
            string cref = element.GetAttribute("cref");
            string href = element.GetAttribute("href");
            string langword = element.GetAttribute("langword");

            string displayText = element.InnerText;
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
            else if (!string.IsNullOrEmpty(langword))
            {
                displayText = langword;
            }

            if (!string.IsNullOrEmpty(displayText))
            {
                var line = GetOrCreateCurrentLine(section);
                line.Segments.Add(new RenderedSegment(displayText, RenderedSegmentType.Link, linkTarget));
            }
        }

        private static void RenderParamRef(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            string name = element.GetAttribute("name");
            if (!string.IsNullOrEmpty(name))
            {
                var line = GetOrCreateCurrentLine(section);
                line.Segments.Add(new RenderedSegment(name, RenderedSegmentType.ParamRef));
            }
        }

        private static void RenderTypeParamRef(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            string name = element.GetAttribute("name");
            if (!string.IsNullOrEmpty(name))
            {
                var line = GetOrCreateCurrentLine(section);
                line.Segments.Add(new RenderedSegment(name, RenderedSegmentType.TypeParamRef));
            }
        }

        private static void RenderInlineCode(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            string code = element.InnerText;
            if (!string.IsNullOrEmpty(code))
            {
                var line = GetOrCreateCurrentLine(section);
                line.Segments.Add(new RenderedSegment(code, RenderedSegmentType.Code));
            }
        }

        private static void RenderCodeBlock(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            section.Lines.Add(new RenderedLine()); // Blank line before

            string[] codeLines = element.InnerText.Split('\n');
            foreach (string codeLine in codeLines)
            {
                var line = new RenderedLine();
                line.Segments.Add(new RenderedSegment("    " + codeLine.TrimEnd('\r'), RenderedSegmentType.Code));
                section.Lines.Add(line);
            }

            section.Lines.Add(new RenderedLine()); // Blank line after
        }

        private static void RenderList(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            string listType = element.GetAttribute("type");
            int itemNumber = 1;

            foreach (System.Xml.XmlNode child in element.ChildNodes)
            {
                if (child is System.Xml.XmlElement itemElement && itemElement.Name.ToLowerInvariant() == "item")
                {
                    var line = new RenderedLine();
                    string bullet = listType == "number" ? $"{itemNumber++}. " : "• ";
                    line.Segments.Add(new RenderedSegment(bullet));

                    // Get term and description if present
                    var term = itemElement.SelectSingleNode("term");
                    var description = itemElement.SelectSingleNode("description");

                    if (term != null)
                    {
                        line.Segments.Add(new RenderedSegment(term.InnerText.Trim(), RenderedSegmentType.Bold));
                        if (description != null)
                        {
                            line.Segments.Add(new RenderedSegment(" - "));
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
        }

        private static void RenderBold(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            var line = GetOrCreateCurrentLine(section);
            line.Segments.Add(new RenderedSegment(element.InnerText, RenderedSegmentType.Bold));
        }

        private static void RenderItalic(System.Xml.XmlElement element, RenderedCommentSection section)
        {
            var line = GetOrCreateCurrentLine(section);
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

            // Remove type prefix (T:, M:, P:, F:, E:)
            if (cref.Length > 2 && cref[1] == ':')
            {
                cref = cref.Substring(2);
            }

            // Get just the type/member name (last part after .)
            int lastDot = cref.LastIndexOf('.');
            if (lastDot >= 0)
            {
                cref = cref.Substring(lastDot + 1);
            }

            // Remove parameter list for methods
            int parenIndex = cref.IndexOf('(');
            if (parenIndex >= 0)
            {
                cref = cref.Substring(0, parenIndex);
            }

            return cref;
        }

        private static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            // Normalize whitespace
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }
    }
}
