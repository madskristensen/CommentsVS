using System.Collections.Generic;
using System.ComponentModel.Composition;
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
        ICommandHandler<EscapeKeyCommandArgs>
    {
        public string DisplayName => "Handle Down Arrow Key in Rendered Comments";

        // Down Arrow: Hide rendering
        public bool ExecuteCommand(EscapeKeyCommandArgs args, CommandExecutionContext executionContext)
        {
            // Only handle if in Full rendering mode
            if (General.Instance.CommentRenderingMode == RenderingMode.Off)
            {
                return false; // Let VS handle arrow key normally
            }

            // Cast to IWpfTextView if possible
            if (args.TextView is not IWpfTextView wpfTextView)
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
            ITextSnapshot snapshot = args.SubjectBuffer.CurrentSnapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);

            if (commentStyle == null)
            {
                return false;
            }

            var parser = new XmlDocCommentParser(commentStyle);
            IReadOnlyList<XmlDocCommentBlock> blocks = parser.FindAllCommentBlocks(snapshot);

            // Find if caret is within any rendered comment
            foreach (XmlDocCommentBlock block in blocks)
            {
                if (caretLine >= block.StartLine && caretLine <= block.EndLine)
                {
                    // We're in a rendered comment - hide it
                    return tagger.HandleEscapeKey(block.StartLine);
                }
            }

            return false; // Let VS handle arrow key normally
        }

        public CommandState GetCommandState(EscapeKeyCommandArgs args)
        {
            return CommandState.Unspecified;
        }
    }
}
