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
        /// <remarks>
        /// This property is an alias for <see cref="Constants.MaxFileSize"/> for backward compatibility.
        /// </remarks>
        public static int MaxFileSize => Constants.MaxFileSize;

        private readonly object _lock = new();
        private ITextSnapshot _cachedSnapshot;
        private List<XmlDocCommentBlock> _cachedBlocks;
        private LanguageCommentStyle _cachedCommentStyle;

        // Track affected line range to enable smart cache invalidation
        private int _invalidatedStartLine = -1;
        private int _invalidatedEndLine = -1;

        // Store buffer reference for event subscription (using instance method avoids lambda allocation)
        private ITextBuffer _subscribedBuffer;

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
                // Check if cache is valid (same version means no changes since last parse)
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

                // Reset invalidation tracking
                _invalidatedStartLine = -1;
                _invalidatedEndLine = -1;

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
                _invalidatedStartLine = -1;
                _invalidatedEndLine = -1;
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
                    cache.SubscribeToBuffer(buffer);
                    return cache;
                });
        }

        /// <summary>
        /// Subscribes to buffer change events using an instance method (avoids lambda closure allocation).
        /// </summary>
        private void SubscribeToBuffer(ITextBuffer buffer)
        {
            _subscribedBuffer = buffer;
            _subscribedBuffer.Changed += OnBufferChanged;
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

        /// <summary>
        /// Handles buffer changes. The version check in GetCommentBlocks will trigger 
        /// a re-parse on next access when the snapshot version differs.
        /// </summary>
        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            // The snapshot version comparison in GetCommentBlocks will automatically
            // trigger a re-parse when the snapshot version changes.
            // We don't need to explicitly invalidate here - just let the version
            // check handle it. This avoids unnecessary work when multiple changes
            // occur before the next GetCommentBlocks call.

            // Track which lines were affected for potential future incremental updates
            lock (_lock)
            {
                foreach (ITextChange change in e.Changes)
                {
                    var startLine = e.After.GetLineFromPosition(change.NewPosition).LineNumber;
                    var endLine = e.After.GetLineFromPosition(change.NewEnd).LineNumber;

                    if (_invalidatedStartLine < 0 || startLine < _invalidatedStartLine)
                    {
                        _invalidatedStartLine = startLine;
                    }
                    if (endLine > _invalidatedEndLine)
                    {
                        _invalidatedEndLine = endLine;
                    }
                }
            }
        }
    }
}
