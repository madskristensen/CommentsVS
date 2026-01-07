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
        [Name(CommentTagClassificationTypes.Metadata)]
        internal static ClassificationTypeDefinition MetadataType = null;
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
}
