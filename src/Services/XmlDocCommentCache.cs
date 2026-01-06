using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace CommentsVS.Services
{
    /// <summary>
    /// Provides caching for parsed XML documentation comment blocks per text buffer.
    /// Caches are invalidated incrementally when the buffer changes.
    /// </summary>
    internal sealed class XmlDocCommentCache
    {
        /// <summary>
        /// Maximum file size (in characters) to process. Files larger than this are skipped for performance.
        /// </summary>
        public const int MaxFileSize = 150_000;

        private readonly object _lock = new();
        private ITextSnapshot _cachedSnapshot;
        private List<XmlDocCommentBlock> _cachedBlocks;
        private LanguageCommentStyle _cachedCommentStyle;

        /// <summary>
        /// Gets the cached comment blocks for the buffer, parsing if necessary.
        /// Returns null if the file is too large to process.
        /// </summary>
        /// <param name="snapshot">The current text snapshot.</param>
        /// <param name="commentStyle">The comment style for the language.</param>
        /// <returns>The list of comment blocks, or null if file is too large.</returns>
        public IReadOnlyList<XmlDocCommentBlock> GetCommentBlocks(ITextSnapshot snapshot, LanguageCommentStyle commentStyle)
        {
            if (snapshot == null || commentStyle == null)
            {
                return null;
            }

            // Skip large files for performance
            if (snapshot.Length > MaxFileSize)
            {
                return null;
            }

            lock (_lock)
            {
                // Check if cache is valid
                if (_cachedSnapshot != null &&
                    _cachedSnapshot.Version.VersionNumber == snapshot.Version.VersionNumber &&
                    _cachedCommentStyle == commentStyle)
                {
                    return _cachedBlocks;
                }

                // Parse and cache
                var parser = new XmlDocCommentParser(commentStyle);
                _cachedBlocks = [.. parser.FindAllCommentBlocks(snapshot)];
                _cachedSnapshot = snapshot;
                _cachedCommentStyle = commentStyle;

                return _cachedBlocks;
            }
        }

        /// <summary>
        /// Invalidates the cache. Call when the buffer changes significantly.
        /// </summary>
        public void Invalidate()
        {
            lock (_lock)
            {
                _cachedSnapshot = null;
                _cachedBlocks = null;
                _cachedCommentStyle = null;
            }
        }

        /// <summary>
        /// Gets or creates a cache instance for the given text buffer.
        /// </summary>
        /// <param name="buffer">The text buffer.</param>
        /// <returns>The cache instance for this buffer.</returns>
        public static XmlDocCommentCache GetOrCreate(ITextBuffer buffer)
        {
            if (buffer == null)
            {
                return null;
            }

            return buffer.Properties.GetOrCreateSingletonProperty(
                typeof(XmlDocCommentCache),
                () =>
                {
                    var cache = new XmlDocCommentCache();
                    // Subscribe to buffer changes to invalidate cache
                    buffer.Changed += (s, e) => cache.OnBufferChanged(e);
                    return cache;
                });
        }

        /// <summary>
        /// Checks if a file size is within the processing limit.
        /// </summary>
        /// <param name="snapshot">The text snapshot to check.</param>
        /// <returns>True if the file should be processed, false if too large.</returns>
        public static bool ShouldProcess(ITextSnapshot snapshot)
        {
            return snapshot != null && snapshot.Length <= MaxFileSize;
        }

        private void OnBufferChanged(TextContentChangedEventArgs e)
        {
            // For now, invalidate entire cache on any change.
            // Future optimization: could do incremental updates for changes
            // that don't affect comment structure.
            Invalidate();
        }
    }
}
