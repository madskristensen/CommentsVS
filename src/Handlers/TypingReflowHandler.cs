using System.ComponentModel.Composition;
using System.Threading;

using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Handlers
{
    /// <summary>
    /// Monitors typing in XML documentation comments and triggers reflow when lines exceed max length.
    /// Uses debouncing to ensure smooth typing experience without swallowing characters.
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(SupportedContentTypes.CSharp)]
    [ContentType(SupportedContentTypes.VisualBasic)]
    [ContentType(SupportedContentTypes.FSharp)]
    [ContentType(SupportedContentTypes.CPlusPlus)]
    [ContentType(SupportedContentTypes.TypeScript)]
    [ContentType(SupportedContentTypes.JavaScript)]
    [ContentType(SupportedContentTypes.Razor)]
    [ContentType(SupportedContentTypes.Sql)]
    [ContentType(SupportedContentTypes.PowerShell)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]


    internal sealed class TypingReflowHandler : IWpfTextViewCreationListener
    {


        private const int _debounceDelayMs = 300;

        public void TextViewCreated(IWpfTextView textView)
        {
            _ = textView.Properties.GetOrCreateSingletonProperty(
                () => new TypingReflowTracker(textView));
        }

        /// <summary>
        /// Tracks typing in a specific text view and triggers reflow when appropriate.
        /// </summary>
        private sealed class TypingReflowTracker : IDisposable
        {
            private readonly IWpfTextView _textView;
            private CancellationTokenSource _debounceCts;
            private bool _isReflowing;
            private bool _disposed;

            public TypingReflowTracker(IWpfTextView textView)
            {
                _textView = textView;
                _textView.TextBuffer.Changed += OnTextBufferChanged;
                _textView.Closed += OnTextViewClosed;
            }

            private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
            {
                if (_isReflowing || _disposed)
                {
                    return;
                }

                // Check setting synchronously before doing any work
                if (!General.Instance.ReflowOnTyping)
                {
                    return;
                }

                // Only process single-character insertions (typing)
                if (!IsSingleCharacterTyping(e))
                {
                    return;
                }

                // Get the change position from the event's Changes collection
                if (e.Changes == null || e.Changes.Count == 0)
                {
                    return;
                }

                var changePosition = e.Changes[0].NewPosition + e.Changes[0].NewLength;


                // Cancel any pending reflow
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = new CancellationTokenSource();

                CancellationToken token = _debounceCts.Token;

                // Schedule debounced reflow check
                // Note: Fire-and-forget is intentional here for debouncing UX, but we catch all exceptions
                _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(_debounceDelayMs, token).ConfigureAwait(false);

                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);

                        if (_disposed || token.IsCancellationRequested)
                        {
                            return;
                        }

                        await TryReflowAtPositionAsync(changePosition, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when typing continues - debounce was cancelled
                    }
                    catch (Exception ex)
                    {
                        // Log unexpected exceptions to prevent silent failures
                        await ex.LogAsync();
                    }
                });
            }

            private static bool IsSingleCharacterTyping(TextContentChangedEventArgs e)
            {
                if (e.Changes.Count != 1)
                {
                    return false;
                }

                ITextChange change = e.Changes[0];

                // Single character insert (not delete, not replace multiple)
                return change.NewLength >= 1 && change.NewLength <= 2 && change.OldLength == 0;
            }

            private async System.Threading.Tasks.Task TryReflowAtPositionAsync(int position, CancellationToken token)
            {
                General options = await General.GetLiveInstanceAsync();

                ITextSnapshot snapshot = _textView.TextSnapshot;
                if (position >= snapshot.Length)
                {
                    position = snapshot.Length > 0 ? snapshot.Length - 1 : 0;
                }

                // Check if current line exceeds max length (quick check before parsing)
                ITextSnapshotLine line = snapshot.GetLineFromPosition(position);
                if (line.Length <= options.MaxLineLength)
                {
                    return;
                }

                var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);
                if (commentStyle == null)
                {
                    return;
                }

                var parser = new XmlDocCommentParser(commentStyle);
                XmlDocCommentBlock block = parser.FindCommentBlockAtPosition(snapshot, position);

                if (block == null)
                {
                    return;
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                // Perform reflow
                CommentReflowEngine engine = options.CreateReflowEngine();
                var reflowed = engine.ReflowComment(block);

                if (string.IsNullOrEmpty(reflowed))
                {
                    return;
                }

                // Quick length check before expensive string comparison
                if (reflowed.Length == block.Span.Length)
                {
                    var currentText = snapshot.GetText(block.Span);
                    if (reflowed == currentText)
                    {
                        return;
                    }
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                // Calculate caret offset from end of block to preserve position
                var caretPosition = _textView.Caret.Position.BufferPosition.Position;
                var offsetFromBlockEnd = block.Span.End - caretPosition;

                _isReflowing = true;
                try
                {
                    using (ITextEdit edit = _textView.TextBuffer.CreateEdit())
                    {
                        _ = edit.Replace(block.Span, reflowed);
                        _ = edit.Apply();
                    }

                    // Calculate new caret position based on length difference
                    // Avoids re-parsing the comment block
                    ITextSnapshot newSnapshot = _textView.TextSnapshot;
                    var lengthDelta = reflowed.Length - block.Span.Length;
                    var newBlockEnd = block.Span.End + lengthDelta;
                    var newCaretPosition = newBlockEnd - offsetFromBlockEnd;

                    // Clamp to valid range
                    newCaretPosition = Math.Max(block.Span.Start, Math.Min(newCaretPosition, newSnapshot.Length));

                    var newCaretPoint = new SnapshotPoint(newSnapshot, newCaretPosition);
                    _ = _textView.Caret.MoveTo(newCaretPoint);
                }
                finally
                {
                    _isReflowing = false;
                }
            }

            private void OnTextViewClosed(object sender, EventArgs e)
            {
                Dispose();
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _textView.TextBuffer.Changed -= OnTextBufferChanged;
                _textView.Closed -= OnTextViewClosed;
            }
        }
    }
}
