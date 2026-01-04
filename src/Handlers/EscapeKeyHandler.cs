using System.ComponentModel.Composition;
using System.Linq;
using CommentsVS.Adornments;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Handlers
{
    /// <summary>
    /// Handles arrow keys to expand/collapse/hide rendered comments.
    /// Right Arrow: Expand, Left Arrow: Collapse, Down Arrow: Hide rendering
    /// </summary>
    [Export(typeof(ICommandHandler))]
    [ContentType("CSharp")]
    [ContentType("Basic")]
    [Name(nameof(RenderedCommentNavigationHandler))]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class RenderedCommentNavigationHandler : 
        ICommandHandler<LeftKeyCommandArgs>,
        ICommandHandler<RightKeyCommandArgs>,
        ICommandHandler<DownKeyCommandArgs>
    {
        public string DisplayName => "Handle Arrow Keys in Rendered Comments";

        // Right Arrow: Expand comment
        public bool ExecuteCommand(RightKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            return HandleArrowKey(args.TextView, args.SubjectBuffer, ArrowAction.Expand);
        }

        public CommandState GetCommandState(RightKeyCommandArgs args)
        {
            return CommandState.Unspecified;
        }

        // Left Arrow: Collapse comment
        public bool ExecuteCommand(LeftKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            return HandleArrowKey(args.TextView, args.SubjectBuffer, ArrowAction.Collapse);
        }

        public CommandState GetCommandState(LeftKeyCommandArgs args)
        {
            return CommandState.Unspecified;
        }

        // Down Arrow: Hide rendering
        public bool ExecuteCommand(DownKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            return HandleArrowKey(args.TextView, args.SubjectBuffer, ArrowAction.Hide);
        }

        public CommandState GetCommandState(DownKeyCommandArgs args)
        {
            return CommandState.Unspecified;
        }

        private enum ArrowAction
        {
            Expand,
            Collapse,
            Hide
        }

        private bool HandleArrowKey(ITextView textView, ITextBuffer textBuffer, ArrowAction action)
        {
            // Only handle if rendered comments are enabled
            if (!General.Instance.EnableRenderedComments)
            {
                return false; // Let VS handle arrow key normally
            }

            // Cast to IWpfTextView if possible
            if (!(textView is IWpfTextView wpfTextView))
            {
                return false;
            }

            // Check if we have the tagger for this view
            if (!wpfTextView.Properties.TryGetProperty(typeof(RenderedCommentIntraTextTagger), out RenderedCommentIntraTextTagger tagger))
            {
                return false;
            }

            // Get current caret line
            var caretLine = wpfTextView.Caret.Position.BufferPosition.GetContainingLine().LineNumber;
            var snapshot = textBuffer.CurrentSnapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);

            if (commentStyle == null)
            {
                return false;
            }

            var parser = new XmlDocCommentParser(commentStyle);
            var blocks = parser.FindAllCommentBlocks(snapshot);

            // Find if caret is within any rendered comment
            foreach (var block in blocks)
            {
                if (caretLine >= block.StartLine && caretLine <= block.EndLine)
                {
                    // We're in a rendered comment - handle the arrow key
                    switch (action)
                    {
                        case ArrowAction.Expand:
                            return tagger.ExpandComment(block.StartLine);
                        case ArrowAction.Collapse:
                            return tagger.CollapseComment(block.StartLine);
                        case ArrowAction.Hide:
                            return tagger.HandleEscapeKey(block.StartLine);
                    }
                }
            }

            return false; // Let VS handle arrow key normally
        }
    }
}
