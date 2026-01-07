using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Serializes and deserializes anchor cache to/from JSON files.
    /// Optimized for performance with minimal allocations.
    /// </summary>
    internal static class AnchorCacheSerializer
    {
        private const string _cacheFileName = "CodeAnchors.json";
        private const string _vsFolderName = ".vs";

        private static readonly JsonSerializer _serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            Formatting = Formatting.None, // Compact for smaller file size
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        });

        /// <summary>
        /// Gets the cache file path for the given solution directory.
        /// </summary>
        /// <param name="solutionDirectory">The solution root directory.</param>
        /// <returns>The full path to the cache file, or null if invalid.</returns>
        public static string GetCacheFilePath(string solutionDirectory)
        {
            if (string.IsNullOrEmpty(solutionDirectory))
            {
                return null;
            }

            var vsFolder = Path.Combine(solutionDirectory, _vsFolderName);
            return Path.Combine(vsFolder, _cacheFileName);
        }

        /// <summary>
        /// Saves the anchor cache to disk.
        /// </summary>
        /// <param name="solutionDirectory">The solution root directory.</param>
        /// <param name="cache">The cache data to save.</param>
        /// <returns>True if saved successfully, false otherwise.</returns>
        public static bool Save(string solutionDirectory, IReadOnlyDictionary<string, IReadOnlyList<AnchorItem>> cache)
        {
            var filePath = GetCacheFilePath(solutionDirectory);
            if (filePath == null)
            {
                return false;
            }

            try
            {
                // Ensure .vs folder exists
                var vsFolder = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(vsFolder))
                {
                    Directory.CreateDirectory(vsFolder);
                }

                // Convert to serializable format
                var cacheData = new AnchorCacheData
                {
                    Version = 1,
                    Files = []
                };

                foreach (KeyValuePair<string, IReadOnlyList<AnchorItem>> kvp in cache)
                {
                    var entries = new List<AnchorEntryData>(kvp.Value.Count);
                    foreach (AnchorItem anchor in kvp.Value)
                    {
                        entries.Add(new AnchorEntryData
                        {
                            T = (int)anchor.AnchorType,
                            L = anchor.LineNumber,
                            C = anchor.Column,
                            M = anchor.Message,
                            O = anchor.Owner,
                            I = anchor.IssueReference,
                            A = anchor.AnchorId,
                            R = anchor.RawMetadata
                        });
                    }
                    cacheData.Files[kvp.Key] = entries;
                }

                // Write with streaming for better memory usage
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                using (var writer = new StreamWriter(stream))
                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    _serializer.Serialize(jsonWriter, cacheData);
                }

                return true;
            }
            catch (Exception ex)
            {
                ex.Log();
                return false;
            }
        }

        /// <summary>
        /// Loads the anchor cache from disk.
        /// </summary>
        /// <param name="solutionDirectory">The solution root directory.</param>
        /// <returns>The loaded cache data, or null if not found or invalid.</returns>
        public static Dictionary<string, IReadOnlyList<AnchorItem>> Load(string solutionDirectory)
        {
            var filePath = GetCacheFilePath(solutionDirectory);
            if (filePath == null || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                AnchorCacheData cacheData;

                // Read with streaming for better memory usage
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
                using (var reader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    cacheData = _serializer.Deserialize<AnchorCacheData>(jsonReader);
                }

                if (cacheData?.Files == null || cacheData.Version != 1)
                {
                    return null;
                }

                // Convert back to AnchorItem objects
                var result = new Dictionary<string, IReadOnlyList<AnchorItem>>(
                    cacheData.Files.Count,
                    StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, List<AnchorEntryData>> kvp in cacheData.Files)
                {
                    var filePath2 = kvp.Key;
                    var fileName = Path.GetFileName(filePath2);

                    var anchors = new List<AnchorItem>(kvp.Value.Count);
                    foreach (AnchorEntryData entry in kvp.Value)
                    {
                        anchors.Add(new AnchorItem
                        {
                            AnchorType = (AnchorType)entry.T,
                            FilePath = filePath2,
                            LineNumber = entry.L,
                            Column = entry.C,
                            Message = entry.M,
                            Owner = entry.O,
                            IssueReference = entry.I,
                            AnchorId = entry.A,
                            RawMetadata = entry.R
                        });
                    }

                    result[filePath2] = anchors;
                }

                return result;
            }
            catch (Exception ex)
            {
                ex.Log();
                return null;
            }
        }

        /// <summary>
        /// Deletes the cache file if it exists.
        /// </summary>
        /// <param name="solutionDirectory">The solution root directory.</param>
        public static void Delete(string solutionDirectory)
        {
            var filePath = GetCacheFilePath(solutionDirectory);
            if (filePath != null && File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    ex.Log();
                }
            }
        }

        /// <summary>
        /// Root cache data structure with version for future compatibility.
        /// </summary>
        private class AnchorCacheData
        {
            [JsonProperty("v")]
            public int Version { get; set; }

            [JsonProperty("f")]
            public Dictionary<string, List<AnchorEntryData>> Files { get; set; }
        }

        /// <summary>
        /// Compact anchor entry with short property names for smaller file size.
        /// </summary>
        private class AnchorEntryData
        {
            [JsonProperty("t")]
            public int T { get; set; } // AnchorType

            [JsonProperty("l")]
            public int L { get; set; } // LineNumber

            [JsonProperty("c")]
            public int C { get; set; } // Column

            [JsonProperty("m")]
            public string M { get; set; } // Message

            [JsonProperty("o")]
            public string O { get; set; } // Owner

            [JsonProperty("i")]
            public string I { get; set; } // IssueReference

            [JsonProperty("a")]
            public string A { get; set; } // AnchorId

            [JsonProperty("r")]
            public string R { get; set; } // RawMetadata
        }
    }
}
