using System.Collections.Generic;
using CommentsVS.Options;

namespace CommentsVS.Services
{
    /// <summary>
    /// Shared constants used across the extension.
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// Maximum file size (in characters) to process. Files larger than this are skipped for performance.
        /// </summary>
        public const int MaxFileSize = 150_000;

        /// <summary>
        /// Built-in anchor keywords used for comment tag detection and classification.
        /// </summary>
        public static readonly string[] BuiltInAnchorKeywords = ["TODO", "HACK", "NOTE", "BUG", "FIXME", "UNDONE", "REVIEW", "ANCHOR"];

        /// <summary>
        /// Anchor keywords used for comment tag detection and classification.
        /// Includes both built-in and custom tags from settings.
        /// </summary>
        public static readonly string[] AnchorKeywords = BuiltInAnchorKeywords;

        /// <summary>
        /// Gets all anchor keywords including custom tags from settings.
        /// </summary>
        /// <returns>Array of all anchor keywords (built-in + custom).</returns>
        public static IReadOnlyList<string> GetAllAnchorKeywords()
        {
            HashSet<string> customTags = General.Instance.GetCustomTagsSet();
            if (customTags.Count == 0)
            {
                return BuiltInAnchorKeywords;
            }

            return [.. BuiltInAnchorKeywords, .. customTags];
        }
    }
}
