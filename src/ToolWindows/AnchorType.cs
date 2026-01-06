using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Types of anchors that can be found in comments.
    /// </summary>
    public enum AnchorType
    {
        Todo,
        Hack,
        Note,
        Bug,
        Fixme,
        Undone,
        Review,
        Anchor
    }

    /// <summary>
    /// Extension methods for <see cref="AnchorType"/>.
    /// </summary>
    public static class AnchorTypeExtensions
    {
        /// <summary>
        /// Gets the display name for the anchor type.
        /// </summary>
        public static string GetDisplayName(this AnchorType anchorType)
        {
            return anchorType switch
            {
                AnchorType.Todo => "TODO",
                AnchorType.Hack => "HACK",
                AnchorType.Note => "NOTE",
                AnchorType.Bug => "BUG",
                AnchorType.Fixme => "FIXME",
                AnchorType.Undone => "UNDONE",
                AnchorType.Review => "REVIEW",
                AnchorType.Anchor => "ANCHOR",
                _ => anchorType.ToString().ToUpperInvariant()
            };
        }

        /// <summary>
        /// Gets the image moniker for the anchor type icon.
        /// </summary>
        public static ImageMoniker GetImageMoniker(this AnchorType anchorType)
        {
            return anchorType switch
            {
                AnchorType.Todo => KnownMonikers.Checklist,
                AnchorType.Hack => KnownMonikers.StatusWarning,
                AnchorType.Note => KnownMonikers.StatusInformation,
                AnchorType.Bug => KnownMonikers.Bug,
                AnchorType.Fixme => KnownMonikers.StatusError,
                AnchorType.Undone => KnownMonikers.Undo,
                AnchorType.Review => KnownMonikers.CodeReview,
                AnchorType.Anchor => KnownMonikers.Bookmark,
                _ => KnownMonikers.QuestionMark
            };
        }

        /// <summary>
        /// Parses a string to an <see cref="AnchorType"/>.
        /// </summary>
        /// <param name="value">The string value (case-insensitive).</param>
        /// <returns>The parsed anchor type, or null if not recognized.</returns>
        public static AnchorType? Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return value.ToUpperInvariant() switch
            {
                "TODO" => AnchorType.Todo,
                "HACK" => AnchorType.Hack,
                "NOTE" => AnchorType.Note,
                "BUG" => AnchorType.Bug,
                "FIXME" => AnchorType.Fixme,
                "UNDONE" => AnchorType.Undone,
                "REVIEW" => AnchorType.Review,
                "ANCHOR" => AnchorType.Anchor,
                _ => null
            };
        }
    }
}
