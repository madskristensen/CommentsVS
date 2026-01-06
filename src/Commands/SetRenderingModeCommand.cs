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
    /// Command to set the rendering mode to Off.
    /// </summary>
    [Command(PackageIds.SetRenderingModeOff)]
    internal sealed class SetRenderingModeOffCommand : BaseCommand<SetRenderingModeOffCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Checked = General.Instance.CommentRenderingMode == RenderingMode.Off;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await SetRenderingModeHelper.SetModeAsync(RenderingMode.Off);
        }
    }

    /// <summary>
    /// Command to set the rendering mode to Compact.
    /// </summary>
    [Command(PackageIds.SetRenderingModeCompact)]
    internal sealed class SetRenderingModeCompactCommand : BaseCommand<SetRenderingModeCompactCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Checked = General.Instance.CommentRenderingMode == RenderingMode.Compact;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await SetRenderingModeHelper.SetModeAsync(RenderingMode.Compact);
        }
    }

    /// <summary>
    /// Command to set the rendering mode to Full.
    /// </summary>
    [Command(PackageIds.SetRenderingModeFull)]
    internal sealed class SetRenderingModeFullCommand : BaseCommand<SetRenderingModeFullCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Checked = General.Instance.CommentRenderingMode == RenderingMode.Full;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await SetRenderingModeHelper.SetModeAsync(RenderingMode.Full);
        }
    }

    /// <summary>
    /// Helper class to set rendering mode and handle associated state changes.
    /// </summary>
    internal static class SetRenderingModeHelper
    {
        /// <summary>
        /// Event raised when the rendered comments state changes.
        /// </summary>
        public static event EventHandler RenderedCommentsStateChanged;

        public static async Task SetModeAsync(RenderingMode mode)
        {
            RenderingMode previousMode = General.Instance.CommentRenderingMode;

            if (previousMode == mode)
            {
                return;
            }

            General.Instance.CommentRenderingMode = mode;
            await General.Instance.SaveAsync();

            // Notify that rendered comments state changed
            RenderedCommentsStateChanged?.Invoke(null, EventArgs.Empty);

            // When switching to/from Compact mode, expand all XML doc comments so they can
            // re-collapse with the correct collapsed text.
            if (mode == RenderingMode.Compact || previousMode == RenderingMode.Compact)
            {
                await ExpandAndRecollapseXmlDocCommentsAsync();
            }

            var modeName = mode switch
            {
                RenderingMode.Off => "Off",
                RenderingMode.Compact => "Compact",
                RenderingMode.Full => "Full",
                _ => "Off"
            };

            await VS.StatusBar.ShowMessageAsync($"Comment rendering mode: {modeName}");
        }

        /// <summary>
        /// Expands and re-collapses all XML doc comment regions to refresh their collapsed text.
        /// </summary>
        private static async Task ExpandAndRecollapseXmlDocCommentsAsync()
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView?.TextView == null)
            {
                return;
            }

            IWpfTextView textView = docView.TextView;
            ITextSnapshot snapshot = textView.TextSnapshot;

            IComponentModel2 componentModel = await VS.Services.GetComponentModelAsync();
            IOutliningManagerService outliningManagerService = componentModel.GetService<IOutliningManagerService>();
            IOutliningManager outliningManager = outliningManagerService?.GetOutliningManager(textView);

            if (outliningManager == null)
            {
                return;
            }

            var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
            var commentRegions = outliningManager
                .GetAllRegions(fullSpan)
                .Where(r => IsXmlDocCommentRegion(r, snapshot))
                .ToList();

            if (commentRegions.Count == 0)
            {
                return;
            }

            // First expand all collapsed regions
            var collapsedRegions = commentRegions.OfType<ICollapsed>().ToList();
            foreach (ICollapsed collapsed in collapsedRegions)
            {
                outliningManager.Expand(collapsed);
            }

            // Small delay to let the outlining manager update
            await Task.Delay(50);

            // Re-collapse all regions that were collapsed
            ITextSnapshot currentSnapshot = textView.TextSnapshot;
            var currentFullSpan = new SnapshotSpan(currentSnapshot, 0, currentSnapshot.Length);
            IEnumerable<ICollapsible> currentRegions = outliningManager
                .GetAllRegions(currentFullSpan)
                .Where(r => IsXmlDocCommentRegion(r, currentSnapshot));

            foreach (ICollapsible region in currentRegions)
            {
                if (!region.IsCollapsed)
                {
                    outliningManager.TryCollapse(region);
                }
            }
        }

        private static bool IsXmlDocCommentRegion(ICollapsible region, ITextSnapshot snapshot)
        {
            SnapshotSpan extent = region.Extent.GetSpan(snapshot);
            var text = extent.GetText().TrimStart();
            return text.StartsWith("///") || text.StartsWith("'''");
        }
    }
}
