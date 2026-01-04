using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
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
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Adornments
{
    /// <summary>
    /// Provides line transforms to shrink comment lines when rendered view is enabled.
    /// </summary>
    [Export(typeof(ILineTransformSourceProvider))]
    [ContentType("CSharp")]
    [ContentType("Basic")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [Name("RenderedCommentLineTransformSourceProvider")]
    internal sealed class RenderedCommentLineTransformSourceProvider : ILineTransformSourceProvider
    {
        [Export(typeof(AdornmentLayerDefinition))]
        [Name("RenderedCommentAdornment")]
        [Order(After = PredefinedAdornmentLayers.Caret)]
        internal AdornmentLayerDefinition EditorAdornmentLayer = null;

        public ILineTransformSource Create(IWpfTextView view)
        {
            return view.Properties.GetOrCreateSingletonProperty(
                () => new RenderedCommentLineTransformSource(view));
        }
    }

    /// <summary>
    /// Line transform source that shrinks comment lines when rendered view is enabled.
    /// </summary>
    internal sealed class RenderedCommentLineTransformSource : ILineTransformSource, IDisposable
    {
        private static readonly Regex _cSharpDocCommentRegex = new(@"^\s*///", RegexOptions.Compiled);
        private static readonly Regex _vBDocCommentRegex = new(@"^\s*'''", RegexOptions.Compiled);

        private readonly IWpfTextView _textView;
        private readonly IAdornmentLayer _adornmentLayer;
        private readonly List<CommentRenderInfo> _renderInfos = [];
        private readonly Dictionary<int, bool> _expandedComments = []; // Key: StartLine
        private readonly HashSet<int> _editModeComments = []; // Comments currently in edit mode (showing raw text)
        private bool _disposed;

        public RenderedCommentLineTransformSource(IWpfTextView textView)
        {
            _textView = textView;
            _adornmentLayer = textView.GetAdornmentLayer("RenderedCommentAdornment");


            _textView.LayoutChanged += OnLayoutChanged;
            _textView.Caret.PositionChanged += OnCaretPositionChanged;
            _textView.Closed += OnViewClosed;
            _textView.VisualElement.PreviewKeyDown += OnPreviewKeyDown;

            ToggleRenderedCommentsCommand.RenderedCommentsStateChanged += OnRenderedStateChanged;
        }

        public LineTransform GetLineTransform(ITextViewLine line, double yPosition, ViewRelativePosition placement)
        {
            if (!General.Instance.EnableRenderedComments || _disposed)
            {
                return new LineTransform(1.0);
            }

            // Check if this line is part of a doc comment
            var lineText = line.Extent.GetText();
            var contentType = _textView.TextBuffer.ContentType.TypeName;
            Regex regex = contentType == "Basic" ? _vBDocCommentRegex : _cSharpDocCommentRegex;

            if (!regex.IsMatch(lineText))
            {
                return new LineTransform(1.0);
            }

            var lineNumber = line.Start.GetContainingLine().LineNumber;
            CommentRenderInfo renderInfo = FindRenderInfoForLine(lineNumber);

            if (renderInfo != null && !renderInfo.IsInEditMode)
            {
                // Scale this line based on the render info
                return new LineTransform(renderInfo.VerticalScale);
            }

            return new LineTransform(1.0);
        }

        private CommentRenderInfo FindRenderInfoForLine(int lineNumber)
        {
            foreach (CommentRenderInfo info in _renderInfos)
            {
                if (lineNumber >= info.StartLine && lineNumber <= info.EndLine)
                {
                    return info;
                }
            }
            return null;
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (!General.Instance.EnableRenderedComments || _disposed)
            {
                _adornmentLayer?.RemoveAllAdornments();
                return;
            }

            // Clear and rebuild render info
            _renderInfos.Clear();
            _adornmentLayer.RemoveAllAdornments();

            ITextSnapshot snapshot = _textView.TextSnapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);
            if (commentStyle == null)
            {
                return;
            }

            var parser = new XmlDocCommentParser(commentStyle);
            IReadOnlyList<XmlDocCommentBlock> commentBlocks = parser.FindAllCommentBlocks(snapshot);

            foreach (XmlDocCommentBlock block in commentBlocks)
            {
                try
                {
                    CreateRenderInfoAndAdornment(block, snapshot);
                }
                catch
                {
                    // Skip blocks that fail
                }
            }
        }

        private void CreateRenderInfoAndAdornment(XmlDocCommentBlock block, ITextSnapshot snapshot)
        {
            ITextSnapshotLine startLine = snapshot.GetLineFromLineNumber(block.StartLine);
            ITextSnapshotLine endLine = snapshot.GetLineFromLineNumber(block.EndLine);

            // Check if this comment is in edit mode (user explicitly entered it)
            var isInEditMode = _editModeComments.Contains(block.StartLine);

            // Calculate heights
            var lineHeight = _textView.FormattedLineSource?.LineHeight ?? 15;
            var columnWidth = _textView.FormattedLineSource?.ColumnWidth ?? 8;
            var numLines = block.EndLine - block.StartLine + 1;
            var originalHeight = numLines * lineHeight;

            // Render the comment
            RenderedComment renderedComment = XmlDocCommentRenderer.Render(block);


            // Check if this comment has expandable content (sections beyond summary)
            var hasExpandableContent = renderedComment.HasAdditionalSections;

            // Get expanded state from dictionary, default to collapsed if has expandable content
            var isExpanded = false;
            if (_expandedComments.TryGetValue(block.StartLine, out var savedState))
            {
                isExpanded = savedState;
            }

            // Calculate the width for wrapping (100 chars total, accounting for indentation)
            var indentChars = block.Indentation?.Length ?? 0;
            var maxChars = 100 - indentChars;
            var maxTextWidth = maxChars * columnWidth;

            // Calculate rendered height based on actual content
            var renderedHeight = CalculateRenderedHeight(renderedComment, isExpanded, hasExpandableContent,
                maxChars, lineHeight);



            // Calculate scale to fit rendered content in original space
            // In edit mode, show full size (scale = 1.0); otherwise scale to fit rendered content
            var scale = isInEditMode ? 1.0 : renderedHeight / originalHeight;
            scale = Math.Max(0.1, scale); // Don't scale below 10%

            var renderInfo = new CommentRenderInfo
            {
                StartLine = block.StartLine,
                EndLine = block.EndLine,
                VerticalScale = scale,
                IsInEditMode = isInEditMode,
                Block = block,
                RenderedComment = renderedComment,
                RenderedHeight = renderedHeight,
                IsExpanded = isExpanded,
                HasExpandableContent = hasExpandableContent
            };
            _renderInfos.Add(renderInfo);

            // Don't show adornment if in edit mode
            if (isInEditMode)
            {
                return;
            }

            // Create and position the adornment
            var span = new SnapshotSpan(startLine.Start, endLine.End);

            // Get the geometry for the entire comment block
            // This reflects the actual space after line transforms are applied
            Geometry geometry = _textView.TextViewLines.GetMarkerGeometry(span);
            if (geometry == null)
            {
                return;
            }

            Rect bounds = geometry.Bounds;

            // The adornment height should match the geometry bounds exactly
            // The line transform has already allocated the correct space
            var adornmentBounds = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);

            // Calculate the left position for the rendered text
            // We need to find the pixel position where the /// starts
            double bgLeftPos;
            
            // Get the indentation of the comment
            var commentLineText = startLine.GetText();
            var commentIndentLength = commentLineText.Length - commentLineText.TrimStart().Length;
            
            // Find a suitable reference line (not scaled) that's long enough to measure from
            // Try lines after the comment first, then before
            IWpfTextViewLine suitableViewLine = null;
            ITextSnapshotLine suitableRefLine = null;
            
            // Search for a line after the comment that's long enough
            for (int i = block.EndLine + 1; i < Math.Min(block.EndLine + 10, snapshot.LineCount); i++)
            {
                ITextSnapshotLine candidateLine = snapshot.GetLineFromLineNumber(i);
                if (candidateLine.Length >= commentIndentLength)
                {
                    IWpfTextViewLine viewLine = _textView.TextViewLines.GetTextViewLineContainingBufferPosition(candidateLine.Start);
                    if (viewLine != null)
                    {
                        suitableViewLine = viewLine;
                        suitableRefLine = candidateLine;
                        break;
                    }
                }
            }
            
            // If not found, search before the comment
            if (suitableViewLine == null)
            {
                for (int i = block.StartLine - 1; i >= Math.Max(0, block.StartLine - 10); i--)
                {
                    ITextSnapshotLine candidateLine = snapshot.GetLineFromLineNumber(i);
                    if (candidateLine.Length >= commentIndentLength)
                    {
                        IWpfTextViewLine viewLine = _textView.TextViewLines.GetTextViewLineContainingBufferPosition(candidateLine.Start);
                        if (viewLine != null)
                        {
                            suitableViewLine = viewLine;
                            suitableRefLine = candidateLine;
                            break;
                        }
                    }
                }
            }
            
            if (suitableViewLine != null && suitableRefLine != null)
            {
                // Get the character bounds at the comment's indentation position
                var targetPosition = suitableRefLine.Start.Position + commentIndentLength;
                TextBounds charBounds = suitableViewLine.GetCharacterBounds(new SnapshotPoint(snapshot, targetPosition));
                bgLeftPos = charBounds.Left;
            }
            else
            {
                // Fallback: use columnWidth calculation
                bgLeftPos = _textView.ViewportLeft + (commentIndentLength * columnWidth);
            }

            // No padding - rendered text should start at the same position as ///
            UIElement adornment = CreateVisualElement(renderInfo, adornmentBounds, maxTextWidth, 0);


            // Position adornment where /// begins (same indentation as the comment)
            Canvas.SetLeft(adornment, bgLeftPos);
            Canvas.SetTop(adornment, bounds.Top);

            _adornmentLayer.AddAdornment(
                AdornmentPositioningBehavior.TextRelative,
                span,
                null,
                adornment,
                null);
        }

        private UIElement CreateVisualElement(CommentRenderInfo renderInfo, Rect bounds, double maxTextWidth, double textPadding)
        {
            RenderedComment comment = renderInfo.RenderedComment;
            var isExpanded = renderInfo.IsExpanded;
            var hasExpandableContent = renderInfo.HasExpandableContent;

            // Width should cover from background start to end of viewport
            var width = _textView.ViewportWidth;

            var fontSize = _textView.FormattedLineSource?.DefaultTextProperties?.FontRenderingEmSize ?? 13;
            FontFamily fontFamily = _textView.FormattedLineSource?.DefaultTextProperties?.Typeface?.FontFamily
                ?? new FontFamily("Consolas");
            var lineHeight = _textView.FormattedLineSource?.LineHeight ?? 15;

            Brush textBrush = GetCommentTextBrush();
            Brush linkBrush = GetLinkBrush();
            Brush headingBrush = GetHeadingBrush();
            Brush bgBrush = GetBackgroundBrush();

            // Use the bounds height - this is the space allocated by line transforms
            var gridHeight = bounds.Height;

            // Create container with background - positioned at bgLeftPos via Canvas.SetLeft
            var contentPanel = new Border
            {
                Background = bgBrush,
                Width = width,
                Height = gridHeight,
                Padding = new Thickness(textPadding, 0, 0, 0), // Padding to align text after "/// "
                ClipToBounds = true,
                Tag = renderInfo.StartLine
            };

            // Double-click to enter edit mode
            contentPanel.MouseLeftButtonDown += OnAdornmentMouseDown;

            // Create the main content panel
            var mainPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Render based on expanded/collapsed state
            if (isExpanded || !hasExpandableContent)
            {
                // Show all sections with proper formatting
                RenderAllSections(mainPanel, comment, maxTextWidth, fontSize, fontFamily, lineHeight,
                    textBrush, linkBrush, headingBrush, hasExpandableContent, renderInfo.StartLine);
            }
            else
            {
                // Show only summary with expander
                RenderCollapsedView(mainPanel, comment, maxTextWidth, fontSize, fontFamily,
                    textBrush, linkBrush, headingBrush, renderInfo.StartLine);
            }

            contentPanel.Child = mainPanel;

            return contentPanel;
        }


        private void RenderCollapsedView(StackPanel mainPanel, RenderedComment comment, double maxTextWidth,
            double fontSize, FontFamily fontFamily, Brush textBrush, Brush linkBrush, Brush headingBrush, int startLine)
        {
            var containerPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Summary text
            var summaryText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = textBrush,
                FontFamily = fontFamily,
                FontSize = fontSize,
                MaxWidth = maxTextWidth - 20, // Leave room for expander
                VerticalAlignment = VerticalAlignment.Top
            };

            RenderedCommentSection summary = comment.Summary;
            if (summary != null)
            {
                RenderSectionContent(summaryText, summary.ProseLines, textBrush, linkBrush, headingBrush);
            }

            containerPanel.Children.Add(summaryText);

            // Add expander using unicode character
            var expander = new TextBlock
            {
                Text = "▶",  // Right-pointing triangle
                Foreground = linkBrush,
                FontFamily = fontFamily,
                FontSize = fontSize * 0.85,
                Margin = new Thickness(3, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Cursor = Cursors.Hand,
                ToolTip = "Show more",
                Tag = startLine
            };
            expander.MouseLeftButtonDown += OnExpanderClicked;
            containerPanel.Children.Add(expander);

            mainPanel.Children.Add(containerPanel);
        }

        private void RenderAllSections(StackPanel mainPanel, RenderedComment comment, double maxTextWidth,
            double fontSize, FontFamily fontFamily, double lineHeight, Brush textBrush, Brush linkBrush,
            Brush headingBrush, bool hasExpandableContent, int startLine)
        {
            var isFirstSection = true;

            foreach (RenderedCommentSection section in comment.Sections)
            {
                if (section.IsEmpty)
                    continue;

                // Add spacing between sections (but not before the first)
                if (!isFirstSection)
                {
                    mainPanel.Children.Add(new Border { Height = lineHeight * 0.25 });
                }

                // For summary section with expandable content, add the collapse button
                if (section.Type == CommentSectionType.Summary && hasExpandableContent)
                {
                    var containerPanel = new StackPanel { Orientation = Orientation.Horizontal };

                    var summaryText = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = textBrush,
                        FontFamily = fontFamily,
                        FontSize = fontSize,
                        MaxWidth = maxTextWidth - 20, // Leave room for collapser
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    RenderSectionContent(summaryText, section.Lines, textBrush, linkBrush, headingBrush);
                    containerPanel.Children.Add(summaryText);

                    // Add collapser using unicode character
                    var collapser = new TextBlock
                    {
                        Text = "▼",  // Down-pointing triangle
                        Foreground = linkBrush,
                        FontFamily = fontFamily,
                        FontSize = fontSize * 0.85,
                        Margin = new Thickness(3, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Top,
                        Cursor = Cursors.Hand,
                        ToolTip = "Show less",
                        Tag = startLine
                    };
                    collapser.MouseLeftButtonDown += OnExpanderClicked;
                    containerPanel.Children.Add(collapser);

                    mainPanel.Children.Add(containerPanel);
                }
                else if (section.Type == CommentSectionType.Summary)
                {
                    // Summary without expander - clean, no label needed
                    var summaryText = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = textBrush,
                        FontFamily = fontFamily,
                        FontSize = fontSize,
                        MaxWidth = maxTextWidth,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    RenderSectionContent(summaryText, section.Lines, textBrush, linkBrush, headingBrush);
                    mainPanel.Children.Add(summaryText);
                }
                else
                {
                    // Non-summary sections - render inline with label
                    TextBlock sectionText = CreateInlineSectionBlock(section, fontSize, fontFamily,
                        maxTextWidth, textBrush, linkBrush, headingBrush);
                    mainPanel.Children.Add(sectionText);
                }

                isFirstSection = false;
            }
        }

        /// <summary>
        /// Creates an inline section block: "Label: content" all on same line(s), wrapping naturally.
        /// This creates a clean, VS-native look similar to Quick Info tooltips.
        /// </summary>
        private TextBlock CreateInlineSectionBlock(RenderedCommentSection section, double fontSize,
            FontFamily fontFamily, double maxTextWidth, Brush textBrush, Brush linkBrush, Brush headingBrush)
        {
            var textBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = fontFamily,
                FontSize = fontSize,
                MaxWidth = maxTextWidth,
                Foreground = textBrush
            };

            // Add the label inline
            (var labelText, Brush labelBrush) = GetSectionLabelInfo(section, textBrush, linkBrush);

            if (!string.IsNullOrEmpty(labelText))
            {
                textBlock.Inlines.Add(new Run(labelText)
                {
                    Foreground = labelBrush,
                    FontWeight = FontWeights.SemiBold
                });
            }

            // Add the content inline after the label
            RenderSectionContent(textBlock, section.Lines, textBrush, linkBrush, headingBrush);

            return textBlock;
        }

        /// <summary>
        /// Gets clean, VS-native label text for a section.
        /// Design: Tasteful emojis for visual scannability, minimal text labels.
        /// </summary>
        private (string label, Brush brush) GetSectionLabelInfo(RenderedCommentSection section,
            Brush textBrush, Brush linkBrush)
        {
            var paramBrush = new SolidColorBrush(Color.FromRgb(86, 156, 214)); // VS blue for params
            var returnsBrush = new SolidColorBrush(Color.FromRgb(78, 201, 176)); // Teal for returns
            var exceptionBrush = new SolidColorBrush(Color.FromRgb(206, 145, 120)); // Muted orange for exceptions

            return section.Type switch
            {
                CommentSectionType.Param => ((string label, Brush brush))($"{section.Name}: ", paramBrush),
                CommentSectionType.TypeParam => ((string label, Brush brush))($"〈{section.Name}〉 ", paramBrush),// Angle brackets for type params
                CommentSectionType.Returns => ((string label, Brush brush))("↩ ", returnsBrush),// Clean return arrow
                CommentSectionType.Exception => ((string label, Brush brush))($"⚠ {section.Name}: ", exceptionBrush),// Warning for exceptions
                CommentSectionType.Remarks => ("✎ ", textBrush),// Pencil for remarks
                CommentSectionType.Example => ("» ", textBrush),// Chevron for examples
                CommentSectionType.Value => ("= ", textBrush),// Equals for value
                CommentSectionType.SeeAlso => ("→ ", linkBrush),// Arrow for see also
                _ => (null, textBrush),
            };
        }

        private TextBlock CreateSectionLabel(RenderedCommentSection section, double fontSize,
            FontFamily fontFamily, Brush headingBrush, Brush linkBrush)
        {
            // Legacy method - kept for compatibility but redesigned
            (var labelText, Brush labelBrush) = GetSectionLabelInfo(section, headingBrush, linkBrush);

            if (string.IsNullOrEmpty(labelText))
                return null;

            return new TextBlock
            {
                Text = labelText,
                FontFamily = fontFamily,
                FontSize = fontSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = labelBrush
            };
        }

        private void RenderSectionContent(TextBlock textBlock, IEnumerable<RenderedLine> lines,
            Brush textBrush, Brush linkBrush, Brush headingBrush)
        {
            var firstSegment = true;
            foreach (RenderedLine line in lines)
            {
                if (line.IsBlank)
                {
                    continue;
                }

                // Add space between lines (not before first)
                if (!firstSegment)
                {
                    textBlock.Inlines.Add(new Run(" ") { Foreground = textBrush });
                }
                firstSegment = false;

                foreach (RenderedSegment segment in line.Segments)
                {
                    textBlock.Inlines.Add(CreateInline(segment, textBrush, linkBrush, headingBrush));
                }
            }
        }

        private void RenderLinesToTextBlock(TextBlock textBlock, IEnumerable<RenderedLine> lines,
            Brush textBrush, Brush linkBrush, Brush headingBrush)
        {
            RenderSectionContent(textBlock, lines, textBrush, linkBrush, headingBrush);
        }



        private double CalculateRenderedHeight(RenderedComment comment, bool isExpanded,
            bool hasExpandableContent, int maxCharsPerLine, double lineHeight)
        {
            double totalHeight = 0;

            if (!isExpanded && hasExpandableContent)
            {
                // Collapsed: only summary section (single line of wrapped text)
                RenderedCommentSection summary = comment.Summary;
                if (summary != null)
                {
                    var summaryChars = GetSectionCharCount(summary.Lines);
                    // Account for expander button taking ~5 chars
                    var effectiveMaxChars = maxCharsPerLine - 5;
                    var summaryLines = Math.Max(1, (int)Math.Ceiling((double)summaryChars / effectiveMaxChars));
                    totalHeight = summaryLines * lineHeight;
                }
                else
                {
                    totalHeight = lineHeight;
                }
            }
            else
            {
                // Expanded: calculate height for each section
                // New design: inline labels, so content and label share same line(s)
                var isFirstSection = true;
                foreach (RenderedCommentSection section in comment.Sections)
                {
                    if (section.IsEmpty)
                        continue;

                    // Add spacing between sections
                    if (!isFirstSection)
                    {
                        totalHeight += lineHeight * 0.25;
                    }

                    // Calculate content height - label is inline now, not a separate line
                    var contentChars = GetSectionCharCount(section.Lines);

                    // Add label chars for non-summary sections
                    if (section.Type != CommentSectionType.Summary)
                    {
                        var labelChars = GetLabelCharCount(section);
                        contentChars += labelChars;
                    }

                    var effectiveMaxChars = section.Type == CommentSectionType.Summary && hasExpandableContent
                        ? maxCharsPerLine - 5  // Account for expander
                        : maxCharsPerLine;
                    var contentLines = Math.Max(1, (int)Math.Ceiling((double)contentChars / effectiveMaxChars));
                    totalHeight += contentLines * lineHeight;

                    isFirstSection = false;
                }
            }

            // Add a small buffer to ensure content fits
            return Math.Max(lineHeight, totalHeight + lineHeight * 0.15);
        }

        private int GetLabelCharCount(RenderedCommentSection section)
        {
            // Updated to match new emoji-based labels
            return section.Type switch
            {
                CommentSectionType.Param => (section.Name?.Length ?? 0) + 2,// "Name: "
                CommentSectionType.TypeParam => (section.Name?.Length ?? 0) + 4,// "〈Name〉 "
                CommentSectionType.Returns => 2,// "↩ "
                CommentSectionType.Exception => 4 + (section.Name?.Length ?? 0),// "⚠ Name: "
                CommentSectionType.Remarks => 2,// "✎ "
                CommentSectionType.Example => 2,// "» "
                CommentSectionType.Value => 2,// "= "
                CommentSectionType.SeeAlso => 2,// "→ "
                _ => 0,
            };
        }

        private int GetSectionCharCount(IEnumerable<RenderedLine> lines)
        {
            var totalChars = 0;
            foreach (RenderedLine line in lines)
            {
                if (!line.IsBlank)
                {
                    foreach (RenderedSegment segment in line.Segments)
                    {
                        totalChars += segment.Text?.Length ?? 0;
                    }
                    totalChars += 1; // space between lines
                }
            }
            return totalChars;
        }

        private void OnExpanderClicked(object sender, MouseButtonEventArgs e)
        {
            int? startLine = null;
            
            if (sender is Border border && border.Tag is int borderStartLine)
            {
                startLine = borderStartLine;
            }
            else if (sender is TextBlock textBlock && textBlock.Tag is int textBlockStartLine)
            {
                startLine = textBlockStartLine;
            }
            else if (sender is FrameworkElement element && element.Tag is int elementStartLine)
            {
                startLine = elementStartLine;
            }

            if (startLine.HasValue)
            {
                // Toggle expanded state
                if (_expandedComments.TryGetValue(startLine.Value, out var currentState))
                {
                    _expandedComments[startLine.Value] = !currentState;
                }
                else
                {
                    _expandedComments[startLine.Value] = true; // Default was collapsed, now expand
                }

                // Force a re-layout to update the display
                ForceRelayout();
                e.Handled = true;
            }
        }

        private void OnAdornmentMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Only handle double-click to enter edit mode
            if (e.ClickCount == 2 && sender is Border border && border.Tag is int startLine)
            {
                EnterEditMode(startLine);
                e.Handled = true;
            }
            // Single clicks are ignored (no more accidental exits)
        }


        private void EnterEditMode(int startLine)
        {
            if (_disposed || _textView.IsClosed)
            {
                return;
            }

            // Mark this comment as being in edit mode
            _editModeComments.Add(startLine);

            // Move caret to the start of the comment to enter edit mode
            ITextSnapshot snapshot = _textView.TextSnapshot;
            if (startLine >= 0 && startLine < snapshot.LineCount)
            {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(startLine);
                var lineText = line.GetText();

                // Position caret after the /// prefix for convenience
                var caretPosition = line.Start.Position;
                var prefixEnd = lineText.IndexOf("///", StringComparison.Ordinal);
                if (prefixEnd >= 0)
                {
                    caretPosition = line.Start.Position + prefixEnd + 3;
                    // Skip the space after /// if present
                    if (caretPosition < line.End.Position &&
                        snapshot[caretPosition] == ' ')
                    {
                        caretPosition++;
                    }
                }

                _textView.Caret.MoveTo(new SnapshotPoint(snapshot, caretPosition));
                _textView.Caret.EnsureVisible();
            }

            // Force re-layout to show raw text
            ForceRelayout();
        }

        private void ForceRelayout()
        {
            if (!_disposed && _textView is { IsClosed: false, InLayout: false })
            {
                _textView.ViewScroller.ScrollViewportVerticallyByPixels(0.001);
                _textView.ViewScroller.ScrollViewportVerticallyByPixels(-0.001);
            }
        }

        private Inline CreateInline(RenderedSegment segment, Brush textBrush, Brush linkBrush, Brush headingBrush)
        {
            switch (segment.Type)
            {
                case RenderedSegmentType.Link:
                    // Clean link style - just color, no underline (VS-native)
                    return new Run(segment.Text)
                    {
                        Foreground = linkBrush
                    };

                case RenderedSegmentType.ParamRef:
                case RenderedSegmentType.TypeParamRef:
                    // Parameter references - italic, slightly different color
                    return new Run(segment.Text)
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(86, 156, 214)), // VS param blue
                        FontStyle = FontStyles.Italic
                    };

                case RenderedSegmentType.Heading:
                    return new Run(segment.Text)
                    {
                        FontWeight = FontWeights.SemiBold,
                        Foreground = headingBrush
                    };

                case RenderedSegmentType.Bold:
                    return new Run(segment.Text)
                    {
                        FontWeight = FontWeights.SemiBold,
                        Foreground = textBrush
                    };

                case RenderedSegmentType.Italic:
                    return new Run(segment.Text)
                    {
                        FontStyle = FontStyles.Italic,
                        Foreground = textBrush
                    };

                case RenderedSegmentType.Code:
                    // Inline code - subtle, clean
                    return new Run(segment.Text)
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(214, 157, 133)), // Muted string color
                        FontFamily = new FontFamily("Consolas")
                    };

                default:
                    return new Run(segment.Text) { Foreground = textBrush };
            }
        }

        private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            if (!General.Instance.EnableRenderedComments || _disposed)
            {
                return;
            }

            // Check if caret moved out of a comment that was in edit mode
            var newLine = e.NewPosition.BufferPosition.GetContainingLine().LineNumber;

            // Find if caret left any comment that was in edit mode
            var commentsToExitEditMode = new List<int>();
            foreach (var startLine in _editModeComments)
            {
                CommentRenderInfo info = _renderInfos.FirstOrDefault(r => r.StartLine == startLine);
                if (info != null)
                {
                    // If caret is no longer in this comment, exit edit mode
                    if (newLine < info.StartLine || newLine > info.EndLine)
                    {
                        commentsToExitEditMode.Add(startLine);
                    }
                }
            }

            // Exit edit mode for comments the caret left
            if (commentsToExitEditMode.Count > 0)
            {
                foreach (var startLine in commentsToExitEditMode)
                {
                    _editModeComments.Remove(startLine);
                }
                ForceRelayout();
            }
        }

        private bool IsLineInComment(int lineNumber)
        {
            foreach (CommentRenderInfo info in _renderInfos)
            {
                if (lineNumber >= info.StartLine && lineNumber <= info.EndLine)
                {
                    return true;
                }
            }
            return false;
        }

        private CommentRenderInfo FindCommentAtLine(int lineNumber)
        {
            foreach (CommentRenderInfo info in _renderInfos)
            {
                if (lineNumber >= info.StartLine && lineNumber <= info.EndLine)
                {
                    return info;
                }
            }
            return null;
        }

        private void OnRenderedStateChanged(object sender, EventArgs e)
        {
            // Force a re-layout
            if (!_disposed && _textView is { IsClosed: false, InLayout: false })
            {
                IWpfTextViewLine firstLine = _textView.TextViewLines?.FirstVisibleLine;
                if (firstLine != null)
                {
                    _textView.DisplayTextLineContainingBufferPosition(
                        firstLine.Start,
                        firstLine.Top - _textView.ViewportTop,
                        ViewRelativePosition.Top);
                }
            }
        }

        private Brush GetBackgroundBrush()
        {
            Brush background = _textView.Background;
            if (background is SolidColorBrush solidBrush)
            {
                return solidBrush;
            }
            return Brushes.White;
        }

        private Brush GetCommentTextBrush()
        {
            // Nice gray that works in both light and dark themes
            return new SolidColorBrush(Color.FromRgb(128, 128, 128));
        }

        private Brush GetHeadingBrush()
        {
            // Slightly darker/more prominent for headings
            return new SolidColorBrush(Color.FromRgb(100, 100, 100));
        }

        private Brush GetLinkBrush()
        {
            // Muted blue for links
            return new SolidColorBrush(Color.FromRgb(86, 156, 214));
        }

        private Brush GetCodeBrush()
        {
            // Slightly different color for inline code
            return new SolidColorBrush(Color.FromRgb(110, 110, 110));
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            Dispose();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!General.Instance.EnableRenderedComments || _disposed)
            {
                return;
            }

            // Escape key: Enter edit mode for rendered comments
            if (e.Key == Key.Escape)
            {
                var caretLine = _textView.Caret.Position.BufferPosition.GetContainingLine().LineNumber;

                // First, check if caret is inside a rendered comment (not already in edit mode)
                foreach (CommentRenderInfo info in _renderInfos)
                {
                    if (caretLine >= info.StartLine && caretLine <= info.EndLine && !info.IsInEditMode)
                    {
                        EnterEditMode(info.StartLine);
                        e.Handled = true;
                        return;
                    }
                }

                // Check if we're on a line adjacent to a rendered comment
                foreach (CommentRenderInfo info in _renderInfos)
                {
                    // If caret is on line just before comment, move into comment
                    if (caretLine == info.StartLine - 1)
                    {
                        EnterEditMode(info.StartLine);
                        e.Handled = true;
                        return;
                    }
                    // If caret is on line just after comment, move into comment (at end)
                    if (caretLine == info.EndLine + 1)
                    {
                        EnterEditMode(info.EndLine);
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _textView.LayoutChanged -= OnLayoutChanged;
            _textView.Caret.PositionChanged -= OnCaretPositionChanged;
            _textView.Closed -= OnViewClosed;
            _textView.VisualElement.PreviewKeyDown -= OnPreviewKeyDown;
            ToggleRenderedCommentsCommand.RenderedCommentsStateChanged -= OnRenderedStateChanged;
        }

        private class CommentRenderInfo
        {
            public int StartLine { get; set; }
            public int EndLine { get; set; }
            public double VerticalScale { get; set; }
            public bool IsInEditMode { get; set; }
            public XmlDocCommentBlock Block { get; set; }
            public RenderedComment RenderedComment { get; set; }
            public double RenderedHeight { get; set; }
            public bool IsExpanded { get; set; }
            public bool HasExpandableContent { get; set; }
        }
    }
}
