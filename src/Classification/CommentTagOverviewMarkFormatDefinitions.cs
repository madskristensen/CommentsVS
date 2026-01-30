using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Classification
{
    /// <summary>
    /// Format definition names for overview mark (scrollbar marker) colors.
    /// These must match the Name attribute on each format definition class.
    /// </summary>
    internal static class OverviewMarkFormatNames
    {
        public const string Todo = "CommentTag.OverviewMark.TODO";
        public const string Hack = "CommentTag.OverviewMark.HACK";
        public const string Note = "CommentTag.OverviewMark.NOTE";
        public const string Bug = "CommentTag.OverviewMark.BUG";
        public const string Fixme = "CommentTag.OverviewMark.FIXME";
        public const string Undone = "CommentTag.OverviewMark.UNDONE";
        public const string Review = "CommentTag.OverviewMark.REVIEW";
        public const string Anchor = "CommentTag.OverviewMark.ANCHOR";
        public const string Custom = "CommentTag.OverviewMark.Custom";
    }

    #region Overview Mark Format Definitions

    [Export(typeof(EditorFormatDefinition))]
    [Name(OverviewMarkFormatNames.Todo)]
    [UserVisible(true)]
    internal sealed class TodoOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public TodoOverviewMarkFormatDefinition()
        {
            DisplayName = "Comment Tag Scrollbar Mark - TODO";
            BackgroundColor = Colors.Orange;
            ForegroundColor = Colors.Orange;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(OverviewMarkFormatNames.Hack)]
    [UserVisible(true)]
    internal sealed class HackOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public HackOverviewMarkFormatDefinition()
        {
            DisplayName = "Comment Tag Scrollbar Mark - HACK";
            BackgroundColor = Colors.Crimson;
            ForegroundColor = Colors.Crimson;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(OverviewMarkFormatNames.Note)]
    [UserVisible(true)]
    internal sealed class NoteOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public NoteOverviewMarkFormatDefinition()
        {
            DisplayName = "Comment Tag Scrollbar Mark - NOTE";
            BackgroundColor = Colors.LimeGreen;
            ForegroundColor = Colors.LimeGreen;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(OverviewMarkFormatNames.Bug)]
    [UserVisible(true)]
    internal sealed class BugOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public BugOverviewMarkFormatDefinition()
        {
            DisplayName = "Comment Tag Scrollbar Mark - BUG";
            BackgroundColor = Colors.Red;
            ForegroundColor = Colors.Red;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(OverviewMarkFormatNames.Fixme)]
    [UserVisible(true)]
    internal sealed class FixmeOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public FixmeOverviewMarkFormatDefinition()
        {
            DisplayName = "Comment Tag Scrollbar Mark - FIXME";
            BackgroundColor = Colors.OrangeRed;
            ForegroundColor = Colors.OrangeRed;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(OverviewMarkFormatNames.Undone)]
    [UserVisible(true)]
    internal sealed class UndoneOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public UndoneOverviewMarkFormatDefinition()
        {
            DisplayName = "Comment Tag Scrollbar Mark - UNDONE";
            BackgroundColor = Colors.MediumPurple;
            ForegroundColor = Colors.MediumPurple;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(OverviewMarkFormatNames.Review)]
    [UserVisible(true)]
    internal sealed class ReviewOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public ReviewOverviewMarkFormatDefinition()
        {
            DisplayName = "Comment Tag Scrollbar Mark - REVIEW";
            BackgroundColor = Colors.DodgerBlue;
            ForegroundColor = Colors.DodgerBlue;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(OverviewMarkFormatNames.Anchor)]
    [UserVisible(true)]
    internal sealed class AnchorOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public AnchorOverviewMarkFormatDefinition()
        {
            DisplayName = "Comment Tag Scrollbar Mark - ANCHOR";
            BackgroundColor = Colors.Teal;
            ForegroundColor = Colors.Teal;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(OverviewMarkFormatNames.Custom)]
    [UserVisible(true)]
    internal sealed class CustomOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public CustomOverviewMarkFormatDefinition()
        {
            DisplayName = "Comment Tag Scrollbar Mark - Custom";
            BackgroundColor = Colors.Goldenrod;
            ForegroundColor = Colors.Goldenrod;
        }
    }

    #endregion
}
