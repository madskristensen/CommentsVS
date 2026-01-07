using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using CommentsVS.ToolWindows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command handler for the type filter combo box in the Code Anchors toolbar.
    /// </summary>
    internal sealed class TypeFilterComboCommand
    {
        private static TypeFilterComboCommand _instance;
        private readonly Package _package;
        private OleMenuCommand _command;

        private static readonly string[] TypeOptions =
        {
            "All Types",
            "TODO",
            "HACK",
            "NOTE",
            "BUG",
            "FIXME",
            "UNDONE",
            "REVIEW",
            "ANCHOR"
        };

        private string _currentTypeText = "All Types";

        private TypeFilterComboCommand(Package package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));

            if (commandService != null)
            {
                // Register the combo box command
                var comboCommandID = new CommandID(PackageGuids.CommentsVS, PackageIds.TypeFilterCombo);
                _command = new OleMenuCommand(OnComboCommand, comboCommandID);
                commandService.AddCommand(_command);

                // Register the get list command
                var getListCommandID = new CommandID(PackageGuids.CommentsVS, PackageIds.TypeFilterComboGetList);
                var getListCommand = new OleMenuCommand(OnGetList, getListCommandID);
                commandService.AddCommand(getListCommand);
            }
        }

        public static void Initialize(Package package, OleMenuCommandService commandService)
        {
            _instance = new TypeFilterComboCommand(package, commandService);
        }

        private void OnComboCommand(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e is OleMenuCmdEventArgs eventArgs)
            {
                object input = eventArgs.InValue;
                IntPtr vOut = eventArgs.OutValue;

                if (vOut != IntPtr.Zero)
                {
                    // Return current selection
                    Marshal.GetNativeVariantForObject(_currentTypeText, vOut);
                }
                else if (input != null)
                {
                    // User selected something
                    string selectedType = input.ToString();
                    if (Array.IndexOf(TypeOptions, selectedType) >= 0)
                    {
                        _currentTypeText = selectedType;
                        ApplyTypeFilter(selectedType);
                    }
                }
            }
        }

        private void OnGetList(object sender, EventArgs e)
        {
            if (e is OleMenuCmdEventArgs eventArgs)
            {
                object inParam = eventArgs.InValue;
                IntPtr vOut = eventArgs.OutValue;

                if (vOut != IntPtr.Zero)
                {
                    // Return the list of options
                    Marshal.GetNativeVariantForObject(TypeOptions, vOut);
                }
            }
        }

        private void ApplyTypeFilter(string typeText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Convert "All Types" to "All" to match existing filter logic
            string filterValue = typeText == "All Types" ? "All" : typeText;

            if (CodeAnchorsToolWindow.Instance != null)
            {
                CodeAnchorsToolWindow.Instance.SetTypeFilter(filterValue);
            }
        }
    }
}
