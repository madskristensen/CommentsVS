using CommentsVS.ToolWindows;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command to toggle grouping by file in the Code Anchors tool window.
    /// </summary>
    [Command(PackageIds.ToggleGroupByFile)]
    internal sealed class ToggleGroupByFileCommand : BaseCommand<ToggleGroupByFileCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Checked = CodeAnchorsToolWindow.Instance?.Control?.IsGroupByFileEnabled ?? false;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            CodeAnchorsToolWindow toolWindow = await CodeAnchorsToolWindow.GetInstanceAsync();
            toolWindow?.Control?.ToggleGroupByFile();
        }
    }
}
