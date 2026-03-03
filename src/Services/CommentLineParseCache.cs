using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace CommentsVS.Services
{
    /// <summary>
    /// Caches parsed comment span and LINK data per line for a specific text buffer snapshot.
    /// </summary>
    internal sealed class CommentLineParseCache
    {
        private static readonly IReadOnlyList<LinkAnchorInfo> _emptyLinks = [];
        private static readonly IReadOnlyList<(int Start, int Length)> _emptyCommentSpans = [];

        private readonly object _lock = new();
        private ITextSnapshot _snapshot;
        private readonly Dictionary<int, ParsedCommentLineData> _lineCache = [];

        /// <summary>
        /// Gets or creates a cache instance for the given text buffer.
        /// </summary>
        public static CommentLineParseCache GetOrCreate(ITextBuffer buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            return buffer.Properties.GetOrCreateSingletonProperty(
                typeof(CommentLineParseCache),
                () => new CommentLineParseCache());
        }

        /// <summary>
        /// Gets parsed data for the specified line in the snapshot.
        /// </summary>
        public ParsedCommentLineData GetLineData(ITextSnapshot snapshot, int lineNumber)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (lineNumber < 0 || lineNumber >= snapshot.LineCount)
            {
                throw new ArgumentOutOfRangeException(nameof(lineNumber));
            }

            lock (_lock)
            {
                if (_snapshot == null || _snapshot.Version.VersionNumber != snapshot.Version.VersionNumber)
                {
                    _snapshot = snapshot;
                    _lineCache.Clear();
                }

                if (_lineCache.TryGetValue(lineNumber, out ParsedCommentLineData cachedData))
                {
                    return cachedData;
                }

                ParsedCommentLineData parsedData = ParseLine(snapshot.GetLineFromLineNumber(lineNumber).GetText());
                _lineCache[lineNumber] = parsedData;
                return parsedData;
            }
        }

        private static ParsedCommentLineData ParseLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new ParsedCommentLineData(false, _emptyCommentSpans, _emptyLinks);
            }

            List<(int Start, int Length)> commentSpans = [.. CommentSpanHelper.FindCommentSpans(text)];
            if (commentSpans.Count == 0)
            {
                return new ParsedCommentLineData(false, _emptyCommentSpans, _emptyLinks);
            }

            IReadOnlyList<LinkAnchorInfo> parsedLinks = _emptyLinks;
            if (LinkAnchorParser.ContainsLinkAnchor(text))
            {
                List<LinkAnchorInfo> filteredLinks = [];
                IReadOnlyList<LinkAnchorInfo> links = LinkAnchorParser.Parse(text);

                foreach (LinkAnchorInfo link in links)
                {
                    if (IntersectsAnyCommentSpan(link.StartIndex, link.Length, commentSpans))
                    {
                        filteredLinks.Add(link);
                    }
                }

                if (filteredLinks.Count > 0)
                {
                    parsedLinks = filteredLinks;
                }
            }

            var hasIssueCandidate = text.IndexOf('#') >= 0;
            return new ParsedCommentLineData(hasIssueCandidate, commentSpans, parsedLinks);
        }

        private static bool IntersectsAnyCommentSpan(int start, int length, List<(int Start, int Length)> commentSpans)
        {
            var end = start + Math.Max(0, length);

            foreach ((int spanStart, int spanLength) in commentSpans)
            {
                var spanEnd = spanStart + spanLength;
                if (start < spanEnd && end > spanStart)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Parsed line data used by taggers.
    /// </summary>
    internal sealed class ParsedCommentLineData(
        bool hasIssueCandidate,
        IReadOnlyList<(int Start, int Length)> commentSpans,
        IReadOnlyList<LinkAnchorInfo> links)
    {
        public bool HasIssueCandidate { get; } = hasIssueCandidate;

        public IReadOnlyList<(int Start, int Length)> CommentSpans { get; } = commentSpans;

        public IReadOnlyList<LinkAnchorInfo> Links { get; } = links;
    }
}
