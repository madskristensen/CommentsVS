using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Interaction logic for CodeAnchorsControl.xaml
    /// </summary>
    public partial class CodeAnchorsControl : UserControl
    {
        private readonly ObservableCollection<AnchorItem> _allAnchors = new ObservableCollection<AnchorItem>();
        private readonly CollectionViewSource _viewSource;
        private string _currentTypeFilter = "All";
        private string _currentSearchFilter = string.Empty;
        private bool _groupByFile;

        /// <summary>
        /// Event raised when an anchor is activated (double-click or Enter).
        /// </summary>
        public event EventHandler<AnchorItem> AnchorActivated;

        /// <summary>
        /// Event raised when refresh is requested.
        /// </summary>
        public event EventHandler RefreshRequested;

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
        public IReadOnlyList<AnchorItem> AllAnchors => _allAnchors.ToList();

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

            int currentIndex = AnchorDataGrid.SelectedIndex;
            int nextIndex = currentIndex + 1;

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

            int currentIndex = AnchorDataGrid.SelectedIndex;
            int prevIndex = currentIndex - 1;

            if (prevIndex < 0)
            {
                prevIndex = AnchorDataGrid.Items.Count - 1; // Wrap around
            }

            AnchorDataGrid.SelectedIndex = prevIndex;
            AnchorDataGrid.ScrollIntoView(AnchorDataGrid.SelectedItem);

            return SelectedAnchor;
        }

        private void UpdateStatus()
        {
            int totalCount = _allAnchors.Count;
            int visibleCount = _viewSource.View.Cast<object>().Count();

            bool hasTypeFilter = _currentTypeFilter != "All";
            bool hasSearchFilter = !string.IsNullOrWhiteSpace(_currentSearchFilter);

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
                bool passesTypeFilter = _currentTypeFilter == "All" ||
                    anchor.TypeDisplayName.Equals(_currentTypeFilter, StringComparison.OrdinalIgnoreCase);

                // Check search filter
                bool passesSearchFilter = string.IsNullOrWhiteSpace(_currentSearchFilter) ||
                    MatchesSearch(anchor, _currentSearchFilter);

                e.Accepted = passesTypeFilter && passesSearchFilter;
            }
        }

        private bool MatchesSearch(AnchorItem anchor, string searchText)
        {
            // Search in message, file name, project name, and metadata (case-insensitive)
            return (anchor.Message != null && anchor.Message.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (anchor.FileName != null && anchor.FileName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (anchor.Project != null && anchor.Project.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (anchor.Owner != null && anchor.Owner.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (anchor.IssueReference != null && anchor.IssueReference.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (anchor.AnchorId != null && anchor.AnchorId.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (anchor.MetadataDisplay != null && anchor.MetadataDisplay.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Guard against event firing during InitializeComponent before _viewSource is initialized
            if (_viewSource == null)
            {
                return;
            }

            _currentSearchFilter = SearchTextBox.Text ?? string.Empty;
            _viewSource.View.Refresh();
            UpdateStatus();
        }

        private void FilterComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Guard against event firing during InitializeComponent before _viewSource is initialized
            if (_viewSource == null)
            {
                return;
            }

            if (FilterComboBox.SelectedItem is ComboBoxItem item)
            {
                _currentTypeFilter = item.Content.ToString();
                _viewSource.View.Refresh();
                UpdateStatus();
            }
        }

        private void GroupByFileCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // Guard against event firing during InitializeComponent before _viewSource is initialized
            if (_viewSource == null)
            {
                return;
            }

            _groupByFile = GroupByFileCheckBox.IsChecked ?? false;

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
                // Find the CrispImage in the row and set its moniker
                DataGridRow row = e.Row;
                row.Loaded += (s, args) =>
                {
                    CrispImage image = FindVisualChild<CrispImage>(row);
                    if (image != null)
                    {
                        image.Moniker = anchor.AnchorType.GetImageMoniker();
                    }
                };
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
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
