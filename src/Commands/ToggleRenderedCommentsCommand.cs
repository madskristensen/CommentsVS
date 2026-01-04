using CommentsVS.Options;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command to toggle the rendered view of XML documentation comments.
    /// </summary>
    [Command(PackageIds.ToggleRenderedComments)]
    internal sealed class ToggleRenderedCommentsCommand : BaseCommand<ToggleRenderedCommentsCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Checked = General.Instance.EnableRenderedComments;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            // Toggle the setting
            General.Instance.EnableRenderedComments = !General.Instance.EnableRenderedComments;
            await General.Instance.SaveAsync();

            // Notify that rendered comments state changed
            RenderedCommentsStateChanged?.Invoke(this, EventArgs.Empty);

            string state = General.Instance.EnableRenderedComments ? "enabled" : "disabled";
            await VS.StatusBar.ShowMessageAsync($"Rendered comments {state}");
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
