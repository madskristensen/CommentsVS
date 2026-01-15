using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CommentsVS.Services
{
    /// <summary>
    /// Represents a parsed link anchor from a comment.
    /// </summary>
    public sealed record LinkAnchorInfo
    {
        /// <summary>
        /// Gets the full matched text (e.g., "LINK: path/to/file.cs:45#anchor").
        /// </summary>
        public string FullMatch { get; init; }

        /// <summary>
        /// Gets the file path portion (e.g., "path/to/file.cs").
        /// </summary>
        public string FilePath { get; init; }

        /// <summary>
        /// Gets the start line number, if specified (1-based).
        /// </summary>
        public int? LineNumber { get; init; }

        /// <summary>
        /// Gets the end line number for range links (1-based).
        /// </summary>
        public int? EndLineNumber { get; init; }

        /// <summary>
        /// Gets the anchor name (e.g., "section-name" from "#section-name").
        /// </summary>
        public string AnchorName { get; init; }

        /// <summary>
        /// Gets a value indicating whether this is a local anchor reference (current file).
        /// </summary>
        public bool IsLocalAnchor => string.IsNullOrEmpty(FilePath) && !string.IsNullOrEmpty(AnchorName);

        /// <summary>
        /// Gets a value indicating whether this link has a line number.
        /// </summary>
        public bool HasLineNumber => LineNumber.HasValue;

        /// <summary>
        /// Gets a value indicating whether this link has a line range.
        /// </summary>
        public bool HasLineRange => LineNumber.HasValue && EndLineNumber.HasValue;

        /// <summary>
        /// Gets a value indicating whether this link references an anchor.
        /// </summary>
        public bool HasAnchor => !string.IsNullOrEmpty(AnchorName);

        /// <summary>
        /// Gets the start position of the full match within the line.
        /// </summary>
        public int StartIndex { get; init; }

        /// <summary>
        /// Gets the length of the full matched text.
        /// </summary>
        public int Length { get; init; }

        /// <summary>
        /// Gets the start position of the target portion (path/anchor) within the line.
        /// This excludes the "LINK:" prefix.
        /// </summary>
        public int TargetStartIndex { get; init; }

        /// <summary>
        /// Gets the length of the target portion (path:line#anchor).
        /// </summary>
        public int TargetLength { get; init; }
    }

    /// <summary>
    /// Parses LINK anchor syntax from comments.
    /// </summary>
    /// <remarks>
    /// Supported syntax:
    /// <list type="bullet">
    /// <item><c>LINK: path/to/file.cs</c> - Basic file link</item>
    /// <item><c>LINK: ./relative/path/file.cs</c> - Relative path</item>
    /// <item><c>LINK: ../sibling/folder/file.cs</c> - Parent-relative path</item>
    /// <item><c>LINK path/to/file.cs</c> - Without colon</item>
    /// <item><c>LINK: Services/UserService.cs:45</c> - File at line 45</item>
    /// <item><c>LINK: Database/Schema.sql:100-150</c> - File with line range</item>
    /// <item><c>LINK: Services/UserService.cs#validate-input</c> - File with anchor</item>
    /// <item><c>LINK: #local-anchor</c> - Anchor in current file</item>
    /// <item><c>LINK: ./file.cs:50#section-name</c> - Line and anchor</item>
    /// </list>
    /// </remarks>
    public static class LinkAnchorParser
    {
        /// <summary>
        /// Regex pattern to match LINK syntax in comments.
        /// Captures: keyword, optional path, optional line/range, optional anchor.
        /// </summary>
        /// <remarks>
        /// Pattern breakdown:
        /// - (?&lt;prefix&gt;...) - LINK keyword with specific rules:
        ///   - LINK (uppercase) can be followed by space or colon
        ///   - link (lowercase) MUST be followed by colon
        /// - (?:#(?&lt;localanchor&gt;[A-Za-z0-9_-]+)) - Local anchor only (#anchor-name)
        /// - OR: (?&lt;path&gt;...) - File path (can contain spaces, stops at :digit, #anchor, LINK keyword, or end of line)
        ///   - (?::(?&lt;line&gt;\d+)(?:-(?&lt;endline&gt;\d+))?)? - Optional :line or :line-endline
        ///   - (?:#(?&lt;fileanchor&gt;[A-Za-z0-9_-]+))? - Optional #anchor
        /// File paths can contain spaces (e.g., "images/Add group calendar.png")
        /// Trailing whitespace is trimmed from paths in code.
        /// </remarks>
        private static readonly Regex _linkRegex = new(
            @"(?<prefix>(?:\bLINK\s*:?\s*|\blink:\s*))(?:(?<localanchor>#[A-Za-z0-9_-]+)|(?<path>(?:[./\\@~])?(?:[^\r\n#:]|:(?!\d))+?)(?::(?<line>\d+)(?:-(?<endline>\d+))?)?(?:#(?<fileanchor>[A-Za-z0-9_-]+))?(?=\s*(?:\bLINK\b|\blink:|$|\r|\n)))",
            RegexOptions.Compiled);

        /// <summary>
        /// Cached empty result list to avoid allocations for the common "no links" case.
        /// </summary>
        private static readonly IReadOnlyList<LinkAnchorInfo> _emptyResult = [];

        /// <summary>
        /// Parses all LINK references in the given text.
        /// </summary>
        /// <param name="text">The text to parse (typically a single line).</param>
        /// <returns>All parsed link anchors found in the text.</returns>
        public static IReadOnlyList<LinkAnchorInfo> Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return _emptyResult;
            }

            // Fast pre-check: must contain "LINK" (uppercase, can be followed by space or colon)
            // or "link:" (lowercase, must have colon)
            var hasUppercaseLink = text.IndexOf("LINK", StringComparison.Ordinal) >= 0;
            var hasLowercaseLink = text.IndexOf("link:", StringComparison.Ordinal) >= 0;

            if (!hasUppercaseLink && !hasLowercaseLink)
            {
                return _emptyResult;
            }

            var results = new List<LinkAnchorInfo>();

            foreach (Match match in _linkRegex.Matches(text))
            {
                // Calculate target position (excludes "LINK:" prefix)
                Group prefixGroup = match.Groups["prefix"];
                var prefixLength = prefixGroup.Success ? prefixGroup.Length : 0;

                string filePath = null;
                int? lineNumber = null;
                int? endLineNumber = null;
                string anchorName = null;

                // Check for local anchor (#anchor-name only)
                Group localAnchorGroup = match.Groups["localanchor"];
                if (localAnchorGroup.Success)
                {
                    // Remove the leading # from the anchor name
                    anchorName = localAnchorGroup.Value.TrimStart('#');
                }
                else
                {
                    // File path with optional line/anchor
                    Group pathGroup = match.Groups["path"];
                    if (pathGroup.Success)
                    {
                        // Trim trailing whitespace that may be captured when path contains spaces
                        filePath = pathGroup.Value.TrimEnd();
                    }

                    Group lineGroup = match.Groups["line"];
                    if (lineGroup.Success && int.TryParse(lineGroup.Value, out var line))
                    {
                        lineNumber = line;
                    }

                    Group endLineGroup = match.Groups["endline"];
                    if (endLineGroup.Success && int.TryParse(endLineGroup.Value, out var endLine))
                    {
                        endLineNumber = endLine;
                    }

                    Group fileAnchorGroup = match.Groups["fileanchor"];
                    if (fileAnchorGroup.Success)
                    {
                        anchorName = fileAnchorGroup.Value;
                    }
                }

                var info = new LinkAnchorInfo
                {
                    FullMatch = match.Value,
                    StartIndex = match.Index,
                    Length = match.Length,
                    TargetStartIndex = match.Index + prefixLength,
                    TargetLength = match.Length - prefixLength,
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    EndLineNumber = endLineNumber,
                    AnchorName = anchorName
                };

                results.Add(info);
            }

            return results;
        }

        /// <summary>
        /// Checks if the given text contains any LINK references.
        /// </summary>
        /// <param name="text">The text to check.</param>
        /// <returns>True if the text contains at least one LINK reference.</returns>
        public static bool ContainsLinkAnchor(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // Fast pre-check: must contain "LINK" (uppercase) or "link:" (lowercase with colon)
            var hasUppercaseLink = text.IndexOf("LINK", StringComparison.Ordinal) >= 0;
            var hasLowercaseLink = text.IndexOf("link:", StringComparison.Ordinal) >= 0;

            return (hasUppercaseLink || hasLowercaseLink) && _linkRegex.IsMatch(text);
        }

        /// <summary>
        /// Gets the LINK reference at the specified position in the text.
        /// Only returns a link if the position is within the clickable target portion (not the "LINK:" prefix).
        /// </summary>
        /// <param name="text">The text to search.</param>
        /// <param name="position">The 0-based position within the text.</param>
        /// <returns>The link anchor at the position, or null if none found.</returns>
        public static LinkAnchorInfo GetLinkAtPosition(string text, int position)
        {
            if (string.IsNullOrEmpty(text) || position < 0 || position >= text.Length)
            {
                return null;
            }

            IReadOnlyList<LinkAnchorInfo> links = Parse(text);
            foreach (LinkAnchorInfo link in links)
            {
                // Only match if position is within the target portion (path/anchor), not the "LINK:" prefix
                if (position >= link.TargetStartIndex && position <= link.TargetStartIndex + link.TargetLength)
                {
                    return link;
                }
            }

            return null;
        }
    }
}
