using CommentsVS.Options;

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
            ToggleRenderedCommentsCommand.RaiseStateChanged();

            // When switching to/from Compact mode, expand all XML doc comments so they can
            // re-collapse with the correct collapsed text.
            if (mode == RenderingMode.Compact || previousMode == RenderingMode.Compact)
            {
                await ToggleRenderedCommentsCommand.ExpandAndRecollapseXmlDocCommentsAsync();
            }

            string modeName = mode switch
            {
                RenderingMode.Off => "Off",
                RenderingMode.Compact => "Compact",
                RenderingMode.Full => "Full",
                _ => "Off"
            };

            await VS.StatusBar.ShowMessageAsync($"Comment rendering mode: {modeName}");
        }
    }
}
