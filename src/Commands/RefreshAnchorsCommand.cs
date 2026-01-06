using CommentsVS.ToolWindows;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command to refresh the anchors list by re-scanning the entire solution.
    /// </summary>
    [Command(PackageIds.RefreshAnchors)]
    internal sealed class RefreshAnchorsCommand : BaseCommand<RefreshAnchorsCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            if (CodeAnchorsToolWindow.Instance != null)
            {
                await CodeAnchorsToolWindow.Instance.ScanSolutionAsync();
            }
        }
    }
}
