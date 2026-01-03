global using Community.VisualStudio.Toolkit;

global using Microsoft.VisualStudio.Shell;

global using System;

global using Task = System.Threading.Tasks.Task;

using System.Runtime.InteropServices;
using System.Threading;
using CommentsVS.Handlers;
using CommentsVS.Options;
using Microsoft.VisualStudio;

namespace CommentsVS
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.CommentsVSString)]
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "CommentsVS", "General", 0, 0, true, SupportsProfiles = true)]
    [ProvideProfile(typeof(OptionsProvider.GeneralOptions), "CommentsVS", "General", 0, 0, true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class CommentsVSPackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();
            await FormatDocumentHandler.RegisterAsync();
        }
    }
}