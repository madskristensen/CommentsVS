using System.Collections.Generic;

namespace CommentsVS.Services
{
    /// <summary>
    /// Helper methods for detecting comment spans in source code text.
    /// </summary>
    internal static class CommentSpanHelper
    {
        /// <summary>
        /// Finds all comment spans in the given text (both full-line and inline comments).
        /// </summary>
        /// <param name="text">The line text to analyze.</param>
        /// <returns>Enumerable of (Start, Length) tuples representing comment portions.</returns>
        public static IEnumerable<(int Start, int Length)> FindCommentSpans(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            // Check if entire line is a comment (starts with comment prefix)
            if (LanguageCommentStyle.IsCommentLine(text))
            {
                yield return (0, text.Length);
                yield break;
            }

            // Look for inline single-line comments (//)
            var inlineCommentIndex = text.IndexOf("//");
            if (inlineCommentIndex >= 0)
            {
                // Make sure it's not inside a string literal
                if (!IsInsideStringLiteral(text, inlineCommentIndex))
                {
                    yield return (inlineCommentIndex, text.Length - inlineCommentIndex);
                }
            }
        }

        /// <summary>
        /// Checks if a position is inside a string literal.
        /// Uses a heuristic based on quote counting.
        /// </summary>
        /// <param name="text">The text to analyze.</param>
        /// <param name="position">The position to check.</param>
        /// <returns>True if the position is inside a string literal.</returns>
        public static bool IsInsideStringLiteral(string text, int position)
        {
            var quoteCount = 0;
            var inVerbatim = false;

            for (var i = 0; i < position; i++)
            {
                if (text[i] == '@' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    inVerbatim = true;
                    quoteCount++;
                    i++; // Skip the quote
                    continue;
                }

                if (text[i] == '"')
                {
                    // Check if it's escaped (not in verbatim)
                    if (!inVerbatim && i > 0 && text[i - 1] == '\\')
                    {
                        // Count consecutive backslashes
                        var backslashCount = 0;
                        for (var j = i - 1; j >= 0 && text[j] == '\\'; j--)
                        {
                            backslashCount++;
                        }

                        // If odd number of backslashes, the quote is escaped
                        if (backslashCount % 2 == 1)
                        {
                            continue;
                        }
                    }

                    quoteCount++;

                    // If we were in verbatim and hit a quote, check for double-quote escape
                    if (inVerbatim && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        i++; // Skip the second quote in ""
                        continue;
                    }

                    if (quoteCount % 2 == 0)
                    {
                        inVerbatim = false;
                    }
                }
            }

            // Odd quote count means we're inside a string
            return quoteCount % 2 == 1;
        }
    }
}
