using System.Collections.Generic;

namespace CommentsVS.Services
{
    internal static class CommentCaretMapper
    {
        public static int? MapCaretOffset(
            string originalText,
            string reflowedText,
            int originalCaretOffset,
            LanguageCommentStyle commentStyle,
            bool isMultiLineStyle)
        {
            if (originalText == null) throw new ArgumentNullException(nameof(originalText));
            if (reflowedText == null) throw new ArgumentNullException(nameof(reflowedText));
            if (commentStyle == null) throw new ArgumentNullException(nameof(commentStyle));

            originalCaretOffset = Math.Max(0, Math.Min(originalCaretOffset, originalText.Length));

            List<ContentUnit> originalUnits = CreateContentUnits(originalText, commentStyle, isMultiLineStyle);
            List<ContentUnit> reflowedUnits = CreateContentUnits(reflowedText, commentStyle, isMultiLineStyle);

            if (originalUnits.Count != reflowedUnits.Count)
            {
                return null;
            }

            var consumedUnits = 0;

            for (var i = 0; i < originalUnits.Count; i++)
            {
                if (originalUnits[i].Value != reflowedUnits[i].Value)
                {
                    return null;
                }

                if (originalCaretOffset > originalUnits[i].Start)
                {
                    consumedUnits = i + 1;
                }
            }

            return consumedUnits == 0
                ? GetFirstContentOffset(reflowedText, commentStyle, isMultiLineStyle)
                : reflowedUnits[consumedUnits - 1].End;
        }

        private static List<ContentUnit> CreateContentUnits(
            string text,
            LanguageCommentStyle commentStyle,
            bool isMultiLineStyle)
        {
            List<ContentRange> ranges = GetContentRanges(text, commentStyle, isMultiLineStyle);
            var units = new List<ContentUnit>();
            ContentUnit pendingWhitespace = null;

            foreach (ContentRange range in ranges)
            {
                if (pendingWhitespace != null)
                {
                    pendingWhitespace.End = range.Start;
                }

                for (var position = range.Start; position < range.End; position++)
                {
                    var value = text[position];

                    if (char.IsWhiteSpace(value))
                    {
                        if (pendingWhitespace == null)
                        {
                            pendingWhitespace = new ContentUnit(' ', position, position + 1);
                            units.Add(pendingWhitespace);
                        }
                        else
                        {
                            pendingWhitespace.End = position + 1;
                        }

                        continue;
                    }

                    pendingWhitespace = null;
                    units.Add(new ContentUnit(value, position, position + 1));
                }

                if (range.End < text.Length)
                {
                    if (pendingWhitespace == null)
                    {
                        pendingWhitespace = new ContentUnit(' ', range.End, range.End);
                        units.Add(pendingWhitespace);
                    }
                }
            }

            while (units.Count > 0 && units[units.Count - 1].Value == ' ')
            {
                units.RemoveAt(units.Count - 1);
            }

            return units;
        }

        private static List<ContentRange> GetContentRanges(
            string text,
            LanguageCommentStyle commentStyle,
            bool isMultiLineStyle)
        {
            var ranges = new List<ContentRange>();
            var lineStart = 0;

            while (lineStart <= text.Length)
            {
                var lineEnd = text.IndexOf('\n', lineStart);
                if (lineEnd < 0)
                {
                    lineEnd = text.Length;
                }

                var contentStart = GetContentStart(text, lineStart, lineEnd, commentStyle, isMultiLineStyle);
                var contentEnd = GetContentEnd(text, contentStart, lineEnd, commentStyle, isMultiLineStyle);

                if (contentStart < contentEnd)
                {
                    ranges.Add(new ContentRange(contentStart, contentEnd));
                }

                if (lineEnd == text.Length)
                {
                    break;
                }

                lineStart = lineEnd + 1;
            }

            return ranges;
        }

        private static int GetContentStart(
            string text,
            int lineStart,
            int lineEnd,
            LanguageCommentStyle commentStyle,
            bool isMultiLineStyle)
        {
            var position = lineStart;

            while (position < lineEnd && char.IsWhiteSpace(text[position]))
            {
                position++;
            }

            if (!isMultiLineStyle)
            {
                string prefix = commentStyle.SingleLineDocPrefix;
                if (StartsWith(text, position, lineEnd, prefix))
                {
                    position += prefix.Length;
                    if (position < lineEnd && text[position] == ' ')
                    {
                        position++;
                    }
                }

                return position;
            }

            string start = commentStyle.MultiLineDocStart;
            if (StartsWith(text, position, lineEnd, start))
            {
                return position + start.Length;
            }

            if (position < lineEnd && text[position] == '*')
            {
                position++;
                if (position < lineEnd && text[position] == ' ')
                {
                    position++;
                }
            }

            return position;
        }

        private static int GetContentEnd(
            string text,
            int contentStart,
            int lineEnd,
            LanguageCommentStyle commentStyle,
            bool isMultiLineStyle)
        {
            var contentEnd = lineEnd;

            if (contentEnd > contentStart && text[contentEnd - 1] == '\r')
            {
                contentEnd--;
            }

            if (!isMultiLineStyle || contentStart >= contentEnd)
            {
                return contentEnd;
            }

            string end = commentStyle.MultiLineDocEnd;
            var delimiter = text.LastIndexOf(end, contentEnd - 1, contentEnd - contentStart, StringComparison.Ordinal);
            return delimiter >= contentStart ? delimiter : contentEnd;
        }

        private static int GetFirstContentOffset(
            string text,
            LanguageCommentStyle commentStyle,
            bool isMultiLineStyle)
        {
            List<ContentRange> ranges = GetContentRanges(text, commentStyle, isMultiLineStyle);
            return ranges.Count > 0 ? ranges[0].Start : 0;
        }

        private static bool StartsWith(string text, int start, int end, string value)
        {
            return !string.IsNullOrEmpty(value) &&
                start + value.Length <= end &&
                string.CompareOrdinal(text, start, value, 0, value.Length) == 0;
        }

        private sealed class ContentUnit(char value, int start, int end)
        {
            public char Value { get; } = value;
            public int Start { get; } = start;
            public int End { get; set; } = end;
        }

        private readonly struct ContentRange(int start, int end)
        {
            public int Start { get; } = start;
            public int End { get; } = end;
        }
    }
}
