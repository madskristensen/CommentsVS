using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using CommentsVS.ToolWindows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command handler for the scope filter combo box in the Code Anchors toolbar.
    /// </summary>
    internal sealed class ScopeFilterComboCommand
    {
        private static ScopeFilterComboCommand _instance;
        private readonly Package _package;
        private OleMenuCommand _command;

        private static readonly string[] ScopeOptions =
        [
            "Entire Solution",
            "Current Project",
            "Current Document",
            "Open Documents"
        ];

        private string _currentScopeText = "Entire Solution";

        private ScopeFilterComboCommand(Package package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));

            if (commandService != null)
            {
                // Register the combo box command
                var comboCommandID = new CommandID(PackageGuids.CommentsVS, PackageIds.ScopeFilterCombo);
                _command = new OleMenuCommand(OnComboCommand, comboCommandID);
                commandService.AddCommand(_command);

                // Register the get list command
                var getListCommandID = new CommandID(PackageGuids.CommentsVS, PackageIds.ScopeFilterComboGetList);
                var getListCommand = new OleMenuCommand(OnGetList, getListCommandID);
                commandService.AddCommand(getListCommand);
            }
        }

        public static void Initialize(Package package, OleMenuCommandService commandService)
        {
            _instance = new ScopeFilterComboCommand(package, commandService);
        }

        private void OnComboCommand(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e is OleMenuCmdEventArgs eventArgs)
            {
                var input = eventArgs.InValue;
                IntPtr vOut = eventArgs.OutValue;

                if (vOut != IntPtr.Zero)
                {
                    // Return current selection
                    Marshal.GetNativeVariantForObject(_currentScopeText, vOut);
                }
                else if (input != null)
                {
                    // User selected something
                    var selectedScope = input.ToString();
                    if (Array.IndexOf(ScopeOptions, selectedScope) >= 0)
                    {
                        _currentScopeText = selectedScope;
                        ApplyScopeFilter(selectedScope);
                    }
                }
            }
        }

        private void OnGetList(object sender, EventArgs e)
        {
            if (e is OleMenuCmdEventArgs eventArgs)
            {
                var inParam = eventArgs.InValue;
                IntPtr vOut = eventArgs.OutValue;

                if (vOut != IntPtr.Zero)
                {
                    // Return the list of options
                    Marshal.GetNativeVariantForObject(ScopeOptions, vOut);
                }
            }
        }

        private void ApplyScopeFilter(string scopeText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            AnchorScope scope = scopeText switch
            {
                "Entire Solution" => AnchorScope.EntireSolution,
                "Current Project" => AnchorScope.CurrentProject,
                "Current Document" => AnchorScope.CurrentDocument,
                "Open Documents" => AnchorScope.OpenDocuments,
                _ => AnchorScope.EntireSolution
            };

            if (CodeAnchorsToolWindow.Instance != null)
            {
                CodeAnchorsToolWindow.Instance.SetScope(scope);
            }
        }
    }
}
