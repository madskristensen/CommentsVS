using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace CommentsVS.SuggestedActions
{
    /// <summary>
    /// Light bulb action to reflow an XML documentation comment block.
    /// </summary>
    internal sealed class ReflowCommentSuggestedAction : ISuggestedAction
    {
        private readonly ITrackingSpan _trackingSpan;
        private readonly XmlDocCommentBlock _commentBlock;
        private readonly ITextBuffer _textBuffer;

        public ReflowCommentSuggestedAction(
            ITrackingSpan trackingSpan,
            XmlDocCommentBlock commentBlock,
            ITextBuffer textBuffer)
        {
            _trackingSpan = trackingSpan;
            _commentBlock = commentBlock;
            _textBuffer = textBuffer;
        }

        public string DisplayText => "Reflow XML Documentation Comment";

        public bool HasActionSets => false;

        public bool HasPreview => true;

        public string IconAutomationText => null;

        public ImageMoniker IconMoniker => KnownMonikers.FormatDocument;

        public string InputGestureText => null;

        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
        }

        public Task<object> GetPreviewAsync(CancellationToken cancellationToken)
        {
            return Task.Run<object>(async () =>
            {
                var options = await General.GetLiveInstanceAsync();

                var engine = new CommentReflowEngine(
                    options.MaxLineLength,
                    options.UseCompactStyleForShortSummaries,
                    options.PreserveBlankLines);

                var reflowed = engine.ReflowComment(_commentBlock);

                if (string.IsNullOrEmpty(reflowed))
                {
                    return "No changes needed";
                }

                return reflowed;
            }, cancellationToken);
        }

        public void Invoke(CancellationToken cancellationToken)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                var options = await General.GetLiveInstanceAsync();

                var engine = new CommentReflowEngine(
                    options.MaxLineLength,
                    options.UseCompactStyleForShortSummaries,
                    options.PreserveBlankLines);

                var reflowed = engine.ReflowComment(_commentBlock);

                if (!string.IsNullOrEmpty(reflowed))
                {
                    var snapshot = _textBuffer.CurrentSnapshot;

                    // Re-parse to get current span (might have shifted)
                    var commentStyle = _commentBlock.CommentStyle;
                    var parser = new XmlDocCommentParser(commentStyle);

                    var currentSpan = _trackingSpan.GetSpan(snapshot);
                    var currentBlock = parser.FindCommentBlockAtPosition(snapshot, currentSpan.Start);

                    if (currentBlock != null)
                    {
                        using (var edit = _textBuffer.CreateEdit())
                        {
                            edit.Replace(currentBlock.Span, reflowed);
                            edit.Apply();
                        }
                    }
                }
            });
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }

        public void Dispose()
        {
        }
    }
}
