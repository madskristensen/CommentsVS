using System.Windows.Media;
using CommentsVS.Options;
using CommentsVS.Services;
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
        Anchor,
        Custom
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
                AnchorType.Custom => "CUSTOM",
                _ => anchorType.ToString().ToUpperInvariant()
            };
        }

        /// <summary>
        /// Gets the color for the anchor type from VS Fonts and Colors settings.
        /// Falls back to default colors if settings are not available.
        /// </summary>
        public static Color GetColor(this AnchorType anchorType)
        {
            // Try to get color from VS Fonts and Colors settings via the service
            CommentTagColorService colorService = CommentTagColorService.Instance;
            if (colorService != null)
            {
                return colorService.GetColor(anchorType);
            }

            // Fallback to default colors if service is not available
            return CommentTagColorService.GetDefaultColor(anchorType);
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
                AnchorType.Custom => KnownMonikers.Tag,
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

                /// <summary>
                /// Parses a string to an <see cref="AnchorType"/>, including custom tags.
                /// </summary>
                /// <param name="value">The string value (case-insensitive).</param>
                /// <param name="filePath">The file path for .editorconfig lookup.</param>
                /// <param name="customTagName">If the tag is a custom tag, outputs the tag name in uppercase.</param>
                /// <returns>The parsed anchor type, or null if not recognized.</returns>
                public static AnchorType? ParseWithCustom(string value, string filePath, out string customTagName)
                {
                    customTagName = null;

                    if (string.IsNullOrEmpty(value))
                    {
                        return null;
                    }

                    var upperValue = value.ToUpperInvariant();

                    // Check built-in tags first
                    AnchorType? builtInType = upperValue switch
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

                    if (builtInType != null)
                    {
                        return builtInType;
                    }

                    // Check if it's a custom tag (from .editorconfig or Options page)
                    if (EditorConfigSettings.GetCustomAnchorTags(filePath).Contains(upperValue))
                    {
                        customTagName = upperValue;
                        return AnchorType.Custom;
                    }

                    return null;
                }
            }
        }
