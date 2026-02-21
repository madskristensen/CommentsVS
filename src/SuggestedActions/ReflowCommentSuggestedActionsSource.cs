using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.SuggestedActions
{
    /// <summary>
    /// MEF provider that creates the suggested actions source for XML doc comment reflow.
    /// </summary>
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("Reflow XML Doc Comment Suggested Actions")]
    [ContentType("text")]
    internal sealed class ReflowCommentSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
    {
        [Import(typeof(ITextStructureNavigatorSelectorService))]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        [Import]
        internal IOutliningManagerService OutliningManagerService { get; set; }

        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
        {
            if (textView == null || textBuffer == null)
            {
                return null;
            }

            IOutliningManager outliningManager = OutliningManagerService?.GetOutliningManager(textView);

            return new ReflowCommentSuggestedActionsSource(textView, textBuffer, outliningManager);
        }
    }

    /// <summary>
    /// Provides suggested actions for reflowing XML documentation comments.
    /// </summary>
    internal sealed class ReflowCommentSuggestedActionsSource(
        ITextView textView,
        ITextBuffer textBuffer,
        IOutliningManager outliningManager) : ISuggestedActionsSource
    {
        public event EventHandler<EventArgs> SuggestedActionsChanged { add { } remove { } }

        public void Dispose()
        {
        }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(
            ISuggestedActionCategorySet requestedActionCategories,
            SnapshotSpan range,
            CancellationToken cancellationToken)
        {
            XmlDocCommentBlock block = TryGetCommentBlockUnderCaret();
            if (block == null || !WouldReflowChangeAnything(block))
            {
                return [];
            }

            ITrackingSpan trackingSpan = range.Snapshot.CreateTrackingSpan(
                block.Span,
                SpanTrackingMode.EdgeInclusive);

            var action = new ReflowCommentSuggestedAction(
                trackingSpan,
                block,
                textBuffer,
                textView as IWpfTextView,
                outliningManager);

            return
            [
                new SuggestedActionSet(
                    categoryName: PredefinedSuggestedActionCategoryNames.Refactoring,
                    actions: [action],
                    title: "XML Documentation",
                    priority: SuggestedActionSetPriority.Low)
            ];
        }

        public Task<bool> HasSuggestedActionsAsync(
            ISuggestedActionCategorySet requestedActionCategories,
            SnapshotSpan range,
            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                XmlDocCommentBlock block = TryGetCommentBlockUnderCaret();
                return block != null && WouldReflowChangeAnything(block);
            }, cancellationToken);
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }

        /// <summary>
        /// Checks if reflowing the comment block would actually change anything.
        /// </summary>
        private bool WouldReflowChangeAnything(XmlDocCommentBlock block)
        {
            // Get the current settings - .editorconfig overrides Options page
            var maxLineLength = EditorConfigSettings.GetMaxLineLength(textView);

            // Check if any line exceeds the threshold
            ITextSnapshot snapshot = textBuffer.CurrentSnapshot;

            // Guard against stale block (buffer may have been modified since block was created)
            if (block.Span.End > snapshot.Length || block.EndLine >= snapshot.LineCount)
            {
                return false;
            }

            for (var lineNum = block.StartLine; lineNum <= block.EndLine; lineNum++)
            {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNum);
                if (line.Length > maxLineLength)
                {
                    return true;
                }
            }

            // Also check if reflow would produce different output
            // (e.g., compact style conversion, multi-line to single-line, etc.)
            CommentReflowEngine engine = EditorConfigSettings.CreateReflowEngine(textView);
            var reflowed = engine.ReflowComment(block);

            if (reflowed == null)
            {
                return false;
            }

            // Compare with original text
            var originalText = snapshot.GetText(block.Span);
            return !string.Equals(originalText, reflowed, StringComparison.Ordinal);
        }

        /// <summary>
        /// Tries to find an XML doc comment block at the current caret position.
        /// </summary>
        private XmlDocCommentBlock TryGetCommentBlockUnderCaret()
        {
            SnapshotPoint caretPosition = textView.Caret.Position.BufferPosition;
            ITextSnapshot snapshot = caretPosition.Snapshot;

            var contentType = textBuffer.ContentType.TypeName;
            var commentStyle = LanguageCommentStyle.GetForContentType(contentType);

            if (commentStyle == null)
            {
                return null;
            }

            var parser = new XmlDocCommentParser(commentStyle);
            return parser.FindCommentBlockAtPosition(snapshot, caretPosition.Position);
        }
    }
}
