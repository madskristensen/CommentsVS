using System.Collections.Generic;
using System.Linq;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command to remove all comments except task comments (TODO, HACK, etc.) from the current document.
    /// </summary>
    [Command(PackageIds.RemoveAllExceptTaskComments)]
    internal sealed class RemoveAllExceptTaskCommentsCommand : BaseCommand<RemoveAllExceptTaskCommentsCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView?.TextView == null)
            {
                return;
            }

            IWpfTextView view = docView.TextView;
            IEnumerable<IMappingSpan> mappingSpans = CommentRemovalService.GetClassificationSpans(view, "comment");

            if (!mappingSpans.Any())
            {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                CommentRemovalService.RemoveComments(view, mappingSpans, preserveTaskComments: true);
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }
    }
}
