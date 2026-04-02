using System.Collections.Generic;
using System.Threading.Tasks;
using CommentsVS.Services;
using Microsoft.VisualStudio.Shell.Interop;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Coordinates document-level events (save/open/close) for the Code Anchors tool window,
    /// triggering rescans of individual files when they change.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DocumentEventCoordinator"/> class.
    /// </remarks>
    /// <param name="cache">The anchor cache.</param>
    /// <param name="scanner">The solution scanner.</param>
    internal sealed class DocumentEventCoordinator(SolutionAnchorCache cache, SolutionAnchorScanner scanner) : IDisposable
    {
        private readonly SolutionAnchorCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        private readonly SolutionAnchorScanner _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        private bool _disposed;
        private string _lastActiveDocumentPath;

        /// <summary>
        /// Raised when a document has been scanned and the UI should refresh.
        /// </summary>
        public event EventHandler DocumentScanned;

        /// <summary>
        /// Raised when the active document changes in the editor.
        /// </summary>
        public event EventHandler ActiveDocumentChanged;

        /// <summary>
        /// Subscribes to document events. Must be called from the UI thread.
        /// </summary>
        public void Subscribe()
        {
            VS.Events.DocumentEvents.Saved += OnDocumentSaved;
            VS.Events.DocumentEvents.Opened += OnDocumentOpened;
            VS.Events.DocumentEvents.Closed += OnDocumentClosed;
            VS.Events.WindowEvents.ActiveFrameChanged += OnActiveFrameChanged;
        }

        private void OnDocumentSaved(string filePath)
        {
            // Clear .editorconfig caches when a .editorconfig file is saved
            if (System.IO.Path.GetFileName(filePath).Equals(".editorconfig", StringComparison.OrdinalIgnoreCase))
            {
                EditorConfigSettings.ClearCaches();
            }

            // Rescan the saved file and update the cache
            ScanAndUpdateCacheAsync(filePath).FireAndForget();
        }

        private void OnDocumentOpened(string filePath)
        {
            // Scan the opened file and add to cache (for misc files without a solution)
            ScanAndUpdateCacheAsync(filePath).FireAndForget();
        }

        /// <summary>
        /// Shared async method for scanning a file and updating the cache.
        /// Reduces lambda allocations compared to inline async delegates.
        /// </summary>
        private async Task ScanAndUpdateCacheAsync(string filePath)
        {
            if (_cache == null || _scanner == null || _disposed)
            {
                return;
            }

            var projectName = await GetProjectNameForFileAsync(filePath);
            IReadOnlyList<AnchorItem> anchors = await _scanner.ScanFileAsync(filePath, projectName);
            _cache.AddOrUpdateFile(filePath, anchors);

            // Notify UI to refresh
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!_disposed)
            {
                DocumentScanned?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnDocumentClosed(string filePath)
        {
            // Remove the file from cache when closed (for misc files without a solution)
            // Only do this when no solution is loaded, otherwise the file stays in cache
            HandleDocumentClosedAsync(filePath).FireAndForget();
        }

        /// <summary>
        /// Shared async method for handling document close.
        /// Reduces lambda allocations compared to inline async delegates.
        /// </summary>
        private async Task HandleDocumentClosedAsync(string filePath)
        {
            if (_cache == null || _disposed)
            {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Check if a solution is loaded
            var solutionLoaded = await VS.Solutions.IsOpenAsync();
            if (!solutionLoaded && !_disposed)
            {
                // No solution - remove the file from cache
                _cache.RemoveFile(filePath);
                DocumentScanned?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets the project name for the specified file path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns>The project name, or null if not found.</returns>
        public async Task<string> GetProjectNameForFileAsync(string filePath)
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

        private void OnActiveFrameChanged(ActiveFrameChangeEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_disposed)
            {
                return;
            }

            // Check if the new frame is a document frame by trying to get its document path
            string newDocumentPath = GetDocumentPathFromFrame(e.NewFrame);
            if (string.IsNullOrEmpty(newDocumentPath))
            {
                return;
            }

            // Only fire the event if the active document actually changed
            if (!string.Equals(_lastActiveDocumentPath, newDocumentPath, StringComparison.OrdinalIgnoreCase))
            {
                _lastActiveDocumentPath = newDocumentPath;
                ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private static string GetDocumentPathFromFrame(WindowFrame frame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (frame == null)
            {
                return null;
            }

            try
            {
                // Get the document moniker (file path) from the frame
                // __VSFPROPID.VSFPROPID_pszMkDocument contains the document moniker for document windows
                ((IVsWindowFrame)frame).GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out object docPath);
                return docPath as string;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            VS.Events.DocumentEvents.Saved -= OnDocumentSaved;
            VS.Events.DocumentEvents.Opened -= OnDocumentOpened;
            VS.Events.DocumentEvents.Closed -= OnDocumentClosed;
            VS.Events.WindowEvents.ActiveFrameChanged -= OnActiveFrameChanged;
        }
    }
}
