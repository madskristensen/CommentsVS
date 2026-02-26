using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Classification
{
    /// <summary>
    /// Classification type definitions for comment tags.
    /// </summary>
    internal static class CommentTagClassificationTypeDefinitions
    {
        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Todo)]
        internal static ClassificationTypeDefinition TodoType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Hack)]
        internal static ClassificationTypeDefinition HackType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Note)]
        internal static ClassificationTypeDefinition NoteType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Bug)]
        internal static ClassificationTypeDefinition BugType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Fixme)]
        internal static ClassificationTypeDefinition FixmeType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Undone)]
        internal static ClassificationTypeDefinition UndoneType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Review)]
        internal static ClassificationTypeDefinition ReviewType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Anchor)]
        internal static ClassificationTypeDefinition AnchorType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Custom)]
        internal static ClassificationTypeDefinition CustomType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Metadata)]
        internal static ClassificationTypeDefinition MetadataType = null;

        // Prefix-based comment highlighting (Better Comments style)
        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.PrefixAlert)]
        internal static ClassificationTypeDefinition PrefixAlertType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.PrefixQuery)]
        internal static ClassificationTypeDefinition PrefixQueryType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.PrefixImportant)]
        internal static ClassificationTypeDefinition PrefixImportantType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.PrefixStrikethrough)]
        internal static ClassificationTypeDefinition PrefixStrikethroughType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.PrefixDisabled)]
        internal static ClassificationTypeDefinition PrefixDisabledType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.PrefixQuote)]
        internal static ClassificationTypeDefinition PrefixQuoteType = null;
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.Todo)]
    [Name(CommentTagClassificationTypes.Todo)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class TodoFormatDefinition : ClassificationFormatDefinition
    {
        public TodoFormatDefinition()
        {
            DisplayName = "Comment Tag - TODO";
            ForegroundColor = Colors.Orange;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.Hack)]
    [Name(CommentTagClassificationTypes.Hack)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class HackFormatDefinition : ClassificationFormatDefinition
    {
        public HackFormatDefinition()
        {
            DisplayName = "Comment Tag - HACK";
            ForegroundColor = Colors.Crimson;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.Note)]
    [Name(CommentTagClassificationTypes.Note)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class NoteFormatDefinition : ClassificationFormatDefinition
    {
        public NoteFormatDefinition()
        {
            DisplayName = "Comment Tag - NOTE";
            ForegroundColor = Colors.LimeGreen;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.Bug)]
    [Name(CommentTagClassificationTypes.Bug)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class BugFormatDefinition : ClassificationFormatDefinition
    {
        public BugFormatDefinition()
        {
            DisplayName = "Comment Tag - BUG";
            ForegroundColor = Colors.Red;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.Fixme)]
    [Name(CommentTagClassificationTypes.Fixme)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class FixmeFormatDefinition : ClassificationFormatDefinition
    {
        public FixmeFormatDefinition()
        {
            DisplayName = "Comment Tag - FIXME";
            ForegroundColor = Colors.OrangeRed;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.Undone)]
    [Name(CommentTagClassificationTypes.Undone)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class UndoneFormatDefinition : ClassificationFormatDefinition
    {
        public UndoneFormatDefinition()
        {
            DisplayName = "Comment Tag - UNDONE";
            ForegroundColor = Colors.MediumPurple;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.Review)]
    [Name(CommentTagClassificationTypes.Review)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class ReviewFormatDefinition : ClassificationFormatDefinition
    {
        public ReviewFormatDefinition()
        {
            DisplayName = "Comment Tag - REVIEW";
            ForegroundColor = Colors.DodgerBlue;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.Anchor)]
    [Name(CommentTagClassificationTypes.Anchor)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class AnchorFormatDefinition : ClassificationFormatDefinition
    {
        public AnchorFormatDefinition()
        {
            DisplayName = "Comment Tag - ANCHOR";
            // Teal works well in both light and dark themes
            ForegroundColor = Colors.Teal;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.Custom)]
    [Name(CommentTagClassificationTypes.Custom)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class CustomTagFormatDefinition : ClassificationFormatDefinition
    {
        public CustomTagFormatDefinition()
        {
            DisplayName = "Comment Tag - Custom";
            // Goldenrod works well in both light and dark themes
            ForegroundColor = Colors.Goldenrod;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.Metadata)]
    [Name(CommentTagClassificationTypes.Metadata)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class CommentTagMetadataFormatDefinition : ClassificationFormatDefinition
    {
        public CommentTagMetadataFormatDefinition()
        {
            DisplayName = "Comment Tag - Metadata";
            // Teal/cyan works well in both light and dark themes
            ForegroundColor = Color.FromRgb(0, 128, 128);
        }
    }

    #region Prefix-based comment highlighting (Better Comments style)

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.PrefixAlert)]
    [Name(CommentTagClassificationTypes.PrefixAlert)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class PrefixAlertFormatDefinition : ClassificationFormatDefinition
    {
        public PrefixAlertFormatDefinition()
        {
            DisplayName = "Comment - Alert (!)";
            ForegroundColor = Colors.Red;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.PrefixQuery)]
    [Name(CommentTagClassificationTypes.PrefixQuery)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class PrefixQueryFormatDefinition : ClassificationFormatDefinition
    {
        public PrefixQueryFormatDefinition()
        {
            DisplayName = "Comment - Query (?)";
            ForegroundColor = Colors.DodgerBlue;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.PrefixImportant)]
    [Name(CommentTagClassificationTypes.PrefixImportant)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class PrefixImportantFormatDefinition : ClassificationFormatDefinition
    {
        public PrefixImportantFormatDefinition()
        {
            DisplayName = "Comment - Important (*)";
            ForegroundColor = Colors.LimeGreen;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.PrefixStrikethrough)]
    [Name(CommentTagClassificationTypes.PrefixStrikethrough)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class PrefixStrikethroughFormatDefinition : ClassificationFormatDefinition
    {
        public PrefixStrikethroughFormatDefinition()
        {
            DisplayName = "Comment - Strikethrough (//)";
            ForegroundColor = Colors.Gray;
            // Don't set TextDecorations here - VS doesn't properly persist "no strikethrough"
            // when users remove it. Users can opt-in to strikethrough via Fonts & Colors.
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.PrefixDisabled)]
    [Name(CommentTagClassificationTypes.PrefixDisabled)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class PrefixDisabledFormatDefinition : ClassificationFormatDefinition
    {
        public PrefixDisabledFormatDefinition()
        {
            DisplayName = "Comment - Disabled (-)";
            ForegroundColor = Colors.DarkGray;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.PrefixQuote)]
    [Name(CommentTagClassificationTypes.PrefixQuote)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class PrefixQuoteFormatDefinition : ClassificationFormatDefinition
    {
        public PrefixQuoteFormatDefinition()
        {
            DisplayName = "Comment - Quote (>)";
            ForegroundColor = Colors.MediumPurple;
            IsItalic = true;
        }
    }


    #endregion

    #region Rendered comment format definitions
    public class MoreDefinitions
    {
        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.RenderedText)]
        internal static ClassificationTypeDefinition RenderedTextType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.RenderedHeading)]
        internal static ClassificationTypeDefinition RenderedHeadingType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.RenderedCode)]
        internal static ClassificationTypeDefinition RenderedCodeType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.RenderedLink)]
        internal static ClassificationTypeDefinition RenderedLinkType = null;
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.RenderedText)]
    [Name(CommentTagClassificationTypes.RenderedText)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class RenderedTextFormatDefinition : ClassificationFormatDefinition
    {
        public RenderedTextFormatDefinition()
        {
            DisplayName = "Rendered Comment - Text";
            // Gray works well for subtle comment text in both themes
            ForegroundColor = Color.FromRgb(128, 128, 128);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.RenderedHeading)]
    [Name(CommentTagClassificationTypes.RenderedHeading)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class RenderedHeadingFormatDefinition : ClassificationFormatDefinition
    {
        public RenderedHeadingFormatDefinition()
        {
            DisplayName = "Rendered Comment - Heading";
            // Green matches VS comment color association
            ForegroundColor = Color.FromRgb(87, 166, 74);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.RenderedCode)]
    [Name(CommentTagClassificationTypes.RenderedCode)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class RenderedCodeFormatDefinition : ClassificationFormatDefinition
    {
        public RenderedCodeFormatDefinition()
        {
            DisplayName = "Rendered Comment - Code";
            // Brownish color for inline code
            ForegroundColor = Color.FromRgb(156, 120, 100);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = CommentTagClassificationTypes.RenderedLink)]
    [Name(CommentTagClassificationTypes.RenderedLink)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class RenderedLinkFormatDefinition : ClassificationFormatDefinition
    {
        public RenderedLinkFormatDefinition()
        {
            DisplayName = "Rendered Comment - Link";
            // Blue for links and references
            ForegroundColor = Color.FromRgb(86, 156, 214);
        }
    }

    #endregion
}