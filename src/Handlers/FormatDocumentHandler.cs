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
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                var options = await General.GetLiveInstanceAsync();
                if (!options.ReflowOnFormatDocument)
                {
                    return CommandProgression.Continue;
                }

                DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
                if (docView?.TextBuffer == null)
                {
                    return CommandProgression.Continue;
                }

                ITextBuffer buffer = docView.TextBuffer;
                ITextView textView = docView.TextView;

                string contentType = buffer.ContentType.TypeName;
                var commentStyle = LanguageCommentStyle.GetForContentType(contentType);

                if (commentStyle == null)
                {
                    return CommandProgression.Continue;
                }

                try
                {
                    ReflowComments(buffer, textView, commentStyle, options, formatSelection);
                    return CommandProgression.Continue;
                }
                catch
                {
                    return CommandProgression.Continue;
                }
            });
        }

        private static void ReflowComments(
            ITextBuffer buffer,
            ITextView textView,
            LanguageCommentStyle commentStyle,
            General options,
            bool selectionOnly)
        {
            var snapshot = buffer.CurrentSnapshot;
            var parser = new XmlDocCommentParser(commentStyle);
            var engine = new CommentReflowEngine(
                options.MaxLineLength,
                options.UseCompactStyleForShortSummaries,
                options.PreserveBlankLines);

            System.Collections.Generic.IReadOnlyList<XmlDocCommentBlock> blocks;

            if (selectionOnly && !textView.Selection.IsEmpty)
            {
                var selectionSpan = textView.Selection.SelectedSpans[0];
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

            using (var edit = buffer.CreateEdit())
            {
                for (int i = blocks.Count - 1; i >= 0; i--)
                {
                    var block = blocks[i];
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
