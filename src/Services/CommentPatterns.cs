using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CommentsVS.Options;

namespace CommentsVS.Services
{
    /// <summary>
    /// Shared regex patterns for comment and anchor detection.
    /// </summary>
    internal static class CommentPatterns
    {
        /// <summary>
        /// Pattern string for built-in anchor keywords (TODO, HACK, NOTE, etc.).
        /// </summary>
        public const string BuiltInAnchorKeywordsPattern = "TODO|HACK|NOTE|BUG|FIXME|UNDONE|REVIEW|ANCHOR";

        private const string BuiltInCommentTagKeywordsPattern = BuiltInAnchorKeywordsPattern + "|LINK";

        private const string BuiltInCommentTagPattern = @"(?:(?<tag>\b(?:" + BuiltInCommentTagKeywordsPattern + @")\b)[:!]?|(?<tag>\b(?i:" + BuiltInCommentTagKeywordsPattern + @")\b)[:!])";

        private static readonly object _syncLock = new();
        private static volatile string _cachedCustomTags;
        private static volatile string _cachedAnchorKeywordsPattern;
        private static volatile Regex _cachedAnchorClassificationRegex;
        private static volatile Regex _cachedAnchorWithMetadataRegex;
        private static volatile Regex _cachedAnchorServiceRegex;
        private static volatile Regex _cachedMetadataParseRegex;

        /// <summary>
        /// Regex to match comment tags (anchors) with optional trailing delimiter for uppercase tags.
        /// Captures the tag keyword in the "tag" group.
        /// </summary>
        public static readonly Regex CommentTagRegex = new(
            BuiltInCommentTagPattern,
            RegexOptions.Compiled);

        /// <summary>
        /// Regex to match comment line prefixes (C-style, VB-style).
        /// </summary>
        public static readonly Regex CommentLineRegex = new(
            @"^\s*(//|/\*|\*|')",
            RegexOptions.Compiled);

        /// <summary>
        /// Regex to match anchor tags with optional metadata for parsing.
        /// </summary>
        public static Regex MetadataParseRegex
        {
            get
            {
                EnsurePatternsCurrent();
                return _cachedMetadataParseRegex;
            }
        }

        private static void EnsurePatternsCurrent()
        {
            var currentCustomTags = General.Instance?.CustomTags ?? string.Empty;

            if (_cachedAnchorKeywordsPattern != null && _cachedCustomTags == currentCustomTags)
            {
                return;
            }

            lock (_syncLock)
            {
                if (_cachedAnchorKeywordsPattern != null && _cachedCustomTags == currentCustomTags)
                {
                    return;
                }

                _cachedAnchorKeywordsPattern = BuildAnchorKeywordsPattern(currentCustomTags);
                RebuildRegexPatterns();

                // Update the tag cache last to ensure readers see consistent state
                _cachedCustomTags = currentCustomTags;
            }
        }

        /// <summary>
        /// Builds the anchor keywords alternation pattern from a raw comma-separated custom-tags string.
        /// Pure and side-effect free, so it can be exercised directly by unit tests without touching
        /// General.Instance.
        /// </summary>
        internal static string BuildAnchorKeywordsPattern(string customTagsStr)
        {
            // Parse custom tags directly here instead of calling General.Instance.GetCustomTagsSet()
            // to avoid race conditions if General.Instance changes during execution
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(customTagsStr))
            {
                foreach (var tag in customTagsStr.Split([','], StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = tag.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        tags.Add(trimmed.ToUpperInvariant());
                    }
                }
            }

            if (tags.Count == 0)
            {
                return BuiltInAnchorKeywordsPattern;
            }

            // Escape custom tags for regex safety and join with built-in pattern
            IEnumerable<string> escapedCustomTags = tags.Select(Regex.Escape);
            return BuiltInAnchorKeywordsPattern + "|" + string.Join("|", escapedCustomTags);
        }

        private static void RebuildRegexPatterns()
        {
            (_cachedAnchorClassificationRegex, _cachedAnchorWithMetadataRegex, _cachedAnchorServiceRegex, _cachedMetadataParseRegex) =
                BuildRegexPatterns(_cachedAnchorKeywordsPattern);
        }

        /// <summary>
        /// Builds the classification, metadata, service, and metadata-parse regexes from an already-resolved
        /// anchor keywords pattern string. Pure and side-effect free (no VS Shell / General.Instance dependency),
        /// so it can be exercised directly by unit tests.
        /// </summary>
        /// <param name="keywordsPattern">The alternation pattern of anchor keywords, e.g. from <see cref="BuildAnchorKeywordsPattern"/>.</param>
        internal static (Regex Classification, Regex WithMetadata, Regex Service, Regex MetadataParse) BuildRegexPatterns(string keywordsPattern)
        {
            var classificationTagPattern = @"(?:(?<tag>\b(?:" + keywordsPattern + @")\b)[:!]?|(?<tag>\b(?i:" + keywordsPattern + @")\b)[:!])";
            var serviceTagPattern = @"(?:(?<tag>\b(?:" + keywordsPattern + @")\b)\s*(?<metadata>(?:\([^)]*\)|\[[^\]]*\]))?\s*[:!]?|(?<tag>\b(?i:" + keywordsPattern + @")\b)\s*(?<metadata>(?:\([^)]*\)|\[[^\]]*\]))?\s*[:!])";
            var metadataTagPattern = @"(?:(?<tag>\b(?:" + keywordsPattern + @")\b)|(?<tag>\b(?i:" + keywordsPattern + @")\b(?=\s*(?:\([^)]*\)|\[[^\]]*\])\s*[:!])))";

            // Anchor must be the first word after comment prefix (and optional whitespace/asterisks)
            // This prevents matching "bug" in "straightforward bug fix"
            var classification = new Regex(
                @"(?<=//\s*)" + classificationTagPattern + @"|" +
                @"(?<=/\*[\s\*]*)" + classificationTagPattern + @"|" +
                @"(?<='\s*)" + classificationTagPattern + @"|" +
                @"(?<=^\s*\*\s*)" + classificationTagPattern,
                RegexOptions.Compiled | RegexOptions.Multiline);

            var withMetadata = new Regex(
                metadataTagPattern + @"(?<metadata>\s*(?:\([^)]*\)|\[[^\]]*\]))",
                RegexOptions.Compiled);

            var service = new Regex(
                @"(?<prefix>//|/\*|'|<!--)\s*" + serviceTagPattern + @"\s*(?<message>.*?)(?:\*/|-->|$)",
                RegexOptions.Compiled);

            var metadataParse = new Regex(
                @"(?:(?<tag>\b(?:" + keywordsPattern + @")\b)(?:\s*(?:\((?<metaParen>[^)]*)\)|\[(?<metaBracket>[^\]]*)\]))?\s*[:!]?|(?<tag>\b(?i:" + keywordsPattern + @")\b)(?:\s*(?:\((?<metaParen>[^)]*)\)|\[(?<metaBracket>[^\]]*)\]))?\s*[:!]) ?",
                RegexOptions.Compiled);

            return (classification, withMetadata, service, metadataParse);
        }
    }
}
