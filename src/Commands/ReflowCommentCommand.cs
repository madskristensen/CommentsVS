using CommentsVS.Services;
using Microsoft.VisualStudio.Text;

namespace CommentsVS.Commands
{
    [Command(PackageIds.ReflowComment)]
    internal sealed class ReflowCommentCommand : BaseCommand<ReflowCommentCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView?.TextBuffer == null || docView.TextView == null)
            {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            ITextBuffer buffer = docView.TextBuffer;
            if (!EditorConfigSettings.IsEnabled(TextBufferHelper.GetFilePath(buffer)))
            {
                return;
            }

            LanguageCommentStyle commentStyle = LanguageCommentStyle.GetForContentType(buffer.ContentType);
            if (commentStyle == null)
            {
                return;
            }

            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            var caretPosition = docView.TextView.Caret.Position.BufferPosition.Position;
            var parser = new XmlDocCommentParser(commentStyle);
            XmlDocCommentBlock block = parser.FindCommentBlockAtPosition(snapshot, caretPosition);
            if (block == null)
            {
                return;
            }

            CommentReflowEngine engine = EditorConfigSettings.CreateReflowEngine(docView.TextView);
            string reflowed = engine.ReflowComment(block);
            if (string.IsNullOrEmpty(reflowed))
            {
                return;
            }

            string originalText = snapshot.GetText(block.Span);
            if (string.Equals(originalText, reflowed, StringComparison.Ordinal))
            {
                return;
            }

            int? mappedCaretOffset = CommentCaretMapper.MapCaretOffset(
                originalText,
                reflowed,
                caretPosition - block.Span.Start,
                block.CommentStyle,
                block.IsMultiLineStyle);

            using (ITextEdit edit = buffer.CreateEdit())
            {
                _ = edit.Replace(block.Span, reflowed);
                _ = edit.Apply();
            }

            if (mappedCaretOffset.HasValue)
            {
                ITextSnapshot updatedSnapshot = buffer.CurrentSnapshot;
                var updatedPosition = Math.Min(
                    block.Span.Start + mappedCaretOffset.Value,
                    updatedSnapshot.Length);
                _ = docView.TextView.Caret.MoveTo(new SnapshotPoint(updatedSnapshot, updatedPosition));
            }
        }
    }
}
