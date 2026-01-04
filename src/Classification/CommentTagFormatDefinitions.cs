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
        internal static ClassificationTypeDefinition TodoType;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Hack)]
        internal static ClassificationTypeDefinition HackType;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Note)]
        internal static ClassificationTypeDefinition NoteType;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Bug)]
        internal static ClassificationTypeDefinition BugType;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Fixme)]
        internal static ClassificationTypeDefinition FixmeType;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Undone)]
        internal static ClassificationTypeDefinition UndoneType;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CommentTagClassificationTypes.Review)]
        internal static ClassificationTypeDefinition ReviewType;
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
}
