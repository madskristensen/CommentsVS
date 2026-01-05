using System.Linq;
using CommentsVS.Options;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command to cycle through XML documentation comment rendering modes.
    /// </summary>
    [Command(PackageIds.ToggleRenderedComments)]
    internal sealed class ToggleRenderedCommentsCommand : BaseCommand<ToggleRenderedCommentsCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Checked = General.Instance.CommentRenderingMode != RenderingMode.Off;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            // Cycle through modes: Off -> Compact -> Full -> Off
            var currentMode = General.Instance.CommentRenderingMode;
            var nextMode = currentMode switch
            {
                RenderingMode.Off => RenderingMode.Compact,
                RenderingMode.Compact => RenderingMode.Full,
                RenderingMode.Full => RenderingMode.Off,
                _ => RenderingMode.Off
            };

            General.Instance.CommentRenderingMode = nextMode;
            await General.Instance.SaveAsync();

            // Notify that rendered comments state changed
            RenderedCommentsStateChanged?.Invoke(this, EventArgs.Empty);

            // When switching to/from Compact mode, expand all XML doc comments so they can
            // re-collapse with the correct collapsed text. This is necessary because VS caches
            // the collapsed text and doesn't automatically update it when tags change.
            if (nextMode == RenderingMode.Compact || currentMode == RenderingMode.Compact)
            {
                await ExpandAndRecollapseXmlDocCommentsAsync();
            }

            var modeName = nextMode switch
            {
                RenderingMode.Off => "Off",
                RenderingMode.Compact => "Compact",
                RenderingMode.Full => "Full",
                _ => "Off"
            };
            await VS.StatusBar.ShowMessageAsync($"Comment rendering mode: {modeName}");
        }

        /// <summary>
        /// Expands all XML documentation comment outlining regions and then re-collapses them
        /// if the "Collapsed by Default" setting is enabled. This forces VS to regenerate
        /// the collapsed text based on the current rendering mode.
        /// </summary>
        internal static async Task ExpandAndRecollapseXmlDocCommentsAsync()
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView?.TextView == null)
            {
                return;
            }

            IWpfTextView textView = docView.TextView;

            // Get the outlining manager service
            IComponentModel2 componentModel = await VS.Services.GetComponentModelAsync();
            IOutliningManagerService outliningManagerService = componentModel.GetService<IOutliningManagerService>();
            IOutliningManager outliningManager = outliningManagerService?.GetOutliningManager(textView);

            if (outliningManager == null)
            {
                return;
            }

            var snapshot = textView.TextSnapshot;
            var fullSpan = new Microsoft.VisualStudio.Text.SnapshotSpan(snapshot, 0, snapshot.Length);
            
            // Get all collapsed XML doc comment regions
            var collapsedCommentRegions = outliningManager.GetCollapsedRegions(fullSpan, false)
                .Where(r => IsXmlDocCommentRegion(r, snapshot))
                .ToList();

            // Expand all collapsed XML doc comment regions
            foreach (var region in collapsedCommentRegions)
            {
                outliningManager.Expand(region);
            }

            // If "Collapsed by Default" is enabled for this mode, re-collapse after a short delay
            var shouldRecollapse = General.Instance.CollapseCommentsOnFileOpen &&
                                  (General.Instance.CommentRenderingMode == RenderingMode.Off ||
                                   General.Instance.CommentRenderingMode == RenderingMode.Compact);

            if (shouldRecollapse && collapsedCommentRegions.Any())
            {
                // Wait a bit for the expansion to complete and tags to regenerate
                await System.Threading.Tasks.Task.Delay(100);

                // Get all XML doc comment regions again (now expanded)
                var allCommentRegions = outliningManager.GetAllRegions(fullSpan)
                    .Where(r => IsXmlDocCommentRegion(r, snapshot) && !r.IsCollapsed)
                    .ToList();

                // Collapse them again with the new collapsed text
                foreach (var region in allCommentRegions)
                {
                    outliningManager.TryCollapse(region);
                }
            }
        }

        /// <summary>
        /// Determines if a collapsible region is an XML documentation comment.
        /// </summary>
        private static bool IsXmlDocCommentRegion(ICollapsible region, Microsoft.VisualStudio.Text.ITextSnapshot snapshot)
        {
            try
            {
                var extent = region.Extent.GetSpan(snapshot);
                var startLine = extent.Start.GetContainingLine();
                var text = startLine.GetText().TrimStart();

                // Check if this looks like an XML doc comment
                return text.StartsWith("///") || text.StartsWith("'''");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Event raised when the rendered comments state changes.
        /// </summary>
        public static event EventHandler RenderedCommentsStateChanged;

        /// <summary>
        /// Raises the state changed event.
        /// </summary>
        internal static void RaiseStateChanged()
        {
            RenderedCommentsStateChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}

