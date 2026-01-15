using CommentsVS.ToolWindows;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command to navigate to the next anchor in the Code Anchors tool window.
    /// </summary>
    [Command(PackageIds.NextAnchor)]
    internal sealed class NextAnchorCommand : BaseCommand<NextAnchorCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            // Ensure the tool window is shown
            await CodeAnchorsToolWindow.ShowAsync();

            // Navigate to the next anchor
            CodeAnchorsToolWindow toolWindow = await CodeAnchorsToolWindow.GetInstanceAsync();
            if (toolWindow != null)
            {
                await toolWindow.NavigateToNextAnchorAsync();
            }
        }
    }
}
