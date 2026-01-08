using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommentsVS.Options;
using Microsoft.VisualStudio.Text;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Tool window for displaying code anchors (TODO, HACK, ANCHOR, etc.) from the entire solution.
    /// </summary>
    public class CodeAnchorsToolWindow : BaseToolWindow<CodeAnchorsToolWindow>
    {
        private CodeAnchorsControl _control;
        private readonly AnchorService _anchorService = new();
        private SolutionAnchorScanner _scanner;
        private SolutionAnchorCache _cache;
        private AnchorScope _currentScope = AnchorScope.EntireSolution;

        /// <summary>
        /// Gets the current instance of the tool window (set after CreateAsync is called).
        /// </summary>
        public static CodeAnchorsToolWindow Instance { get; private set; }

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

            // Subscribe to scanner events (doesn't require main thread)
            _scanner.ScanStarted += OnScanStarted;
            _scanner.ScanProgress += OnScanProgress;
            _scanner.ScanCompleted += OnScanCompleted;

            // Get options before switching threads
            General options = await General.GetLiveInstanceAsync();

            // Switch to main thread for WPF control creation and VS event subscriptions
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _control = new CodeAnchorsControl();
            _control.AnchorActivated += OnAnchorActivated;

            // Subscribe to solution events
            VS.Events.SolutionEvents.OnAfterOpenSolution += OnSolutionOpened;
            VS.Events.SolutionEvents.OnAfterCloseSolution += OnSolutionClosed;

            // Subscribe to document events for real-time updates
            VS.Events.DocumentEvents.Saved += OnDocumentSaved;
            VS.Events.DocumentEvents.Opened += OnDocumentOpened;
            VS.Events.DocumentEvents.Closed += OnDocumentClosed;

            // Start scanning if enabled (runs on background thread)
            if (options.ScanSolutionOnLoad)
            {
                // Fire and forget - don't block tool window creation
                ScanSolutionAsync().FireAndForget();
            }

            return _control;
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
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            AnchorItem anchor = _control?.SelectNextAnchor();
            if (anchor != null)
            {
                await NavigateToAnchorAsync(anchor);
            }
        }

        /// <summary>
        /// Navigates to the previous anchor in the list.
        /// </summary>
        public async Task NavigateToPreviousAnchorAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            AnchorItem anchor = _control?.SelectPreviousAnchor();
            if (anchor != null)
            {
                await NavigateToAnchorAsync(anchor);
            }
        }

        private void OnAnchorActivated(object sender, AnchorItem anchor) => NavigateToAnchorAsync(anchor).FireAndForget();

        private void OnSolutionOpened(Solution solution)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                // Try to load cache from disk first
                var solutionDir = await GetSolutionDirectoryAsync();
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
                    await ScanSolutionAsync();
                }
            }).FireAndForget();
        }

        private void OnSolutionClosed()
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                // Save cache to disk before clearing
                var solutionDir = await GetSolutionDirectoryAsync();
                if (!string.IsNullOrEmpty(solutionDir))
                {
                    _cache?.SaveToDisk(solutionDir);
                }

                _cache?.Clear();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _control?.ClearAnchors();
                _control?.UpdateStatus("No solution loaded");
            }).FireAndForget();
        }

        private void OnDocumentSaved(string filePath)
        {
            // Rescan the saved file and update the cache
            if (_cache != null && _scanner != null)
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    var projectName = await GetProjectNameForFileAsync(filePath);
                    IReadOnlyList<AnchorItem> anchors = await _scanner.ScanFileAsync(filePath, projectName);
                    _cache.AddOrUpdateFile(filePath, anchors);

                    // Refresh the UI
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    RefreshAnchorsFromCache();
                }).FireAndForget();
            }
        }

        private void OnDocumentOpened(string filePath)
        {
            // Scan the opened file and add to cache (for misc files without a solution)
            if (_cache != null && _scanner != null)
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    var projectName = await GetProjectNameForFileAsync(filePath);
                    IReadOnlyList<AnchorItem> anchors = await _scanner.ScanFileAsync(filePath, projectName);
                    _cache.AddOrUpdateFile(filePath, anchors);

                    // Refresh the UI
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    RefreshAnchorsFromCache();
                }).FireAndForget();
            }
        }

        private void OnDocumentClosed(string filePath)
        {
            // Remove the file from cache when closed (for misc files without a solution)
            // Only do this when no solution is loaded, otherwise the file stays in cache
            if (_cache != null)
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    // Check if a solution is loaded
                    var solutionLoaded = await VS.Solutions.IsOpenAsync();
                    if (!solutionLoaded)
                    {
                        // No solution - remove the file from cache
                        _cache.RemoveFile(filePath);
                        RefreshAnchorsFromCache();
                    }
                }).FireAndForget();
            }
        }

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
                    var solutionDir = await GetSolutionDirectoryAsync();
                    if (!string.IsNullOrEmpty(solutionDir))
                    {
                        _cache?.SaveToDisk(solutionDir);
                    }
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
                IReadOnlyList<AnchorItem> filteredAnchors = await ApplyScopeFilterAsync(allAnchors);
                _control.UpdateAnchors(filteredAnchors);
            });
        }

        private async Task<IReadOnlyList<AnchorItem>> ApplyScopeFilterAsync(IReadOnlyList<AnchorItem> anchors)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            return _currentScope switch
            {
                AnchorScope.EntireSolution => anchors,
                AnchorScope.CurrentProject => await FilterByCurrentProjectAsync(anchors),
                AnchorScope.CurrentDocument => await FilterByCurrentDocumentAsync(anchors),
                AnchorScope.OpenDocuments => await FilterByOpenDocumentsAsync(anchors),
                _ => anchors,
            };
        }

        private async Task<IReadOnlyList<AnchorItem>> FilterByCurrentProjectAsync(IReadOnlyList<AnchorItem> anchors)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView?.FilePath == null)
            {
                return anchors;
            }

            var projectName = await GetProjectNameForFileAsync(docView.FilePath);
            if (string.IsNullOrEmpty(projectName))
            {
                return anchors;
            }

            return [.. anchors.Where(a => a.Project?.Equals(projectName, StringComparison.OrdinalIgnoreCase) == true)];
        }

        private async Task<IReadOnlyList<AnchorItem>> FilterByCurrentDocumentAsync(IReadOnlyList<AnchorItem> anchors)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();

            if (docView?.FilePath == null)
            {
                return [];
            }

            return [.. anchors.Where(a => a.FilePath?.Equals(docView.FilePath, StringComparison.OrdinalIgnoreCase) == true)];
        }

        private async Task<IReadOnlyList<AnchorItem>> FilterByOpenDocumentsAsync(IReadOnlyList<AnchorItem> anchors)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Use DTE to get open documents
                EnvDTE.DTE dte = await VS.GetServiceAsync<EnvDTE.DTE, EnvDTE.DTE>();
                if (dte?.Documents == null)
                {
                    return anchors;
                }

                var openPathsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (EnvDTE.Document doc in dte.Documents)
                {
                    if (!string.IsNullOrEmpty(doc.FullName))
                    {
                        openPathsSet.Add(doc.FullName);
                    }
                }

                return [.. anchors.Where(a => a.FilePath != null && openPathsSet.Contains(a.FilePath))];
            }
            catch
            {
                // Fall back to returning all anchors if we can't get open documents
                return anchors;
            }
        }

        private async Task NavigateToAnchorAsync(AnchorItem anchor)
        {
            if (anchor == null)
            {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Open the document
            DocumentView docView = await VS.Documents.OpenAsync(anchor.FilePath);
            if (docView?.TextView == null)
            {
                return;
            }

            // Navigate to the line
            try
            {
                ITextSnapshot snapshot = docView.TextView.TextSnapshot;
                if (anchor.LineNumber > 0 && anchor.LineNumber <= snapshot.LineCount)
                {
                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(anchor.LineNumber - 1);
                    SnapshotPoint point = line.Start.Add(Math.Min(anchor.Column, line.Length));

                    docView.TextView.Caret.MoveTo(point);
                    docView.TextView.ViewScroller.EnsureSpanVisible(
                        new SnapshotSpan(point, 0),
                        Microsoft.VisualStudio.Text.Editor.EnsureSpanVisibleOptions.AlwaysCenter);
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        private async Task<string> GetProjectNameForFileAsync(string filePath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                PhysicalFile file = await PhysicalFile.FromFileAsync(filePath);
                Project project = file?.ContainingProject;
                return project?.Name;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> GetSolutionDirectoryAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                Solution solution = await VS.Solutions.GetCurrentSolutionAsync();
                var solutionPath = solution?.FullPath;
                if (!string.IsNullOrEmpty(solutionPath))
                {
                    return System.IO.Path.GetDirectoryName(solutionPath);
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }
    }
}
