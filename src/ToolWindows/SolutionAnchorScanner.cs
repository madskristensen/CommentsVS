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
                await CollectFilesFromFileSystemAsync(solutionDir, solution.FullName, filesToScan, extensionsToScan, foldersToIgnore, ct).ConfigureAwait(false);

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

                        (var filePath, var projectName) = fileInfo;

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

        private static Task CollectFilesFromFileSystemAsync(
            string solutionDir,
            string solutionFullName,
            List<(string, string)> filesToScan,
            HashSet<string> extensionsToScan,
            HashSet<string> foldersToIgnore,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var solutionName = Path.GetFileNameWithoutExtension(solutionFullName);
                var pendingDirectories = new Stack<string>();
                pendingDirectories.Push(solutionDir);

                while (pendingDirectories.Count > 0)
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    string currentDirectory = pendingDirectories.Pop();

                    try
                    {
                        foreach (string directory in Directory.EnumerateDirectories(currentDirectory))
                        {
                            if (ct.IsCancellationRequested)
                            {
                                break;
                            }

                            string folderName = Path.GetFileName(directory);
                            if (!string.IsNullOrEmpty(folderName) && foldersToIgnore.Contains(folderName))
                            {
                                continue;
                            }

                            pendingDirectories.Push(directory);
                        }

                        foreach (string filePath in Directory.EnumerateFiles(currentDirectory))
                        {
                            if (ct.IsCancellationRequested)
                            {
                                break;
                            }

                            string extension = Path.GetExtension(filePath);
                            if (!string.IsNullOrEmpty(extension) && extensionsToScan.Contains(extension))
                            {
                                filesToScan.Add((filePath, solutionName));
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip directories that cannot be accessed
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // Skip directories removed while scanning
                    }
                    catch (IOException)
                    {
                        // Skip transient file system errors
                    }
                }
            }, ct);
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
