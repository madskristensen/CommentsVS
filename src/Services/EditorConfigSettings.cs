using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CommentsVS.Options;
using EditorConfig.Core;
using Microsoft.VisualStudio.Text.Editor;

namespace CommentsVS.Services
{
    /// <summary>
    /// Reads .editorconfig settings that override the Options page values.
    /// </summary>
    /// <remarks>
    /// Supports the following .editorconfig properties:
    /// <list type="bullet">
    /// <item><c>max_line_length</c> - Standard property for maximum line length (overrides MaxLineLength option)</item>
    /// <item><c>custom_anchor_tags</c> - Comma-separated list of custom anchor tags (overrides CustomTags option)</item>
    /// <item><c>custom_anchor_tag_prefixes</c> - Comma-separated list of optional prefix characters for comment tags (overrides TagPrefixes option)</item>
    /// </list>
    /// </remarks>
    internal static class EditorConfigSettings
    {
        private static readonly EditorConfigParser _parser = new();

        // Cache for compiled regex objects keyed by the anchor pattern string
        private static readonly ConcurrentDictionary<string, Regex> _classificationRegexCache = new();
        private static readonly ConcurrentDictionary<string, Regex> _metadataRegexCache = new();
        private static readonly ConcurrentDictionary<string, Regex> _serviceRegexCache = new();

        // Cache for custom tags from .editorconfig files keyed by directory path
        private static readonly ConcurrentDictionary<string, HashSet<string>> _customTagsCache = new();

        // Cache for tag prefix patterns from .editorconfig files keyed by directory path
        private static readonly ConcurrentDictionary<string, string> _tagPrefixCache = new();

        // Cache for per-file enablement because .editorconfig sections can vary by file name or extension
        private static readonly ConcurrentDictionary<string, bool> _enabledCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Built-in anchor keywords that are always recognized.
        /// </summary>
        public static readonly string[] BuiltInAnchorTags = ["TODO", "HACK", "NOTE", "BUG", "FIXME", "UNDONE", "REVIEW", "ANCHOR"];

        /// <summary>
        /// Regex pattern for built-in anchor tags.
        /// </summary>
        public const string BuiltInAnchorPattern = "TODO|HACK|NOTE|BUG|FIXME|UNDONE|REVIEW|ANCHOR";

        /// <summary>
        /// Returns whether CommentsVS features are enabled for a file.
        /// </summary>
        public static bool IsEnabled(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return true;
            }

            string fullPath;
            try
            {
                fullPath = System.IO.Path.GetFullPath(filePath);
            }
            catch (Exception)
            {
                return true;
            }

            return _enabledCache.GetOrAdd(fullPath, path =>
            {
                try
                {
                    FileConfiguration config = _parser.Parse(path);
                    return config.Properties.TryGetValue("commentsvs_enabled", out var value)
                        ? ParseEnabledValue(value)
                        : true;
                }
                catch (Exception)
                {
                    return true;
                }
            });
        }

        internal static bool ParseEnabledValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "false" or "off" or "no" or "0" => false,
                _ => true
            };
        }

        /// <summary>
        /// Gets all anchor tags: built-in tags plus custom tags from .editorconfig or Options page.
        /// </summary>
        /// <param name="filePath">The file path to get .editorconfig settings for (can be null).</param>
        /// <returns>All anchor tags that should be recognized for the given file.</returns>
        public static IReadOnlyList<string> GetAllAnchorTags(string filePath)
        {
            HashSet<string> customTags = GetCustomAnchorTags(filePath);
            if (customTags.Count == 0)
            {
                return BuiltInAnchorTags;
            }

            return [.. BuiltInAnchorTags, .. customTags];
        }

        /// <summary>
        /// Builds the anchor keywords regex pattern for a specific file.
        /// </summary>
        /// <param name="filePath">The file path to get .editorconfig settings for (can be null).</param>
        /// <returns>A regex pattern string matching all anchor tags for this file.</returns>
        public static string GetAnchorKeywordsPattern(string filePath)
        {
            HashSet<string> customTags = GetCustomAnchorTags(filePath);
            return BuildAnchorKeywordsPattern(customTags);
        }

        /// <summary>
        /// Builds the anchor keywords regex pattern from an already-resolved set of custom tags.
        /// Pure and side-effect free, so it can be exercised directly by unit tests.
        /// </summary>
        /// <param name="customTags">The resolved set of custom anchor tags (can be empty).</param>
        internal static string BuildAnchorKeywordsPattern(IEnumerable<string> customTags)
        {
            var tags = customTags as ICollection<string> ?? [.. customTags];
            if (tags.Count == 0)
            {
                return BuiltInAnchorPattern;
            }

            IEnumerable<string> escapedCustomTags = tags.Select(Regex.Escape);
            return BuiltInAnchorPattern + "|" + string.Join("|", escapedCustomTags);
        }

        /// <summary>
        /// Gets the tag prefix pattern for a specific file.
        /// Checks .editorconfig first, falls back to Options page.
        /// </summary>
        /// <param name="filePath">The file path to get .editorconfig settings for (can be null).</param>
        /// <returns>A regex character class pattern (e.g., "[@$]"), or null if no prefixes configured.</returns>
        public static string GetTagPrefixPattern(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                var directory = System.IO.Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    var cacheKey = "prefix:" + directory;
                    if (_tagPrefixCache.TryGetValue(cacheKey, out var cached))
                    {
                        return cached;
                    }

                    try
                    {
                        FileConfiguration config = _parser.Parse(filePath);
                        if (config.Properties.TryGetValue("custom_anchor_tag_prefixes", out var value)
                            && !string.IsNullOrWhiteSpace(value))
                        {
                            var pattern = BuildPrefixPattern(value);
                            _tagPrefixCache[cacheKey] = pattern;
                            return pattern;
                        }
                    }
                    catch
                    {
                        // Fall through to options
                    }

                    var fallback = General.Instance.GetTagPrefixPattern();
                    _tagPrefixCache[cacheKey] = fallback;
                    return fallback;
                }
            }

            return General.Instance.GetTagPrefixPattern();
        }

        /// <summary>
        /// Builds a regex character class pattern from a comma-separated list of prefix characters.
        /// Pure and side-effect free, so it can be exercised directly by unit tests.
        /// </summary>
        internal static string BuildPrefixPattern(string tagPrefixes)
        {
            if (string.IsNullOrWhiteSpace(tagPrefixes))
            {
                return null;
            }

            var chars = new HashSet<char>();
            foreach (var part in tagPrefixes.Split([','], StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 1)
                {
                    chars.Add(trimmed[0]);
                }
            }

            if (chars.Count == 0)
            {
                return null;
            }

            return "[" + Regex.Escape(new string([.. chars])) + "]";
        }

        /// <summary>
        /// Gets the regex cache key combining keywords pattern and prefix pattern.
        /// </summary>
        private static string GetRegexCacheKey(string filePath)
        {
            var keywords = GetAnchorKeywordsPattern(filePath);
            var prefix = GetTagPrefixPattern(filePath);
            return keywords + "|pfx:" + (prefix ?? "");
        }

        /// <summary>
        /// Builds the optional tag prefix regex fragment.
        /// Returns empty string when no prefixes are configured.
        /// Pure and side-effect free, so it can be exercised directly by unit tests.
        /// </summary>
        internal static string BuildPrefixFragment(string prefixPattern)
        {
            if (prefixPattern == null)
            {
                return "";
            }

            // Captures the prefix char in <tagprefix> group, optional with \s* after
            return @"(?<tagprefix>" + prefixPattern + @")?\s*";
        }

        /// <summary>
        /// Builds the optional tag prefix regex fragment for a specific file, resolving the prefix
        /// pattern from .editorconfig/Options first.
        /// </summary>
        private static string GetPrefixFragment(string filePath)
        {
            return BuildPrefixFragment(GetTagPrefixPattern(filePath));
        }

        /// <summary>
        /// Builds a classification regex for anchor tags in a specific file.
        /// Uses caching to avoid recompiling the same regex pattern multiple times.
        /// </summary>
        /// <param name="filePath">The file path to get .editorconfig settings for (can be null).</param>
        /// <returns>A compiled regex for matching anchor tags in comments.</returns>
        public static Regex GetAnchorClassificationRegex(string filePath)
        {
            var cacheKey = GetRegexCacheKey(filePath);
            var p = GetAnchorKeywordsPattern(filePath);
            var prefixPattern = GetTagPrefixPattern(filePath);

            return _classificationRegexCache.GetOrAdd(cacheKey, _ => BuildAnchorClassificationRegex(p, prefixPattern));
        }

        /// <summary>
        /// Builds a classification regex for anchor tags from explicit pattern strings.
        /// Pure and side-effect free, so it can be exercised directly by unit tests without touching
        /// .editorconfig or General.Instance.
        /// </summary>
        /// <param name="keywordsPattern">The alternation pattern of anchor keywords.</param>
        /// <param name="prefixPattern">The optional tag prefix character-class pattern, or null.</param>
        internal static Regex BuildAnchorClassificationRegex(string keywordsPattern, string prefixPattern)
        {
            var pfx = BuildPrefixFragment(prefixPattern);
            var tagPattern = @"(?:(?<tag>\b(?:" + keywordsPattern + @")\b)[:!]?|(?<tag>\b(?i:" + keywordsPattern + @")\b)[:!])";

            return new Regex(
                @"(?<=//\s*)" + pfx + tagPattern + @"|" +
                @"(?<=/\*[\s\*]*)" + pfx + tagPattern + @"|" +
                @"(?<='\s*)" + pfx + tagPattern + @"|" +
                @"(?<=^\s*\*\s*)" + pfx + tagPattern,
                RegexOptions.Compiled | RegexOptions.Multiline);
        }

        /// <summary>
        /// Builds a metadata regex for anchor tags in a specific file.
        /// Uses caching to avoid recompiling the same regex pattern multiple times.
        /// </summary>
        /// <param name="filePath">The file path to get .editorconfig settings for (can be null).</param>
        /// <returns>A compiled regex for matching anchor tags with metadata.</returns>
        public static Regex GetAnchorWithMetadataRegex(string filePath)
        {
            var cacheKey = GetRegexCacheKey(filePath);
            var p = GetAnchorKeywordsPattern(filePath);

            return _metadataRegexCache.GetOrAdd(cacheKey, _ => BuildAnchorWithMetadataRegex(p));
        }

        /// <summary>
        /// Builds a metadata regex for anchor tags from an explicit keywords pattern.
        /// Pure and side-effect free, so it can be exercised directly by unit tests.
        /// </summary>
        /// <param name="keywordsPattern">The alternation pattern of anchor keywords.</param>
        internal static Regex BuildAnchorWithMetadataRegex(string keywordsPattern)
        {
            var metadataTagPattern = @"(?:\b(?:" + keywordsPattern + @")\b|\b(?i:" + keywordsPattern + @")\b(?=\s*(?:\([^)]*\)|\[[^\]]*\])\s*[:!]))";

            return new Regex(
                metadataTagPattern + @"(?<metadata>\s*(?:\([^)]*\)|\[[^\]]*\]))",
                RegexOptions.Compiled);
        }

        /// <summary>
        /// Builds a regex for scanning anchors in comments for the Code Anchors window.
        /// Uses caching to avoid recompiling the same regex pattern multiple times.
        /// </summary>
        /// <param name="filePath">The file path to get .editorconfig settings for (can be null).</param>
        /// <returns>A compiled regex for matching anchor tags with prefix, metadata, and message groups.</returns>
        public static Regex GetAnchorServiceRegex(string filePath)
        {
            var cacheKey = GetRegexCacheKey(filePath);
            var p = GetAnchorKeywordsPattern(filePath);
            var prefixPattern = GetTagPrefixPattern(filePath);

            return _serviceRegexCache.GetOrAdd(cacheKey, _ => BuildAnchorServiceRegex(p, prefixPattern));
        }

        /// <summary>
        /// Builds a service regex for scanning anchors in comments from explicit pattern strings.
        /// Pure and side-effect free, so it can be exercised directly by unit tests without touching
        /// .editorconfig or General.Instance.
        /// </summary>
        /// <param name="keywordsPattern">The alternation pattern of anchor keywords.</param>
        /// <param name="prefixPattern">The optional tag prefix character-class pattern, or null.</param>
        internal static Regex BuildAnchorServiceRegex(string keywordsPattern, string prefixPattern)
        {
            var pfx = BuildPrefixFragment(prefixPattern);
            var serviceTagPattern = @"(?:(?<tag>\b(?:" + keywordsPattern + @")\b)\s*(?<metadata>(?:\([^)]*\)|\[[^\]]*\]))?\s*[:!]?|(?<tag>\b(?i:" + keywordsPattern + @")\b)\s*(?<metadata>(?:\([^)]*\)|\[[^\]]*\]))?\s*[:!])";

            return new Regex(
                @"(?<prefix>//|/\*|'|<!--)\s*" + pfx + serviceTagPattern + @"\s*(?<message>.*?)(?:\*/|-->|$)",
                RegexOptions.Compiled);
        }

        /// <summary>
        /// Checks if the given tag is a recognized anchor tag (built-in or custom).
        /// </summary>
        /// <param name="tag">The tag to check (case-insensitive).</param>
        /// <param name="filePath">The file path to get .editorconfig settings for (can be null).</param>
        /// <returns>True if the tag is recognized.</returns>
        public static bool IsAnchorTag(string tag, string filePath)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            var upperTag = tag.ToUpperInvariant();

            // Check built-in tags first (fast path)
            foreach (var builtIn in BuiltInAnchorTags)
            {
                if (builtIn == upperTag)
                {
                    return true;
                }
            }

            // Check custom tags
            return GetCustomAnchorTags(filePath).Contains(upperTag);
        }

        /// <summary>
        /// Gets the max line length from .editorconfig if defined, otherwise returns the value from Options.
        /// </summary>
        /// <param name="textView">The text view to get .editorconfig settings from.</param>
        /// <returns>The max line length value.</returns>
        public static int GetMaxLineLength(ITextView textView)
        {
            var editorConfigValue = GetMaxLineLengthFromEditorConfig(textView);
            return editorConfigValue ?? General.Instance.MaxLineLength;
        }

        /// <summary>
        /// Gets the max_line_length value from .editorconfig settings for the current file.
        /// Uses the CodingConventionsSnapshot from the text view options.
        /// </summary>
        /// <param name="textView">The text view to get options from.</param>
        /// <returns>The max_line_length value if found, otherwise null.</returns>
        public static int? GetMaxLineLengthFromEditorConfig(ITextView textView)
        {
            try
            {
                // Get the coding conventions from the text view options
                // This contains all .editorconfig properties that apply to the current file
                if (textView?.Options?.GetOptionValue<IReadOnlyDictionary<string, object>>("CodingConventionsSnapshot") is IReadOnlyDictionary<string, object> conventions
                    && conventions.TryGetValue("max_line_length", out var value))
                {
                    if (value is int intValue)
                    {
                        return intValue;
                    }

                    if (value is string stringValue && int.TryParse(stringValue, out var parsed))
                    {
                        return parsed;
                    }
                }
            }
            catch
            {
                // If we can't read from the text view options, just return null
            }

            return null;
        }

        /// <summary>
        /// Creates a CommentReflowEngine configured with the effective settings.
        /// Uses .editorconfig max_line_length if defined, otherwise falls back to Options page.
        /// </summary>
        /// <param name="textView">The text view to get .editorconfig settings from.</param>
        /// <returns>A configured CommentReflowEngine instance.</returns>
        public static CommentReflowEngine CreateReflowEngine(ITextView textView)
        {
            General options = General.Instance;
            return new CommentReflowEngine(
                GetMaxLineLength(textView),
                options.UseCompactStyleForShortSummaries,
                options.PreserveBlankLines);
        }

        /// <summary>
        /// Gets the custom anchor tags for a file, checking .editorconfig first then falling back to Options.
        /// Uses caching to avoid repeated file I/O for .editorconfig parsing.
        /// Cache is invalidated when the Refresh command is invoked or when a .editorconfig file is saved.
        /// </summary>
        /// <param name="filePath">The file path to get settings for (can be null).</param>
        /// <returns>The custom anchor tags as a HashSet.</returns>
        public static HashSet<string> GetCustomAnchorTags(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                // Use directory as cache key since .editorconfig applies to all files in a directory
                var directory = System.IO.Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    // Check cache first
                    if (_customTagsCache.TryGetValue(directory, out HashSet<string> cached))
                    {
                        return cached;
                    }

                    try
                    {
                        FileConfiguration config = _parser.Parse(filePath);
                        if (config.Properties.TryGetValue("custom_anchor_tags", out var value)
                            && !string.IsNullOrWhiteSpace(value))
                        {
                            HashSet<string> tags = ParseCustomTags(value);
                            _customTagsCache[directory] = tags;
                            return tags;
                        }
                        else
                        {
                            // Cache empty result too to avoid re-parsing
                            HashSet<string> fallbackTags = General.Instance.GetCustomTagsSet();
                            _customTagsCache[directory] = fallbackTags;
                            return fallbackTags;
                        }
                    }
                    catch
                    {
                        // Fall back to options on any error
                    }
                }
            }

            return General.Instance.GetCustomTagsSet();
        }

        /// <summary>
        /// Clears the caches. Call when the Refresh command is invoked or when a .editorconfig file is saved.
        /// </summary>
        public static void ClearCaches()
        {
            _classificationRegexCache.Clear();
            _metadataRegexCache.Clear();
            _serviceRegexCache.Clear();
            _customTagsCache.Clear();
            _tagPrefixCache.Clear();
            _enabledCache.Clear();
        }

        private static HashSet<string> ParseCustomTags(string customTags)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(customTags))
            {
                foreach (var tag in customTags.Split([','], StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = tag.Trim().ToUpperInvariant();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        _ = tags.Add(trimmed);
                    }
                }
            }
            return tags;
        }
    }
}
