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
    internal sealed class ReflowCommentSuggestedAction(
        ITrackingSpan trackingSpan,
        XmlDocCommentBlock commentBlock,
        ITextBuffer textBuffer) : ISuggestedAction
    {
        public string DisplayText => "Reflow comment";

        public bool HasActionSets => false;

        public bool HasPreview => true;

        public string IconAutomationText => null;

        public ImageMoniker IconMoniker => KnownMonikers.TextLeft;

        public string InputGestureText => null;

        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
        }

        public Task<object> GetPreviewAsync(CancellationToken cancellationToken)
        {
            return Task.Run<object>(async () =>
            {
                General options = await General.GetLiveInstanceAsync();
                CommentReflowEngine engine = options.CreateReflowEngine();

                var reflowed = engine.ReflowComment(commentBlock);

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

                General options = await General.GetLiveInstanceAsync();
                CommentReflowEngine engine = options.CreateReflowEngine();

                var reflowed = engine.ReflowComment(commentBlock);

                if (!string.IsNullOrEmpty(reflowed))
                {
                    ITextSnapshot snapshot = textBuffer.CurrentSnapshot;

                    // Re-parse to get current span (might have shifted)
                    LanguageCommentStyle commentStyle = commentBlock.CommentStyle;
                    var parser = new XmlDocCommentParser(commentStyle);

                    SnapshotSpan currentSpan = trackingSpan.GetSpan(snapshot);
                    XmlDocCommentBlock currentBlock = parser.FindCommentBlockAtPosition(snapshot, currentSpan.Start);

                    if (currentBlock != null)
                    {
                        using (ITextEdit edit = textBuffer.CreateEdit())
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
