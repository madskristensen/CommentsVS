using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using CommentsVS.Commands;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Adornments
{
    /// <summary>
    /// Simple command implementation for keyboard bindings
    /// </summary>
    internal class DelegateCommand : ICommand
    {
        private readonly Action _execute;

        public DelegateCommand(Action execute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public event EventHandler CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => _execute();
    }

    [Export(typeof(IViewTaggerProvider))]
    [ContentType("CSharp")]
    [ContentType("Basic")]
    [TagType(typeof(IntraTextAdornmentTag))]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class RenderedCommentIntraTextTaggerProvider : IViewTaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView == null || !(textView is IWpfTextView wpfTextView))
                return null;

            if (textView.TextBuffer != buffer)
                return null;

            return wpfTextView.Properties.GetOrCreateSingletonProperty(
                () => new RenderedCommentIntraTextTagger(wpfTextView)) as ITagger<T>;
        }
    }

    internal sealed class RenderedCommentIntraTextTagger : IntraTextAdornmentTagger<XmlDocCommentBlock, FrameworkElement>
    {
        private readonly HashSet<int> _temporarilyHiddenComments = new HashSet<int>();
        private int? _lastCaretLine;

        public RenderedCommentIntraTextTagger(IWpfTextView view) : base(view)
        {
            ToggleRenderedCommentsCommand.RenderedCommentsStateChanged += OnRenderedStateChanged;
            view.Caret.PositionChanged += OnCaretPositionChanged;

            // Hook into keyboard events at multiple levels
            view.VisualElement.PreviewKeyDown += OnViewKeyDown;

            // Add input binding for ESC key
            var escapeBinding = new KeyBinding(
                new DelegateCommand(HandleEscapeKeyInternal),
                Key.Escape,
                ModifierKeys.None);
            view.VisualElement.InputBindings.Add(escapeBinding);

            // Store tagger in view properties so command handler can find it
            view.Properties[typeof(RenderedCommentIntraTextTagger)] = this;
        }

        /// <summary>
        /// Public method for command handler to invoke ESC key behavior.
        /// Returns true if the ESC key was handled (hidden a comment).
        /// </summary>
        public bool HandleEscapeKey(int startLine)
        {
            // If comment is rendered, hide it
            if (!_temporarilyHiddenComments.Contains(startLine))
            {
                HideCommentRendering(startLine);
                return true;
            }

            return false;
        }

        private void HandleEscapeKeyInternal()
        {
            if (!General.Instance.EnableRenderedComments)
                return;

            var caretLine = view.Caret.Position.BufferPosition.GetContainingLine().LineNumber;
            var snapshot = view.TextBuffer.CurrentSnapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);

            if (commentStyle != null)
            {
                var parser = new XmlDocCommentParser(commentStyle);
                var blocks = parser.FindAllCommentBlocks(snapshot);

                foreach (var block in blocks)
                {
                    if (caretLine >= block.StartLine && caretLine <= block.EndLine)
                    {
                        HandleEscapeKey(block.StartLine);
                        return;
                    }
                }
            }
        }

        private void OnViewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && General.Instance.EnableRenderedComments)
            {
                // Check if caret is on a rendered comment line
                var caretLine = view.Caret.Position.BufferPosition.GetContainingLine().LineNumber;
                var snapshot = view.TextBuffer.CurrentSnapshot;
                var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);

                if (commentStyle != null)
                {
                    var parser = new XmlDocCommentParser(commentStyle);
                    var blocks = parser.FindAllCommentBlocks(snapshot);

                    // Find if caret is within any rendered comment
                    foreach (var block in blocks)
                    {
                        if (caretLine >= block.StartLine && caretLine <= block.EndLine)
                        {
                            // If comment is rendered (not temporarily hidden), hide it
                            if (!_temporarilyHiddenComments.Contains(block.StartLine))
                            {
                                HideCommentRendering(block.StartLine);
                                e.Handled = true;
                                return;
                            }
                        }
                    }
                }
            }
        }

        private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            var currentLine = e.NewPosition.BufferPosition.GetContainingLine().LineNumber;

            // If caret moved to a different line, check if we should re-enable rendering
            if (_lastCaretLine.HasValue && _lastCaretLine.Value != currentLine)
            {
                // Check if we moved away from any temporarily hidden comments
                if (_temporarilyHiddenComments.Count > 0)
                {
                    var snapshot = view.TextBuffer.CurrentSnapshot;
                    var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);
                    if (commentStyle != null)
                    {
                        var parser = new XmlDocCommentParser(commentStyle);
                        var blocks = parser.FindAllCommentBlocks(snapshot);

                        // Find which comments should be re-enabled
                        var toReEnable = new List<int>();
                        foreach (var hiddenLine in _temporarilyHiddenComments)
                        {
                            var block = blocks.FirstOrDefault(b => b.StartLine == hiddenLine);
                            if (block != null)
                            {
                                // Check if caret is outside this comment's range
                                if (currentLine < block.StartLine || currentLine > block.EndLine)
                                {
                                    toReEnable.Add(hiddenLine);
                                }
                            }
                        }

                        // Re-enable rendering for comments the caret moved away from
                        if (toReEnable.Count > 0)
                        {
                            foreach (var line in toReEnable)
                            {
                                _temporarilyHiddenComments.Remove(line);
                            }

                            // Defer the refresh to avoid layout exceptions
#pragma warning disable VSTHRD001, VSTHRD110 // Intentional fire-and-forget for UI update
                            view.VisualElement.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (!view.IsClosed)
                                {
                                    RefreshTags();
                                }
                            }), System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001, VSTHRD110
                        }
                    }
                }
            }

            _lastCaretLine = currentLine;
        }

        private void OnRenderedStateChanged(object sender, EventArgs e)
        {
            // Clear temporary hides when toggling rendered mode
            _temporarilyHiddenComments.Clear();

            // Defer refresh to avoid layout exceptions
#pragma warning disable VSTHRD001, VSTHRD110 // Intentional fire-and-forget for UI update
            view.VisualElement.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!view.IsClosed)
                {
                    var snapshot = view.TextBuffer.CurrentSnapshot;
                    RaiseTagsChanged(new SnapshotSpan(snapshot, 0, snapshot.Length));
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001, VSTHRD110
        }

        protected override IEnumerable<Tuple<SnapshotSpan, PositionAffinity?, XmlDocCommentBlock>> GetAdornmentData(
            NormalizedSnapshotSpanCollection spans)
        {
            if (!General.Instance.EnableRenderedComments || spans.Count == 0)
                yield break;

            ITextSnapshot snapshot = spans[0].Snapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);
            if (commentStyle == null)
                yield break;

            var parser = new XmlDocCommentParser(commentStyle);
            IReadOnlyList<XmlDocCommentBlock> blocks = parser.FindAllCommentBlocks(snapshot);

            foreach (XmlDocCommentBlock block in blocks)
            {
                // Skip comments that are temporarily hidden
                if (_temporarilyHiddenComments.Contains(block.StartLine))
                    continue;

                // Create span that covers entire comment including indentation
                // This ensures the rendered view starts at the exact same position as the comment
                var adjustedSpan = new SnapshotSpan(snapshot, block.Span.Start, block.Span.Length);

                if (!spans.IntersectsWith(new NormalizedSnapshotSpanCollection(adjustedSpan)))
                    continue;

                // For non-zero length spans, affinity should be null
                yield return Tuple.Create(adjustedSpan, (PositionAffinity?)null, block);
            }
        }

        protected override FrameworkElement CreateAdornment(XmlDocCommentBlock block, SnapshotSpan span)
        {
            return CreateRenderedCommentElement(block);
        }

        protected override bool UpdateAdornment(FrameworkElement adornment, XmlDocCommentBlock data)
        {
            // Return false to recreate the adornment
            return false;
        }

        private FrameworkElement CreateRenderedCommentElement(XmlDocCommentBlock block)
        {
            RenderedComment renderedComment = XmlDocCommentRenderer.Render(block);

            var fontSize = view.FormattedLineSource?.DefaultTextProperties?.FontRenderingEmSize ?? 13.0;
            var fontFamily = view.FormattedLineSource?.DefaultTextProperties?.Typeface?.FontFamily
                ?? new FontFamily("Consolas");

            // Calculate character width for monospace font
            var charWidth = fontSize * 0.6;  // Approximate width of a character in monospace

            // Calculate indentation width in pixels
            var indentWidth = block.Indentation.Length * charWidth;

            // Gray color palette for clean, subtle appearance
            var textBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));        // Gray for all text
            var linkBrush = new SolidColorBrush(Color.FromRgb(86, 156, 214));         // VS blue for links
            var codeBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));        // Lighter gray for code
            var paramBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));       // Medium gray for params

            // Main container - no padding/margin to match exact line height
            var outerBorder = new Border
            {
                Background = Brushes.Transparent,  // No background
                Padding = new Thickness(0),  // No padding to avoid indentation
                Margin = new Thickness(indentWidth, 0, 0, 0),  // Add left margin for indentation
                Focusable = true,
                Tag = block.StartLine,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };

            // Add double-click handler to hide this specific comment's rendering
            outerBorder.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2 && s is Border border && border.Tag is int startLine)
                {
                    HideCommentRendering(startLine);
                    e.Handled = true;
                }
            };

            // Add keyboard handler for ESC key on the border
            outerBorder.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && s is Border border && border.Tag is int startLine)
                {
                    HideCommentRendering(startLine);
                    e.Handled = true;
                }
            };

            // Ensure the border gets focus when mouse enters
            outerBorder.MouseEnter += (s, e) =>
            {
                ((Border)s).Focus();
                Keyboard.Focus((Border)s);
            };

            var summaryPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top
            };

            var summaryBlock = new TextBlock
            {
                FontFamily = fontFamily,
                FontSize = fontSize,
                Foreground = textBrush,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };
            TextOptions.SetTextFormattingMode(summaryBlock, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(summaryBlock, TextRenderingMode.Auto);

            RenderedCommentSection summary = renderedComment.Summary;
            if (summary != null)
            {
                RenderSectionContent(summaryBlock, summary.ProseLines, textBrush, linkBrush, codeBrush, paramBrush);
            }

            summaryPanel.Children.Add(summaryBlock);
            outerBorder.Child = summaryPanel;
            return outerBorder;
        }

        private void RenderSectionContent(TextBlock textBlock, IEnumerable<RenderedLine> lines,
            Brush defaultBrush, Brush linkBrush, Brush codeBrush, Brush paramBrush)
        {
            var first = true;
            foreach (RenderedLine line in lines)
            {
                if (line.IsBlank)
                    continue;

                if (!first)
                    textBlock.Inlines.Add(new Run(" ") { Foreground = defaultBrush });
                first = false;

                foreach (RenderedSegment segment in line.Segments)
                {
                    textBlock.Inlines.Add(CreateInline(segment, defaultBrush, linkBrush, codeBrush, paramBrush));
                }
            }
        }

        private Inline CreateInline(RenderedSegment segment, Brush defaultBrush, Brush linkBrush,
            Brush codeBrush, Brush paramBrush)
        {
            return segment.Type switch
            {
                RenderedSegmentType.Link => new Run(segment.Text)
                {
                    Foreground = linkBrush,
                    TextDecorations = TextDecorations.Underline,
                    Cursor = Cursors.Hand
                },
                RenderedSegmentType.ParamRef or RenderedSegmentType.TypeParamRef =>
                    new Run(segment.Text)
                    {
                        Foreground = paramBrush,
                        FontStyle = FontStyles.Italic,
                        FontWeight = FontWeights.Normal  // Normal weight, not bold
                    },
                RenderedSegmentType.Bold =>
                    new Run(segment.Text)
                    {
                        FontWeight = FontWeights.Normal,  // Normal weight, not bold
                        Foreground = defaultBrush
                    },
                RenderedSegmentType.Italic =>
                    new Run(segment.Text)
                    {
                        FontStyle = FontStyles.Italic,
                        Foreground = defaultBrush
                    },
                RenderedSegmentType.Code =>
                    new Run(segment.Text)
                    {
                        Foreground = codeBrush,
                        FontFamily = new FontFamily("Consolas"),
                        Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128))
                    },
                _ => new Run(segment.Text.TrimStart()) { Foreground = defaultBrush }
            };
        }

        private void HideCommentRendering(int startLine)
        {
            // Temporarily hide rendering for this specific comment
            _temporarilyHiddenComments.Add(startLine);

            // Defer refresh to avoid layout exceptions
#pragma warning disable VSTHRD001, VSTHRD110 // Intentional fire-and-forget for UI update
            view.VisualElement.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!view.IsClosed)
                {
                    RefreshTags();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001, VSTHRD110
        }

        private void RefreshTags()
        {
            var snapshot = view.TextBuffer.CurrentSnapshot;
            RaiseTagsChanged(new SnapshotSpan(snapshot, 0, snapshot.Length));
        }
    }
}
