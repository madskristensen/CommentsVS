using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace CommentsVS.Handlers
{
    /// <summary>
    /// Handles Format Document command to reflow XML documentation comments.
    /// </summary>
    internal static class FormatDocumentHandler
    {
        public static async Task RegisterAsync()
        {
            await VS.Commands.InterceptAsync(
                KnownCommands.Edit_FormatDocument.Guid,
                KnownCommands.Edit_FormatDocument.ID,
                () => ExecuteFormat(formatSelection: false));

            await VS.Commands.InterceptAsync(
                KnownCommands.Edit_FormatSelection.Guid,
                KnownCommands.Edit_FormatSelection.ID,
                () => ExecuteFormat(formatSelection: true));
        }

        private static CommandProgression ExecuteFormat(bool formatSelection)
        {
            // Check sync-accessible option first to avoid unnecessary async work
            if (!General.Instance.ReflowOnFormatDocument)
            {
                return CommandProgression.Continue;
            }

            // The intercept callback must return synchronously, so we use JoinableTaskFactory.Run
            // but minimize async work by checking options synchronously first
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
                if (docView?.TextBuffer == null)
                {
                    return CommandProgression.Continue;
                }

                ITextBuffer buffer = docView.TextBuffer;
                ITextView textView = docView.TextView;

                if (!EditorConfigSettings.IsEnabled(TextBufferHelper.GetFilePath(buffer)))
                {
                    return CommandProgression.Continue;
                }

                var contentType = buffer.ContentType.TypeName;
                var commentStyle = LanguageCommentStyle.GetForContentType(contentType);

                if (commentStyle == null)
                {
                    return CommandProgression.Continue;
                }

                try
                {
                    ReflowComments(buffer, textView, commentStyle, formatSelection);
                }
                catch (Exception ex)
                {
                    await ex.LogAsync();
                }

                return CommandProgression.Continue;
            });
        }

        private static void ReflowComments(
            ITextBuffer buffer,
            ITextView textView,
            LanguageCommentStyle commentStyle,
            bool selectionOnly)
        {
            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            var parser = new XmlDocCommentParser(commentStyle);
            CommentReflowEngine engine = EditorConfigSettings.CreateReflowEngine(textView);

            System.Collections.Generic.IReadOnlyList<XmlDocCommentBlock> blocks;

            if (selectionOnly && !textView.Selection.IsEmpty)
            {
                SnapshotSpan selectionSpan = textView.Selection.SelectedSpans[0];
                var span = new Span(selectionSpan.Start.Position, selectionSpan.Length);
                blocks = parser.FindCommentBlocksInSpan(snapshot, span);
            }
            else
            {
                blocks = parser.FindAllCommentBlocks(snapshot);
            }

            if (blocks.Count == 0)
            {
                return;
            }

            using (ITextEdit edit = buffer.CreateEdit())
            {
                for (var i = blocks.Count - 1; i >= 0; i--)
                {
                    XmlDocCommentBlock block = blocks[i];
                    var reflowed = engine.ReflowComment(block);

                    if (!string.IsNullOrEmpty(reflowed))
                    {
                        edit.Replace(block.Span, reflowed);
                    }
                }

                edit.Apply();
            }
        }
    }
}
