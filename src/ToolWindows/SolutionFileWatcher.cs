using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommentsVS.Options;
using EnvDTE80;
using DTESolution = EnvDTE.Solution;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Watches for file changes in the solution and triggers re-scanning.
    /// </summary>
    public class SolutionFileWatcher : IDisposable
    {
        private readonly SolutionAnchorScanner _scanner;
        private readonly SolutionAnchorCache _cache;
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>(StringComparer.OrdinalIgnoreCase);
        private readonly object _watcherLock = new object();
        private readonly HashSet<string> _pendingChanges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _pendingLock = new object();
        private Timer _debounceTimer;
        private bool _disposed;

        /// <summary>
        /// Event raised when a file has been scanned after a change.
        /// </summary>
        public event EventHandler<FileScannedEventArgs> FileScanned;

        /// <summary>
        /// Initializes a new instance of the <see cref="SolutionFileWatcher"/> class.
        /// </summary>
        public SolutionFileWatcher(SolutionAnchorScanner scanner, SolutionAnchorCache cache)
        {
            _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// Starts watching the solution for file changes.
        /// </summary>
        public async Task StartWatchingAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();


            // Stop any existing watchers
            StopWatching();

            DTE2 dte = await VS.GetServiceAsync<EnvDTE.DTE, DTE2>();
            EnvDTE.Solution solution = dte?.Solution;

            if (solution == null || string.IsNullOrEmpty(solution.FullName))
            {
                return;
            }

            string solutionDir = Path.GetDirectoryName(solution.FullName);
            if (string.IsNullOrEmpty(solutionDir) || !Directory.Exists(solutionDir))
            {
                return;

            }

            General options = await General.GetLiveInstanceAsync();
            HashSet<string> extensionsToScan = options.GetFileExtensionsSet();

            // Create a watcher for the solution directory
            CreateWatcher(solutionDir, extensionsToScan);
        }

        /// <summary>
        /// Stops watching for file changes.
        /// </summary>
        public void StopWatching()
        {
            lock (_watcherLock)
            {
                foreach (var watcher in _watchers.Values)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                _watchers.Clear();
            }

            _debounceTimer?.Dispose();
            _debounceTimer = null;

            lock (_pendingLock)
            {
                _pendingChanges.Clear();
            }
        }

        private void CreateWatcher(string directory, HashSet<string> extensionsToScan)
        {
            lock (_watcherLock)
            {
                if (_watchers.ContainsKey(directory))
                {
                    return;
                }

                try
                {
                    var watcher = new FileSystemWatcher(directory)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                    };

                    watcher.Changed += (s, e) => OnFileChanged(e.FullPath, extensionsToScan);
                    watcher.Created += (s, e) => OnFileChanged(e.FullPath, extensionsToScan);
                    watcher.Deleted += (s, e) => OnFileDeleted(e.FullPath, extensionsToScan);
                    watcher.Renamed += (s, e) => OnFileRenamed(e.OldFullPath, e.FullPath, extensionsToScan);
                    watcher.Error += OnWatcherError;

                    watcher.EnableRaisingEvents = true;
                    _watchers[directory] = watcher;
                }
                catch (Exception)
                {
                    // Directory might not be accessible
                }
            }
        }

        private void OnFileChanged(string filePath, HashSet<string> extensionsToScan)
        {
            if (!ShouldProcessFile(filePath, extensionsToScan))
            {
                return;
            }

            QueueFileForProcessing(filePath);
        }

        private void OnFileDeleted(string filePath, HashSet<string> extensionsToScan)
        {
            if (!ShouldProcessFile(filePath, extensionsToScan))
            {
                return;
            }

            // Remove from cache immediately
            _cache.RemoveFile(filePath);
            FileScanned?.Invoke(this, new FileScannedEventArgs(filePath, wasDeleted: true));
        }

        private void OnFileRenamed(string oldPath, string newPath, HashSet<string> extensionsToScan)
        {
            // Handle old file removal
            if (ShouldProcessFile(oldPath, extensionsToScan))
            {
                _cache.RemoveFile(oldPath);
            }

            // Handle new file addition
            if (ShouldProcessFile(newPath, extensionsToScan))
            {
                QueueFileForProcessing(newPath);
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            // Log or handle watcher errors if needed
            // For now, just restart watching
            _ = RestartWatchingAsync();
        }

        private async Task RestartWatchingAsync()
        {
            await Task.Delay(1000); // Brief delay before restarting
            await StartWatchingAsync();
        }

        private bool ShouldProcessFile(string filePath, HashSet<string> extensionsToScan)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            string extension = Path.GetExtension(filePath);
            if (!extensionsToScan.Contains(extension))
            {
                return false;
            }

            // Check for ignored folders
            var options = General.GetLiveInstanceAsync().GetAwaiter().GetResult();
            HashSet<string> ignoredFolders = options.GetIgnoredFoldersSet();

            string[] pathParts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string part in pathParts)
            {
                if (ignoredFolders.Contains(part))
                {
                    return false;
                }
            }

            return true;
        }

        private void QueueFileForProcessing(string filePath)
        {
            lock (_pendingLock)
            {
                _pendingChanges.Add(filePath);
            }

            // Debounce - wait 500ms before processing to batch rapid changes
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(ProcessPendingChanges, null, 500, Timeout.Infinite);
        }

        private void ProcessPendingChanges(object state)
        {
            List<string> filesToProcess;
            lock (_pendingLock)
            {
                if (_pendingChanges.Count == 0)
                {
                    return;
                }

                filesToProcess = new List<string>(_pendingChanges);
                _pendingChanges.Clear();
            }

            // Process on background thread
            Task.Run(async () =>
            {
                foreach (string filePath in filesToProcess)
                {
                    if (_disposed)
                    {
                        break;
                    }

                    try
                    {
                        if (!File.Exists(filePath))
                        {
                            _cache.RemoveFile(filePath);
                            FileScanned?.Invoke(this, new FileScannedEventArgs(filePath, wasDeleted: true));
                            continue;
                        }

                        // Get project name (best effort)
                        string projectName = await GetProjectNameForFileAsync(filePath);

                        IReadOnlyList<AnchorItem> anchors = await _scanner.ScanFileAsync(filePath, projectName);
                        _cache.AddOrUpdateFile(filePath, anchors);
                        FileScanned?.Invoke(this, new FileScannedEventArgs(filePath, wasDeleted: false));
                    }
                    catch (Exception)
                    {
                        // Skip files that can't be processed
                    }
                }
            });
        }

        private async Task<string> GetProjectNameForFileAsync(string filePath)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                DTE2 dte = await VS.GetServiceAsync<EnvDTE.DTE, DTE2>();
                if (dte?.Solution == null)
                {
                    return null;
                }

                EnvDTE.ProjectItem projectItem = dte.Solution.FindProjectItem(filePath);
                return projectItem?.ContainingProject?.Name;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Disposes the file watcher and releases all resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopWatching();
        }
    }

    /// <summary>
    /// Event arguments for file scanned events.
    /// </summary>
    public class FileScannedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the file path that was scanned.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets a value indicating whether the file was deleted.
        /// </summary>
        public bool WasDeleted { get; }

        public FileScannedEventArgs(string filePath, bool wasDeleted)
        {
            FilePath = filePath;
            WasDeleted = wasDeleted;
        }
    }
}
