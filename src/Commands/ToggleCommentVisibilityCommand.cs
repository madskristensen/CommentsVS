using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command to toggle visibility of XML documentation comments by collapsing/expanding
    /// their outlining regions.
    /// </summary>
    [Command(PackageIds.ToggleCommentVisibility)]
    internal sealed class ToggleCommentVisibilityCommand : BaseCommand<ToggleCommentVisibilityCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView?.TextView == null)
            {
                return;
            }

            IWpfTextView textView = docView.TextView;
            ITextSnapshot snapshot = textView.TextSnapshot;

            // Get the outlining manager service
            IComponentModel2 componentModel = await VS.Services.GetComponentModelAsync();
            IOutliningManagerService outliningManagerService = componentModel.GetService<IOutliningManagerService>();
            IOutliningManager outliningManager = outliningManagerService?.GetOutliningManager(textView);

            if (outliningManager == null)
            {
                return;
            }

            // Get all collapsible regions and filter to XML doc comment regions
            var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
            List<ICollapsible> commentRegions = [.. outliningManager.GetAllRegions(fullSpan).Where(r => IsXmlDocCommentRegion(r, snapshot))];

            if (!commentRegions.Any())
            {
                await VS.StatusBar.ShowMessageAsync("No XML documentation comment regions found");
                return;
            }

            // Check if majority are collapsed - if so, expand; otherwise collapse
            var collapsedCount = commentRegions.Count(r => r.IsCollapsed);
            var shouldExpand = collapsedCount > commentRegions.Count / 2;

            if (shouldExpand)
            {
                // Expand all XML doc comment regions
                foreach (ICollapsible region in commentRegions.Where(r => r.IsCollapsed))
                {
                    if (region is ICollapsed collapsed)
                    {
                        outliningManager.Expand(collapsed);
                    }
                }

                await VS.StatusBar.ShowMessageAsync("XML documentation comments expanded");
            }
            else
            {
                // Collapse all XML doc comment regions
                foreach (ICollapsible region in commentRegions.Where(r => !r.IsCollapsed))
                {
                    outliningManager.TryCollapse(region);
                }

                await VS.StatusBar.ShowMessageAsync("XML documentation comments collapsed");
            }
        }

        private static bool IsXmlDocCommentRegion(ICollapsible region, ITextSnapshot snapshot)
        {
            SnapshotSpan span = region.Extent.GetSpan(snapshot);
            var text = span.GetText().TrimStart();

            // Check if region starts with XML doc comment prefix
            return text.StartsWith("///") || text.StartsWith("'''");
        }
    }
}
