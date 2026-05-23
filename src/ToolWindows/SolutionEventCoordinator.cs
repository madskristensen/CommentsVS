using System.IO;
using System.Threading.Tasks;
using CommentsVS.Options;
using Microsoft.VisualStudio.Shell.Interop;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Coordinates solution-level events (open/close) for the Code Anchors tool window,
    /// handling cache persistence and triggering solution scans.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SolutionEventCoordinator"/> class.
    /// </remarks>
    /// <param name="cache">The anchor cache.</param>
    /// <param name="scanner">The solution scanner.</param>
    internal sealed class SolutionEventCoordinator(SolutionAnchorCache cache, SolutionAnchorScanner scanner) : IDisposable
    {
        private readonly SolutionAnchorCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        private readonly SolutionAnchorScanner _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        private bool _disposed;

        /// <summary>
        /// Raised when the cache has been loaded or updated and the UI should refresh.
        /// </summary>
        public event EventHandler CacheUpdated;

        /// <summary>
        /// Raised when the solution is closed and UI should be cleared.
        /// </summary>
        public event EventHandler SolutionClosed;

        /// <summary>
        /// Subscribes to solution events. Must be called from the UI thread.
        /// </summary>
        public void Subscribe()
        {
            VS.Events.SolutionEvents.OnAfterOpenSolution += OnSolutionOpened;
            VS.Events.SolutionEvents.OnAfterCloseSolution += OnSolutionClosedInternal;
        }

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
                        // Notify UI to refresh with loaded cache
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        CacheUpdated?.Invoke(this, EventArgs.Empty);
                    }
                }

                // If cache not loaded or scan on load is enabled, scan the solution
                General options = await General.GetLiveInstanceAsync();
                if (!cacheLoaded || options.ScanSolutionOnLoad)
                {
                    await _scanner.ScanSolutionAsync();
                }
            }).FireAndForget();
        }

        private void OnSolutionClosedInternal()
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                // Save cache to disk before clearing
                var solutionDir = await GetSolutionDirectoryAsync();
                if (!string.IsNullOrEmpty(solutionDir))
                {
                    _ = (_cache?.SaveToDisk(solutionDir));
                }

                _cache?.Clear();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                SolutionClosed?.Invoke(this, EventArgs.Empty);
            }).FireAndForget();
        }

        /// <summary>
        /// Saves the cache to disk for the current solution.
        /// </summary>
        public async Task SaveCacheAsync()
        {
            var solutionDir = await GetSolutionDirectoryAsync();
            if (!string.IsNullOrEmpty(solutionDir))
            {
                _ = (_cache?.SaveToDisk(solutionDir));
            }
        }

        /// <summary>
        /// Gets the current solution directory path.
        /// </summary>
        public async Task<string> GetSolutionDirectoryAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Use IVsSolution.GetSolutionInfo so we return the correct directory in both
                // regular solution mode and Open Folder mode (where DTE.Solution.FullName is
                // the folder path itself, causing Path.GetDirectoryName to return its parent).
                if (Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SVsSolution)) is IVsSolution solution)
                {
                    solution.GetSolutionInfo(out var solutionDirectory, out _, out _);
                    if (!string.IsNullOrWhiteSpace(solutionDirectory))
                    {
                        return solutionDirectory;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            VS.Events.SolutionEvents.OnAfterOpenSolution -= OnSolutionOpened;
            VS.Events.SolutionEvents.OnAfterCloseSolution -= OnSolutionClosedInternal;
        }
    }
}
