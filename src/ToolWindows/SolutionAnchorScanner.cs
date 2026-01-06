using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommentsVS.Options;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using DTESolution = EnvDTE.Solution;
using DTEProject = EnvDTE.Project;
using DTEProjectItem = EnvDTE.ProjectItem;
using DTEProjectItems = EnvDTE.ProjectItems;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Service for scanning entire solutions for code anchors in background.
    /// </summary>
    public class SolutionAnchorScanner
    {
        private readonly AnchorService _anchorService;
        private readonly SolutionAnchorCache _cache;
        private CancellationTokenSource _scanCts;
        private readonly object _scanLock = new object();
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
        /// Initializes a new instance of the <see cref="SolutionAnchorScanner"/> class.
        /// </summary>
        public SolutionAnchorScanner(AnchorService anchorService, SolutionAnchorCache cache)
        {
            _anchorService = anchorService ?? throw new ArgumentNullException(nameof(anchorService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

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

                string solutionDir = Path.GetDirectoryName(solution.FullName);
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

                int totalFiles = filesToScan.Count;
                int processedFiles = 0;
                int totalAnchors = 0;

                // Switch to background thread for file processing
                await Task.Run(async () =>
                {
                    foreach ((string filePath, string projectName) in filesToScan)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            break;
                        }

                        try
                        {
                            IReadOnlyList<AnchorItem> anchors = await ScanFileAsync(filePath, projectName);
                            if (anchors.Count > 0)
                            {
                                _cache.AddOrUpdateFile(filePath, anchors);
                                totalAnchors += anchors.Count;
                            }
                        }
                        catch (Exception)
                        {
                            // Skip files that can't be read
                        }

                        processedFiles++;

                        // Report progress every 10 files or at the end
                        if (processedFiles % 10 == 0 || processedFiles == totalFiles)
                        {
                            ScanProgress?.Invoke(this, new ScanProgressEventArgs(processedFiles, totalFiles, totalAnchors));
                        }
                    }
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
                return Array.Empty<AnchorItem>();
            }

            string content = await ReadFileAsync(filePath);
            if (string.IsNullOrEmpty(content))
            {
                return Array.Empty<AnchorItem>();
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
                string projectName = project.Name;

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
                        string filePath = item.FileNames[1];
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            string extension = Path.GetExtension(filePath);
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
            string[] pathParts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string part in pathParts)
            {
                if (foldersToIgnore.Contains(part))
                {
                    return true;
                }
            }
            return false;
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
    public class ScanCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the total number of anchors found.
        /// </summary>
        public int TotalAnchors { get; }

        /// <summary>
        /// Gets a value indicating whether the scan was cancelled.
        /// </summary>
        public bool WasCancelled { get; }

        /// <summary>
        /// Gets the error message if the scan failed, or null if successful.
        /// </summary>
        public string ErrorMessage { get; }

        public ScanCompletedEventArgs(int totalAnchors, bool wasCancelled, string errorMessage)
        {
            TotalAnchors = totalAnchors;
            WasCancelled = wasCancelled;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Event arguments for scan progress updates.
    /// </summary>
    public class ScanProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the number of files processed so far.
        /// </summary>
        public int ProcessedFiles { get; }

        /// <summary>
        /// Gets the total number of files to process.
        /// </summary>
        public int TotalFiles { get; }

        /// <summary>
        /// Gets the number of anchors found so far.
        /// </summary>
        public int AnchorsFound { get; }

        public ScanProgressEventArgs(int processedFiles, int totalFiles, int anchorsFound)
        {
            ProcessedFiles = processedFiles;
            TotalFiles = totalFiles;
            AnchorsFound = anchorsFound;
        }
    }
}
