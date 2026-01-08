using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Interaction logic for CodeAnchorsControl.xaml
    /// </summary>
    public partial class CodeAnchorsControl : UserControl
    {
        private readonly ObservableCollection<AnchorItem> _allAnchors = [];
        private readonly CollectionViewSource _viewSource;
        private string _currentTypeFilter = "All";
        private string _currentSearchFilter = string.Empty;
        private bool _currentMatchCase;
        private bool _groupByFile;

        /// <summary>
        /// Event raised when an anchor is activated (double-click or Enter).
        /// </summary>
        public event EventHandler<AnchorItem> AnchorActivated;

        public CodeAnchorsControl()
        {
            InitializeComponent();

            _viewSource = new CollectionViewSource { Source = _allAnchors };
            _viewSource.Filter += ViewSource_Filter;
            AnchorDataGrid.ItemsSource = _viewSource.View;

            // Set up icon binding after data grid is loaded
            AnchorDataGrid.LoadingRow += AnchorDataGrid_LoadingRow;
        }

        /// <summary>
        /// Applies a search filter from the VS search box.
        /// </summary>
        /// <param name="searchText">The text to search for.</param>
        /// <param name="matchCase">Whether to match case.</param>
        /// <returns>The number of matching results.</returns>
        public uint ApplySearchFilter(string searchText, bool matchCase)
        {
            _currentSearchFilter = searchText ?? string.Empty;
            _currentMatchCase = matchCase;

            _viewSource.View.Refresh();
            UpdateStatus();

            return (uint)_viewSource.View.Cast<object>().Count();
        }

        /// <summary>
        /// Sets the type filter for anchors.
        /// </summary>
        /// <param name="typeFilter">The type filter (All, TODO, HACK, etc.).</param>
        public void SetTypeFilter(string typeFilter)
        {
            _currentTypeFilter = string.IsNullOrEmpty(typeFilter) ? "All" : typeFilter;

            _viewSource.View.Refresh();
            UpdateStatus();
        }

        /// <summary>
        /// Clears the search filter and shows all anchors.
        /// </summary>
        public void ClearSearchFilter()
        {
            _currentSearchFilter = string.Empty;
            _currentMatchCase = false;

            _viewSource.View.Refresh();
            UpdateStatus();
        }

        /// <summary>
        /// Updates the anchors displayed in the control.
        /// </summary>
        /// <param name="anchors">The anchors to display.</param>
        public void UpdateAnchors(IEnumerable<AnchorItem> anchors)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _allAnchors.Clear();
            foreach (AnchorItem anchor in anchors)
            {
                _allAnchors.Add(anchor);
            }

            UpdateStatus();
            _viewSource.View.Refresh();
        }

        /// <summary>
        /// Adds anchors to the existing collection.
        /// </summary>
        /// <param name="anchors">The anchors to add.</param>
        public void AddAnchors(IEnumerable<AnchorItem> anchors)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (AnchorItem anchor in anchors)
            {
                _allAnchors.Add(anchor);
            }

            UpdateStatus();
            _viewSource.View.Refresh();
        }

        /// <summary>
        /// Removes all anchors from a specific file.
        /// </summary>
        /// <param name="filePath">The file path to remove anchors for.</param>
        public void RemoveAnchorsForFile(string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var toRemove = _allAnchors.Where(a => a.FilePath == filePath).ToList();
            foreach (AnchorItem anchor in toRemove)
            {
                _allAnchors.Remove(anchor);
            }

            UpdateStatus();
            _viewSource.View.Refresh();
        }

        /// <summary>
        /// Clears all anchors.
        /// </summary>
        public void ClearAnchors()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _allAnchors.Clear();
            UpdateStatus();
        }

        /// <summary>
        /// Gets the currently selected anchor.
        /// </summary>
        public AnchorItem SelectedAnchor => AnchorDataGrid.SelectedItem as AnchorItem;

        /// <summary>
        /// Gets all anchors currently in the list.
        /// </summary>
        public IReadOnlyList<AnchorItem> AllAnchors => [.. _allAnchors];

        /// <summary>
        /// Gets the currently filtered/visible anchors (respects search and type filters).
        /// </summary>
        public IReadOnlyList<AnchorItem> FilteredAnchors => [.. _viewSource.View.Cast<AnchorItem>()];

        /// <summary>
        /// Selects the next anchor in the list.
        /// </summary>
        /// <returns>The newly selected anchor, or null if none.</returns>
        public AnchorItem SelectNextAnchor()
        {
            if (_viewSource.View.IsEmpty)
            {
                return null;
            }

            var currentIndex = AnchorDataGrid.SelectedIndex;
            var nextIndex = currentIndex + 1;

            if (nextIndex >= AnchorDataGrid.Items.Count)
            {
                nextIndex = 0; // Wrap around
            }

            AnchorDataGrid.SelectedIndex = nextIndex;
            AnchorDataGrid.ScrollIntoView(AnchorDataGrid.SelectedItem);

            return SelectedAnchor;
        }

        /// <summary>
        /// Selects the previous anchor in the list.
        /// </summary>
        /// <returns>The newly selected anchor, or null if none.</returns>
        public AnchorItem SelectPreviousAnchor()
        {
            if (_viewSource.View.IsEmpty)
            {
                return null;
            }

            var currentIndex = AnchorDataGrid.SelectedIndex;
            var prevIndex = currentIndex - 1;

            if (prevIndex < 0)
            {
                prevIndex = AnchorDataGrid.Items.Count - 1; // Wrap around
            }

            AnchorDataGrid.SelectedIndex = prevIndex;
            AnchorDataGrid.ScrollIntoView(AnchorDataGrid.SelectedItem);

            return SelectedAnchor;
        }

        /// <summary>
        /// Updates the status bar with a custom message and progress.
        /// </summary>
        /// <param name="message">The status message to display.</param>
        public void UpdateStatus(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            StatusText.Text = message;
        }

        private void UpdateStatus()
        {
            var totalCount = _allAnchors.Count;
            var visibleCount = _viewSource.View.Cast<object>().Count();

            var hasTypeFilter = _currentTypeFilter != "All";
            var hasSearchFilter = !string.IsNullOrWhiteSpace(_currentSearchFilter);

            if (!hasTypeFilter && !hasSearchFilter)
            {
                StatusText.Text = $"{totalCount} anchor(s) found";
            }
            else
            {
                var filterParts = new List<string>();
                if (hasTypeFilter)
                {
                    filterParts.Add($"type: {_currentTypeFilter}");
                }
                if (hasSearchFilter)
                {
                    filterParts.Add($"search: \"{_currentSearchFilter}\"");
                }
                StatusText.Text = $"{visibleCount} of {totalCount} anchor(s) shown ({string.Join(", ", filterParts)})";
            }
        }

        private void ViewSource_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item is AnchorItem anchor)
            {
                // Check type filter
                var passesTypeFilter = _currentTypeFilter == "All" ||
                    anchor.TypeDisplayName.Equals(_currentTypeFilter, StringComparison.OrdinalIgnoreCase);

                // Check search filter
                var passesSearchFilter = string.IsNullOrWhiteSpace(_currentSearchFilter) ||
                    MatchesSearch(anchor, _currentSearchFilter);

                e.Accepted = passesTypeFilter && passesSearchFilter;
            }
        }

        private bool MatchesSearch(AnchorItem anchor, string searchText)
        {
            // Determine comparison type based on match case setting
            StringComparison comparison = _currentMatchCase
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            // Search in message, file name, project name, and metadata
            return (anchor.Message != null && anchor.Message.IndexOf(searchText, comparison) >= 0) ||
                   (anchor.FileName != null && anchor.FileName.IndexOf(searchText, comparison) >= 0) ||
                   (anchor.Project != null && anchor.Project.IndexOf(searchText, comparison) >= 0) ||
                   (anchor.Owner != null && anchor.Owner.IndexOf(searchText, comparison) >= 0) ||
                   (anchor.IssueReference != null && anchor.IssueReference.IndexOf(searchText, comparison) >= 0) ||
                   (anchor.AnchorId != null && anchor.AnchorId.IndexOf(searchText, comparison) >= 0) ||
                   (anchor.MetadataDisplay != null && anchor.MetadataDisplay.IndexOf(searchText, comparison) >= 0);
        }

        /// <summary>
        /// Gets a value indicating whether group by file is enabled.
        /// </summary>
        public bool IsGroupByFileEnabled => _groupByFile;

        /// <summary>
        /// Toggles the group by file setting.
        /// </summary>
        public void ToggleGroupByFile()
        {
            _groupByFile = !_groupByFile;
            ApplyGrouping();
        }

        private void ApplyGrouping()
        {
            _viewSource.View.GroupDescriptions.Clear();

            if (_groupByFile)
            {
                _viewSource.View.GroupDescriptions.Add(new PropertyGroupDescription("FileName"));
            }
        }

        private void AnchorDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ActivateSelectedAnchor();
        }

        private void AnchorDataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ActivateSelectedAnchor();
                e.Handled = true;
            }
        }

        private void ActivateSelectedAnchor()
        {
            if (SelectedAnchor != null)
            {
                AnchorActivated?.Invoke(this, SelectedAnchor);
            }
        }

        private void AnchorDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is AnchorItem anchor)
            {
                // Find the Ellipse in the row and set its fill color based on anchor type
                // Use LayoutUpdated event to ensure the visual tree is ready
                void OnLayoutUpdated(object s, EventArgs args)
                {
                    e.Row.LayoutUpdated -= OnLayoutUpdated;
                    System.Windows.Shapes.Ellipse ellipse = FindVisualChild<System.Windows.Shapes.Ellipse>(e.Row);
                    if (ellipse != null)
                    {
                        ellipse.Fill = new System.Windows.Media.SolidColorBrush(anchor.AnchorType.GetColor());
                    }
                }
                e.Row.LayoutUpdated += OnLayoutUpdated;
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                {
                    return found;
                }

                T result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}
