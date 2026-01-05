using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
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
using Microsoft.VisualStudio.Text.Outlining;
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
        [Import]
        internal IOutliningManagerService OutliningManagerService { get; set; }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView == null || !(textView is IWpfTextView wpfTextView))
                return null;

            if (textView.TextBuffer != buffer)
                return null;

            var outliningManager = OutliningManagerService?.GetOutliningManager(textView);

            return wpfTextView.Properties.GetOrCreateSingletonProperty(
                () => new RenderedCommentIntraTextTagger(wpfTextView, outliningManager)) as ITagger<T>;
        }
    }

    internal sealed class RenderedCommentIntraTextTagger : IntraTextAdornmentTagger<XmlDocCommentBlock, FrameworkElement>
    {
        private readonly HashSet<int> _temporarilyHiddenComments = new HashSet<int>();
        private readonly IOutliningManager _outliningManager;
        private int? _lastCaretLine;

        public RenderedCommentIntraTextTagger(IWpfTextView view, IOutliningManager outliningManager) : base(view)
        {
            _outliningManager = outliningManager;

            ToggleRenderedCommentsCommand.RenderedCommentsStateChanged += OnRenderedStateChanged;
            view.Caret.PositionChanged += OnCaretPositionChanged;

            // Listen for zoom level changes to refresh adornments with new font size
            view.ZoomLevelChanged += OnZoomLevelChanged;

            // Listen for outlining expand/collapse to toggle raw source view
            if (_outliningManager != null)
            {
                _outliningManager.RegionsExpanded += OnRegionsExpanded;
                _outliningManager.RegionsCollapsed += OnRegionsCollapsed;
            }

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

        private void OnRegionsExpanded(object sender, RegionsExpandedEventArgs e)
        {
            var renderingMode = General.Instance.CommentRenderingMode;
            if (renderingMode != RenderingMode.Compact && renderingMode != RenderingMode.Full)
            {
                return;
            }

            // When outlining region is expanded, show raw source (hide rendered adornment)
            var snapshot = view.TextBuffer.CurrentSnapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);
            if (commentStyle == null)
            {
                return;
            }

            var parser = new XmlDocCommentParser(commentStyle);
            var blocks = parser.FindAllCommentBlocks(snapshot);

            foreach (var region in e.ExpandedRegions)
            {
                var regionSpan = region.Extent.GetSpan(snapshot);
                foreach (var block in blocks)
                {
                    var blockSpan = new SnapshotSpan(snapshot, block.Span);
                    if (regionSpan.IntersectsWith(blockSpan))
                    {
                        _temporarilyHiddenComments.Add(block.StartLine);
                    }
                }
            }

            DeferredRefreshTags();
        }

        private void OnRegionsCollapsed(object sender, RegionsCollapsedEventArgs e)
        {
            var renderingMode = General.Instance.CommentRenderingMode;
            if (renderingMode != RenderingMode.Compact && renderingMode != RenderingMode.Full)
            {
                return;
            }

            // When outlining region is collapsed, re-show rendered adornment
            var snapshot = view.TextBuffer.CurrentSnapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);
            if (commentStyle == null)
            {
                return;
            }

            var parser = new XmlDocCommentParser(commentStyle);
            var blocks = parser.FindAllCommentBlocks(snapshot);

            foreach (var region in e.CollapsedRegions)
            {
                var regionSpan = region.Extent.GetSpan(snapshot);
                foreach (var block in blocks)
                {
                    var blockSpan = new SnapshotSpan(snapshot, block.Span);
                    if (regionSpan.IntersectsWith(blockSpan))
                    {
                        _temporarilyHiddenComments.Remove(block.StartLine);
                    }
                }
            }

            DeferredRefreshTags();
        }

        private void DeferredRefreshTags()
        {
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

        private void OnZoomLevelChanged(object sender, ZoomLevelChangedEventArgs e)
        {
            // Refresh all adornments when zoom changes so font size updates
            RefreshTags();
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
            if (General.Instance.CommentRenderingMode != RenderingMode.Full)
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
            if (e.Key == Key.Escape && General.Instance.CommentRenderingMode == RenderingMode.Full)
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
                        var blocksToReEnable = new List<XmlDocCommentBlock>();
                        foreach (var hiddenLine in _temporarilyHiddenComments.ToList())
                        {
                            var block = blocks.FirstOrDefault(b => b.StartLine == hiddenLine);
                            if (block != null)
                            {
                                // Check if caret is outside this comment's range
                                if (currentLine < block.StartLine || currentLine > block.EndLine)
                                {
                                    blocksToReEnable.Add(block);
                                    _temporarilyHiddenComments.Remove(hiddenLine);
                                }
                            }
                        }

                        // Re-enable rendering for comments the caret moved away from
                        if (blocksToReEnable.Count > 0)
                        {
                            // Collapse any expanded outlining regions for these comments
                            foreach (var block in blocksToReEnable)
                            {
                                CollapseOutliningRegion(block, snapshot);
                            }

                            DeferredRefreshTags();
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
            var renderingMode = General.Instance.CommentRenderingMode;

            // Only provide adornments in Compact or Full mode
            if (renderingMode != RenderingMode.Compact && renderingMode != RenderingMode.Full)
            {
                yield break;
            }

            if (spans.Count == 0)
            {
                yield break;
            }

            ITextSnapshot snapshot = spans[0].Snapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);

            if (commentStyle == null)
            {
                yield break;
            }

            var parser = new XmlDocCommentParser(commentStyle);
            IReadOnlyList<XmlDocCommentBlock> commentBlocks = parser.FindAllCommentBlocks(snapshot);

            foreach (XmlDocCommentBlock block in commentBlocks)
            {
                // Skip temporarily hidden comments (user pressed ESC to edit)
                if (_temporarilyHiddenComments.Contains(block.StartLine))
                {
                    continue;
                }

                var blockSpan = new SnapshotSpan(snapshot, block.Span);

                if (!spans.IntersectsWith(new NormalizedSnapshotSpanCollection(blockSpan)))
                {
                    continue;
                }

                // The adornment replaces the entire comment block span
                yield return Tuple.Create(blockSpan, (PositionAffinity?)PositionAffinity.Predecessor, block);
            }
        }

        protected override FrameworkElement CreateAdornment(XmlDocCommentBlock block, SnapshotSpan span)
        {
            var renderingMode = General.Instance.CommentRenderingMode;

            // Get editor font settings - use 1pt smaller than editor font
            var editorFontSize = view.FormattedLineSource?.DefaultTextProperties?.FontRenderingEmSize ?? 13.0;
            var fontSize = Math.Max(editorFontSize - 1.0, 8.0); // At least 8pt
            var fontFamily = view.FormattedLineSource?.DefaultTextProperties?.Typeface?.FontFamily
                ?? new FontFamily("Consolas");

            // Gray color for subtle appearance
            var textBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));
            var headingBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));

            // Calculate the pixel width for the indentation margin
            var indentMargin = CalculateIndentationWidth(block.Indentation, fontFamily, editorFontSize);

            if (renderingMode == RenderingMode.Full)
            {
                return CreateFullModeAdornment(block, fontSize, fontFamily, textBrush, headingBrush, indentMargin);
            }
            else
            {
                return CreateCompactModeAdornment(block, fontSize, fontFamily, textBrush, indentMargin);
            }
        }

        /// <summary>
        /// Calculates the pixel width of the indentation string using the editor font.
        /// </summary>
        private double CalculateIndentationWidth(string indentation, FontFamily fontFamily, double fontSize)
        {
            if (string.IsNullOrEmpty(indentation))
            {
                return 0;
            }

            // Use a FormattedText to measure the width of the indentation
            var formattedText = new FormattedText(
                indentation,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(view.VisualElement).PixelsPerDip);

            return formattedText.WidthIncludingTrailingWhitespace;
        }

        private FrameworkElement CreateCompactModeAdornment(XmlDocCommentBlock block, double fontSize,
            FontFamily fontFamily, Brush textBrush, double indentMargin)
        {
            // Compact: single line with stripped summary, truncated at 100 chars
            var strippedSummary = XmlDocCommentRenderer.GetStrippedSummary(block);
            if (string.IsNullOrWhiteSpace(strippedSummary))
            {
                strippedSummary = "...";
            }
            else if (strippedSummary.Length > 100)
            {
                strippedSummary = strippedSummary.Substring(0, 97) + "...";
            }

            var textBlock = new TextBlock
            {
                Text = strippedSummary,
                FontFamily = fontFamily,
                FontSize = fontSize,
                Foreground = textBrush,
                Background = Brushes.Transparent,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(indentMargin, 0, 0, 0),
                ToolTip = CreateTooltip(block),
                Cursor = Cursors.Hand
            };

            // Double-click to switch to raw source mode for editing
            AttachDoubleClickHandler(textBlock, block);

            return textBlock;
        }

        private FrameworkElement CreateFullModeAdornment(XmlDocCommentBlock block, double fontSize,
            FontFamily fontFamily, Brush textBrush, Brush headingBrush, double indentMargin)
        {
            RenderedComment rendered = XmlDocCommentRenderer.Render(block);

            // If only summary, use compact display
            if (!rendered.HasAdditionalSections)
            {
                return CreateCompactModeAdornment(block, fontSize, fontFamily, textBrush, indentMargin);
            }

            // Calculate line height for spacing
            var lineHeight = fontSize * 1.4;

            // Full mode: show all sections with improved formatting and whitespace
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Background = Brushes.Transparent,
                Margin = new Thickness(indentMargin, 0, 0, 0)
            };

            // Summary lines (semibold for emphasis), word wrapped at 100 chars
            var summary = XmlDocCommentRenderer.GetStrippedSummary(block);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                var wrappedLines = WordWrap(summary, 100);
                for (int i = 0; i < wrappedLines.Count; i++)
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = wrappedLines[i],
                        FontFamily = fontFamily,
                        FontSize = fontSize,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = textBrush,
                        TextWrapping = TextWrapping.NoWrap,
                        Margin = new Thickness(0, 0, 0, i == wrappedLines.Count - 1 ? lineHeight * 0.3 : 0)
                    });
                }
            }

            // Group params and type params
            var paramSections = rendered.AdditionalSections
                .Where(s => s.Type == CommentSectionType.Param)
                .ToList();
            var typeParamSections = rendered.AdditionalSections
                .Where(s => s.Type == CommentSectionType.TypeParam)
                .ToList();
            var otherSections = rendered.AdditionalSections
                .Where(s => s.Type != CommentSectionType.Param && s.Type != CommentSectionType.TypeParam)
                .ToList();

            // Type parameters (if any)
            if (typeParamSections.Count > 0)
            {
                foreach (RenderedCommentSection section in typeParamSections)
                {
                    AddParameterLine(panel, section, fontSize, fontFamily, textBrush, headingBrush, lineHeight);
                }
                // Add spacing after type params group if there are more sections
                if (paramSections.Count > 0 || otherSections.Count > 0)
                {
                    panel.Children.Add(CreateSpacer(lineHeight * 0.2));
                }
            }

            // Parameters (if any)
            if (paramSections.Count > 0)
            {
                foreach (RenderedCommentSection section in paramSections)
                {
                    AddParameterLine(panel, section, fontSize, fontFamily, textBrush, headingBrush, lineHeight);
                }
                // Add spacing after params group if there are more sections
                if (otherSections.Count > 0)
                {
                    panel.Children.Add(CreateSpacer(lineHeight * 0.2));
                }
            }

            // Other sections (Returns, Exceptions, Remarks, etc.)
            for (int i = 0; i < otherSections.Count; i++)
            {
                AddSectionLine(panel, otherSections[i], fontSize, fontFamily, textBrush, headingBrush, lineHeight);
            }

            // Double-click anywhere on the panel to switch to raw source mode for editing
            panel.Cursor = Cursors.Hand;
            AttachDoubleClickHandler(panel, block);

            return panel;
        }

        /// <summary>
        /// Word wraps text at the specified maximum line length.
        /// </summary>
        private static List<string> WordWrap(string text, int maxLineLength)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return lines;
            }

            var words = text.Split(' ');
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                if (currentLine.Length == 0)
                {
                    currentLine.Append(word);
                }
                else if (currentLine.Length + 1 + word.Length <= maxLineLength)
                {
                    currentLine.Append(' ').Append(word);
                }
                else
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                    currentLine.Append(word);
                }
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
            }

            return lines;
        }

        private static FrameworkElement CreateSpacer(double height)
        {
            return new Border { Height = height, Background = Brushes.Transparent };
        }

        private static void AddParameterLine(StackPanel panel, RenderedCommentSection section,
            double fontSize, FontFamily fontFamily, Brush textBrush, Brush headingBrush, double lineHeight)
        {
            var content = GetSectionContent(section);
            var name = section.Name ?? "";
            var prefix = "• " + name + " — ";
            var fullText = prefix + content;

            // Word wrap at 100 chars
            var wrappedLines = WordWrap(fullText, 100);
            
            for (int i = 0; i < wrappedLines.Count; i++)
            {
                var textBlock = new TextBlock
                {
                    FontFamily = fontFamily,
                    FontSize = fontSize,
                    Foreground = textBrush,
                    TextWrapping = TextWrapping.NoWrap,
                    Margin = new Thickness(i > 0 ? fontSize * 1.5 : 0, 0, 0, i == wrappedLines.Count - 1 ? lineHeight * 0.1 : 0)
                };

                var lineText = wrappedLines[i];
                
                if (i == 0)
                {
                    // First line has bullet, name (bold), and start of content
                    textBlock.Inlines.Add(new Run("• ") { Foreground = textBrush });
                    textBlock.Inlines.Add(new Run(name)
                    {
                        Foreground = headingBrush,
                        FontWeight = FontWeights.SemiBold
                    });
                    // Get the content part after the prefix
                    var contentStart = prefix.Length;
                    if (lineText.Length > contentStart)
                    {
                        textBlock.Inlines.Add(new Run(" — " + lineText.Substring(contentStart)) { Foreground = textBrush });
                    }
                    else if (content.Length > 0)
                    {
                        textBlock.Inlines.Add(new Run(" — ") { Foreground = textBrush });
                    }
                }
                else
                {
                    // Continuation lines are plain text
                    textBlock.Text = lineText;
                }

                panel.Children.Add(textBlock);
            }
        }

        private static void AddSectionLine(StackPanel panel, RenderedCommentSection section,
            double fontSize, FontFamily fontFamily, Brush textBrush, Brush headingBrush, double lineHeight)
        {
            var content = GetSectionContent(section);
            var heading = GetSectionHeading(section);
            var fullText = heading + " " + content;

            // Word wrap at 100 chars
            var wrappedLines = WordWrap(fullText, 100);

            for (int i = 0; i < wrappedLines.Count; i++)
            {
                var textBlock = new TextBlock
                {
                    FontFamily = fontFamily,
                    FontSize = fontSize,
                    Foreground = textBrush,
                    TextWrapping = TextWrapping.NoWrap,
                    Margin = new Thickness(i > 0 ? fontSize * 1.5 : 0, 0, 0, i == wrappedLines.Count - 1 ? lineHeight * 0.1 : 0)
                };

                var lineText = wrappedLines[i];

                if (i == 0 && !string.IsNullOrEmpty(heading))
                {
                    // First line has heading (bold) and start of content
                    textBlock.Inlines.Add(new Run(heading)
                    {
                        Foreground = headingBrush,
                        FontWeight = FontWeights.SemiBold
                    });
                    var contentStart = heading.Length + 1; // +1 for space
                    if (lineText.Length > contentStart)
                    {
                        textBlock.Inlines.Add(new Run(" " + lineText.Substring(contentStart)) { Foreground = textBrush });
                    }
                }
                else
                {
                    // Continuation lines or lines without heading
                    textBlock.Text = lineText;
                }

                panel.Children.Add(textBlock);
            }
        }

        private static string GetSectionHeading(RenderedCommentSection section)
        {
            return section.Type switch
            {
                CommentSectionType.Returns => "Returns:",
                CommentSectionType.Exception => $"Throws {section.Name}:",
                CommentSectionType.Remarks => "Remarks:",
                CommentSectionType.Example => "Example:",
                CommentSectionType.Value => "Value:",
                CommentSectionType.SeeAlso => "See also:",
                _ => ""
            };
        }

        private static string GetSectionContent(RenderedCommentSection section)
        {
            return string.Join(" ", section.Lines
                .Where(l => !l.IsBlank)
                .SelectMany(l => l.Segments)
                .Select(s => s.Text));
        }

        private static object CreateTooltip(XmlDocCommentBlock block)
        {
            // Show the raw XML content as tooltip
            return block.XmlContent;
        }

        protected override bool UpdateAdornment(FrameworkElement adornment, XmlDocCommentBlock data)
        {
            // Always recreate to pick up font size changes
            return false;
        }

        /// <summary>
        /// Attaches a double-click handler to switch the comment into raw source mode for editing.
        /// </summary>
        private void AttachDoubleClickHandler(FrameworkElement element, XmlDocCommentBlock block)
        {
            element.MouseLeftButtonDown += (sender, e) =>
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    SwitchToRawSourceMode(block);
                }
            };
        }

        /// <summary>
        /// Switches a specific comment to raw source mode and positions the caret at the start.
        /// </summary>
        private void SwitchToRawSourceMode(XmlDocCommentBlock block)
        {
            // Hide the rendered adornment
            HideCommentRendering(block.StartLine);

            // Position the caret at the start of the comment block
            var snapshot = view.TextBuffer.CurrentSnapshot;
            var startLine = snapshot.GetLineFromLineNumber(block.StartLine);
            var caretPosition = new SnapshotPoint(snapshot, startLine.Start.Position + block.Indentation.Length);

            // Expand any collapsed outlining regions that contain this comment
            ExpandOutliningRegion(block, snapshot);

            // Defer caret positioning to ensure the adornment is removed first
#pragma warning disable VSTHRD001, VSTHRD110 // Intentional fire-and-forget for UI update
            view.VisualElement.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!view.IsClosed)
                {
                    view.Caret.MoveTo(caretPosition);
                    view.ViewScroller.EnsureSpanVisible(new SnapshotSpan(caretPosition, 0));
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001, VSTHRD110
        }

        /// <summary>
        /// Expands any collapsed outlining regions that contain the comment block.
        /// </summary>
        private void ExpandOutliningRegion(XmlDocCommentBlock block, ITextSnapshot snapshot)
        {
            if (_outliningManager == null)
            {
                return;
            }

            try
            {
                var blockSpan = new SnapshotSpan(snapshot, block.Span);
                var collapsedRegions = _outliningManager.GetCollapsedRegions(blockSpan);

                foreach (var region in collapsedRegions)
                {
                    _outliningManager.Expand(region);
                }
            }
            catch
            {
                // Ignore errors when expanding regions
            }
        }

        /// <summary>
        /// Collapses any expanded outlining regions that correspond to the comment block.
        /// </summary>
        private void CollapseOutliningRegion(XmlDocCommentBlock block, ITextSnapshot snapshot)
        {
            if (_outliningManager == null)
            {
                return;
            }

            try
            {
                var blockSpan = new SnapshotSpan(snapshot, block.Span);
                var allRegions = _outliningManager.GetAllRegions(blockSpan);

                foreach (var region in allRegions)
                {
                    // Only collapse regions that match the comment block (not parent regions like class/method)
                    var regionSpan = region.Extent.GetSpan(snapshot);
                    
                    // Check if this region approximately matches the comment block
                    // (allowing for slight differences in span boundaries)
                    var regionStartLine = snapshot.GetLineNumberFromPosition(regionSpan.Start);
                    var regionEndLine = snapshot.GetLineNumberFromPosition(regionSpan.End);
                    
                    if (regionStartLine == block.StartLine && regionEndLine == block.EndLine)
                    {
                        if (!region.IsCollapsed)
                        {
                            _outliningManager.TryCollapse(region);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors when collapsing regions
            }
        }

        private void HideCommentRendering(int startLine)
        {
            _temporarilyHiddenComments.Add(startLine);
            RefreshTags();
        }

        private void RefreshTags()
        {
            var snapshot = view.TextBuffer.CurrentSnapshot;
            RaiseTagsChanged(new SnapshotSpan(snapshot, 0, snapshot.Length));
        }
    }
}
