global using System;
global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using Task = System.Threading.Tasks.Task;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using CommentsVS.Commands;
using CommentsVS.Handlers;
using CommentsVS.Options;
using CommentsVS.Services;
using CommentsVS.ToolWindows;
using Microsoft.VisualStudio;

namespace CommentsVS
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.CommentsVSString)]
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), Vsix.Name, "General", 0, 0, true, SupportsProfiles = true)]
    [ProvideProfile(typeof(OptionsProvider.GeneralOptions), Vsix.Name, "General", 0, 0, true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideToolWindow(typeof(CodeAnchorsToolWindowPane), Style = VsDockStyle.Tabbed, Window = WindowGuids.ErrorList)]
    [ProvideToolWindowVisibility(typeof(CodeAnchorsToolWindowPane), VSConstants.UICONTEXT.NoSolution_string)]
    [ProvideToolWindowVisibility(typeof(CodeAnchorsToolWindowPane), VSConstants.UICONTEXT.SolutionHasSingleProject_string)]
    [ProvideToolWindowVisibility(typeof(CodeAnchorsToolWindowPane), VSConstants.UICONTEXT.SolutionHasMultipleProjects_string)]
    [ProvideToolWindowVisibility(typeof(CodeAnchorsToolWindowPane), VSConstants.UICONTEXT.EmptySolution_string)]
    public sealed class CommentsVSPackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();
            this.RegisterToolWindows();
            await FormatDocumentHandler.RegisterAsync();

            // Switch to main thread to register combo box command
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Subscribe to solution close events to clear caches
            VS.Events.SolutionEvents.OnAfterCloseSolution += OnSolutionClosed;

            // Register the scope filter combo box command
            if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
            {
                ScopeFilterComboCommand.Initialize(this, commandService);
                TypeFilterComboCommand.Initialize(this, commandService);
            }
        }

        private void OnSolutionClosed()
        {
            // Clear the Git repository cache to prevent stale data
            // when the user opens a different solution or changes Git remotes
            GitRepositoryService.ClearCache();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from solution close events
                VS.Events.SolutionEvents.OnAfterCloseSolution -= OnSolutionClosed;
            }

            base.Dispose(disposing);
        }
    }
}
