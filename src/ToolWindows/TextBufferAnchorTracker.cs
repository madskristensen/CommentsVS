using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Tracks text buffer changes and updates the Code Anchors cache in real-time.
    /// This MEF component listens for text document creation and subscribes to buffer changes,
    /// enabling the Code Anchors window to update as the user types without requiring a save.
    /// </summary>
    [Export(typeof(ITextBufferAnchorTracker))]
    [ContentType("code")]
    internal sealed class TextBufferAnchorTracker : ITextBufferAnchorTracker, IDisposable
    {
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;
        private readonly AnchorService _anchorService = new();
        private readonly Dictionary<ITextBuffer, BufferTrackingInfo> _trackedBuffers = [];
        private readonly object _lock = new();
        private bool _disposed;

        // Debounce timer to avoid excessive updates during fast typing
        private const int DebounceDelayMs = 500;

        [ImportingConstructor]
        public TextBufferAnchorTracker(ITextDocumentFactoryService textDocumentFactoryService)
        {
            _textDocumentFactoryService = textDocumentFactoryService;
            _textDocumentFactoryService.TextDocumentCreated += OnTextDocumentCreated;
            _textDocumentFactoryService.TextDocumentDisposed += OnTextDocumentDisposed;
        }

        /// <summary>
        /// Event raised when a buffer's anchors have been updated.
        /// </summary>
        public event EventHandler<BufferAnchorsUpdatedEventArgs> BufferAnchorsUpdated;

        /// <summary>
        /// Starts tracking an already-open text buffer.
        /// Call this for documents that were opened before the tracker was instantiated.
        /// </summary>
        public void TrackBuffer(ITextBuffer buffer, string filePath)
        {
            if (_disposed || buffer == null || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            StartTrackingBuffer(buffer, filePath);
        }

        private void OnTextDocumentCreated(object sender, TextDocumentEventArgs e)
        {
            if (_disposed || e.TextDocument?.TextBuffer == null)
            {
                return;
            }

            StartTrackingBuffer(e.TextDocument.TextBuffer, e.TextDocument.FilePath);
        }

        private void StartTrackingBuffer(ITextBuffer buffer, string filePath)
        {
            if (IsTemporaryFilePath(filePath))
            {
                return;
            }

            lock (_lock)
            {
                if (_trackedBuffers.ContainsKey(buffer))
                {
                    return;
                }

                var trackingInfo = new BufferTrackingInfo(filePath);
                _trackedBuffers[buffer] = trackingInfo;
                buffer.Changed += OnBufferChanged;
            }
        }

        private void OnTextDocumentDisposed(object sender, TextDocumentEventArgs e)
        {
            if (e.TextDocument?.TextBuffer == null)
            {
                return;
            }

            var buffer = e.TextDocument.TextBuffer;

            lock (_lock)
            {
                if (_trackedBuffers.TryGetValue(buffer, out var trackingInfo))
                {
                    buffer.Changed -= OnBufferChanged;
                    trackingInfo.Dispose();
                    _trackedBuffers.Remove(buffer);
                }
            }
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (_disposed || sender is not ITextBuffer buffer)
            {
                return;
            }

            BufferTrackingInfo trackingInfo;
            lock (_lock)
            {
                if (!_trackedBuffers.TryGetValue(buffer, out trackingInfo))
                {
                    return;
                }
            }

            // Cancel any pending update and schedule a new one (debounce)
            trackingInfo.CancelPendingUpdate();
            trackingInfo.ScheduleUpdate(() => UpdateAnchorsForBuffer(buffer, trackingInfo.FilePath));
        }

        private void UpdateAnchorsForBuffer(ITextBuffer buffer, string filePath)
        {
            if (_disposed || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            try
            {
                // Scan the buffer for anchors
                IReadOnlyList<AnchorItem> anchors = _anchorService.ScanBuffer(buffer, filePath, projectName: null);

                // Update the cache if tool window exists
                var toolWindow = CodeAnchorsToolWindow.Instance;
                if (toolWindow?.Cache != null)
                {
                    toolWindow.Cache.AddOrUpdateFile(filePath, anchors);

                    // Notify listeners (including the tool window) to refresh UI
                    BufferAnchorsUpdated?.Invoke(this, new BufferAnchorsUpdatedEventArgs(filePath, anchors));
                }
            }
            catch
            {
                // Silently ignore errors during background scanning
            }
        }

        /// <summary>
        /// Returns true if the file path refers to a temporary VS buffer (e.g. "Temp.txt")
        /// that should not appear in the Code Anchors window.
        /// </summary>
        private static bool IsTemporaryFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return true;
            }

            // Temporary buffers created by VS often have no directory component (e.g. "Temp.txt")
            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
            {
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _textDocumentFactoryService.TextDocumentCreated -= OnTextDocumentCreated;
            _textDocumentFactoryService.TextDocumentDisposed -= OnTextDocumentDisposed;

            lock (_lock)
            {
                foreach (var kvp in _trackedBuffers)
                {
                    kvp.Key.Changed -= OnBufferChanged;
                    kvp.Value.Dispose();
                }
                _trackedBuffers.Clear();
            }
        }

        /// <summary>
        /// Holds tracking information for a single buffer.
        /// </summary>
        private sealed class BufferTrackingInfo : IDisposable
        {
            public string FilePath { get; }
            private CancellationTokenSource _cts;
            private readonly object _lock = new();

            public BufferTrackingInfo(string filePath)
            {
                FilePath = filePath;
            }

            public void CancelPendingUpdate()
            {
                lock (_lock)
                {
                    _cts?.Cancel();
                    _cts?.Dispose();
                    _cts = null;
                }
            }

            public void ScheduleUpdate(Action updateAction)
            {
                lock (_lock)
                {
                    _cts = new CancellationTokenSource();
                    var token = _cts.Token;

                    // Schedule the update after debounce delay
                    Task.Delay(DebounceDelayMs, token).ContinueWith(t =>
                    {
                        if (!t.IsCanceled && !token.IsCancellationRequested)
                        {
                            updateAction();
                        }
                    }, token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
                }
            }

            public void Dispose()
            {
                CancelPendingUpdate();
            }
        }
    }

    /// <summary>
    /// Interface for the text buffer anchor tracker.
    /// </summary>
    public interface ITextBufferAnchorTracker
    {
        /// <summary>
        /// Event raised when a buffer's anchors have been updated.
        /// </summary>
        event EventHandler<BufferAnchorsUpdatedEventArgs> BufferAnchorsUpdated;

        /// <summary>
        /// Starts tracking an already-open text buffer.
        /// Call this for documents that were opened before the tracker was instantiated.
        /// </summary>
        /// <param name="buffer">The text buffer to track.</param>
        /// <param name="filePath">The file path associated with the buffer.</param>
        void TrackBuffer(ITextBuffer buffer, string filePath);
    }

    /// <summary>
    /// Event arguments for buffer anchors updated events.
    /// </summary>
    public sealed class BufferAnchorsUpdatedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public IReadOnlyList<AnchorItem> Anchors { get; }

        public BufferAnchorsUpdatedEventArgs(string filePath, IReadOnlyList<AnchorItem> anchors)
        {
            FilePath = filePath;
            Anchors = anchors;
        }
    }
}
