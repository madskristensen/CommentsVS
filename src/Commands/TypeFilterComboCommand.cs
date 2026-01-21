using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.InteropServices;
using CommentsVS.Options;
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

        private static readonly string[] BuiltInTypeOptions =
        [
            "All Types",
            "TODO",
            "HACK",
            "NOTE",
            "BUG",
            "FIXME",
            "UNDONE",
            "REVIEW",
            "ANCHOR"
        ];

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
                var input = eventArgs.InValue;
                IntPtr vOut = eventArgs.OutValue;

                if (vOut != IntPtr.Zero)
                {
                    // Return current selection
                    Marshal.GetNativeVariantForObject(_currentTypeText, vOut);
                }
                else if (input != null)
                {
                    // User selected something
                    var selectedType = input.ToString();
                    var allOptions = GetAllTypeOptions();
                    if (Array.IndexOf(allOptions, selectedType) >= 0)
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
                var inParam = eventArgs.InValue;
                IntPtr vOut = eventArgs.OutValue;

                if (vOut != IntPtr.Zero)
                {
                    // Return the list of options (built-in + custom tags)
                    var allOptions = GetAllTypeOptions();
                    Marshal.GetNativeVariantForObject(allOptions, vOut);
                }
            }
        }

        /// <summary>
        /// Gets all type options including built-in and custom tags from settings.
        /// </summary>
        private string[] GetAllTypeOptions()
        {
            var customTags = General.Instance.GetCustomTagsSet();
            if (customTags.Count == 0)
            {
                return BuiltInTypeOptions;
            }

            // Filter out custom tags that match built-in tags (case-insensitive)
            // and return built-in options followed by sorted custom tags
            var builtInTagNames = new HashSet<string>(BuiltInTypeOptions.Skip(1), StringComparer.OrdinalIgnoreCase); // Skip "All Types"
            var uniqueCustomTags = customTags.Where(tag => !builtInTagNames.Contains(tag)).OrderBy(tag => tag);
            
            return [.. BuiltInTypeOptions, .. uniqueCustomTags];
        }

        private void ApplyTypeFilter(string typeText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Convert "All Types" to "All" to match existing filter logic
            var filterValue = typeText == "All Types" ? "All" : typeText;

            CodeAnchorsToolWindow.Instance?.SetTypeFilter(filterValue);
        }
    }
}
