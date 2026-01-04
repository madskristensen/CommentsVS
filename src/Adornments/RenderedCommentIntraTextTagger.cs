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
        private readonly HashSet<int> _expandedComments = new HashSet<int>();
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
        /// Returns true if the ESC key was handled (collapsed or hidden a comment).
        /// </summary>
        public bool HandleEscapeKey(int startLine)
        {
            // If comment is expanded, collapse it first
            if (_expandedComments.Contains(startLine))
            {
                _expandedComments.Remove(startLine);
                RefreshTags();
                return true;
            }

            // If comment is rendered, hide it
            if (!_temporarilyHiddenComments.Contains(startLine))
            {
                HideCommentRendering(startLine);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Expand a comment to show all sections.
        /// Returns true if the comment was expanded.
        /// </summary>
        public bool ExpandComment(int startLine)
        {
            // Only expand if comment has more content and is not already expanded
            if (!_expandedComments.Contains(startLine))
            {
                _expandedComments.Add(startLine);

#pragma warning disable VSTHRD001, VSTHRD110 // Intentional fire-and-forget for UI update
                view.VisualElement.Dispatcher.BeginInvoke(
                    new Action(() => RefreshTags()),
                    System.Windows.Threading.DispatcherPriority.Input);
#pragma warning restore VSTHRD001, VSTHRD110

                return true;
            }

            return false;
        }

        /// <summary>
        /// Collapse an expanded comment to show only summary.
        /// Returns true if the comment was collapsed.
        /// </summary>
        public bool CollapseComment(int startLine)
        {
            // Only collapse if comment is currently expanded
            if (_expandedComments.Contains(startLine))
            {
                _expandedComments.Remove(startLine);

#pragma warning disable VSTHRD001, VSTHRD110 // Intentional fire-and-forget for UI update
                view.VisualElement.Dispatcher.BeginInvoke(
                    new Action(() => RefreshTags()),
                    System.Windows.Threading.DispatcherPriority.Input);
#pragma warning restore VSTHRD001, VSTHRD110

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
                            // If comment is expanded, collapse it first
                            if (_expandedComments.Contains(block.StartLine))
                            {
                                _expandedComments.Remove(block.StartLine);
                                RefreshTags();
                                e.Handled = true;
                                return;
                            }

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
            var lineHeight = view.FormattedLineSource?.LineHeight ?? 15.0;

            // Calculate character width for monospace font
            var charWidth = fontSize * 0.6;  // Approximate width of a character in monospace

            // Calculate indentation width in pixels
            var indentWidth = block.Indentation.Length * charWidth;

            // Calculate max width for 100 characters total (including indentation)
            // Subtract indentation to ensure total line length doesn't exceed 100 chars
            var maxContentWidth = (100 - block.Indentation.Length) * charWidth;

            // Gray color palette for clean, subtle appearance
            var textBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));        // Gray for all text
            var labelBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));       // Gray for labels
            var linkBrush = new SolidColorBrush(Color.FromRgb(86, 156, 214));         // VS blue for links
            var codeBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));        // Lighter gray for code
            var paramBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));       // Medium gray for params

            var isExpanded = _expandedComments.Contains(block.StartLine);
            var hasMoreContent = renderedComment.HasAdditionalSections;

            // Main container - no padding/margin to match exact line height
            var outerBorder = new Border
            {
                Background = Brushes.Transparent,  // No background
                Padding = new Thickness(0),  // No padding to avoid indentation
                Margin = new Thickness(indentWidth, 0, 0, 0),  // Add left margin for indentation
                Focusable = true,
                Tag = block.StartLine
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

            var container = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            if (isExpanded && hasMoreContent)
            {
                // Expanded view with clean appearance
                RenderExpandedView(container, renderedComment, block, fontSize, fontFamily, lineHeight,
                    textBrush, labelBrush, linkBrush, codeBrush, paramBrush, maxContentWidth);
            }
            else
            {
                // Compact summary view with inline expand button
                RenderCompactView(container, renderedComment, block, fontSize, fontFamily,
                    textBrush, linkBrush, codeBrush, paramBrush, hasMoreContent, maxContentWidth);
            }

            outerBorder.Child = container;
            return outerBorder;
        }

        private void RenderCompactView(StackPanel container, RenderedComment comment, XmlDocCommentBlock block,
            double fontSize, FontFamily fontFamily, Brush textBrush, Brush linkBrush,
            Brush codeBrush, Brush paramBrush, bool hasMoreContent, double maxWidth)
        {
            var summaryPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top
            };

            var summaryBlock = new TextBlock
            {
                FontFamily = fontFamily,
                FontSize = fontSize,  // Use exact font size
                Foreground = textBrush,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(0),
                Margin = new Thickness(0)
            };

            RenderedCommentSection summary = comment.Summary;
            if (summary != null)
            {
                RenderSectionContent(summaryBlock, summary.ProseLines, textBrush, linkBrush, codeBrush, paramBrush);
            }

            summaryPanel.Children.Add(summaryBlock);

            // Expand button with hover effect
            if (hasMoreContent)
            {
                var expanderContainer = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0, 86, 156, 214)),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(3, 0, 3, 0),  // Minimal horizontal padding only
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                    Cursor = Cursors.Hand,
                    Focusable = false  // Prevent focus, handle click immediately
                };

                var expander = new TextBlock
                {
                    Text = "⋯",
                    FontFamily = fontFamily,
                    FontSize = fontSize,
                    Foreground = linkBrush,
                    FontWeight = FontWeights.Bold,
                    ToolTip = "Show parameters, returns, and more",
                    VerticalAlignment = VerticalAlignment.Top,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    IsHitTestVisible = false,  // Let clicks pass through to the border
                    Focusable = false
                };

                expanderContainer.Child = expander;
                expanderContainer.Tag = block.StartLine;

                // Use PreviewMouseLeftButtonDown for immediate response
                expanderContainer.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    if (s is Border border && border.Tag is int startLine)
                    {
                        _expandedComments.Add(startLine);

                        // Defer refresh to avoid double-click issue
#pragma warning disable VSTHRD001, VSTHRD110 // Intentional fire-and-forget for UI update
                        view.VisualElement.Dispatcher.BeginInvoke(
                            new Action(() => RefreshTags()),
                            System.Windows.Threading.DispatcherPriority.Input);
#pragma warning restore VSTHRD001, VSTHRD110

                        e.Handled = true;
                    }
                };

                // Hover effect
                expanderContainer.MouseEnter += (s, e) =>
                {
                    ((Border)s).Background = new SolidColorBrush(Color.FromArgb(20, 86, 156, 214));
                };
                expanderContainer.MouseLeave += (s, e) =>
                {
                    ((Border)s).Background = new SolidColorBrush(Color.FromArgb(0, 86, 156, 214));
                };

                summaryPanel.Children.Add(expanderContainer);
            }

            container.Children.Add(summaryPanel);
        }

        private void RenderExpandedView(StackPanel container, RenderedComment comment, XmlDocCommentBlock block,
            double fontSize, FontFamily fontFamily, double lineHeight, Brush textBrush, Brush labelBrush,
            Brush linkBrush, Brush codeBrush, Brush paramBrush, double maxWidth)
        {
            var isFirst = true;

            foreach (RenderedCommentSection section in comment.Sections)
            {
                if (section.IsEmpty)
                    continue;

                // Add spacing between sections (no horizontal lines)
                if (!isFirst)
                {
                    container.Children.Add(new Border { Height = lineHeight * 0.5 });
                }

                var sectionPanel = new StackPanel { Orientation = Orientation.Vertical };

                // Label and content on same line for compact sections
                if (section.Type != CommentSectionType.Summary)
                {
                    var sectionLine = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 0, 0, 0)
                    };

                    var label = GetSectionLabel(section);
                    if (!string.IsNullOrEmpty(label))
                    {
                        var labelBlock = new TextBlock
                        {
                            Text = label + ": ",
                            FontFamily = fontFamily,
                            FontSize = fontSize,
                            Foreground = labelBrush,
                            FontWeight = FontWeights.SemiBold,
                            VerticalAlignment = VerticalAlignment.Top,
                            Padding = new Thickness(0),
                            Margin = new Thickness(0, 0, 4, 0)
                        };
                        sectionLine.Children.Add(labelBlock);
                    }

                    var contentBlock = new TextBlock
                    {
                        FontFamily = fontFamily,
                        FontSize = fontSize,
                        Foreground = textBrush,
                        TextWrapping = TextWrapping.Wrap,
                        Padding = new Thickness(0),
                        MaxWidth = maxWidth,
                        VerticalAlignment = VerticalAlignment.Top
                    };

                    RenderSectionContent(contentBlock, section.Lines, textBrush, linkBrush, codeBrush, paramBrush);
                    sectionLine.Children.Add(contentBlock);
                    sectionPanel.Children.Add(sectionLine);
                }
                else
                {
                    // Summary section - no label
                    var contentBlock = new TextBlock
                    {
                        FontFamily = fontFamily,
                        FontSize = fontSize,
                        Foreground = textBrush,
                        TextWrapping = TextWrapping.Wrap,
                        Padding = new Thickness(0),
                        MaxWidth = maxWidth,
                        VerticalAlignment = VerticalAlignment.Top
                    };

                    RenderSectionContent(contentBlock, section.Lines, textBrush, linkBrush, codeBrush, paramBrush);
                    sectionPanel.Children.Add(contentBlock);
                }

                container.Children.Add(sectionPanel);
                isFirst = false;
            }

            // Collapse button at bottom - compact design
            var collapsePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, lineHeight * 0.5, 0, 0)
            };

            var collapseBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0, 86, 156, 214)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(6, 2, 6, 2),
                Cursor = Cursors.Hand,
                Tag = block.StartLine,
                Focusable = false
            };

            var collapser = new TextBlock
            {
                Text = "▲",
                FontFamily = fontFamily,
                FontSize = fontSize * 0.85,
                Foreground = linkBrush,
                IsHitTestVisible = false,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Focusable = false,
                ToolTip = "Collapse to summary"
            };

            collapseBorder.Child = collapser;

            // Use PreviewMouseLeftButtonDown for immediate response
            collapseBorder.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (s is Border border && border.Tag is int startLine)
                {
                    _expandedComments.Remove(startLine);

                    // Defer refresh to avoid double-click issue
#pragma warning disable VSTHRD001, VSTHRD110 // Intentional fire-and-forget for UI update
                    view.VisualElement.Dispatcher.BeginInvoke(
                        new Action(() => RefreshTags()),
                        System.Windows.Threading.DispatcherPriority.Input);
#pragma warning restore VSTHRD001, VSTHRD110

                    e.Handled = true;
                }
            };

            // Hover effect
            collapseBorder.MouseEnter += (s, e) =>
            {
                ((Border)s).Background = new SolidColorBrush(Color.FromArgb(20, 86, 156, 214));
            };
            collapseBorder.MouseLeave += (s, e) =>
            {
                ((Border)s).Background = new SolidColorBrush(Color.FromArgb(0, 86, 156, 214));
            };

            collapsePanel.Children.Add(collapseBorder);
            container.Children.Add(collapsePanel);
        }

        private string GetSectionLabel(RenderedCommentSection section)
        {
            return section.Type switch
            {
                CommentSectionType.Param => $"{section.Name}",
                CommentSectionType.TypeParam => $"<{section.Name}>",
                CommentSectionType.Returns => "Returns",
                CommentSectionType.Exception => $"{section.Name}",
                CommentSectionType.Remarks => "Remarks",
                CommentSectionType.Example => "Example",
                CommentSectionType.Value => "Value",
                CommentSectionType.SeeAlso => "See Also",
                CommentSectionType.Summary => "Summary",
                _ => null
            };
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
                        FontWeight = FontWeights.SemiBold
                    },
                RenderedSegmentType.Bold =>
                    new Run(segment.Text)
                    {
                        FontWeight = FontWeights.Bold,
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
