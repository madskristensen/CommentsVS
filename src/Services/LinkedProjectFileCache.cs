using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace CommentsVS.Services
{
    /// <summary>
    /// Parses project files to find linked source files that live outside the project directory,
    /// caching results by the project file's last-write timestamp so re-scans are cheap.
    /// </summary>
    internal sealed class LinkedProjectFileCache
    {
        private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        private sealed class CacheEntry(DateTime lastWriteUtc, IReadOnlyList<string> files)
        {
            public DateTime LastWriteUtc { get; } = lastWriteUtc;
            public IReadOnlyList<string> Files { get; } = files;
        }

        /// <summary>
        /// Returns the set of source files referenced by <paramref name="projectFilePath"/>
        /// that are physically located outside <paramref name="projectDir"/>.
        /// Results are cached and invalidated whenever the project file's write-time changes.
        /// Safe to call from any thread.
        /// </summary>
        public IReadOnlyList<string> GetLinkedFiles(string projectFilePath, string projectDir, HashSet<string> extensionsToScan)
        {
            try
            {
                var lastWrite = File.GetLastWriteTimeUtc(projectFilePath);

                lock (_lock)
                {
                    if (_cache.TryGetValue(projectFilePath, out var entry) && entry.LastWriteUtc == lastWrite)
                    {
                        return entry.Files;
                    }
                }

                var files = Parse(projectFilePath, projectDir, extensionsToScan);

                lock (_lock)
                {
                    _cache[projectFilePath] = new CacheEntry(lastWrite, files);
                }

                return files;
            }
            catch
            {
                return [];
            }
        }

        /// <summary>
        /// Clears all cached entries. Call this when the solution is closed or changed.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }

        private static IReadOnlyList<string> Parse(string projectFilePath, string projectDir, HashSet<string> extensionsToScan)
        {
            var result = new List<string>();

            // Ensure projectDir ends with separator so prefix check is unambiguous
            var projectDirNormalized = projectDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                       + Path.DirectorySeparatorChar;

            try
            {
                var doc = XDocument.Load(projectFilePath);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                foreach (var itemGroup in doc.Descendants(ns + "ItemGroup"))
                {
                    foreach (var item in itemGroup.Elements())
                    {
                        var include = (string)item.Attribute("Include");
                        if (string.IsNullOrEmpty(include))
                        {
                            continue;
                        }

                        // Skip glob patterns — expanding them adds complexity and linked files are nearly always explicit paths
                        if (include.IndexOfAny(['*', '?']) >= 0)
                        {
                            continue;
                        }

                        include = include.Replace('/', Path.DirectorySeparatorChar);

                        string fullPath;
                        try
                        {
                            fullPath = Path.GetFullPath(Path.Combine(projectDir, include));
                        }
                        catch
                        {
                            continue;
                        }

                        // Only interested in files outside the project directory
                        if (fullPath.StartsWith(projectDirNormalized, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var ext = Path.GetExtension(fullPath);
                        if (string.IsNullOrEmpty(ext) || !extensionsToScan.Contains(ext))
                        {
                            continue;
                        }

                        if (File.Exists(fullPath))
                        {
                            result.Add(fullPath);
                        }
                    }
                }
            }
            catch
            {
                // Return whatever was collected before the failure
            }

            return result;
        }
    }
}
