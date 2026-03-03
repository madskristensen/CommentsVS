using System;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;

namespace CommentsVS.Services
{
    /// <summary>
    /// Buffers frequent text change notifications and raises one merged tags-changed event.
    /// </summary>
    internal sealed class BufferedTagChangeNotifier : IDisposable
    {
        private readonly object _lock = new();
        private readonly Action<SnapshotSpanEventArgs> _raiseTagsChanged;
        private readonly Timer _timer;
        private readonly int _debounceMs;

        private ITextSnapshot _pendingSnapshot;
        private int _pendingStart = int.MaxValue;
        private int _pendingEnd = -1;
        private bool _disposed;

        public BufferedTagChangeNotifier(Action<SnapshotSpanEventArgs> raiseTagsChanged, int debounceMs = 60)
        {
            if (raiseTagsChanged == null)
            {
                throw new ArgumentNullException(nameof(raiseTagsChanged));
            }

            _raiseTagsChanged = raiseTagsChanged;
            _debounceMs = debounceMs;
            _timer = new Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Queues the changed line ranges from a buffer change event.
        /// </summary>
        public void Queue(TextContentChangedEventArgs e)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _pendingSnapshot = e.After;

                foreach (ITextChange change in e.Changes)
                {
                    ITextSnapshotLine startLine = e.After.GetLineFromPosition(change.NewPosition);
                    ITextSnapshotLine endLine = e.After.GetLineFromPosition(change.NewEnd);

                    if (startLine.Start.Position < _pendingStart)
                    {
                        _pendingStart = startLine.Start.Position;
                    }

                    if (endLine.End.Position > _pendingEnd)
                    {
                        _pendingEnd = endLine.End.Position;
                    }
                }

                _ = _timer.Change(_debounceMs, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Queues a full-buffer invalidation.
        /// </summary>
        public void QueueFullBuffer(ITextSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _pendingSnapshot = snapshot;
                _pendingStart = 0;
                _pendingEnd = snapshot.Length;
                _ = _timer.Change(_debounceMs, Timeout.Infinite);
            }
        }

        private void OnTimerTick(object state)
        {
            SnapshotSpanEventArgs args = null;

            lock (_lock)
            {
                if (_disposed || _pendingSnapshot == null || _pendingStart == int.MaxValue || _pendingEnd < _pendingStart)
                {
                    return;
                }

                var start = Math.Max(0, _pendingStart);
                var end = Math.Min(_pendingSnapshot.Length, _pendingEnd);

                if (end < start)
                {
                    return;
                }

                args = new SnapshotSpanEventArgs(new SnapshotSpan(_pendingSnapshot, start, end - start));

                _pendingStart = int.MaxValue;
                _pendingEnd = -1;
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _raiseTagsChanged(args);
            }).FireAndForget();
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            _timer.Dispose();
        }
    }
}
