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

            // Start scanning if enabled (runs on background thread)
            if (options.ScanSolutionOnLoad)
            {
                // Fire and forget - don't block tool window creation
                ScanSolutionAsync().FireAndForget();
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
