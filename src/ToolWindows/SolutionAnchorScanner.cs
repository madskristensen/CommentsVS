using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommentsVS.Options;
using CommentsVS.Services;
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
        private readonly LinkedProjectFileCache _linkedFileCache = new();
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

                using (_cache.BeginUpdate())
                {
                    // Clear cache before scanning
                    _cache.Clear();

                    // Collect all files to scan
                    var filesToScan = new List<(string FilePath, string ProjectName)>();
                    await CollectFilesFromFileSystemAsync(solutionDir, solution.FullName, filesToScan, extensionsToScan, foldersToIgnore, _linkedFileCache, ct).ConfigureAwait(false);

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
        /// Scans a text buffer for anchors. Use this for live document updates.
        /// </summary>
        /// <param name="buffer">The text buffer to scan.</param>
        /// <param name="filePath">The file path associated with the buffer.</param>
        /// <param name="projectName">The project name (optional).</param>
        /// <returns>A list of anchors found in the buffer.</returns>
        public IReadOnlyList<AnchorItem> ScanBuffer(Microsoft.VisualStudio.Text.ITextBuffer buffer, string filePath, string projectName = null)
        {
            if (buffer == null || string.IsNullOrEmpty(filePath))
            {
                return [];
            }

            return _anchorService.ScanBuffer(buffer, filePath, projectName);
        }

        /// <summary>
        /// Scans for newly linked files added to a project file and adds them to the anchor cache.
        /// Use this when a project file is saved to pick up new external linked files without a full re-scan.
        /// </summary>
        /// <param name="projectFilePath">Full path to the saved project file.</param>
        /// <returns>True if any new files with anchors were found and added to the cache.</returns>
        public async Task<bool> ScanNewLinkedFilesForProjectAsync(string projectFilePath)
        {
            if (string.IsNullOrEmpty(projectFilePath) || !File.Exists(projectFilePath))
            {
                return false;
            }

            var projectDir = Path.GetDirectoryName(projectFilePath);
            var projectName = Path.GetFileNameWithoutExtension(projectFilePath);

            // LinkedProjectFileCache re-parses automatically because the file's write-time changed
            var linkedFiles = _linkedFileCache.GetLinkedFiles(projectFilePath, projectDir);

            var anyNew = false;
            foreach (var filePath in linkedFiles)
            {
                // Only scan files not already in the anchor cache
                if (_cache.ContainsFile(filePath))
                {
                    continue;
                }

                var anchors = await ScanFileAsync(filePath, projectName);
                _cache.AddOrUpdateFile(filePath, anchors);

                if (anchors.Count > 0)
                {
                    anyNew = true;
                }
            }

            return anyNew;
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

        private static readonly string[] _projectFilePatterns = ["*.csproj", "*.vbproj", "*.fsproj", "*.esproj", "*.vcxproj", "*.shproj"];

        private static Task CollectFilesFromFileSystemAsync(
            string solutionDir,
            string solutionFullName,
            List<(string, string)> filesToScan,
            HashSet<string> extensionsToScan,
            HashSet<string> foldersToIgnore,
            LinkedProjectFileCache linkedFileCache,
            CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var solutionName = Path.GetFileNameWithoutExtension(solutionFullName);

                // Map from directory path to project name for all discovered projects
                var projectsByDirectory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var allFiles = new List<(string FilePath, string Directory)>();
                // Track files already added to avoid duplicates from multiple projects linking the same file
                var addedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var pendingDirectories = new Stack<string>();
                pendingDirectories.Push(solutionDir);

                while (pendingDirectories.Count > 0)
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    var currentDirectory = pendingDirectories.Pop();

                    try
                    {
                        // Discover project files in this directory
                        foreach (var pattern in _projectFilePatterns)
                        {
                            foreach (var projectFile in Directory.EnumerateFiles(currentDirectory, pattern))
                            {
                                var projectName = Path.GetFileNameWithoutExtension(projectFile);
                                projectsByDirectory[currentDirectory] = projectName;

                                // Add any linked files that live outside this project's directory
                                foreach (var linkedFile in linkedFileCache.GetLinkedFiles(projectFile, currentDirectory))
                                {
                                    if (addedFiles.Add(linkedFile))
                                    {
                                        filesToScan.Add((linkedFile, projectName));
                                    }
                                }

                                break;
                            }

                            if (projectsByDirectory.ContainsKey(currentDirectory))
                            {
                                break;
                            }
                        }

                        foreach (var directory in Directory.EnumerateDirectories(currentDirectory))
                        {
                            if (ct.IsCancellationRequested)
                            {
                                break;
                            }

                            var folderName = Path.GetFileName(directory);
                            if (!string.IsNullOrEmpty(folderName) && foldersToIgnore.Contains(folderName))
                            {
                                continue;
                            }

                            pendingDirectories.Push(directory);
                        }

                        foreach (var filePath in Directory.EnumerateFiles(currentDirectory))
                        {
                            if (ct.IsCancellationRequested)
                            {
                                break;
                            }

                            var extension = Path.GetExtension(filePath);
                            if (!string.IsNullOrEmpty(extension) && extensionsToScan.Contains(extension))
                            {
                                allFiles.Add((filePath, currentDirectory));
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

                // Assign each file to its nearest containing project
                foreach ((var filePath, var fileDirectory) in allFiles)
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    if (addedFiles.Contains(filePath))
                    {
                        continue;
                    }

                    var projectName = FindContainingProjectName(fileDirectory, solutionDir, projectsByDirectory) ?? solutionName;
                    filesToScan.Add((filePath, projectName));
                }
            }, ct);
        }

        /// <summary>
        /// Walks up from <paramref name="directory"/> to <paramref name="solutionDir"/> looking
        /// for the nearest directory that contains a project file.
        /// </summary>
        private static string FindContainingProjectName(string directory, string solutionDir, Dictionary<string, string> projectsByDirectory)
        {
            var dir = directory;
            while (!string.IsNullOrEmpty(dir) && dir.Length >= solutionDir.Length)
            {
                if (projectsByDirectory.TryGetValue(dir, out var projectName))
                {
                    return projectName;
                }

                dir = Path.GetDirectoryName(dir);
            }

            return null;
        }

        private static string ReadFileSync(string filePath)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
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
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
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
