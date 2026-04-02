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
        /// Core regex pattern for LINK syntax (without prefix).
        /// </summary>
        /// <remarks>
        /// Pattern breakdown:
        /// - (?&lt;prefix&gt;...) - LINK keyword (case-insensitive) followed by optional colon and whitespace
        /// - (?:#(?&lt;localanchor&gt;[A-Za-z0-9_-]+)) - Local anchor only (#anchor-name)
        /// - OR: (?&lt;path&gt;...) - File path that must look like an actual file:
        ///   - Must contain a path separator (/ or \) OR a file extension (. followed by alphanumeric)
        ///   - (?::(?&lt;line&gt;\d+)(?:-(?&lt;endline&gt;\d+))?)? - Optional :line or :line-endline
        ///   - (?:#(?&lt;fileanchor&gt;[A-Za-z0-9_-]+))? - Optional #anchor
        /// File paths can contain spaces (e.g., "images/Add group calendar.png")
        /// Trailing whitespace is trimmed from paths in code.
        /// Plain text like "Link this is not a file" is NOT matched.
        /// </remarks>
        private const string _linkCorePattern =
            @"(?<prefix>\bLINK\b\s*:?\s*)(?:(?<localanchor>#[A-Za-z0-9_-]+)|(?<path>(?:[./\\@~])?(?:[^\r\n#:]|:(?!\d))+?)(?::(?<line>\d+)(?:-(?<endline>\d+))?)?(?:#(?<fileanchor>[A-Za-z0-9_-]+))?(?=\s*(?:\bLINK\b|$|\r|\n)))";

        /// <summary>
        /// Default compiled regex (no tag prefixes).
        /// </summary>
        private static readonly Regex _defaultLinkRegex = new(_linkCorePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Cached regex for the current tag prefix setting.
        /// </summary>
        private static Regex _cachedPrefixRegex;
        private static string _cachedPrefixPattern;

        /// <summary>
        /// Optional delegate that returns the current tag prefix pattern (e.g., "[@$]").
        /// Set by the extension package at startup. When null, no prefixes are supported.
        /// This avoids a hard dependency on the Options namespace for shared/benchmark projects.
        /// </summary>
        public static Func<string> GetTagPrefixPattern { get; set; }

        /// <summary>
        /// Cached empty result list to avoid allocations for the common "no links" case.
        /// </summary>
        private static readonly IReadOnlyList<LinkAnchorInfo> _emptyResult = [];

        /// <summary>
        /// Gets the compiled LINK regex, incorporating any configured tag prefixes.
        /// </summary>
        private static Regex GetLinkRegex()
        {
            var prefixPattern = GetTagPrefixPattern?.Invoke();
            if (prefixPattern == null)
            {
                return _defaultLinkRegex;
            }

            if (_cachedPrefixRegex != null && _cachedPrefixPattern == prefixPattern)
            {
                return _cachedPrefixRegex;
            }

            // Prepend optional prefix before the LINK keyword
            var pattern = @"(?:" + prefixPattern + @")?\s*" + _linkCorePattern;
            _cachedPrefixRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            _cachedPrefixPattern = prefixPattern;
            return _cachedPrefixRegex;
        }

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

            // Fast pre-check: must contain LINK in any casing
            if (text.IndexOf("link", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return _emptyResult;
            }

            var results = new List<LinkAnchorInfo>();

            foreach (Match match in GetLinkRegex().Matches(text))
            {
                // Calculate target position (excludes "LINK:" prefix)
                Group prefixGroup = match.Groups["prefix"];
                var prefixLength = prefixGroup.Success ? prefixGroup.Length : 0;
                var targetStartIndex = match.Index + prefixLength;
                var targetLength = match.Length - prefixLength;

                // Guard against leading/trailing whitespace being included in the clickable target span
                while (targetLength > 0 && char.IsWhiteSpace(text[targetStartIndex]))
                {
                    targetStartIndex++;
                    targetLength--;
                }

                while (targetLength > 0 && char.IsWhiteSpace(text[targetStartIndex + targetLength - 1]))
                {
                    targetLength--;
                }

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

                        // Skip paths that don't look like actual file paths (e.g., plain text like "this is not a file")
                        // A valid file path should contain either:
                        // - A path separator (/ or \)
                        // - A file extension (. followed by alphanumeric characters)
                        if (!LooksLikeFilePath(filePath))
                        {
                            continue;
                        }
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
                    TargetStartIndex = targetStartIndex,
                    TargetLength = targetLength,
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

            // Fast pre-check: must contain LINK in any casing
            return text.IndexOf("link", StringComparison.OrdinalIgnoreCase) >= 0 && GetLinkRegex().IsMatch(text);
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
                if (position >= link.TargetStartIndex && position < link.TargetStartIndex + link.TargetLength)
                {
                    return link;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if the given text looks like a file path (contains path separators or a file extension).
        /// </summary>
        /// <param name="path">The text to check.</param>
        /// <returns>True if the text looks like a file path; otherwise, false.</returns>
        /// <remarks>
        /// This prevents plain text like "this is not a file" from being treated as a file path.
        /// A valid file path should contain:
        /// - A path separator (/ or \), OR
        /// - A file extension (. followed by at least one alphanumeric character), OR
        /// - A dot-prefix pattern like .gitignore, .editorconfig (starts with . followed by letters)
        /// </remarks>
        private static bool LooksLikeFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // Contains a path separator - definitely a path
            if (path.IndexOf("/", StringComparison.Ordinal) >= 0 || path.IndexOf("\\", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            // Dot-prefixed hidden files like .gitignore, .editorconfig
            // Must start with a dot followed immediately by at least one letter (no whitespace)
            if (path.Length > 1 && path[0] == '.' && char.IsLetter(path[1]))
            {
                return true;
            }

            // Check for file extension pattern: .ext (e.g., "file.cs", "README.md")
            // Must have a dot followed IMMEDIATELY by alphanumeric characters (no whitespace)
            // This prevents "this is. not a file" from matching
            var lastDotIndex = path.LastIndexOf('.');
            if (lastDotIndex > 0 && lastDotIndex < path.Length - 1)
            {
                // The character immediately after the dot must be alphanumeric (not whitespace)
                if (char.IsLetterOrDigit(path[lastDotIndex + 1]))
                {
                    // Also ensure the extension doesn't contain spaces (e.g., reject ".cs is not")
                    for (var i = lastDotIndex + 1; i < path.Length; i++)
                    {
                        if (char.IsWhiteSpace(path[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
