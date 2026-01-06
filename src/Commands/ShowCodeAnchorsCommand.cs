using CommentsVS.ToolWindows;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command to show the Code Anchors tool window.
    /// </summary>
    [Command(PackageIds.ShowCodeAnchors)]
    internal sealed class ShowCodeAnchorsCommand : BaseCommand<ShowCodeAnchorsCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await CodeAnchorsToolWindow.ShowAsync();
        }
    }
}
