using System.Collections.Generic;

namespace CommentsVS.Adornments
{
    /// <summary>
    /// Manages the visibility state of rendered comments, tracking which comments are
    /// temporarily hidden (user pressed ESC) and which lines were recently edited.
    /// </summary>
    internal sealed class CommentVisibilityManager
    {
        private readonly HashSet<int> _temporarilyHiddenComments = [];
        private readonly HashSet<int> _recentlyEditedLines = [];

        /// <summary>
        /// Raised when the visibility state changes and tags should be refreshed.
        /// </summary>
        public event EventHandler VisibilityChanged;

        /// <summary>
        /// Determines whether a comment starting at the specified line is temporarily hidden.
        /// </summary>
        /// <param name="startLine">The starting line number of the comment block.</param>
        /// <returns>True if the comment is hidden; otherwise, false.</returns>
        public bool IsCommentHidden(int startLine)
            => _temporarilyHiddenComments.Contains(startLine);

        /// <summary>
        /// Determines whether the specified line was recently edited.
        /// </summary>
        /// <param name="lineNumber">The line number to check.</param>
        /// <returns>True if the line was recently edited; otherwise, false.</returns>
        public bool IsLineRecentlyEdited(int lineNumber)
            => _recentlyEditedLines.Contains(lineNumber);

        /// <summary>
        /// Determines whether any line in the specified range was recently edited.
        /// </summary>
        /// <param name="startLine">The starting line number.</param>
        /// <param name="endLine">The ending line number.</param>
        /// <returns>True if any line in the range was recently edited; otherwise, false.</returns>
        public bool HasRecentlyEditedLines(int startLine, int endLine)
        {
            for (var line = startLine; line <= endLine; line++)
            {
                if (_recentlyEditedLines.Contains(line))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the line numbers of all temporarily hidden comments.
        /// </summary>
        /// <returns>An enumerable of hidden comment start line numbers.</returns>
        public IEnumerable<int> GetHiddenCommentLines()
            => [.. _temporarilyHiddenComments];

        /// <summary>
        /// Hides the rendering of a comment starting at the specified line.
        /// </summary>
        /// <param name="startLine">The starting line number of the comment block.</param>
        public void HideComment(int startLine)
        {
            if (_temporarilyHiddenComments.Add(startLine))
            {
                VisibilityChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Shows the rendering of a previously hidden comment.
        /// </summary>
        /// <param name="startLine">The starting line number of the comment block.</param>
        /// <returns>True if the comment was unhidden; false if it wasn't hidden.</returns>
        public bool ShowComment(int startLine)
        {
            if (_temporarilyHiddenComments.Remove(startLine))
            {
                VisibilityChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Marks lines in the specified range as recently edited.
        /// </summary>
        /// <param name="startLine">The starting line number.</param>
        /// <param name="endLine">The ending line number.</param>
        public void MarkLinesEdited(int startLine, int endLine)
        {
            for (var line = startLine; line <= endLine; line++)
            {
                _recentlyEditedLines.Add(line);
            }
        }

        /// <summary>
        /// Clears the edit tracking for lines in the specified range.
        /// </summary>
        /// <param name="startLine">The starting line number.</param>
        /// <param name="endLine">The ending line number.</param>
        /// <returns>True if any lines were cleared; otherwise, false.</returns>
        public bool ClearEditTracking(int startLine, int endLine)
        {
            var anyCleared = false;
            for (var line = startLine; line <= endLine; line++)
            {
                if (_recentlyEditedLines.Remove(line))
                {
                    anyCleared = true;
                }
            }
            return anyCleared;
        }

        /// <summary>
        /// Clears all visibility state (hidden comments and edited lines).
        /// </summary>
        public void ClearAll()
        {
            _temporarilyHiddenComments.Clear();
            _recentlyEditedLines.Clear();
        }

        /// <summary>
        /// Clears only the temporarily hidden comments (used when toggling rendering mode).
        /// </summary>
        public void ClearHiddenComments()
        {
            _temporarilyHiddenComments.Clear();
        }

        /// <summary>
        /// Determines whether any comments are currently hidden.
        /// </summary>
        public bool HasAnyHiddenComments => _temporarilyHiddenComments.Count > 0;

        /// <summary>
        /// Determines whether any lines have been recently edited.
        /// </summary>
        public bool HasAnyRecentlyEditedLines => _recentlyEditedLines.Count > 0;
    }
}
