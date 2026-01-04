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
    /// Handles Down arrow key to hide rendered comments.
    /// Down Arrow: Hide rendering
    /// </summary>
    [Export(typeof(ICommandHandler))]
    [ContentType("CSharp")]
    [ContentType("Basic")]
    [Name(nameof(RenderedCommentNavigationHandler))]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class RenderedCommentNavigationHandler : 
        ICommandHandler<DownKeyCommandArgs>
    {
        public string DisplayName => "Handle Down Arrow Key in Rendered Comments";

        // Down Arrow: Hide rendering
        public bool ExecuteCommand(DownKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            // Only handle if rendered comments are enabled
            if (!General.Instance.EnableRenderedComments)
            {
                return false; // Let VS handle arrow key normally
            }

            // Cast to IWpfTextView if possible
            if (!(args.TextView is IWpfTextView wpfTextView))
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
            var snapshot = args.SubjectBuffer.CurrentSnapshot;
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
                    // We're in a rendered comment - hide it
                    return tagger.HandleEscapeKey(block.StartLine);
                }
            }

            return false; // Let VS handle arrow key normally
        }

        public CommandState GetCommandState(DownKeyCommandArgs args)
        {
            return CommandState.Unspecified;
        }
    }
}
