// Based on Microsoft Visual Studio SDK sample code
// Licensed under the Visual Studio SDK license terms

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace CommentsVS.Adornments
{
    /// <summary>
    /// Helper class for interspersing adornments into text, replacing (eliding) the original text.
    /// </summary>
    internal abstract class IntraTextAdornmentTagger<TData, TAdornment>
        : ITagger<IntraTextAdornmentTag>, IDisposable
        where TAdornment : UIElement
    {
        protected readonly IWpfTextView view;
        private Dictionary<SnapshotSpan, TAdornment> _adornmentCache = [];
        protected ITextSnapshot snapshot { get; private set; }
        private readonly List<SnapshotSpan> _invalidatedSpans = [];
        private bool _disposed;

        /// <summary>
        /// Gets whether this tagger has been disposed.
        /// </summary>
        protected bool IsDisposed => _disposed;

        protected IntraTextAdornmentTagger(IWpfTextView view)
        {
            this.view = view;
            snapshot = view.TextBuffer.CurrentSnapshot;

            this.view.LayoutChanged += HandleLayoutChanged;
            this.view.TextBuffer.Changed += HandleBufferChanged;
            this.view.Closed += OnViewClosed;
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            Dispose();
        }

        protected abstract TAdornment CreateAdornment(TData data, SnapshotSpan span);
        protected abstract bool UpdateAdornment(TAdornment adornment, TData data);
        protected abstract IEnumerable<Tuple<SnapshotSpan, PositionAffinity?, TData>> GetAdornmentData(NormalizedSnapshotSpanCollection spans);

        private void HandleBufferChanged(object sender, TextContentChangedEventArgs args)
        {
            // Detect if this is a significant structural change (lines added/removed)
            // In such cases, we need to invalidate more aggressively
            // Use manual loops instead of LINQ to avoid delegate/enumerator allocations in this hot path
            var hasLineCountChange = false;
            foreach (ITextChange c in args.Changes)
            {
                var oldNewlines = CountNewlines(c.OldText);
                var newNewlines = CountNewlines(c.NewText);
                if (oldNewlines != newNewlines)
                {
                    hasLineCountChange = true;
                    break;
                }
            }

            if (hasLineCountChange)
            {
                // Line count changed - clear cache and invalidate entire visible region
                // This handles Code Cleanup, large pastes, etc. that shift line positions
                _adornmentCache.Clear();

                SnapshotSpan fullSpan = new(args.After, 0, args.After.Length);
                InvalidateSpans([fullSpan]);
                return;
            }

            // For simple edits within existing lines, use targeted invalidation
            var editedSpans = new List<SnapshotSpan>();

            foreach (ITextChange change in args.Changes)
            {
                // Get the line containing the change
                ITextSnapshotLine startLine = args.After.GetLineFromPosition(change.NewPosition);
                ITextSnapshotLine endLine = args.After.GetLineFromPosition(change.NewEnd);

                // Expand to include full lines to ensure adornments on affected lines are refreshed
                var expandedSpan = new SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak);
                editedSpans.Add(expandedSpan);
            }

            InvalidateSpans(editedSpans);
        }

        /// <summary>
        /// Counts newline characters in a string without allocations.
        /// </summary>
        private static int CountNewlines(string text)
        {
            var count = 0;
            foreach (var ch in text)
            {
                if (ch == '\n')
                {
                    count++;
                }
            }
            return count;
        }

        protected void InvalidateSpans(IList<SnapshotSpan> spans)
        {
            lock (_invalidatedSpans)
            {
                var wasEmpty = _invalidatedSpans.Count == 0;
                _invalidatedSpans.AddRange(spans);

                if (wasEmpty && _invalidatedSpans.Count > 0)
#pragma warning disable VSTHRD001, VSTHRD110
                    view.VisualElement.Dispatcher.BeginInvoke(new Action(AsyncUpdate));
#pragma warning restore VSTHRD001, VSTHRD110
            }
        }

        private void AsyncUpdate()
        {
            if (snapshot != view.TextBuffer.CurrentSnapshot)
            {
                ITextSnapshot oldSnapshot = snapshot;
                snapshot = view.TextBuffer.CurrentSnapshot;

                // If line count changed significantly, clear cache entirely
                // This avoids stale adornments from incorrect span translations
                var lineDelta = Math.Abs(snapshot.LineCount - oldSnapshot.LineCount);
                if (lineDelta > 0)
                {
                    _adornmentCache.Clear();
                }
                else
                {
                    // Translate cached adornment spans to new snapshot
                    // Use EdgeInclusive to better track position when text is inserted before/after
                    var translatedAdornmentCache = new Dictionary<SnapshotSpan, TAdornment>();
                    foreach (KeyValuePair<SnapshotSpan, TAdornment> kvp in _adornmentCache)
                    {
                        SnapshotSpan translatedSpan = kvp.Key.TranslateTo(snapshot, SpanTrackingMode.EdgeInclusive);

                        // Only keep the adornment if the translation was successful and the span is still valid
                        // Discard if the span became empty or degenerate
                        if (translatedSpan.Length > 0)
                        {
                            translatedAdornmentCache[translatedSpan] = kvp.Value;
                        }
                    }

                    _adornmentCache = translatedAdornmentCache;
                }
            }

            List<SnapshotSpan> translatedSpans;
            lock (_invalidatedSpans)
            {
                translatedSpans = [.. _invalidatedSpans.Select(s => s.TranslateTo(snapshot, SpanTrackingMode.EdgeInclusive))];
                _invalidatedSpans.Clear();
            }

            if (translatedSpans.Count == 0)
                return;

            SnapshotPoint start = translatedSpans.Select(span => span.Start).Min();
            SnapshotPoint end = translatedSpans.Select(span => span.End).Max();

            RaiseTagsChanged(new SnapshotSpan(start, end));
        }

        protected void RaiseTagsChanged(SnapshotSpan span)
        {
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
        }

        /// <summary>
        /// Clears the adornment cache. Call when adornments need to be completely recreated
        /// (e.g., when rendering mode changes).
        /// </summary>
        protected void ClearAdornmentCache()
        {
            _adornmentCache.Clear();
        }

        private void HandleLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            SnapshotSpan visibleSpan = view.TextViewLines.FormattedSpan;

            var toRemove = (from kvp in _adornmentCache
                            where !kvp.Key.TranslateTo(visibleSpan.Snapshot, SpanTrackingMode.EdgeExclusive).IntersectsWith(visibleSpan)
                            select kvp.Key).ToList();

            foreach (SnapshotSpan span in toRemove)
                _adornmentCache.Remove(span);
        }

        public virtual IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans == null || spans.Count == 0)
                yield break;

            ITextSnapshot requestedSnapshot = spans[0].Snapshot;
            var translatedSpans = new NormalizedSnapshotSpanCollection(
                spans.Select(span => span.TranslateTo(snapshot, SpanTrackingMode.EdgeExclusive)));

            foreach (TagSpan<IntraTextAdornmentTag> tagSpan in GetAdornmentTagsOnSnapshot(translatedSpans))
            {
                SnapshotSpan span = tagSpan.Span.TranslateTo(requestedSnapshot, SpanTrackingMode.EdgeExclusive);
                var tag = new IntraTextAdornmentTag(tagSpan.Tag.Adornment, tagSpan.Tag.RemovalCallback, tagSpan.Tag.Affinity);
                yield return new TagSpan<IntraTextAdornmentTag>(span, tag);
            }
        }

        private IEnumerable<TagSpan<IntraTextAdornmentTag>> GetAdornmentTagsOnSnapshot(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
                yield break;

            ITextSnapshot snapshot = spans[0].Snapshot;

            // Find and remove cached adornments in the affected region
            // This is more aggressive but ensures correctness after text edits
            var toRemove = new HashSet<SnapshotSpan>();
            foreach (KeyValuePair<SnapshotSpan, TAdornment> ar in _adornmentCache)
            {
                // Remove if the cached span is from an old snapshot or intersects with requested spans
                if (ar.Key.Snapshot != snapshot || spans.IntersectsWith(new NormalizedSnapshotSpanCollection(ar.Key)))
                {
                    toRemove.Add(ar.Key);
                }
            }

            foreach (Tuple<SnapshotSpan, PositionAffinity?, TData> spanDataPair in GetAdornmentData(spans).Distinct(new Comparer()))
            {
                SnapshotSpan snapshotSpan = spanDataPair.Item1;
                PositionAffinity? affinity = spanDataPair.Item2;
                TData adornmentData = spanDataPair.Item3;

                TAdornment adornment;
                if (_adornmentCache.TryGetValue(snapshotSpan, out TAdornment cachedAdornment))
                {
                    if (UpdateAdornment(cachedAdornment, adornmentData))
                    {
                        // Keep the cached adornment
                        toRemove.Remove(snapshotSpan);
                        adornment = cachedAdornment;
                    }
                    else
                    {
                        // UpdateAdornment returned false, create a new adornment
                        adornment = CreateAdornment(adornmentData, snapshotSpan);
                        if (adornment == null)
                            continue;

                        adornment.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        // Replace the cached adornment
                        _adornmentCache[snapshotSpan] = adornment;
                        toRemove.Remove(snapshotSpan);
                    }
                }
                else
                {
                    adornment = CreateAdornment(adornmentData, snapshotSpan);
                    if (adornment == null)
                        continue;

                    adornment.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    _adornmentCache.Add(snapshotSpan, adornment);
                }

                yield return new TagSpan<IntraTextAdornmentTag>(snapshotSpan, new IntraTextAdornmentTag(adornment, null, affinity));
            }

            foreach (SnapshotSpan snapshotSpan in toRemove)
                _adornmentCache.Remove(snapshotSpan);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (disposing)
            {
                view.LayoutChanged -= HandleLayoutChanged;
                view.TextBuffer.Changed -= HandleBufferChanged;
                view.Closed -= OnViewClosed;
                _adornmentCache.Clear();
            }
        }

        private class Comparer : IEqualityComparer<Tuple<SnapshotSpan, PositionAffinity?, TData>>
        {
            public bool Equals(Tuple<SnapshotSpan, PositionAffinity?, TData> x, Tuple<SnapshotSpan, PositionAffinity?, TData> y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                return x.Item1.Equals(y.Item1);
            }

            public int GetHashCode(Tuple<SnapshotSpan, PositionAffinity?, TData> obj) => obj.Item1.GetHashCode();
        }
    }
}
