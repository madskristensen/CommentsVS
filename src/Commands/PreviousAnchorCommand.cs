using CommentsVS.ToolWindows;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command to navigate to the previous anchor in the Code Anchors tool window.
    /// </summary>
    [Command(PackageIds.PreviousAnchor)]
    internal sealed class PreviousAnchorCommand : BaseCommand<PreviousAnchorCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            // Ensure the tool window is shown
            await CodeAnchorsToolWindow.ShowAsync();

            // Navigate to the previous anchor
            CodeAnchorsToolWindow toolWindow = await CodeAnchorsToolWindow.GetInstanceAsync();
            if (toolWindow != null)
            {
                await toolWindow.NavigateToPreviousAnchorAsync();
            }
        }
    }
}
