using System.ComponentModel.Composition;
using System.Windows.Media;
using CommentsVS.Classification;
using CommentsVS.ToolWindows;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace CommentsVS.Services
{
    /// <summary>
    /// Service that provides colors for comment tags by reading from VS Fonts and Colors settings.
    /// Falls back to default colors if settings are not available.
    /// </summary>
    [Export(typeof(CommentTagColorService))]
    public sealed class CommentTagColorService
    {
        private static CommentTagColorService _instance;

        private readonly IEditorFormatMapService _formatMapService;
        private IEditorFormatMap _formatMap;

        /// <summary>
        /// Gets the singleton instance of the service.
        /// Returns null if MEF composition hasn't occurred yet.
        /// </summary>
        public static CommentTagColorService Instance
        {
            get => _instance;
            internal set => _instance = value;
        }

        [ImportingConstructor]
        public CommentTagColorService(IEditorFormatMapService formatMapService)
        {
            _formatMapService = formatMapService;
            _instance = this;
        }

        /// <summary>
        /// Gets the color for an anchor type from VS Fonts and Colors settings.
        /// </summary>
        /// <param name="anchorType">The anchor type to get the color for.</param>
        /// <returns>The configured color, or default color if not configured.</returns>
        public Color GetColor(AnchorType anchorType)
        {
            Color? configuredColor = GetConfiguredColor(anchorType);
            return configuredColor ?? GetDefaultColor(anchorType);
        }

        /// <summary>
        /// Gets the color configured in VS Fonts and Colors settings for the specified anchor type.
        /// </summary>
        /// <param name="anchorType">The anchor type.</param>
        /// <returns>The configured color, or null if not set or not available.</returns>
        private Color? GetConfiguredColor(AnchorType anchorType)
        {
            string classificationTypeName = GetClassificationTypeName(anchorType);
            if (string.IsNullOrEmpty(classificationTypeName))
            {
                return null;
            }

            try
            {
                // Get or create the format map (lazy initialization)
                _formatMap ??= _formatMapService?.GetEditorFormatMap("text");

                if (_formatMap == null)
                {
                    return null;
                }

                // Get the format properties for this classification type
                var properties = _formatMap.GetProperties(classificationTypeName);
                if (properties == null)
                {
                    return null;
                }

                // Try to get the foreground color
                if (properties.Contains(EditorFormatDefinition.ForegroundColorId))
                {
                    object colorObj = properties[EditorFormatDefinition.ForegroundColorId];
                    if (colorObj is Color color)
                    {
                        return color;
                    }
                }

                // Try the foreground brush as fallback
                if (properties.Contains(EditorFormatDefinition.ForegroundBrushId))
                {
                    object brushObj = properties[EditorFormatDefinition.ForegroundBrushId];
                    if (brushObj is SolidColorBrush brush)
                    {
                        return brush.Color;
                    }
                }
            }
            catch
            {
                // If anything goes wrong, fall back to default
            }

            return null;
        }

        /// <summary>
        /// Maps an anchor type to its classification type name (as registered in Fonts and Colors).
        /// </summary>
        private static string GetClassificationTypeName(AnchorType anchorType)
        {
            return anchorType switch
            {
                AnchorType.Todo => CommentTagClassificationTypes.Todo,
                AnchorType.Hack => CommentTagClassificationTypes.Hack,
                AnchorType.Note => CommentTagClassificationTypes.Note,
                AnchorType.Bug => CommentTagClassificationTypes.Bug,
                AnchorType.Fixme => CommentTagClassificationTypes.Fixme,
                AnchorType.Undone => CommentTagClassificationTypes.Undone,
                AnchorType.Review => CommentTagClassificationTypes.Review,
                AnchorType.Anchor => CommentTagClassificationTypes.Anchor,
                AnchorType.Custom => CommentTagClassificationTypes.Custom,
                _ => null
            };
        }

        /// <summary>
        /// Gets the default (hardcoded) color for an anchor type.
        /// These are the original colors used before this service existed.
        /// </summary>
        public static Color GetDefaultColor(AnchorType anchorType)
        {
            return anchorType switch
            {
                AnchorType.Todo => Colors.Orange,
                AnchorType.Hack => Colors.Crimson,
                AnchorType.Note => Colors.LimeGreen,
                AnchorType.Bug => Colors.Red,
                AnchorType.Fixme => Colors.OrangeRed,
                AnchorType.Undone => Colors.MediumPurple,
                AnchorType.Review => Colors.DodgerBlue,
                AnchorType.Anchor => Colors.Teal,
                AnchorType.Custom => Colors.Goldenrod,
                _ => Colors.Gray
            };
        }
    }
}
