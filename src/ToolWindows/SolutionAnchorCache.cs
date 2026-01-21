using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Thread-safe in-memory cache for solution-wide anchor scan results.
    /// </summary>
    public class SolutionAnchorCache
    {
        private readonly ConcurrentDictionary<string, IReadOnlyList<AnchorItem>> _fileAnchors =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Event raised when the cache contents change.
        /// </summary>
        public event EventHandler CacheChanged;

        /// <summary>
        /// Gets the total number of anchors in the cache.
        /// </summary>
        public int TotalAnchorCount => _fileAnchors.Values.Sum(list => list.Count);

        /// <summary>
        /// Gets the number of files in the cache.
        /// </summary>
        public int FileCount => _fileAnchors.Count;

        /// <summary>
        /// Adds or updates anchors for a specific file.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="anchors">The anchors found in the file.</param>
        public void AddOrUpdateFile(string filePath, IReadOnlyList<AnchorItem> anchors)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            if (anchors == null || anchors.Count == 0)
            {
                // Remove file if no anchors
                _fileAnchors.TryRemove(filePath, out _);
            }
            else
            {
                _fileAnchors[filePath] = anchors;
            }

            CacheChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Removes a file from the cache.
        /// </summary>
        /// <param name="filePath">The file path to remove.</param>
        public void RemoveFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            if (_fileAnchors.TryRemove(filePath, out _))
            {
                CacheChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets all anchors for a specific file.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns>The anchors in the file, or an empty list if not found.</returns>
        public IReadOnlyList<AnchorItem> GetAnchorsForFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return [];
            }

            return _fileAnchors.TryGetValue(filePath, out IReadOnlyList<AnchorItem> anchors) ? anchors : [];
        }

        /// <summary>
        /// Gets all anchors from all files in the cache.
        /// </summary>
        /// <returns>All cached anchors.</returns>
        public IReadOnlyList<AnchorItem> GetAllAnchors()
        {
            var allAnchors = new List<AnchorItem>();
            foreach (IReadOnlyList<AnchorItem> anchors in _fileAnchors.Values)
            {
                allAnchors.AddRange(anchors);
            }
            return allAnchors;
        }

        /// <summary>
        /// Checks if a file is in the cache.
        /// </summary>
        /// <param name="filePath">The file path to check.</param>
        /// <returns>True if the file is cached, false otherwise.</returns>
        public bool ContainsFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            return _fileAnchors.ContainsKey(filePath);
        }


        /// <summary>
        /// Gets all distinct anchor type display names present in the cache.
        /// </summary>
        /// <returns>A set of unique type display names (e.g., "TODO", "HACK", "PERF").</returns>
        public HashSet<string> GetDistinctTypes()
        {
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IReadOnlyList<AnchorItem> anchors in _fileAnchors.Values)
            {
                foreach (AnchorItem anchor in anchors)
                {
                    types.Add(anchor.TypeDisplayName);
                }
            }
            return types;
        }

        /// <summary>
        /// Clears all cached data.
        /// </summary>
        public void Clear()
        {
            _fileAnchors.Clear();
            CacheChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets all file paths currently in the cache.
        /// </summary>
        /// <returns>Collection of file paths.</returns>
        public IReadOnlyCollection<string> GetCachedFilePaths()
        {
            return [.. _fileAnchors.Keys];
        }

        /// <summary>
        /// Gets a snapshot of all cached data for serialization.
        /// </summary>
        /// <returns>Dictionary of file paths to anchor lists.</returns>
        public IReadOnlyDictionary<string, IReadOnlyList<AnchorItem>> GetSnapshot()
        {
            return new Dictionary<string, IReadOnlyList<AnchorItem>>(_fileAnchors);
        }

        /// <summary>
        /// Loads cached data from a dictionary, replacing existing contents.
        /// </summary>
        /// <param name="data">The data to load.</param>
        public void LoadFrom(Dictionary<string, IReadOnlyList<AnchorItem>> data)
        {
            _fileAnchors.Clear();

            if (data != null)
            {
                foreach (KeyValuePair<string, IReadOnlyList<AnchorItem>> kvp in data)
                {
                    _fileAnchors[kvp.Key] = kvp.Value;
                }
            }

            CacheChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Saves the cache to disk for the given solution directory.
        /// </summary>
        /// <param name="solutionDirectory">The solution root directory.</param>
        /// <returns>True if saved successfully.</returns>
        public bool SaveToDisk(string solutionDirectory)
        {
            return AnchorCacheSerializer.Save(solutionDirectory, GetSnapshot());
        }

        /// <summary>
        /// Loads the cache from disk for the given solution directory.
        /// </summary>
        /// <param name="solutionDirectory">The solution root directory.</param>
        /// <returns>True if loaded successfully.</returns>
        public bool LoadFromDisk(string solutionDirectory)
        {
            Dictionary<string, IReadOnlyList<AnchorItem>> data = AnchorCacheSerializer.Load(solutionDirectory);
            if (data != null)
            {
                LoadFrom(data);
                return true;
            }
            return false;
        }
    }
}
