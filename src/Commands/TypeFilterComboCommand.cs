using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.InteropServices;
using CommentsVS.ToolWindows;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command handler for the type filter combo box in the Code Anchors toolbar.
    /// </summary>
    internal sealed class TypeFilterComboCommand
    {
        private static TypeFilterComboCommand _instance;
        private readonly Package _package;
        private readonly OleMenuCommand _command;

        private static readonly string[] _builtInTypeOptions =
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
        /// Gets type options filtered to only include types that exist in the current anchor list.
        /// </summary>
        private string[] GetAllTypeOptions()
        {
            // Get distinct types from the current cache
            HashSet<string> existingTypes = CodeAnchorsToolWindow.Instance?.Cache?.GetDistinctTypes();

            if (existingTypes == null || existingTypes.Count == 0)
            {
                // No anchors in cache - return just "All Types"
                return ["All Types"];
            }

            // Build list with "All Types" first, then only types that exist in the cache
            var result = new List<string> { "All Types" };

            // Add built-in types that exist in the cache (preserving order)
            foreach (var builtInType in _builtInTypeOptions.Skip(1)) // Skip "All Types" since we already added it
            {
                if (existingTypes.Contains(builtInType))
                {
                    result.Add(builtInType);
                }
            }

            // Add custom types that exist in the cache (sorted)
            var builtInSet = new HashSet<string>(_builtInTypeOptions.Skip(1), StringComparer.OrdinalIgnoreCase);
            IOrderedEnumerable<string> customTypes = existingTypes
                .Where(t => !builtInSet.Contains(t))
                .OrderBy(t => t);
            result.AddRange(customTypes);

            return [.. result];
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
