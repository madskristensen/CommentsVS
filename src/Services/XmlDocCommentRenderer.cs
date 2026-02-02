using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static CommentsVS.Services.RenderedSegment;

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


        private static void EnsureSummarySection(RenderedComment result)
        {
            RenderedCommentSection summary = result.Sections.FirstOrDefault(s => s.Type == CommentSectionType.Summary);

            if (summary == null)
            {
                summary = new RenderedCommentSection(CommentSectionType.Summary);
                result.Sections.Insert(0, summary);
            }

            if (summary.IsEmpty)
            {
                summary.Lines.Clear();
                var line = new RenderedLine();
                line.Segments.Add(new RenderedSegment(XmlDocCommentRenderer.NoSummaryPlaceholder));
                summary.Lines.Add(line);
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
            Heading,
            Strikethrough,
            IssueReference
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
            public const string NoSummaryPlaceholder = "(No summary provided)";

            private static readonly Regex _xmlTagRegex = new(
                @"<(?<closing>/)?(?<tag>\w+)(?<attrs>[^>]*)(?<selfclose>/)?>",
                RegexOptions.Compiled);

            /// <summary>
            /// Pre-compiled regex for whitespace normalization (collapses multiple whitespace to single space).
            /// </summary>
            private static readonly Regex _whitespaceRegex = new(@"\s+", RegexOptions.Compiled);

            /// <summary>
            /// Pre-compiled regex for stripping XML tags (fallback when parsing fails).
            /// </summary>
            private static readonly Regex _stripXmlTagsRegex = new(@"<[^>]+>", RegexOptions.Compiled);

            /// <summary>
            /// Thread-local storage for GitRepositoryInfo during rendering.
            /// This allows ProcessMarkdownInText to access repo info without threading it through all methods.
            /// </summary>
            [ThreadStatic]
            private static GitRepositoryInfo _currentRepoInfo;

            /// <summary>
            /// Renders an XML documentation comment block into formatted segments.
            /// </summary>
            /// <param name="block">The XML doc comment block to render.</param>
            /// <param name="repoInfo">Optional Git repository info for resolving issue reference URLs.</param>
            public static RenderedComment Render(XmlDocCommentBlock block, GitRepositoryInfo repoInfo = null)
            {
                // Store repo info in thread-local storage for internal methods to access
                _currentRepoInfo = repoInfo;
                try
                {
                    return RenderInternal(block);
                }
                finally
                {
                    _currentRepoInfo = null;
                }
            }

            /// <summary>
            /// Renders raw XML documentation content into formatted segments.
            /// This overload is primarily for testing purposes.
            /// </summary>
            /// <param name="xmlContent">The raw XML content (without comment prefixes).</param>
            /// <param name="repoInfo">Optional Git repository info for resolving issue reference URLs.</param>
            public static RenderedComment RenderXmlContent(string xmlContent, GitRepositoryInfo repoInfo = null)
            {
                _currentRepoInfo = repoInfo;
                try
                {
                    return RenderXmlContentInternal(xmlContent);
                }
                finally
                {
                    _currentRepoInfo = null;
                }
            }

            private static RenderedComment RenderXmlContentInternal(string xmlContent)
            {
                var result = new RenderedComment { Indentation = "" };

                if (string.IsNullOrWhiteSpace(xmlContent))
                {
                    EnsureSummarySection(result);
                    return result;
                }

                // Wrap in root element for parsing
                var wrappedXml = $"<root>{xmlContent}</root>";

                try
                {
                    var doc = XDocument.Parse(wrappedXml);

                    foreach (XNode child in doc.Root.Nodes())
                    {
                        RenderTopLevelNode(child, result);
                    }

                    EnsureSummarySection(result);
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

            private static RenderedComment RenderInternal(XmlDocCommentBlock block)
            {
                var result = new RenderedComment { Indentation = block.Indentation };
                var xmlContent = block.XmlContent;

                if (string.IsNullOrWhiteSpace(xmlContent))
                {
                    EnsureSummarySection(result);
                    return result;
                }

                // Wrap in root element for parsing
                var wrappedXml = $"<root>{xmlContent}</root>";

                try
                {
                    var doc = XDocument.Parse(wrappedXml);

                    foreach (XNode child in doc.Root.Nodes())
                    {
                        RenderTopLevelNode(child, result);
                    }

                    EnsureSummarySection(result);
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

            private static void RenderTopLevelNode(XNode node, RenderedComment result)
            {
                if (node is XText textNode)
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
                        // Process markdown patterns in the text (with issue reference support)
                        List<RenderedSegment> segments = ProcessMarkdownInText(text, _currentRepoInfo);
                        foreach (RenderedSegment segment in segments)
                        {
                            line.Segments.Add(segment);
                        }
                    }
                    return;
                }

                if (node is not XElement element)
                {
                    return;
                }

                var tagName = element.Name.LocalName.ToLowerInvariant();

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
                        var paramName = (string)element.Attribute("name") ?? "";
                        RenderSectionElement(element, result, CommentSectionType.Param, $"Parameter '{paramName}':", paramName);
                        break;

                    case "typeparam":
                        var typeParamName = (string)element.Attribute("name") ?? "";
                        RenderSectionElement(element, result, CommentSectionType.TypeParam, $"Type parameter '{typeParamName}':", typeParamName);
                        break;

                    case "exception":
                        var exceptionType = GetTypeNameFromCref((string)element.Attribute("cref") ?? "");
                        RenderSectionElement(element, result, CommentSectionType.Exception, $"Throws {exceptionType}:", exceptionType);
                        break;

                    case "seealso":
                        RenderSeeAlsoSection(element, result);
                        break;

                    case "inheritdoc":
                        RenderInheritDocSection(element, result);
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

            private static void RenderSectionElement(XElement element, RenderedComment result,
                CommentSectionType sectionType, string heading, string name)
            {
                var section = new RenderedCommentSection(sectionType, heading, name);
                RenderChildNodes(element, section);
                result.Sections.Add(section);
            }

            private static void RenderSeeAlsoSection(XElement element, RenderedComment result)
            {
                // Find or create the seealso section
                RenderedCommentSection seeAlsoSection = result.Sections.FirstOrDefault(s => s.Type == CommentSectionType.SeeAlso);
                if (seeAlsoSection == null)
                {
                    seeAlsoSection = new RenderedCommentSection(CommentSectionType.SeeAlso, "See also:");
                    result.Sections.Add(seeAlsoSection);
                }

                // Add the reference
                var cref = (string)element.Attribute("cref") ?? "";
                var href = (string)element.Attribute("href") ?? "";
                var displayText = element.Value;
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

            private static void RenderInheritDocSection(XElement element, RenderedComment result)
            {
                // inheritdoc is a special tag that inherits documentation from a base class or interface.
                // Since we can't resolve the inherited docs at render time, show a descriptive message.
                var section = new RenderedCommentSection(CommentSectionType.Summary);
                var line = new RenderedLine();

                // Check for cref attribute which specifies explicit inheritance source
                var cref = (string)element.Attribute("cref") ?? "";
                if (!string.IsNullOrEmpty(cref))
                {
                    var typeName = GetTypeNameFromCref(cref);
                    line.Segments.Add(new RenderedSegment("(Documentation inherited from ", RenderedSegmentType.Italic));
                    line.Segments.Add(new RenderedSegment(typeName, RenderedSegmentType.Code));
                    line.Segments.Add(new RenderedSegment(")", RenderedSegmentType.Italic));
                }
                else
                {
                    line.Segments.Add(new RenderedSegment("(Documentation inherited)", RenderedSegmentType.Italic));
                }

                section.Lines.Add(line);
                result.Sections.Add(section);
            }

            private static void RenderNode(XNode node, RenderedCommentSection section)
            {
                if (node is XText textNode)
                {
                    RenderTextNode(textNode.Value, section);
                    return;
                }

                if (node is not XElement element)
                {
                    return;
                }

                var tagName = element.Name.LocalName.ToLowerInvariant();

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

            private static void RenderChildNodes(XElement parent, RenderedCommentSection section)
            {
                foreach (XNode child in parent.Nodes())
                {
                    RenderNode(child, section);
                }
            }

            private static void RenderSeeTag(XElement element, RenderedCommentSection section)
            {
                var cref = (string)element.Attribute("cref") ?? "";
                var href = (string)element.Attribute("href") ?? "";
                var langword = (string)element.Attribute("langword") ?? "";

                var displayText = element.Value?.Trim() ?? "";
                RenderedSegmentType segmentType;
                string linkTarget = null;

                if (!string.IsNullOrEmpty(cref))
                {
                    // cref references render as code (not clickable)
                    segmentType = RenderedSegmentType.Code;
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
                    // href links are clickable
                    segmentType = RenderedSegmentType.Link;
                    linkTarget = href;
                    if (string.IsNullOrEmpty(displayText))
                    {
                        displayText = href;
                    }
                }
                else if (!string.IsNullOrEmpty(langword))
                {
                    // Language keywords render as code
                    segmentType = RenderedSegmentType.Code;
                    displayText = langword;
                }
                else
                {
                    segmentType = RenderedSegmentType.Text;
                }

                if (!string.IsNullOrEmpty(displayText))
                {
                    RenderedLine line = GetOrCreateCurrentLine(section);
                    line.Segments.Add(new RenderedSegment(displayText, segmentType, linkTarget));
                }
            }

            private static void RenderParamRef(XElement element, RenderedCommentSection section)
            {
                var name = (string)element.Attribute("name") ?? "";
                if (!string.IsNullOrEmpty(name))
                {
                    RenderedLine line = GetOrCreateCurrentLine(section);
                    line.Segments.Add(new RenderedSegment(name, RenderedSegmentType.Code));
                }
            }

            private static void RenderTypeParamRef(XElement element, RenderedCommentSection section)
            {
                var name = (string)element.Attribute("name") ?? "";
                if (!string.IsNullOrEmpty(name))
                {
                    RenderedLine line = GetOrCreateCurrentLine(section);
                    line.Segments.Add(new RenderedSegment(name, RenderedSegmentType.Code));
                }
            }

            private static void RenderInlineCode(XElement element, RenderedCommentSection section)
            {
                var code = element.Value;
                if (!string.IsNullOrEmpty(code))
                {
                    RenderedLine line = GetOrCreateCurrentLine(section);
                    line.Segments.Add(new RenderedSegment(code, RenderedSegmentType.Code));
                }
            }

            private static void RenderCodeBlock(XElement element, RenderedCommentSection section)
            {
                section.Lines.Add(new RenderedLine()); // Blank line before

                // Preserve the original formatting of code blocks
                var codeContent = element.Value;

                // Split by newlines while preserving empty lines
                var codeLines = codeContent.Split(["\r\n", "\n"], StringSplitOptions.None);

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

            private static void RenderList(XElement element, RenderedCommentSection section)
            {
                // Mark where list content starts (for collapsed view to exclude it)
                if (section.ListContentStartIndex < 0)
                {
                    section.ListContentStartIndex = section.Lines.Count;
                }

                // Add blank line before list for consistent visual separation
                // Only skip if the last line is already blank (to avoid double blanks)
                if (section.Lines.Count == 0 || !section.Lines[section.Lines.Count - 1].IsBlank)
                {
                    section.Lines.Add(new RenderedLine());
                }

                var listType = (string)element.Attribute("type") ?? "";
                var itemNumber = 1;

                foreach (XNode child in element.Nodes())
                {
                    if (child is XElement itemElement && itemElement.Name.LocalName.Equals("item", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var line = new RenderedLine();
                        var bullet = listType == "number" ? $"{itemNumber++}. " : "  • ";
                        line.Segments.Add(new RenderedSegment(bullet));

                        // Get term and description if present
                        XElement term = itemElement.Element("term");
                        XElement description = itemElement.Element("description");

                        if (term != null)
                        {
                            line.Segments.Add(new RenderedSegment(term.Value.Trim(), RenderedSegmentType.Bold));
                            if (description != null)
                            {
                                line.Segments.Add(new RenderedSegment(" – "));
                                line.Segments.Add(new RenderedSegment(description.Value.Trim()));
                            }
                        }
                        else
                        {
                            line.Segments.Add(new RenderedSegment(itemElement.Value.Trim()));
                        }

                        section.Lines.Add(line);
                    }
                }

                // Note: No trailing blank line - section spacing is handled by the tagger
            }

            private static void RenderBold(XElement element, RenderedCommentSection section)
            {
                RenderedLine line = GetOrCreateCurrentLine(section);
                line.Segments.Add(new RenderedSegment(element.Value, RenderedSegmentType.Bold));
            }

            private static void RenderItalic(XElement element, RenderedCommentSection section)
            {
                RenderedLine line = GetOrCreateCurrentLine(section);
                line.Segments.Add(new RenderedSegment(element.Value, RenderedSegmentType.Italic));
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

            /// <summary>
            /// Renders a text node, preserving significant line breaks while normalizing whitespace within lines.
            /// </summary>
            private static void RenderTextNode(string text, RenderedCommentSection section)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                // Split by line breaks to preserve paragraph structure
                var lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

                for (var i = 0; i < lines.Length; i++)
                {
                    var lineText = CleanLineText(lines[i]);

                    if (!string.IsNullOrWhiteSpace(lineText))
                    {
                        RenderedLine line = GetOrCreateCurrentLine(section);
                        // Process markdown patterns in the text (with issue reference support)
                        List<RenderedSegment> segments = ProcessMarkdownInText(lineText, _currentRepoInfo);
                        foreach (RenderedSegment segment in segments)
                        {
                            line.Segments.Add(segment);
                        }
                    }

                    // Add a new line after each source line (except the last one)
                    // This preserves the original line structure
                    if (i < lines.Length - 1)
                    {
                        section.Lines.Add(new RenderedLine());
                    }
                }
            }

            /// <summary>
            /// Cleans text within a single line, normalizing whitespace but preserving leading/trailing spaces
            /// for proper inline element separation.
            /// </summary>
            private static string CleanLineText(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return "";
                }

                // Remember if there was leading/trailing whitespace
                var hadLeadingSpace = text.Length > 0 && char.IsWhiteSpace(text[0]);
                var hadTrailingSpace = text.Length > 0 && char.IsWhiteSpace(text[text.Length - 1]);

                // Normalize horizontal whitespace (spaces, tabs) but not line breaks
                text = _whitespaceRegex.Replace(text, " ");

                var trimmed = text.Trim();

                if (trimmed.Length == 0)
                {
                    // Pure whitespace - return single space if there was any whitespace
                    // but only if it's meaningful for inline element separation
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

                return result;
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

            /// <summary>
            /// Regex for markdown bold (**text** or __text__).
            /// Matches ** or __ delimiters with non-whitespace content.
            /// </summary>
            private static readonly Regex _markdownBoldRegex = new(
                @"(\*\*|__)(?=\S)(.+?)(?<=\S)\1",
                RegexOptions.Compiled);

            /// <summary>
            /// Regex for markdown italic (*text* or _text_).
            /// Uses negative lookbehind/lookahead to avoid matching bold patterns.
            /// </summary>
            private static readonly Regex _markdownItalicRegex = new(
                @"(?<!\*)\*(?!\*)(\S(?:[^*]*\S)?)\*(?!\*)|(?<!_)_(?!_)(\S(?:[^_]*\S)?)_(?!_)",
                RegexOptions.Compiled);

            /// <summary>
            /// Regex for markdown inline code (`code`).
            /// </summary>
            private static readonly Regex _markdownCodeRegex = new(
                @"`([^`]+)`",
                RegexOptions.Compiled);

            /// <summary>
            /// Regex for markdown strikethrough (~~text~~).
            /// </summary>
            private static readonly Regex _markdownStrikethroughRegex = new(
                @"~~(\S(?:[^~]*\S)?)~~",
                RegexOptions.Compiled);

            /// <summary>
            /// Regex for markdown links [text](url).
            /// </summary>
            private static readonly Regex _markdownLinkRegex = new(
                @"\[([^\]]+)\]\(([^)]+)\)",
                RegexOptions.Compiled);

            /// <summary>
            /// Regex for auto-links &lt;url&gt;.
            /// Matches URLs wrapped in angle brackets.
            /// </summary>
            private static readonly Regex _markdownAutoLinkRegex = new(
                @"<(https?://[^>]+)>",
                RegexOptions.Compiled);

            /// <summary>
            /// Regex for issue references like #123.
            /// Must be preceded by whitespace or start of line, followed by word boundary.
            /// </summary>
            private static readonly Regex _issueReferenceRegex = new(
                @"(?<=^|[\s\(\[\{])#(?<number>\d+)\b",
                RegexOptions.Compiled);

            /// <summary>
            /// Processes markdown patterns in text and returns a list of segments.
            /// Supports **bold**, __bold__, *italic*, _italic_, `code`, ~~strikethrough~~, [text](url), &lt;url&gt;, and #123 issue refs.
            /// </summary>
            /// <param name="text">The text to process.</param>
            /// <param name="repoInfo">Optional Git repository info for resolving issue reference URLs.</param>
            public static List<RenderedSegment> ProcessMarkdownInText(string text, GitRepositoryInfo repoInfo = null)
            {
                var segments = new List<RenderedSegment>();

                if (string.IsNullOrEmpty(text))
                {
                    return segments;
                }

                // Process inline code and links first (to prevent other processing inside them)
                MatchCollection codeMatches = _markdownCodeRegex.Matches(text);
                MatchCollection linkMatches = _markdownLinkRegex.Matches(text);
                MatchCollection autoLinkMatches = _markdownAutoLinkRegex.Matches(text);
                MatchCollection boldMatches = _markdownBoldRegex.Matches(text);
                MatchCollection italicMatches = _markdownItalicRegex.Matches(text);
                MatchCollection strikethroughMatches = _markdownStrikethroughRegex.Matches(text);
                MatchCollection issueReferenceMatches = _issueReferenceRegex.Matches(text);

                // Combine all matches and sort by position
                // Tuple: (Start, Length, Content, Type, LinkTarget)
                var allMatches = new List<(int Start, int Length, string Content, RenderedSegmentType Type, string LinkTarget)>();

                foreach (Match match in codeMatches)
                {
                    allMatches.Add((match.Index, match.Length, match.Groups[1].Value, RenderedSegmentType.Code, null));
                }

                // Links: [text](url)
                foreach (Match match in linkMatches)
                {
                    if (!OverlapsWithExisting(allMatches, match.Index, match.Length))
                    {
                        var linkText = match.Groups[1].Value;
                        var linkUrl = match.Groups[2].Value;
                        allMatches.Add((match.Index, match.Length, linkText, RenderedSegmentType.Link, linkUrl));
                    }
                }

                // Auto-links: <url>
                foreach (Match match in autoLinkMatches)
                {
                    if (!OverlapsWithExisting(allMatches, match.Index, match.Length))
                    {
                        var url = match.Groups[1].Value;
                        allMatches.Add((match.Index, match.Length, url, RenderedSegmentType.Link, url));
                    }
                }

                foreach (Match match in boldMatches)
                {
                    // Check if this match overlaps with any code match
                    if (!OverlapsWithExisting(allMatches, match.Index, match.Length))
                    {
                        allMatches.Add((match.Index, match.Length, match.Groups[2].Value, RenderedSegmentType.Bold, null));
                    }
                }

                foreach (Match match in italicMatches)
                {
                    // Check if this match overlaps with any existing match
                    if (!OverlapsWithExisting(allMatches, match.Index, match.Length))
                    {
                        // Group 1 is for * delimiters, Group 2 is for _ delimiters
                        var content = !string.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[1].Value : match.Groups[2].Value;
                        allMatches.Add((match.Index, match.Length, content, RenderedSegmentType.Italic, null));
                    }
                }

                foreach (Match match in strikethroughMatches)
                {
                    if (!OverlapsWithExisting(allMatches, match.Index, match.Length))
                    {
                        allMatches.Add((match.Index, match.Length, match.Groups[1].Value, RenderedSegmentType.Strikethrough, null));
                    }
                }

                // Issue references: #123 (only if we have repo info to resolve the URL)
                if (repoInfo != null)
                {
                    foreach (Match match in issueReferenceMatches)
                    {
                        if (!OverlapsWithExisting(allMatches, match.Index, match.Length))
                        {
                            if (int.TryParse(match.Groups["number"].Value, out var issueNumber))
                            {
                                var url = repoInfo.GetIssueUrl(issueNumber);
                                if (!string.IsNullOrEmpty(url))
                                {
                                    allMatches.Add((match.Index, match.Length, match.Value, RenderedSegmentType.IssueReference, url));
                                }
                            }
                        }
                    }
                }

                // Sort by position
                allMatches.Sort((a, b) => a.Start.CompareTo(b.Start));

                // Build segments
                var currentPos = 0;
                foreach ((int Start, int Length, string Content, RenderedSegmentType Type, string LinkTarget) match in allMatches)
                {
                    // Add text before this match
                    if (match.Start > currentPos)
                    {
                        var beforeText = text.Substring(currentPos, match.Start - currentPos);
                        if (!string.IsNullOrEmpty(beforeText))
                        {
                            segments.Add(new RenderedSegment(beforeText));
                        }
                    }

                    // Add the formatted segment (with link target if applicable)
                    segments.Add(new RenderedSegment(match.Content, match.Type, match.LinkTarget));
                    currentPos = match.Start + match.Length;
                }

                // Add remaining text after last match
                if (currentPos < text.Length)
                {
                    var remainingText = text.Substring(currentPos);
                    if (!string.IsNullOrEmpty(remainingText))
                    {
                        segments.Add(new RenderedSegment(remainingText));
                    }
                }

                // If no matches were found, return the original text as a single segment
                if (segments.Count == 0)
                {
                    segments.Add(new RenderedSegment(text));
                }

                return segments;
            }

            /// <summary>
            /// Checks if a range overlaps with any existing match.
            /// </summary>
            private static bool OverlapsWithExisting(List<(int Start, int Length, string Content, RenderedSegmentType Type, string LinkTarget)> existing, int start, int length)
            {
                var end = start + length;
                foreach ((int Start, int Length, string Content, RenderedSegmentType Type, string LinkTarget) match in existing)
                {
                    var matchEnd = match.Start + match.Length;
                    // Check for any overlap
                    if (start < matchEnd && end > match.Start)
                    {
                        return true;
                    }
                }
                return false;
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
                text = _whitespaceRegex.Replace(text, " ");

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

                return result;
            }

            /// <summary>
            /// Extracts plain text summary from a comment block for compact collapsed display.
            /// Strips all XML tags and returns just the text content from the summary element.
            /// </summary>
            public static string GetStrippedSummary(XmlDocCommentBlock block)
            {
                if (block == null)
                {
                    return string.Empty;
                }

                if (string.IsNullOrWhiteSpace(block.XmlContent))
                {
                    return NoSummaryPlaceholder;
                }

                return GetStrippedSummaryFromXml(block.XmlContent);
            }

            /// <summary>
            /// Extracts plain text summary from raw XML content for compact collapsed display.
            /// This overload is primarily for testing purposes.
            /// </summary>
            /// <param name="xmlContent">The raw XML content (without comment prefixes).</param>
            public static string GetStrippedSummaryFromXml(string xmlContent)
            {
                if (string.IsNullOrWhiteSpace(xmlContent))
                {
                    return NoSummaryPlaceholder;
                }

                var wrappedXml = $"<root>{xmlContent}</root>";

                try
                {
                    var doc = XDocument.Parse(wrappedXml);

                    // Check for inheritdoc element
                    XElement inheritDocNode = doc.Root.Element("inheritdoc");
                    if (inheritDocNode != null)
                    {
                        var cref = (string)inheritDocNode.Attribute("cref") ?? "";
                        if (!string.IsNullOrEmpty(cref))
                        {
                            var typeName = GetTypeNameFromCref(cref);
                            return $"(Documentation inherited from {typeName})";
                        }
                        return "(Documentation inherited)";
                    }

                    // Find the summary element
                    XElement summaryNode = doc.Root.Element("summary");
                    if (summaryNode == null)
                    {
                        return NoSummaryPlaceholder;
                    }

                    // Extract text from summary, preserving inline elements but removing tags
                    var summaryText = CleanText(ExtractPlainText(summaryNode));
                    return string.IsNullOrWhiteSpace(summaryText)
                        ? NoSummaryPlaceholder
                        : summaryText;
                }
                catch
                {
                    // If XML parsing fails, strip tags manually
                    var stripped = StripXmlTags(xmlContent);
                    return string.IsNullOrWhiteSpace(stripped)
                        ? NoSummaryPlaceholder
                        : stripped;
                }
            }

            /// <summary>
            /// Recursively extracts plain text from an XML node, preserving text content but removing tags.
            /// Code-like elements (paramref, typeparamref, see cref, c) are wrapped in backticks for markdown processing.
            /// </summary>
            private static string ExtractPlainText(XElement node)
            {
                var sb = new StringBuilder();

                foreach (XNode child in node.Nodes())
                {
                    if (child is XText textNode)
                    {
                        sb.Append(textNode.Value);
                    }
                    else if (child is XElement element)
                    {
                        // For certain elements, add text representation
                        var tagName = element.Name.LocalName.ToLowerInvariant();
                        switch (tagName)
                        {
                            case "paramref":
                            case "typeparamref":
                                var name = (string)element.Attribute("name") ?? "";
                                if (!string.IsNullOrEmpty(name))
                                {
                                    // Wrap in backticks for markdown code styling
                                    sb.Append($"`{name}`");
                                }
                                break;
                            case "see":
                            case "seealso":
                                var cref = (string)element.Attribute("cref") ?? "";
                                var typeName = GetTypeNameFromCref(cref);
                                if (!string.IsNullOrEmpty(typeName))
                                {
                                    // Wrap in backticks for markdown code styling
                                    sb.Append($"`{typeName}`");
                                }
                                break;
                            case "c":
                                // Inline code - wrap in backticks for markdown code styling
                                var code = element.Value;
                                if (!string.IsNullOrEmpty(code))
                                {
                                    sb.Append($"`{code}`");
                                }
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
                var text = _stripXmlTagsRegex.Replace(xml, " ");
                // Clean up whitespace
                text = _whitespaceRegex.Replace(text, " ");
                return text.Trim();
            }
        }
    }
}
