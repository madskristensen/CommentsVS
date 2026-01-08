using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommentsVS.Options;
using EnvDTE80;
using DTEProject = EnvDTE.Project;
using DTEProjectItem = EnvDTE.ProjectItem;
using DTEProjectItems = EnvDTE.ProjectItems;
using DTESolution = EnvDTE.Solution;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Service for scanning entire solutions for code anchors in background.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SolutionAnchorScanner"/> class.
    /// </remarks>
    public class SolutionAnchorScanner(AnchorService anchorService, SolutionAnchorCache cache)
    {
        private readonly AnchorService _anchorService = anchorService ?? throw new ArgumentNullException(nameof(anchorService));
        private readonly SolutionAnchorCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        private CancellationTokenSource _scanCts;
        private readonly object _scanLock = new();
        private bool _isScanning;

        /// <summary>
        /// Event raised when scanning starts.
        /// </summary>
        public event EventHandler ScanStarted;

        /// <summary>
        /// Event raised when scanning completes.
        /// </summary>
        public event EventHandler<ScanCompletedEventArgs> ScanCompleted;

        /// <summary>
        /// Event raised to report scanning progress.
        /// </summary>
        public event EventHandler<ScanProgressEventArgs> ScanProgress;

        /// <summary>
        /// Gets a value indicating whether a scan is currently in progress.
        /// </summary>
        public bool IsScanning => _isScanning;

        /// <summary>
        /// Scans the entire solution for code anchors in the background.
        /// </summary>
        public async Task ScanSolutionAsync()
        {
            // Cancel any existing scan
            CancelScan();

            lock (_scanLock)
            {
                if (_isScanning)
                {
                    return;
                }
                _isScanning = true;
                _scanCts = new CancellationTokenSource();
            }

            CancellationToken ct = _scanCts.Token;

            try
            {
                ScanStarted?.Invoke(this, EventArgs.Empty);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

                // Get solution info
                DTESolution solution = await GetSolutionAsync();
                if (solution == null || string.IsNullOrEmpty(solution.FullName))
                {
                    ScanCompleted?.Invoke(this, new ScanCompletedEventArgs(0, true, "No solution loaded"));
                    return;
                }

                var solutionDir = Path.GetDirectoryName(solution.FullName);
                if (string.IsNullOrEmpty(solutionDir))
                {
                    ScanCompleted?.Invoke(this, new ScanCompletedEventArgs(0, true, "Invalid solution path"));
                    return;
                }



                // Get settings
                General options = await General.GetLiveInstanceAsync();
                HashSet<string> extensionsToScan = options.GetFileExtensionsSet();
                HashSet<string> foldersToIgnore = options.GetIgnoredFoldersSet();

                // Clear cache before scanning
                _cache.Clear();

                // Collect all files to scan
                var filesToScan = new List<(string FilePath, string ProjectName)>();
                await CollectFilesFromSolutionAsync(solution, filesToScan, extensionsToScan, foldersToIgnore, ct);

                if (ct.IsCancellationRequested)
                {
                    ScanCompleted?.Invoke(this, new ScanCompletedEventArgs(0, true, "Scan cancelled"));
                    return;
                }

                var totalFiles = filesToScan.Count;
                var processedFiles = 0;
                var totalAnchors = 0;
                var lastProgressReport = 0;
                var progressLock = new object();

                // Process files in parallel for improved performance
                await Task.Run(() =>
                {
                    var parallelOptions = new ParallelOptions
                    {
                        CancellationToken = ct,
                        MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) // Leave one core free for UI
                    };

                    Parallel.ForEach(filesToScan, parallelOptions, (fileInfo, loopState) =>
                    {
                        if (ct.IsCancellationRequested)
                        {
                            loopState.Stop();
                            return;
                        }

                        (string filePath, string projectName) = fileInfo;

                        try
                        {
                            // Read file content synchronously (we're already on a background thread)
                            var content = ReadFileSync(filePath);
                            if (!string.IsNullOrEmpty(content))
                            {
                                IReadOnlyList<AnchorItem> anchors = _anchorService.ScanText(content, filePath, projectName);
                                if (anchors.Count > 0)
                                {
                                    _cache.AddOrUpdateFile(filePath, anchors);
                                    Interlocked.Add(ref totalAnchors, anchors.Count);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ex.Log();
                        }

                        var currentProcessed = Interlocked.Increment(ref processedFiles);

                        // Report progress every 10 files or at the end (throttled to avoid UI flooding)
                        if (currentProcessed == totalFiles || currentProcessed - lastProgressReport >= 10)
                        {
                            lock (progressLock)
                            {
                                if (currentProcessed - lastProgressReport >= 10 || currentProcessed == totalFiles)
                                {
                                    lastProgressReport = currentProcessed;
                                    ScanProgress?.Invoke(this, new ScanProgressEventArgs(currentProcessed, totalFiles, totalAnchors));
                                }
                            }
                        }
                    });
                }, ct);

                ScanCompleted?.Invoke(this, new ScanCompletedEventArgs(totalAnchors, false, null));
            }
            catch (OperationCanceledException)
            {
                ScanCompleted?.Invoke(this, new ScanCompletedEventArgs(0, true, "Scan cancelled"));
            }
            catch (Exception ex)
            {
                ScanCompleted?.Invoke(this, new ScanCompletedEventArgs(0, true, ex.Message));
            }
            finally
            {
                lock (_scanLock)
                {
                    _isScanning = false;
                }
            }
        }

        /// <summary>
        /// Scans a single file for anchors and updates the cache.
        /// </summary>
        public async Task<IReadOnlyList<AnchorItem>> ScanFileAsync(string filePath, string projectName = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return [];
            }

            var content = await ReadFileAsync(filePath);
            if (string.IsNullOrEmpty(content))
            {
                return [];
            }

            return _anchorService.ScanText(content, filePath, projectName);
        }

        /// <summary>
        /// Cancels any ongoing scan operation.
        /// </summary>
        public void CancelScan()
        {
            lock (_scanLock)
            {
                _scanCts?.Cancel();
                _scanCts?.Dispose();
                _scanCts = null;
            }
        }

        private async Task<DTESolution> GetSolutionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DTE2 dte = await VS.GetServiceAsync<EnvDTE.DTE, DTE2>();
            return dte?.Solution;
        }

        private async Task CollectFilesFromSolutionAsync(
            DTESolution solution,
            List<(string, string)> filesToScan,
            HashSet<string> extensionsToScan,
            HashSet<string> foldersToIgnore,
            CancellationToken ct)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

            if (solution?.Projects == null)
            {
                return;
            }

            foreach (DTEProject project in solution.Projects)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                await CollectFilesFromProjectAsync(project, filesToScan, extensionsToScan, foldersToIgnore, ct);
            }
        }

        private async Task CollectFilesFromProjectAsync(
            DTEProject project,
            List<(string, string)> filesToScan,
            HashSet<string> extensionsToScan,
            HashSet<string> foldersToIgnore,
            CancellationToken ct)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

            if (project == null)
            {
                return;
            }

            try
            {
                var projectName = project.Name;

                // Handle solution folders (Kind = ProjectKinds.vsProjectKindSolutionFolder)
                if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder)
                {
                    // Solution folder - recurse into sub-projects
                    if (project.ProjectItems != null)
                    {
                        foreach (DTEProjectItem item in project.ProjectItems)
                        {
                            if (ct.IsCancellationRequested)
                            {
                                break;
                            }

                            if (item.SubProject != null)
                            {
                                await CollectFilesFromProjectAsync(item.SubProject, filesToScan, extensionsToScan, foldersToIgnore, ct);
                            }
                        }
                    }
                    return;
                }

                // Regular project - collect files from project items
                if (project.ProjectItems != null)
                {
                    await CollectFilesFromProjectItemsAsync(project.ProjectItems, projectName, filesToScan, extensionsToScan, foldersToIgnore, ct);
                }
            }
            catch (Exception)
            {
                // Skip projects that can't be accessed
            }
        }

        private async Task CollectFilesFromProjectItemsAsync(
            DTEProjectItems items,
            string projectName,
            List<(string, string)> filesToScan,
            HashSet<string> extensionsToScan,
            HashSet<string> foldersToIgnore,
            CancellationToken ct)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

            if (items == null)
            {
                return;
            }

            foreach (DTEProjectItem item in items)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    // Check if this is a folder to ignore
                    if (item.Kind == EnvDTE.Constants.vsProjectItemKindPhysicalFolder ||
                        item.Kind == EnvDTE.Constants.vsProjectItemKindVirtualFolder)
                    {
                        if (foldersToIgnore.Contains(item.Name))
                        {
                            continue; // Skip ignored folders
                        }
                    }

                    // Check if it's a file
                    if (item.Kind == EnvDTE.Constants.vsProjectItemKindPhysicalFile)
                    {
                        var filePath = item.FileNames[1];
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            var extension = Path.GetExtension(filePath);
                            if (extensionsToScan.Contains(extension))
                            {
                                // Double-check the path doesn't contain ignored folders
                                if (!ContainsIgnoredFolder(filePath, foldersToIgnore))
                                {
                                    filesToScan.Add((filePath, projectName));
                                }
                            }
                        }
                    }

                    // Recurse into sub-items (folders, etc.)
                    if (item.ProjectItems != null && item.ProjectItems.Count > 0)
                    {
                        await CollectFilesFromProjectItemsAsync(item.ProjectItems, projectName, filesToScan, extensionsToScan, foldersToIgnore, ct);
                    }
                }
                catch (Exception)
                {
                    // Skip items that can't be accessed
                }
            }
        }

        private static bool ContainsIgnoredFolder(string filePath, HashSet<string> foldersToIgnore)
        {
            var pathParts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var part in pathParts)
            {
                if (foldersToIgnore.Contains(part))
                {
                    return true;
                }
            }
            return false;
        }

        private static string ReadFileSync(string filePath)
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string> ReadFileAsync(string filePath)
        {
            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    return await reader.ReadToEndAsync();
                }
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Event arguments for scan completion.
    /// </summary>
    public class ScanCompletedEventArgs(int totalAnchors, bool wasCancelled, string errorMessage) : EventArgs
    {
        /// <summary>
        /// Gets the total number of anchors found.
        /// </summary>
        public int TotalAnchors { get; } = totalAnchors;

        /// <summary>
        /// Gets a value indicating whether the scan was cancelled.
        /// </summary>
        public bool WasCancelled { get; } = wasCancelled;

        /// <summary>
        /// Gets the error message if the scan failed, or null if successful.
        /// </summary>
        public string ErrorMessage { get; } = errorMessage;
    }

    /// <summary>
    /// Event arguments for scan progress updates.
    /// </summary>
    public class ScanProgressEventArgs(int processedFiles, int totalFiles, int anchorsFound) : EventArgs
    {
        /// <summary>
        /// Gets the number of files processed so far.
        /// </summary>
        public int ProcessedFiles { get; } = processedFiles;

        /// <summary>
        /// Gets the total number of files to process.
        /// </summary>
        public int TotalFiles { get; } = totalFiles;

        /// <summary>
        /// Gets the number of anchors found so far.
        /// </summary>
        public int AnchorsFound { get; } = anchorsFound;
    }
}
