using System.Collections.Generic;
using System.Linq;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text.Editor;

namespace CommentsVS.Adornments
{
    /// <summary>
    /// Tracks caret position changes and determines when the caret moves in or out of comment blocks.
    /// Coordinates with CommentVisibilityManager to restore rendered comments when the caret leaves them.
    /// </summary>
    internal sealed class CaretTracker : IDisposable
    {
        private readonly IWpfTextView _view;
        private readonly CommentVisibilityManager _visibilityManager;
        private int? _lastCaretLine;
        private bool _disposed;

        /// <summary>
        /// Raised when a refresh of tags is needed due to caret movement.
        /// </summary>
        public event EventHandler RefreshRequested;

        /// <summary>
        /// Initializes a new instance of the <see cref="CaretTracker"/> class.
        /// </summary>
        /// <param name="view">The text view to track.</param>
        /// <param name="visibilityManager">The visibility manager for comment state.</param>
        public CaretTracker(IWpfTextView view, CommentVisibilityManager visibilityManager)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _visibilityManager = visibilityManager ?? throw new ArgumentNullException(nameof(visibilityManager));

            _view.Caret.PositionChanged += OnCaretPositionChanged;
        }

        /// <summary>
        /// Gets the current caret line number.
        /// </summary>
        public int CurrentCaretLine => _view.Caret.Position.BufferPosition.GetContainingLine().LineNumber;

        /// <summary>
        /// Determines whether the caret is currently within the specified comment block.
        /// </summary>
        /// <param name="startLine">The starting line of the comment block.</param>
        /// <param name="endLine">The ending line of the comment block.</param>
        /// <returns>True if the caret is within the block; otherwise, false.</returns>
        public bool IsCaretInCommentBlock(int startLine, int endLine)
        {
            var caretLine = CurrentCaretLine;
            return caretLine >= startLine && caretLine <= endLine;
        }

        private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            var currentLine = e.NewPosition.BufferPosition.GetContainingLine().LineNumber;

            // Fast path: no line change means no work needed
            if (!_lastCaretLine.HasValue || _lastCaretLine.Value == currentLine)
            {
                _lastCaretLine = currentLine;
                return;
            }

            var lastLine = _lastCaretLine.Value;
            _lastCaretLine = currentLine;

            // Cache settings to avoid repeated property access
            General settings = General.Instance;
            var isRenderingEnabled = settings.CommentRenderingMode is RenderingMode.Full or RenderingMode.Compact;
            var isCaretEnterMode = settings.EditTrigger == EditTrigger.OnCaretEnter;

            // Early exit: if rendering is off, no hidden comments, and no edit tracking, nothing to do
            var hasHiddenComments = _visibilityManager.HasAnyHiddenComments;
            var hasEditTracking = _visibilityManager.HasAnyRecentlyEditedLines;

            if (!isRenderingEnabled && !hasHiddenComments && !hasEditTracking)
            {
                return;
            }

            // Only fetch blocks if we actually need them
            var needBlocksForEnter = isRenderingEnabled && isCaretEnterMode;
            if (!needBlocksForEnter && !hasHiddenComments && !hasEditTracking)
            {
                return;
            }

            IReadOnlyList<XmlDocCommentBlock> blocks = XmlDocCommentParser.GetCachedCommentBlocks(_view.TextBuffer);
            if (blocks == null || blocks.Count == 0)
            {
                return;
            }

            var shouldRefresh = false;

            // Single pass through blocks to handle all scenarios
            foreach (XmlDocCommentBlock block in blocks)
            {
                var wasInComment = lastLine >= block.StartLine && lastLine <= block.EndLine;
                var nowInComment = currentLine >= block.StartLine && currentLine <= block.EndLine;

                // Scenario 1: Entering a comment (OnCaretEnter mode)
                if (needBlocksForEnter && !wasInComment && nowInComment)
                {
                    if (!_visibilityManager.IsCommentHidden(block.StartLine))
                    {
                        _visibilityManager.HideComment(block.StartLine);
                        shouldRefresh = true;
                    }
                }

                // Scenario 2: Leaving a hidden comment - restore rendering
                if (hasHiddenComments && wasInComment && !nowInComment)
                {
                    if (_visibilityManager.IsCommentHidden(block.StartLine))
                    {
                        _visibilityManager.ShowComment(block.StartLine);
                        shouldRefresh = true;
                    }
                }

                // Scenario 3: Leaving an edited comment - clear edit tracking
                if (hasEditTracking && wasInComment && !nowInComment)
                {
                    if (_visibilityManager.ClearEditTracking(block.StartLine, block.EndLine))
                    {
                        shouldRefresh = true;
                    }
                }
            }

            if (shouldRefresh)
            {
                RefreshRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _view.Caret.PositionChanged -= OnCaretPositionChanged;
        }
    }
}
