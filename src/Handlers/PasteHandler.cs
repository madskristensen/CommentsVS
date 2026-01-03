using System.ComponentModel.Composition;
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
    /// Handles paste operations to detect and reflow XML documentation comments.
    /// </summary>
    [Export(typeof(ICommandHandler))]
    [ContentType("text")]
    [Name(nameof(PasteHandler))]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    public sealed class PasteHandler : ICommandHandler<PasteCommandArgs>
    {
        public string DisplayName => "Reflow XML Doc Comments on Paste";

        public bool ExecuteCommand(PasteCommandArgs args, CommandExecutionContext executionContext)
        {
            // Don't block the paste, let it happen first
            // We need to check BEFORE paste where the caret is, then AFTER paste reflow if needed

            var textView = args.TextView;
            var textBuffer = args.SubjectBuffer;

            // Get the current position before paste
            int caretPositionBeforePaste = textView.Caret.Position.BufferPosition.Position;
            var snapshotBeforePaste = textBuffer.CurrentSnapshot;

            // Check if we're in a doc comment before paste
            var contentType = textBuffer.ContentType.TypeName;
            var commentStyle = LanguageCommentStyle.GetForContentType(contentType);

            if (commentStyle == null)
            {
                return false; // Let paste proceed normally
            }

            var parser = new XmlDocCommentParser(commentStyle);
            var blockBeforePaste = parser.FindCommentBlockAtPosition(snapshotBeforePaste, caretPositionBeforePaste);

            if (blockBeforePaste == null)
            {
                return false; // Not in a doc comment, let paste proceed normally
            }

            // Register to handle after paste completes
            // We return false to let the paste happen, then use Changed event
            ITextVersion versionBeforePaste = textBuffer.CurrentSnapshot.Version;

            void OnBufferChanged(object sender, TextContentChangedEventArgs e)
            {
                textBuffer.Changed -= OnBufferChanged;

                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    await ReflowCommentAfterPasteAsync(textView, textBuffer, caretPositionBeforePaste, blockBeforePaste);
                });
            }

            textBuffer.Changed += OnBufferChanged;

            return false; // Let VS proceed with the paste
        }

        public CommandState GetCommandState(PasteCommandArgs args)
        {
            return CommandState.Unspecified;
        }

        /// <summary>
        /// Reflows the comment block after paste has completed.
        /// </summary>
        private static async System.Threading.Tasks.Task ReflowCommentAfterPasteAsync(
            ITextView textView,
            ITextBuffer textBuffer,
            int originalCaretPosition,
            XmlDocCommentBlock originalBlock)
        {
            var options = await General.GetLiveInstanceAsync();
            if (!options.ReflowOnPaste)
            {
                return;
            }

            var snapshot = textBuffer.CurrentSnapshot;
            var contentType = textBuffer.ContentType.TypeName;

            var commentStyle = LanguageCommentStyle.GetForContentType(contentType);
            if (commentStyle == null)
            {
                return;
            }

            var parser = new XmlDocCommentParser(commentStyle);

            // Find the comment block at the caret's current position (after paste)
            int newCaretPosition = textView.Caret.Position.BufferPosition.Position;
            var block = parser.FindCommentBlockAtPosition(snapshot, newCaretPosition);

            if (block == null)
            {
                // Try original position adjusted for paste
                block = parser.FindCommentBlockAtPosition(snapshot, originalCaretPosition);
            }

            if (block == null)
            {
                return;
            }

            var engine = new CommentReflowEngine(
                options.MaxLineLength,
                options.UseCompactStyleForShortSummaries,
                options.PreserveBlankLines);

            var reflowed = engine.ReflowComment(block);

            if (!string.IsNullOrEmpty(reflowed))
            {
                using (var edit = textBuffer.CreateEdit())
                {
                    edit.Replace(block.Span, reflowed);
                    edit.Apply();
                }
            }
        }
    }
}
