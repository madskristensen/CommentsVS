using CommentsVS.Options;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command to open the extension's settings page from the Code Anchors toolbar.
    /// </summary>
    [Command(PackageIds.ToolbarOpenSettings)]
    internal sealed class ToolbarOpenSettingsCommand : BaseCommand<ToolbarOpenSettingsCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await VS.Settings.OpenAsync<OptionsProvider.GeneralOptions>();
        }
    }
}
