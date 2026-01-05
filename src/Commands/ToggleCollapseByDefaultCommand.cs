using System.Collections.Generic;
using System.Linq;
using CommentsVS.Options;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command to toggle the "Collapse XML Doc Comments by Default" setting.
    /// </summary>
    [Command(PackageIds.ToggleCollapseByDefault)]
    internal sealed class ToggleCollapseByDefaultCommand : BaseCommand<ToggleCollapseByDefaultCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            // Only enabled when rendering mode is Off
            Command.Enabled = General.Instance.CommentRenderingMode == RenderingMode.Off;
            Command.Checked = General.Instance.CollapseCommentsOnFileOpen;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            bool newValue = !General.Instance.CollapseCommentsOnFileOpen;
            General.Instance.CollapseCommentsOnFileOpen = newValue;
            await General.Instance.SaveAsync();

            // Apply to the active document
            await ApplyCollapseStateToActiveDocumentAsync(newValue);

            string state = newValue ? "enabled" : "disabled";
            await VS.StatusBar.ShowMessageAsync($"Collapse XML doc comments by default: {state}");
        }

        /// <summary>
        /// Applies the collapse/expand state to the active document view.
        /// </summary>
        private static async Task ApplyCollapseStateToActiveDocumentAsync(bool collapse)
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();

            if (docView?.TextView == null)
            {
                return;
            }

            IComponentModel2 componentModel = await VS.Services.GetComponentModelAsync();
            IOutliningManagerService outliningManagerService = componentModel?.GetService<IOutliningManagerService>();

            if (outliningManagerService == null)
            {
                return;
            }

            IWpfTextView textView = docView.TextView;
            IOutliningManager outliningManager = outliningManagerService.GetOutliningManager(textView);

            if (outliningManager == null)
            {
                return;
            }

            ITextSnapshot snapshot = textView.TextSnapshot;
            var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
            List<ICollapsible> commentRegions = outliningManager
                .GetAllRegions(fullSpan)
                .Where(r => IsXmlDocCommentRegion(r, snapshot))
                .ToList();

            if (collapse)
            {
                foreach (ICollapsible region in commentRegions.Where(r => !r.IsCollapsed))
                {
                    outliningManager.TryCollapse(region);
                }
            }
            else
            {
                foreach (ICollapsible region in commentRegions.Where(r => r.IsCollapsed))
                {
                    if (region is ICollapsed collapsed)
                    {
                        outliningManager.Expand(collapsed);
                    }
                }
            }
        }

        private static bool IsXmlDocCommentRegion(ICollapsible region, ITextSnapshot snapshot)
        {
            SnapshotSpan span = region.Extent.GetSpan(snapshot);
            string text = span.GetText().TrimStart();

            return text.StartsWith("///") || text.StartsWith("'''");
        }
    }
}
