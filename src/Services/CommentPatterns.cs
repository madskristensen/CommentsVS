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

        private static readonly object _syncLock = new();
        private static volatile string _cachedCustomTags;
        private static volatile string _cachedAnchorKeywordsPattern;
        private static volatile Regex _cachedAnchorClassificationRegex;
        private static volatile Regex _cachedAnchorWithMetadataRegex;
        private static volatile Regex _cachedAnchorServiceRegex;
        private static volatile Regex _cachedMetadataParseRegex;

        /// <summary>
        /// Gets the current anchor keywords pattern including custom tags.
        /// </summary>
        public static string GetAnchorKeywordsPattern()
        {
            EnsurePatternsCurrent();
            return _cachedAnchorKeywordsPattern;
        }

        /// <summary>
        /// Regex to match comment tags (anchors) with optional trailing colon.
        /// Captures the tag keyword in the "tag" group.
        /// </summary>
        public static readonly Regex CommentTagRegex = new(
            @"\b(?<tag>" + BuiltInAnchorKeywordsPattern + @"|LINK)\b:?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Regex to match comment line prefixes (C-style, VB-style).
        /// </summary>
        public static readonly Regex CommentLineRegex = new(
            @"^\s*(//|/\*|\*|')",
            RegexOptions.Compiled);

        /// <summary>
        /// Gets the regex to match anchors in comments for classification.
        /// Looks for anchor keywords after C-style (//), block comment (/*), or VB-style (') comment prefixes.
        /// </summary>
        public static Regex GetAnchorClassificationRegex()
        {
            EnsurePatternsCurrent();
            return _cachedAnchorClassificationRegex;
        }

        /// <summary>
        /// Gets the regex to match anchor keywords with optional metadata (parentheses or brackets).
        /// Captures the metadata in the "metadata" group.
        /// </summary>
        public static Regex GetAnchorWithMetadataRegex()
        {
            EnsurePatternsCurrent();
            return _cachedAnchorWithMetadataRegex;
        }

        /// <summary>
        /// Gets the regex to match anchors in comments for the anchor service.
        /// Captures prefix, tag, metadata, and message groups.
        /// Supports C-style (// and /* */), VB-style ('), and HTML-style (<!-- -->) comments.
        /// </summary>
        public static Regex GetAnchorServiceRegex()
        {
            EnsurePatternsCurrent();
            return _cachedAnchorServiceRegex;
        }

        /// <summary>
        /// Gets the regex to match anchor tags with optional metadata for parsing.
        /// </summary>
        public static Regex GetMetadataParseRegex()
        {
            EnsurePatternsCurrent();
            return _cachedMetadataParseRegex;
        }

        // Keep static fields for backward compatibility with existing code that references them directly
        // These will be updated by EnsurePatternsCurrent()

        /// <summary>
        /// Regex to match anchors in comments for classification.
        /// Use GetAnchorClassificationRegex() for dynamic patterns that include custom tags.
        /// </summary>
        public static Regex AnchorClassificationRegex
        {
            get
            {
                EnsurePatternsCurrent();
                return _cachedAnchorClassificationRegex;
            }
        }

        /// <summary>
        /// Regex to match anchor keywords with optional metadata.
        /// Use GetAnchorWithMetadataRegex() for dynamic patterns that include custom tags.
        /// </summary>
        public static Regex AnchorWithMetadataRegex
        {
            get
            {
                EnsurePatternsCurrent();
                return _cachedAnchorWithMetadataRegex;
            }
        }

        /// <summary>
        /// Regex to match anchors in comments for the anchor service.
        /// Use GetAnchorServiceRegex() for dynamic patterns that include custom tags.
        /// </summary>
        public static Regex AnchorServiceRegex
        {
            get
            {
                EnsurePatternsCurrent();
                return _cachedAnchorServiceRegex;
            }
        }

        /// <summary>
        /// Regex to match anchor tags with optional metadata for parsing.
        /// Use GetMetadataParseRegex() for dynamic patterns that include custom tags.
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

        private static string BuildAnchorKeywordsPattern(string customTagsStr)
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
                        tags.Add(trimmed);
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
            var pattern = _cachedAnchorKeywordsPattern;

            // Anchor must be the first word after comment prefix (and optional whitespace/asterisks)
            // This prevents matching "bug" in "straightforward bug fix"
            _cachedAnchorClassificationRegex = new Regex(
                @"(?<=//\s*)(?<tag>\b(?:" + pattern + @")\b:?)|" +
                @"(?<=/\*[\s\*]*)(?<tag>\b(?:" + pattern + @")\b:?)|" +
                @"(?<='\s*)(?<tag>\b(?:" + pattern + @")\b:?)|" +
                @"(?<=^\s*\*\s*)(?<tag>\b(?:" + pattern + @")\b:?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);

            _cachedAnchorWithMetadataRegex = new Regex(
                @"\b(?:" + pattern + @")\b(?<metadata>\s*(?:\([^)]*\)|\[[^\]]*\]))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            _cachedAnchorServiceRegex = new Regex(
                @"(?<prefix>//|/\*|'|<!--)\s*(?<tag>\b(?:" + pattern + @")\b)\s*(?<metadata>(?:\([^)]*\)|\[[^\]]*\]))?\s*:?\s*(?<message>.*?)(?:\*/|-->|$)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            _cachedMetadataParseRegex = new Regex(
                @"(?<tag>" + pattern + @")(?:\s*(?:\((?<metaParen>[^)]*)\)|\[(?<metaBracket>[^\]]*)\]))?\s*: ?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }
}
