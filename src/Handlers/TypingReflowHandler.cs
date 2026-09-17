using System.ComponentModel.Composition;
using System.Threading;

using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
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
    [ContentType(SupportedContentTypes.PowerShellProTools)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]


    internal sealed class TypingReflowHandler : IWpfTextViewCreationListener
    {

        [Import(AllowDefault = true)]
        internal IAsyncCompletionBroker AsyncCompletionBroker { get; set; }

        [Import(AllowDefault = true)]
        internal ICompletionBroker LegacyCompletionBroker { get; set; }

        private const int _debounceDelayMs = 300;

        public void TextViewCreated(IWpfTextView textView)
        {
            _ = textView.Properties.GetOrCreateSingletonProperty(
                () => new TypingReflowTracker(textView, AsyncCompletionBroker, LegacyCompletionBroker));
        }

        /// <summary>
        /// Tracks typing in a specific text view and triggers reflow when appropriate.
        /// </summary>
        private sealed class TypingReflowTracker : IDisposable
        {
            private readonly IWpfTextView _textView;
            private readonly IAsyncCompletionBroker _asyncCompletionBroker;
            private readonly ICompletionBroker _legacyCompletionBroker;
            private CancellationTokenSource _debounceCts;
            private bool _isReflowing;
            private bool _disposed;

            public TypingReflowTracker(
                IWpfTextView textView,
                IAsyncCompletionBroker asyncCompletionBroker,
                ICompletionBroker legacyCompletionBroker)
            {
                _textView = textView;
                _asyncCompletionBroker = asyncCompletionBroker;
                _legacyCompletionBroker = legacyCompletionBroker;
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
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
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

                        // Don't interfere with IntelliSense. Reflowing while a completion
                        // session is active steals focus from the picklist (e.g. when inserting
                        // <see> XML elements) and can corrupt the caret position. See issue #77.
                        if (IsCompletionActive())
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
                }).FireAndForget();
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

                if (!EditorConfigSettings.IsEnabled(TextBufferHelper.GetFilePath(_textView.TextBuffer)))
                {
                    return;
                }

                ITextSnapshot snapshot = _textView.TextSnapshot;
                if (position >= snapshot.Length)
                {
                    position = snapshot.Length > 0 ? snapshot.Length - 1 : 0;
                }

                // Check if current line exceeds max length (quick check before parsing)
                // Use .editorconfig value if defined, otherwise fall back to Options page
                ITextSnapshotLine line = snapshot.GetLineFromPosition(position);
                var maxLineLength = EditorConfigSettings.GetMaxLineLength(_textView);
                if (line.Length <= maxLineLength)
                {
                    return;
                }

                // Don't reflow while the caret is inside an XML tag (e.g. editing
                // <see cref="..."/>). Wrapping mid-tag breaks the element and disrupts
                // the VS XML editing/IntelliSense experience. See issue #77.
                if (IsCaretInsideXmlTag(snapshot))
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

                // Perform reflow using .editorconfig-aware settings
                CommentReflowEngine engine = EditorConfigSettings.CreateReflowEngine(_textView);
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

                var caretPosition = _textView.Caret.Position.BufferPosition.Position;
                var originalText = snapshot.GetText(block.Span);
                var originalCaretOffset = caretPosition - block.Span.Start;
                var offsetFromBlockEnd = block.Span.End - caretPosition;
                int? mappedCaretOffset = CommentCaretMapper.MapCaretOffset(
                    originalText,
                    reflowed,
                    originalCaretOffset,
                    block.CommentStyle,
                    block.IsMultiLineStyle);

                _isReflowing = true;
                try
                {
                    using (ITextEdit edit = _textView.TextBuffer.CreateEdit())
                    {
                        _ = edit.Replace(block.Span, reflowed);
                        _ = edit.Apply();
                    }

                    ITextSnapshot newSnapshot = _textView.TextSnapshot;
                    var newCaretPosition = mappedCaretOffset.HasValue
                        ? block.Span.Start + mappedCaretOffset.Value
                        : block.Span.Start + reflowed.Length - offsetFromBlockEnd;

                    // Clamp to valid range
                    var newBlockEnd = block.Span.Start + reflowed.Length;
                    newCaretPosition = Math.Max(block.Span.Start, Math.Min(newCaretPosition, newBlockEnd));
                    newCaretPosition = Math.Min(newCaretPosition, newSnapshot.Length);

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

            /// <summary>
            /// Returns true when an IntelliSense completion session is currently active in the view.
            /// </summary>
            private bool IsCompletionActive()
            {
                try
                {
                    if (_asyncCompletionBroker != null &&
                        _asyncCompletionBroker.IsCompletionActive(_textView))
                    {
                        return true;
                    }

                    if (_legacyCompletionBroker != null &&
                        _legacyCompletionBroker.IsCompletionActive(_textView))
                    {
                        return true;
                    }
                }
                catch
                {
                    // If the broker can't answer, err on the side of not reflowing.
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Returns true when the caret sits inside an unclosed XML tag (an unmatched '&lt;'
            /// appears before the caret on the current line), where reflowing would corrupt the tag.
            /// </summary>
            private bool IsCaretInsideXmlTag(ITextSnapshot snapshot)
            {
                var caretPosition = _textView.Caret.Position.BufferPosition.Position;
                if (caretPosition > snapshot.Length)
                {
                    return false;
                }

                ITextSnapshotLine line = snapshot.GetLineFromPosition(Math.Min(caretPosition, Math.Max(snapshot.Length - 1, 0)));
                var lineStart = line.Start.Position;
                if (caretPosition < lineStart)
                {
                    return false;
                }

                var textBeforeCaret = snapshot.GetText(lineStart, caretPosition - lineStart);
                var lastOpen = textBeforeCaret.LastIndexOf('<');
                var lastClose = textBeforeCaret.LastIndexOf('>');

                return lastOpen > lastClose;
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
