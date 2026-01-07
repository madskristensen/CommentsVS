global using System;
global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using Task = System.Threading.Tasks.Task;
using System.Runtime.InteropServices;
using System.Threading;
using System.ComponentModel.Design;
using CommentsVS.Commands;
using CommentsVS.Handlers;
using CommentsVS.Options;
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
    [ProvideToolWindow(typeof(CodeAnchorsToolWindowPane), Style = VsDockStyle.Tabbed, Window = "D78612C7-9962-4B83-95D9-268046DAD23A")]
    public sealed class CommentsVSPackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();
            this.RegisterToolWindows();
            await FormatDocumentHandler.RegisterAsync();

            // Switch to main thread to register combo box command
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            
            // Register the scope filter combo box command
            if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
            {
                ScopeFilterComboCommand.Initialize(this, commandService);
                TypeFilterComboCommand.Initialize(this, commandService);
            }
        }
    }
}
