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
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Adornments
{
    /// <summary>
    /// Simple command implementation for keyboard bindings
    /// </summary>
    internal class DelegateCommand(Action execute) : ICommand
    {
        private readonly Action _execute = execute ?? throw new ArgumentNullException(nameof(execute));

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
            if (textView == null || textView is not IWpfTextView wpfTextView)
                return null;

            if (textView.TextBuffer != buffer)
                return null;

            return wpfTextView.Properties.GetOrCreateSingletonProperty(
                () => new RenderedCommentIntraTextTagger(wpfTextView)) as ITagger<T>;
        }
    }

    internal sealed class RenderedCommentIntraTextTagger : IntraTextAdornmentTagger<XmlDocCommentBlock, FrameworkElement>
    {
        private readonly HashSet<int> _temporarilyHiddenComments = [];
        private readonly HashSet<int> _recentlyEditedLines = [];
        private int? _lastCaretLine;

        public RenderedCommentIntraTextTagger(IWpfTextView view) : base(view)
        {
            SetRenderingModeHelper.RenderedCommentsStateChanged += OnRenderedStateChanged;
            view.Caret.PositionChanged += OnCaretPositionChanged;
            view.TextBuffer.Changed += OnBufferChanged;

            // Listen for zoom level changes to refresh adornments with new font size
            view.ZoomLevelChanged += OnZoomLevelChanged;

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

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            // Track which lines were edited so we can suppress rendering for new/modified comments
            foreach (ITextChange change in e.Changes)
            {
                // Get the line numbers affected by this change
                int startLine = e.After.GetLineFromPosition(change.NewPosition).LineNumber;
                int endLine = e.After.GetLineFromPosition(change.NewEnd).LineNumber;

                for (int line = startLine; line <= endLine; line++)
                {
                    _recentlyEditedLines.Add(line);
                }
            }
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
            ITextSnapshot snapshot = view.TextBuffer.CurrentSnapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);

            if (commentStyle != null)
            {
                var parser = new XmlDocCommentParser(commentStyle);
                IReadOnlyList<XmlDocCommentBlock> blocks = parser.FindAllCommentBlocks(snapshot);

                foreach (XmlDocCommentBlock block in blocks)
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
                ITextSnapshot snapshot = view.TextBuffer.CurrentSnapshot;
                var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);

                if (commentStyle != null)
                {
                    var parser = new XmlDocCommentParser(commentStyle);
                    IReadOnlyList<XmlDocCommentBlock> blocks = parser.FindAllCommentBlocks(snapshot);

                    // Find if caret is within any rendered comment
                    foreach (XmlDocCommentBlock block in blocks)
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

            // If caret moved to a different line, check if we need to update rendering
            if (_lastCaretLine.HasValue && _lastCaretLine.Value != currentLine)
            {
                var shouldRefresh = false;

                ITextSnapshot snapshot = view.TextBuffer.CurrentSnapshot;
                var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);

                if (commentStyle != null)
                {
                    var parser = new XmlDocCommentParser(commentStyle);
                    IReadOnlyList<XmlDocCommentBlock> blocks = parser.FindAllCommentBlocks(snapshot);

                    // Check if we moved away from any temporarily hidden comments (ESC key)
                    foreach (var hiddenLine in _temporarilyHiddenComments.ToList())
                    {
                        XmlDocCommentBlock block = blocks.FirstOrDefault(b => b.StartLine == hiddenLine);
                        if (block != null)
                        {
                            // Check if caret is outside this comment's range
                            if (currentLine < block.StartLine || currentLine > block.EndLine)
                            {
                                _temporarilyHiddenComments.Remove(hiddenLine);
                                shouldRefresh = true;
                            }
                        }
                    }

                    // Check if we moved away from a recently edited comment - clear edit tracking
                    if (_recentlyEditedLines.Count > 0)
                    {
                        // Find which comment block (if any) we moved away from
                        foreach (XmlDocCommentBlock block in blocks)
                        {
                            bool wasInComment = _lastCaretLine.Value >= block.StartLine && _lastCaretLine.Value <= block.EndLine;
                            bool nowInComment = currentLine >= block.StartLine && currentLine <= block.EndLine;

                            // If we moved out of a comment, clear the edit tracking for those lines
                            if (wasInComment && !nowInComment)
                            {
                                for (int line = block.StartLine; line <= block.EndLine; line++)
                                {
                                    _recentlyEditedLines.Remove(line);
                                }
                                shouldRefresh = true;
                            }
                        }
                    }
                }

                if (shouldRefresh)
                {
                    DeferredRefreshTags();
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
                    ITextSnapshot snapshot = view.TextBuffer.CurrentSnapshot;
                    RaiseTagsChanged(new SnapshotSpan(snapshot, 0, snapshot.Length));
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001, VSTHRD110
        }

        protected override IEnumerable<Tuple<SnapshotSpan, PositionAffinity?, XmlDocCommentBlock>> GetAdornmentData(
            NormalizedSnapshotSpanCollection spans)
        {
            RenderingMode renderingMode = General.Instance.CommentRenderingMode;

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

            // Get current caret line
            var caretLine = view.Caret.Position.BufferPosition.GetContainingLine().LineNumber;

            var parser = new XmlDocCommentParser(commentStyle);
            IReadOnlyList<XmlDocCommentBlock> commentBlocks = parser.FindAllCommentBlocks(snapshot);

            foreach (XmlDocCommentBlock block in commentBlocks)
            {
                // Skip temporarily hidden comments (user pressed ESC to edit)
                if (_temporarilyHiddenComments.Contains(block.StartLine))
                {
                    continue;
                }

                // Skip comments that are being actively edited:
                // - Caret is inside the comment AND
                // - The comment was recently modified (user is typing, not just navigating)
                bool caretInComment = caretLine >= block.StartLine && caretLine <= block.EndLine;
                bool commentWasEdited = false;
                for (int line = block.StartLine; line <= block.EndLine; line++)
                {
                    if (_recentlyEditedLines.Contains(line))
                    {
                        commentWasEdited = true;
                        break;
                    }
                }

                if (caretInComment && commentWasEdited)
                {
                    continue;
                }

                // Adjust span to start after indentation so the adornment
                // appears at the same column as the comment prefix (///)
                var adjustedStart = block.Span.Start + block.Indentation.Length;
                var adjustedSpan = new Microsoft.VisualStudio.Text.Span(adjustedStart, block.Span.End - adjustedStart);
                var blockSpan = new SnapshotSpan(snapshot, adjustedSpan);

                if (!spans.IntersectsWith(new NormalizedSnapshotSpanCollection(blockSpan)))
                {
                    continue;
                }

                // The adornment replaces the comment block (excluding leading indentation)
                yield return Tuple.Create(blockSpan, (PositionAffinity?)PositionAffinity.Predecessor, block);
            }
        }

        protected override FrameworkElement CreateAdornment(XmlDocCommentBlock block, SnapshotSpan span)
        {
            RenderingMode renderingMode = General.Instance.CommentRenderingMode;

            // Get editor font settings - use 1pt smaller than editor font
            var editorFontSize = view.FormattedLineSource?.DefaultTextProperties?.FontRenderingEmSize ?? 13.0;
            var fontSize = Math.Max(editorFontSize - 1.0, 8.0); // At least 8pt
            FontFamily fontFamily = view.FormattedLineSource?.DefaultTextProperties?.Typeface?.FontFamily
                ?? new FontFamily("Consolas");

            // Gray color for subtle appearance
            var textBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));
            var headingBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));

            if (renderingMode == RenderingMode.Full)
            {
                return CreateFullModeAdornment(block, fontSize, fontFamily, textBrush, headingBrush);
            }
            else
            {
                return CreateCompactModeAdornment(block, fontSize, fontFamily, textBrush);
            }
        }


        private FrameworkElement CreateCompactModeAdornment(XmlDocCommentBlock block, double fontSize,
            FontFamily fontFamily, Brush textBrush)
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
                FontFamily = fontFamily,
                FontSize = fontSize,
                Foreground = textBrush,
                Background = Brushes.Transparent,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Top,
                // Small top padding to align baseline with code below
                Padding = new Thickness(0, 1, 0, 0),
                ToolTip = CreateTooltip(block),
                Cursor = Cursors.Hand
            };

            // Process markdown in the summary text and create formatted inlines
            var segments = XmlDocCommentRenderer.ProcessMarkdownInText(strippedSummary);
            var headingBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));

            foreach (var segment in segments)
            {
                var inline = CreateInlineForSegment(segment, textBrush, headingBrush, fontFamily);
                textBlock.Inlines.Add(inline);
            }

            // Double-click to switch to raw source mode for editing
            AttachDoubleClickHandler(textBlock, block);

            return textBlock;
        }




        private FrameworkElement CreateFullModeAdornment(XmlDocCommentBlock block, double fontSize,
            FontFamily fontFamily, Brush textBrush, Brush headingBrush)
        {
            RenderedComment rendered = XmlDocCommentRenderer.Render(block);

            // If only summary with no list content, use compact display
            RenderedCommentSection summarySection = rendered.Summary;
            var summaryHasListContent = summarySection != null && summarySection.ListContentStartIndex >= 0;

            if (!rendered.HasAdditionalSections && !summaryHasListContent)
            {
                return CreateCompactModeAdornment(block, fontSize, fontFamily, textBrush);
            }

            // Calculate spacing based on font size for consistent visual rhythm
            var lineHeight = fontSize * 1.35;
            var sectionSpacing = lineHeight * 0.6;  // Space between major sections
            var itemSpacing = lineHeight * 0.15;    // Space between items in same group
            var listIndent = fontSize * 1.2;        // Indent for list items

            // Full mode: show all sections with improved formatting and whitespace
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Background = Brushes.Transparent,
                // Small top margin to align with code below
                Margin = new Thickness(0, 1, 0, 0)
            };

            // Render summary section with full content (including lists)
            if (summarySection != null && !summarySection.IsEmpty)
            {
                RenderSectionContent(panel, summarySection, fontSize, fontFamily, textBrush, headingBrush,
                    listIndent, itemSpacing, isSummary: true);

                // Add spacing after summary if there are more sections
                if (rendered.HasAdditionalSections)
                {
                    panel.Children.Add(CreateSpacer(sectionSpacing));
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
                for (var i = 0; i < typeParamSections.Count; i++)
                {
                    AddParameterLine(panel, typeParamSections[i], fontSize, fontFamily, textBrush, headingBrush,
                        listIndent, itemSpacing, isLast: i == typeParamSections.Count - 1);
                }
                // Add spacing after type params group if there are more sections
                if (paramSections.Count > 0 || otherSections.Count > 0)
                {
                    panel.Children.Add(CreateSpacer(sectionSpacing));
                }
            }

            // Parameters (if any)
            if (paramSections.Count > 0)
            {
                for (var i = 0; i < paramSections.Count; i++)
                {
                    AddParameterLine(panel, paramSections[i], fontSize, fontFamily, textBrush, headingBrush,
                        listIndent, itemSpacing, isLast: i == paramSections.Count - 1);
                }
                // Add spacing after params group if there are more sections
                if (otherSections.Count > 0)
                {
                    panel.Children.Add(CreateSpacer(sectionSpacing));
                }
            }

            // Other sections (Returns, Exceptions, Remarks, etc.)
            for (var i = 0; i < otherSections.Count; i++)
            {
                AddSectionLine(panel, otherSections[i], fontSize, fontFamily, textBrush, headingBrush,
                    listIndent, itemSpacing);

                // Add spacing between other sections
                if (i < otherSections.Count - 1)
                {
                    panel.Children.Add(CreateSpacer(sectionSpacing));
                }
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

        /// <summary>
        /// Renders all content from a section, including prose text and list items.
        /// </summary>
        private static void RenderSectionContent(StackPanel panel, RenderedCommentSection section,
            double fontSize, FontFamily fontFamily, Brush textBrush, Brush headingBrush,
            double listIndent, double itemSpacing, bool isSummary)
        {
            var isFirstLine = true;
            var previousWasListItem = false;

            foreach (RenderedLine line in section.Lines)
            {
                if (line.IsBlank)
                {
                    // Render blank lines as spacers for consistent spacing
                    if (!isFirstLine) // Skip leading blank lines
                    {
                        panel.Children.Add(CreateSpacer(itemSpacing * 1.5));
                    }
                    continue;
                }

                // Build the text content from segments
                var lineText = string.Join("", line.Segments.Select(s => s.Text));

                // Check if this is a list item (starts with bullet or number)
                var isListItem = lineText.TrimStart().StartsWith("•") ||
                                  (lineText.TrimStart().Length > 0 && char.IsDigit(lineText.TrimStart()[0]) && lineText.Contains(". "));

                // Check if line has special formatting segments
                var hasFormattedSegments = line.Segments.Any(s =>
                    s.Type == RenderedSegmentType.Bold ||
                    s.Type == RenderedSegmentType.Italic ||
                    s.Type == RenderedSegmentType.Code ||
                    s.Type == RenderedSegmentType.Strikethrough ||
                    s.Type == RenderedSegmentType.Link ||
                    s.Type == RenderedSegmentType.ParamRef ||
                    s.Type == RenderedSegmentType.TypeParamRef);

                if (hasFormattedSegments)
                {
                    // Render with formatting
                    var textBlock = new TextBlock
                    {
                        FontFamily = fontFamily,
                        FontSize = fontSize,
                        Foreground = textBrush,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 800,
                        Margin = new Thickness(
                            isListItem ? listIndent * 0.3 : 0,
                            0, 0,
                            itemSpacing)
                    };

                    // For summary, make the first non-list line bold
                    if (isSummary && !isListItem && isFirstLine)
                    {
                        textBlock.FontWeight = FontWeights.SemiBold;
                    }

                    foreach (RenderedSegment segment in line.Segments)
                    {
                        var inline = CreateInlineForSegment(segment, textBrush, headingBrush, fontFamily);
                        textBlock.Inlines.Add(inline);
                    }

                    panel.Children.Add(textBlock);
                }
                else
                {
                    // Word wrap the line at 100 chars
                    List<string> wrappedLines = WordWrap(lineText, 100);

                    for (var i = 0; i < wrappedLines.Count; i++)
                    {
                        var textBlock = new TextBlock
                        {
                            FontFamily = fontFamily,
                            FontSize = fontSize,
                            Foreground = textBrush,
                            TextWrapping = TextWrapping.NoWrap,
                            // Use consistent indentation: list items get full indent, continuation lines get more
                            Margin = new Thickness(
                                i > 0 ? listIndent * 1.5 : (isListItem ? listIndent * 0.3 : 0),
                                0, 0,
                                itemSpacing)
                        };

                        var wrappedText = wrappedLines[i];

                        // For summary, make the first non-list line bold
                        if (isSummary && i == 0 && !isListItem && isFirstLine)
                        {
                            textBlock.FontWeight = FontWeights.SemiBold;
                        }

                        textBlock.Text = wrappedText;
                        panel.Children.Add(textBlock);
                    }
                }

                isFirstLine = false;
                previousWasListItem = isListItem;
            }
        }

        /// <summary>
        /// Creates an Inline element for a rendered segment with appropriate formatting.
        /// Returns a Hyperlink for links, Run for other types.
        /// </summary>
        private static Inline CreateInlineForSegment(RenderedSegment segment, Brush textBrush, Brush headingBrush, FontFamily fontFamily)
        {
            switch (segment.Type)
            {
                case RenderedSegmentType.Link:
                    var hyperlink = new Hyperlink(new Run(segment.Text))
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(86, 156, 214)), // Blue link color
                        TextDecorations = TextDecorations.Underline
                    };

                    // Make the link clickable if we have a URL
                    if (!string.IsNullOrEmpty(segment.LinkTarget))
                    {
                        hyperlink.NavigateUri = CreateUri(segment.LinkTarget);
                        hyperlink.RequestNavigate += (sender, e) =>
                        {
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = e.Uri.AbsoluteUri,
                                    UseShellExecute = true
                                });
                            }
                            catch
                            {
                                // Ignore failures to open URL
                            }
                            e.Handled = true;
                        };
                    }
                    return hyperlink;

                default:
                    var run = new Run(segment.Text) { Foreground = textBrush };

                    switch (segment.Type)
                    {
                        case RenderedSegmentType.Bold:
                            run.FontWeight = FontWeights.Bold;
                            break;

                        case RenderedSegmentType.Italic:
                            run.FontStyle = FontStyles.Italic;
                            break;

                        case RenderedSegmentType.Code:
                            run.FontFamily = new FontFamily("Consolas");
                            run.Foreground = new SolidColorBrush(Color.FromRgb(156, 120, 100)); // Brownish code color
                            break;

                        case RenderedSegmentType.Strikethrough:
                            run.TextDecorations = TextDecorations.Strikethrough;
                            break;

                        case RenderedSegmentType.ParamRef:
                        case RenderedSegmentType.TypeParamRef:
                            run.FontStyle = FontStyles.Italic;
                            run.Foreground = new SolidColorBrush(Color.FromRgb(86, 156, 214)); // Blue for refs
                            break;

                        case RenderedSegmentType.Heading:
                            run.FontWeight = FontWeights.SemiBold;
                            run.Foreground = headingBrush;
                            break;
                    }

                    return run;
            }
        }

        /// <summary>
        /// Creates a URI from a link target string, handling relative and absolute URLs.
        /// </summary>
        private static Uri CreateUri(string linkTarget)
        {
            if (string.IsNullOrEmpty(linkTarget))
                return null;

            // Try to create URI directly
            if (Uri.TryCreate(linkTarget, UriKind.Absolute, out var uri))
                return uri;

            // If it looks like a URL but failed, try adding https://
            if (linkTarget.Contains(".") && !linkTarget.Contains(" "))
            {
                if (Uri.TryCreate("https://" + linkTarget, UriKind.Absolute, out uri))
                    return uri;
            }

            return null;
        }

        private static FrameworkElement CreateSpacer(double height)
        {
            return new Border { Height = height, Background = Brushes.Transparent };
        }

        private static void AddParameterLine(StackPanel panel, RenderedCommentSection section,
            double fontSize, FontFamily fontFamily, Brush textBrush, Brush headingBrush,
            double listIndent, double itemSpacing, bool isLast)
        {
            var content = GetSectionContent(section);
            var name = section.Name ?? "";
            var prefix = "• " + name + " — ";
            var fullText = prefix + content;

            // Word wrap at 100 chars
            List<string> wrappedLines = WordWrap(fullText, 100);

            for (var i = 0; i < wrappedLines.Count; i++)
            {
                var textBlock = new TextBlock
                {
                    FontFamily = fontFamily,
                    FontSize = fontSize,
                    Foreground = textBrush,
                    TextWrapping = TextWrapping.NoWrap,
                    Margin = new Thickness(
                        i > 0 ? listIndent : 0,
                        0,
                        0,
                        i == wrappedLines.Count - 1 ? (isLast ? 0 : itemSpacing) : 0)
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
            double fontSize, FontFamily fontFamily, Brush textBrush, Brush headingBrush,
            double listIndent, double itemSpacing)
        {
            var heading = GetSectionHeading(section);

            // Check if section contains code blocks (needs special handling to preserve formatting)
            var hasCodeBlock = section.Lines.Any(l => l.Segments.Any(s => s.Type == RenderedSegmentType.Code));

            if (hasCodeBlock)
            {
                // For sections with code blocks, render heading separately then preserve line structure
                if (!string.IsNullOrEmpty(heading))
                {
                    var headingBlock = new TextBlock
                    {
                        FontFamily = fontFamily,
                        FontSize = fontSize,
                        Foreground = headingBrush,
                        FontWeight = FontWeights.SemiBold,
                        TextWrapping = TextWrapping.NoWrap,
                        Margin = new Thickness(0, 0, 0, itemSpacing),
                        Text = heading
                    };
                    panel.Children.Add(headingBlock);
                }

                // Render each line preserving structure
                foreach (RenderedLine line in section.Lines)
                {
                    if (line.IsBlank)
                    {
                        panel.Children.Add(CreateSpacer(itemSpacing));
                        continue;
                    }

                    var textBlock = new TextBlock
                    {
                        FontFamily = fontFamily,
                        FontSize = fontSize,
                        Foreground = textBrush,
                        TextWrapping = TextWrapping.NoWrap,
                        Margin = new Thickness(0, 0, 0, 2)
                    };

                    // Check if this line is code
                    var isCodeLine = line.Segments.Any(s => s.Type == RenderedSegmentType.Code);
                    if (isCodeLine)
                    {
                        // Use monospace font for code
                        textBlock.FontFamily = new FontFamily("Consolas");
                        textBlock.Margin = new Thickness(listIndent, 0, 0, 2);
                    }

                    foreach (RenderedSegment segment in line.Segments)
                    {
                        var inline = CreateInlineForSegment(segment, textBrush, headingBrush, fontFamily);
                        textBlock.Inlines.Add(inline);
                    }

                    panel.Children.Add(textBlock);
                }
            }
            else
            {
                // For simple sections without code, use the original flattened approach
                var content = GetSectionContent(section);
                var fullText = heading + " " + content;

                // Word wrap at 100 chars
                List<string> wrappedLines = WordWrap(fullText, 100);

                for (var i = 0; i < wrappedLines.Count; i++)
                {
                    var textBlock = new TextBlock
                    {
                        FontFamily = fontFamily,
                        FontSize = fontSize,
                        Foreground = textBrush,
                        TextWrapping = TextWrapping.NoWrap,
                        Margin = new Thickness(
                            i > 0 ? listIndent : 0,
                            0,
                            0,
                            i == wrappedLines.Count - 1 ? itemSpacing * 0.5 : 0)
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
            // Create a WPF-based tooltip with VS theme colors
            var lines = block.XmlContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            var panel = new StackPanel();

            foreach (var line in lines)
            {
                var textBlock = new TextBlock
                {
                    Text = line,
                    TextWrapping = TextWrapping.NoWrap
                };
                textBlock.SetResourceReference(TextBlock.ForegroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolTipTextBrushKey);
                panel.Children.Add(textBlock);
            }

            var tooltip = new ToolTip
            {
                Content = panel
            };
            tooltip.SetResourceReference(Control.BackgroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolTipBrushKey);
            tooltip.SetResourceReference(Control.BorderBrushProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolTipBorderBrushKey);

            return tooltip;
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
            ITextSnapshot snapshot = view.TextBuffer.CurrentSnapshot;
            ITextSnapshotLine startLine = snapshot.GetLineFromLineNumber(block.StartLine);
            var caretPosition = new SnapshotPoint(snapshot, startLine.Start.Position + block.Indentation.Length);

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


        private void HideCommentRendering(int startLine)
        {
            _temporarilyHiddenComments.Add(startLine);
            RefreshTags();
        }

        private void RefreshTags()
        {
            ITextSnapshot snapshot = view.TextBuffer.CurrentSnapshot;
            RaiseTagsChanged(new SnapshotSpan(snapshot, 0, snapshot.Length));
        }
    }
}
