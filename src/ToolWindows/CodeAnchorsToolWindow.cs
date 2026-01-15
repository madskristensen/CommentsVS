using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommentsVS.Options;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Tool window for displaying code anchors (TODO, HACK, ANCHOR, etc.) from the entire solution.
    /// Coordinates helper classes for navigation, solution events, document events, and filtering.
    /// </summary>
    public class CodeAnchorsToolWindow : BaseToolWindow<CodeAnchorsToolWindow>
    {
        private CodeAnchorsControl _control;
        private readonly AnchorService _anchorService = new();
        private SolutionAnchorScanner _scanner;
        private SolutionAnchorCache _cache;
        private AnchorScope _currentScope = AnchorScope.EntireSolution;

        // Extracted helper classes for Single Responsibility Principle
        private AnchorNavigationService _navigationService;
        private SolutionEventCoordinator _solutionEventCoordinator;
        private DocumentEventCoordinator _documentEventCoordinator;
        private AnchorScopeFilter _scopeFilter;

        /// <summary>
        /// Gets the current instance of the tool window (set after CreateAsync is called).
        /// </summary>
        public static CodeAnchorsToolWindow Instance { get; private set; }

        /// <summary>
        /// Gets the tool window instance asynchronously.
        /// This ensures the tool window has been created before returning the instance.
        /// </summary>
        /// <param name="create">If true, creates the tool window if it doesn't exist (but doesn't show it).</param>
        /// <returns>The tool window instance, or null if not yet created and create is false.</returns>
        public static async Task<CodeAnchorsToolWindow> GetInstanceAsync(bool create = false)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (Instance != null)
            {
                return Instance;
            }

            if (create)
            {
                // ShowAsync will create the window (triggering CreateAsync which sets Instance)
                // but we immediately hide it if successful to avoid unwanted UI changes
                ToolWindowPane pane = await ShowAsync(0, create: true);
                if (pane != null)
                {
                    await HideAsync();
                }
            }

            return Instance;
        }

        /// <summary>
        /// Gets the control hosted in this tool window.
        /// </summary>
        public CodeAnchorsControl Control => _control;

        /// <summary>
        /// Gets the solution anchor cache.
        /// </summary>
        public SolutionAnchorCache Cache => _cache;

        /// <summary>
        /// Gets the solution anchor scanner.
        /// </summary>
        public SolutionAnchorScanner Scanner => _scanner;

        /// <summary>
        /// Gets a value indicating whether a scan is currently in progress.
        /// </summary>
        public bool IsScanning => _scanner?.IsScanning ?? false;

        /// <summary>
        /// Gets the current scope filter for anchors.
        /// </summary>
        public AnchorScope CurrentScope => _currentScope;

        public override string GetTitle(int toolWindowId) => "Code Anchors";

        public override Type PaneType => typeof(CodeAnchorsToolWindowPane);

        public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            Instance = this;

            // Initialize services (doesn't require main thread)
            _cache = new SolutionAnchorCache();
            _scanner = new SolutionAnchorScanner(_anchorService, _cache);

            // Initialize helper classes
            _navigationService = new AnchorNavigationService();
            _solutionEventCoordinator = new SolutionEventCoordinator(_cache, _scanner);
            _documentEventCoordinator = new DocumentEventCoordinator(_cache, _scanner);
            _scopeFilter = new AnchorScopeFilter(_documentEventCoordinator);

            // Subscribe to scanner events (doesn't require main thread)
            _scanner.ScanStarted += OnScanStarted;
            _scanner.ScanProgress += OnScanProgress;
            _scanner.ScanCompleted += OnScanCompleted;

            // Subscribe to helper class events
            _solutionEventCoordinator.CacheUpdated += OnCacheUpdated;
            _solutionEventCoordinator.SolutionClosed += OnSolutionClosedEvent;
            _documentEventCoordinator.DocumentScanned += OnDocumentScanned;

            // Get options before switching threads
            General options = await General.GetLiveInstanceAsync();

            // Switch to main thread for WPF control creation and VS event subscriptions
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _control = new CodeAnchorsControl();
            _control.AnchorActivated += OnAnchorActivated;

            // Subscribe helper classes to VS events
            _solutionEventCoordinator.Subscribe();
            _documentEventCoordinator.Subscribe();

            // Check if a solution is already open (window opened after solution loaded)
            Solution currentSolution = await VS.Solutions.GetCurrentSolutionAsync();
            var solutionAlreadyOpen = currentSolution != null && !string.IsNullOrEmpty(currentSolution.FullPath);

            if (solutionAlreadyOpen)
            {
                // Solution is already open - try to load cache and/or scan
                // This handles the case where the tool window is opened after a solution is already loaded
                InitializeForOpenSolutionAsync().FireAndForget();
            }
            else
            {
                // No solution - check if there are any open documents (misc files)
                // and scan them for anchors
                ScanOpenDocumentsAsync().FireAndForget();
            }

            return _control;
        }

        private void OnCacheUpdated(object sender, System.EventArgs e)
        {
            RefreshAnchorsFromCache();
        }

        private void OnSolutionClosedEvent(object sender, System.EventArgs e)
        {
            _control?.ClearAnchors();
            _control?.UpdateStatus("No solution loaded");
        }

        private void OnDocumentScanned(object sender, System.EventArgs e)
        {
            RefreshAnchorsFromCache();
        }

        /// <summary>
        /// Initializes the tool window when a solution is already open.
        /// Tries to load cache first, then scans if needed.
        /// </summary>
        private async Task InitializeForOpenSolutionAsync()
        {
            // Try to load cache from disk first
            var solutionDir = await _solutionEventCoordinator.GetSolutionDirectoryAsync();
            var cacheLoaded = false;

            if (!string.IsNullOrEmpty(solutionDir))
            {
                cacheLoaded = _cache.LoadFromDisk(solutionDir);
                if (cacheLoaded)
                {
                    // Refresh UI with loaded cache
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    RefreshAnchorsFromCache();
                }
            }

            // If cache not loaded or scan on load is enabled, scan the solution
            General options = await General.GetLiveInstanceAsync();
            if (!cacheLoaded || options.ScanSolutionOnLoad)
            {
                await _scanner.ScanSolutionAsync();
            }
        }

        /// <summary>
        /// Scans all currently open documents for anchors (used when no solution is loaded).
        /// </summary>
        private async Task ScanOpenDocumentsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Use DTE to get all open documents
                EnvDTE.DTE dte = await VS.GetServiceAsync<EnvDTE.DTE, EnvDTE.DTE>();
                if (dte?.Documents == null || dte.Documents.Count == 0)
                {
                    _control?.UpdateStatus("No open documents");
                    return;
                }

                var documentCount = 0;
                var anchorCount = 0;

                _control?.UpdateStatus("Scanning open documents...");

                foreach (EnvDTE.Document doc in dte.Documents)
                {
                    var filePath = doc?.FullName;
                    if (string.IsNullOrEmpty(filePath))
                    {
                        continue;
                    }

                    // Scan the file and add to cache
                    IReadOnlyList<AnchorItem> anchors = await _scanner.ScanFileAsync(filePath, projectName: null);
                    _cache.AddOrUpdateFile(filePath, anchors);
                    documentCount++;
                    anchorCount += anchors.Count;
                }

                // Refresh UI with scanned anchors
                        RefreshAnchorsFromCache();

                        // Show scan result in VS StatusBar
                        await VS.StatusBar.ShowMessageAsync($"Code Anchors: Scanned {documentCount} document(s), found {anchorCount} anchor(s)");
                    }
                    catch
                    {
                        // Ignore errors getting open documents
                    }
                }

                /// <summary>
                /// Scans open documents that are not part of the solution (miscellaneous files).
                /// </summary>
                /// <returns>The number of miscellaneous files scanned and anchors found.</returns>
                private async Task<(int fileCount, int anchorCount)> ScanMiscellaneousFilesAsync()
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var fileCount = 0;
                    var anchorCount = 0;

                    try
                    {
                        // Use DTE to get all open documents
                        EnvDTE.DTE dte = await VS.GetServiceAsync<EnvDTE.DTE, EnvDTE.DTE>();
                        if (dte?.Documents == null || dte.Documents.Count == 0)
                        {
                            return (0, 0);
                        }

                        // Get the set of files already in the cache (from solution scan)
                        var cachedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (AnchorItem anchor in _cache.GetAllAnchors())
                        {
                            if (!string.IsNullOrEmpty(anchor.FilePath))
                            {
                                cachedFiles.Add(anchor.FilePath);
                            }
                        }

                        // Also get all files that were scanned (even if they had no anchors)
                        // by checking which open documents are part of the solution
                        Solution solution = await VS.Solutions.GetCurrentSolutionAsync();
                        var solutionDir = !string.IsNullOrEmpty(solution?.FullPath)
                            ? System.IO.Path.GetDirectoryName(solution.FullPath)
                            : null;

                        foreach (EnvDTE.Document doc in dte.Documents)
                        {
                            var filePath = doc?.FullName;
                            if (string.IsNullOrEmpty(filePath))
                            {
                                continue;
                            }

                            // Skip if file is already in cache (was part of solution scan)
                            if (cachedFiles.Contains(filePath))
                            {
                                continue;
                            }

                            // Skip if file is within solution directory (likely part of solution, just had no anchors)
                            if (!string.IsNullOrEmpty(solutionDir) &&
                                filePath.StartsWith(solutionDir, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            // This is a miscellaneous file - scan it
                            IReadOnlyList<AnchorItem> anchors = await _scanner.ScanFileAsync(filePath, projectName: null);
                            _cache.AddOrUpdateFile(filePath, anchors);
                            fileCount++;
                            anchorCount += anchors.Count;
                        }
                    }
                    catch
                    {
                        // Ignore errors
                    }

                    return (fileCount, anchorCount);
                }

                /// <summary>
                /// Refreshes the anchors by scanning either the solution or open documents.
                /// </summary>
                public async Task RefreshAsync()
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    // Check if a solution is open
                    var solutionOpen = await VS.Solutions.IsOpenAsync();

                    if (solutionOpen)
                    {
                        // Scan the full solution
                        await ScanSolutionAsync();

                        // Also scan any miscellaneous files that are open but not part of the solution
                        (int miscFileCount, int miscAnchorCount) = await ScanMiscellaneousFilesAsync();
                        if (miscFileCount > 0)
                        {
                            // Refresh UI to include misc file anchors
                            RefreshAnchorsFromCache();

                            // Update status bar to include misc files info
                            var totalAnchors = _cache.TotalAnchorCount;
                            await VS.StatusBar.ShowMessageAsync(
                                $"Code Anchors: Found {totalAnchors} anchor(s) ({miscAnchorCount} from {miscFileCount} misc file(s))");
                        }
                    }
                    else
                    {
                        // No solution - scan open documents instead
                        await ScanOpenDocumentsAsync();
                    }
                }

        /// <summary>
        /// Scans the entire solution for anchors in the background.
        /// </summary>
        public async Task ScanSolutionAsync()
        {
            if (_scanner == null)
            {
                return;
            }

            await _scanner.ScanSolutionAsync();
        }

        /// <summary>
        /// Sets the scope filter for the tool window.
        /// </summary>
        /// <param name="scope">The new scope to apply.</param>
        public void SetScope(AnchorScope scope)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _currentScope = scope;
            RefreshAnchorsFromCache();
        }

        /// <summary>
        /// Sets the type filter for the tool window.
        /// </summary>
        /// <param name="typeFilter">The type filter to apply (All, TODO, HACK, etc.).</param>
        public void SetTypeFilter(string typeFilter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _control?.SetTypeFilter(typeFilter);
        }

        /// <summary>
        /// Navigates to the next anchor in the list.
        /// </summary>
        public async Task NavigateToNextAnchorAsync()
        {
            await _navigationService.NavigateToNextAnchorAsync(_control);
        }

        /// <summary>
        /// Navigates to the previous anchor in the list.
        /// </summary>
        public async Task NavigateToPreviousAnchorAsync()
        {
            await _navigationService.NavigateToPreviousAnchorAsync(_control);
        }

        private void OnAnchorActivated(object sender, AnchorItem anchor) => _navigationService.NavigateToAnchorAsync(anchor).FireAndForget();

        private void OnScanStarted(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _control?.UpdateStatus("Scanning solution...");
            }).FireAndForget();
        }

        private void OnScanProgress(object sender, ScanProgressEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        _control?.UpdateStatus($"Scanning... {e.ProcessedFiles}/{e.TotalFiles} files ({e.AnchorsFound} anchors)");
                    }).FireAndForget();
                }

                private void OnScanCompleted(object sender, ScanCompletedEventArgs e)
                {
                    ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        if (e.WasCancelled)
                        {
                            _control?.UpdateStatus(e.ErrorMessage ?? "Scan cancelled");
                        }
                        else
                        {
                            // Update the control with all anchors from cache
                            RefreshAnchorsFromCache();

                            // Save cache to disk after successful scan
                            await _solutionEventCoordinator.SaveCacheAsync();

                            // Show scan result in VS StatusBar
                            await VS.StatusBar.ShowMessageAsync($"Code Anchors: Found {e.TotalAnchors} anchor(s)");
                        }
                    }).FireAndForget();
                }

        private void RefreshAnchorsFromCache()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_cache == null || _control == null)
            {
                return;
            }

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                IReadOnlyList<AnchorItem> allAnchors = _cache.GetAllAnchors();
                IReadOnlyList<AnchorItem> filteredAnchors = await _scopeFilter.ApplyFilterAsync(allAnchors, _currentScope);
                _control.UpdateAnchors(filteredAnchors);
            });
        }
    }
}
