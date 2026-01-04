using System;
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
        private static readonly Regex CSharpDocCommentRegex = new Regex(@"^\s*///", RegexOptions.Compiled);
        private static readonly Regex VBDocCommentRegex = new Regex(@"^\s*'''", RegexOptions.Compiled);

        private readonly IWpfTextView _textView;
        private readonly IAdornmentLayer _adornmentLayer;
        private readonly List<CommentRenderInfo> _renderInfos = new List<CommentRenderInfo>();
        private readonly Dictionary<int, bool> _expandedComments = new Dictionary<int, bool>(); // Key: StartLine
        private bool _disposed;
        private bool _wasLayouted;

        public RenderedCommentLineTransformSource(IWpfTextView textView)
        {
            _textView = textView;
            _adornmentLayer = textView.GetAdornmentLayer("RenderedCommentAdornment");

            _textView.LayoutChanged += OnLayoutChanged;
            _textView.Caret.PositionChanged += OnCaretPositionChanged;
            _textView.Closed += OnViewClosed;

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
            var regex = contentType == "Basic" ? VBDocCommentRegex : CSharpDocCommentRegex;

            if (!regex.IsMatch(lineText))
            {
                return new LineTransform(1.0);
            }

            int lineNumber = line.Start.GetContainingLine().LineNumber;
            var renderInfo = FindRenderInfoForLine(lineNumber);

            if (renderInfo != null && !renderInfo.ContainsCaret)
            {
                // Scale this line based on the render info
                return new LineTransform(renderInfo.VerticalScale);
            }

            return new LineTransform(1.0);
        }

        private CommentRenderInfo FindRenderInfoForLine(int lineNumber)
        {
            foreach (var info in _renderInfos)
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

            var snapshot = _textView.TextSnapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);
            if (commentStyle == null)
            {
                return;
            }

            var parser = new XmlDocCommentParser(commentStyle);
            var commentBlocks = parser.FindAllCommentBlocks(snapshot);

            foreach (var block in commentBlocks)
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

            _wasLayouted = true;
        }

        private void CreateRenderInfoAndAdornment(XmlDocCommentBlock block, ITextSnapshot snapshot)
        {
            var startLine = snapshot.GetLineFromLineNumber(block.StartLine);
            var endLine = snapshot.GetLineFromLineNumber(block.EndLine);

            // Check if caret is in this comment
            var caretLine = _textView.Caret.Position.BufferPosition.GetContainingLine().LineNumber;
            bool containsCaret = caretLine >= block.StartLine && caretLine <= block.EndLine;

            // Calculate heights
            double lineHeight = _textView.FormattedLineSource?.LineHeight ?? 15;
            double columnWidth = _textView.FormattedLineSource?.ColumnWidth ?? 8;
            int numLines = block.EndLine - block.StartLine + 1;
            double originalHeight = numLines * lineHeight;

            // Render the comment
            var renderedComment = XmlDocCommentRenderer.Render(block);


            // Check if this comment has expandable content (sections beyond summary)
            bool hasExpandableContent = renderedComment.HasAdditionalSections;

            // Get expanded state from dictionary, default to collapsed if has expandable content
            bool isExpanded = false;
            if (_expandedComments.TryGetValue(block.StartLine, out bool savedState))
            {
                isExpanded = savedState;
            }

            // Calculate the width for wrapping (100 chars total, accounting for indentation)
            int indentChars = block.Indentation?.Length ?? 0;
            int maxChars = 100 - indentChars;
            double maxTextWidth = maxChars * columnWidth;

            // Calculate rendered height based on actual content
            double renderedHeight = CalculateRenderedHeight(renderedComment, isExpanded, hasExpandableContent, 
                maxChars, lineHeight);



            // Calculate scale to fit rendered content in original space
            double scale = containsCaret ? 1.0 : renderedHeight / originalHeight;
            scale = Math.Max(0.1, scale); // Don't scale below 10%

            var renderInfo = new CommentRenderInfo
            {
                StartLine = block.StartLine,
                EndLine = block.EndLine,
                VerticalScale = scale,
                ContainsCaret = containsCaret,
                Block = block,
                RenderedComment = renderedComment,
                RenderedHeight = renderedHeight,
                IsExpanded = isExpanded,
                HasExpandableContent = hasExpandableContent
            };
            _renderInfos.Add(renderInfo);

            // Don't show adornment if caret is in comment
            if (containsCaret)
            {
                return;
            }

            // Create and position the adornment
            var span = new SnapshotSpan(startLine.Start, endLine.End);
            
            // Get the geometry for the entire comment block
            // This reflects the actual space after line transforms are applied
            var geometry = _textView.TextViewLines.GetMarkerGeometry(span);
            if (geometry == null)
            {
                return;
            }

            var bounds = geometry.Bounds;
            
            // The adornment height should match the geometry bounds exactly
            // The line transform has already allocated the correct space
            var adornmentBounds = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            
            // Calculate the left position - should be at the start of line (where indentation begins)
            var firstLineStart = _textView.TextViewLines.GetCharacterBounds(startLine.Start);
            double leftPos = firstLineStart.Left;
            
            var adornment = CreateVisualElement(renderInfo, block.Indentation, adornmentBounds, maxTextWidth);
            
            Canvas.SetLeft(adornment, leftPos);
            Canvas.SetTop(adornment, bounds.Top);

            _adornmentLayer.AddAdornment(
                AdornmentPositioningBehavior.TextRelative,
                span,
                null,
                adornment,
                null);
        }

        private UIElement CreateVisualElement(CommentRenderInfo renderInfo, string indentation, Rect bounds, double maxTextWidth)
        {
            var comment = renderInfo.RenderedComment;
            bool isExpanded = renderInfo.IsExpanded;
            bool hasExpandableContent = renderInfo.HasExpandableContent;

            // Width should cover from text start to end of viewport
            double width = _textView.ViewportWidth;
            
            double fontSize = _textView.FormattedLineSource?.DefaultTextProperties?.FontRenderingEmSize ?? 13;
            var fontFamily = _textView.FormattedLineSource?.DefaultTextProperties?.Typeface?.FontFamily 
                ?? new FontFamily("Consolas");
            double lineHeight = _textView.FormattedLineSource?.LineHeight ?? 15;
            double columnWidth = _textView.FormattedLineSource?.ColumnWidth ?? 8;

            var textBrush = GetCommentTextBrush();
            var linkBrush = GetLinkBrush();
            var headingBrush = GetHeadingBrush();
            var bgBrush = GetBackgroundBrush();

            // Use the bounds height - this is the space allocated by line transforms
            double gridHeight = bounds.Height;

            // Calculate positions
            double indentWidth = (indentation?.Length ?? 0) * columnWidth;
            // Background should start at indentation to cover "/// " prefix
            // This hides the raw comment syntax while keeping indent guides visible
            double bgStartX = indentWidth;

            // Create outer container - transparent, full width
            var outerGrid = new Grid
            {
                Width = width,
                Height = gridHeight,
                Background = Brushes.Transparent, // Keep indent guides visible
                ClipToBounds = true
            };

            // Create inner panel with background that starts at indentation (covers /// prefix)
            var contentPanel = new Border
            {
                Background = bgBrush,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(bgStartX, 0, 0, 0)
            };

            // Create the main content panel
            var mainPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 0)
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
            outerGrid.Children.Add(contentPanel);

            return outerGrid;
        }

        private void RenderCollapsedView(StackPanel mainPanel, RenderedComment comment, double maxTextWidth,
            double fontSize, FontFamily fontFamily, Brush textBrush, Brush linkBrush, Brush headingBrush, int startLine)
        {
            var summaryPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Summary text
            var summaryText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = textBrush,
                FontFamily = fontFamily,
                FontSize = fontSize,
                MaxWidth = maxTextWidth - 30, // Leave room for expander
                VerticalAlignment = VerticalAlignment.Top
            };

            var summary = comment.Summary;
            if (summary != null)
            {
                RenderSectionContent(summaryText, summary.Lines, textBrush, linkBrush, headingBrush);
            }

            summaryPanel.Children.Add(summaryText);

            // Add expander button
            var expander = CreateExpanderButton(false, startLine, linkBrush, fontFamily, fontSize);
            summaryPanel.Children.Add(expander);

            mainPanel.Children.Add(summaryPanel);
        }

        private void RenderAllSections(StackPanel mainPanel, RenderedComment comment, double maxTextWidth,
            double fontSize, FontFamily fontFamily, double lineHeight, Brush textBrush, Brush linkBrush, 
            Brush headingBrush, bool hasExpandableContent, int startLine)
        {
            bool isFirstSection = true;

            foreach (var section in comment.Sections)
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
                    var summaryWithExpander = new StackPanel { Orientation = Orientation.Horizontal };
                    
                    var summaryText = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = textBrush,
                        FontFamily = fontFamily,
                        FontSize = fontSize,
                        MaxWidth = maxTextWidth - 25,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    RenderSectionContent(summaryText, section.Lines, textBrush, linkBrush, headingBrush);
                    summaryWithExpander.Children.Add(summaryText);

                    var collapser = CreateExpanderButton(true, startLine, linkBrush, fontFamily, fontSize);
                    summaryWithExpander.Children.Add(collapser);

                    mainPanel.Children.Add(summaryWithExpander);
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
                    var sectionText = CreateInlineSectionBlock(section, fontSize, fontFamily, 
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
            var (labelText, labelBrush) = GetSectionLabelInfo(section, textBrush, linkBrush);
            
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

            switch (section.Type)
            {
                case CommentSectionType.Param:
                    return ($"{section.Name}: ", paramBrush);
                    
                case CommentSectionType.TypeParam:
                    return ($"〈{section.Name}〉 ", paramBrush);  // Angle brackets for type params
                    
                case CommentSectionType.Returns:
                    return ("↩ ", returnsBrush);  // Clean return arrow
                    
                case CommentSectionType.Exception:
                    return ($"⚠ {section.Name}: ", exceptionBrush);  // Warning for exceptions
                    
                case CommentSectionType.Remarks:
                    return ("✎ ", textBrush);  // Pencil for remarks
                    
                case CommentSectionType.Example:
                    return ("» ", textBrush);  // Chevron for examples
                    
                case CommentSectionType.Value:
                    return ("= ", textBrush);  // Equals for value
                    
                case CommentSectionType.SeeAlso:
                    return ("→ ", linkBrush);  // Arrow for see also
                    
                default:
                    return (null, textBrush);
            }
        }

        private TextBlock CreateSectionLabel(RenderedCommentSection section, double fontSize, 
            FontFamily fontFamily, Brush headingBrush, Brush linkBrush)
        {
            // Legacy method - kept for compatibility but redesigned
            var (labelText, labelBrush) = GetSectionLabelInfo(section, headingBrush, linkBrush);
            
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

        private Border CreateExpanderButton(bool isExpanded, int startLine, Brush linkBrush, 
            FontFamily fontFamily, double fontSize)
        {
            // Clean, subtle expander - just a small indicator
            var expanderText = new TextBlock
            {
                Text = isExpanded ? "▴" : "▾",  // Smaller, cleaner triangles
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)), // Subtle gray
                FontFamily = fontFamily,
                FontSize = fontSize * 0.85,  // Slightly smaller
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = isExpanded 
                    ? "Collapse details" 
                    : "Show parameters, returns, exceptions"
            };

            expanderText.Tag = startLine;
            expanderText.MouseLeftButtonDown += OnExpanderClicked;

            // Wrap in a border for better hit testing
            return new Border
            {
                Child = expanderText,
                Background = Brushes.Transparent,
                Padding = new Thickness(2, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
        }

        private void RenderSectionContent(TextBlock textBlock, IEnumerable<RenderedLine> lines,
            Brush textBrush, Brush linkBrush, Brush headingBrush)
        {
            bool firstSegment = true;
            foreach (var line in lines)
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

                foreach (var segment in line.Segments)
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
                var summary = comment.Summary;
                if (summary != null)
                {
                    int summaryChars = GetSectionCharCount(summary.Lines);
                    // Account for expander button taking ~5 chars
                    int effectiveMaxChars = maxCharsPerLine - 5;
                    int summaryLines = Math.Max(1, (int)Math.Ceiling((double)summaryChars / effectiveMaxChars));
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
                bool isFirstSection = true;
                foreach (var section in comment.Sections)
                {
                    if (section.IsEmpty)
                        continue;

                    // Add spacing between sections
                    if (!isFirstSection)
                    {
                        totalHeight += lineHeight * 0.25;
                    }

                    // Calculate content height - label is inline now, not a separate line
                    int contentChars = GetSectionCharCount(section.Lines);
                    
                    // Add label chars for non-summary sections
                    if (section.Type != CommentSectionType.Summary)
                    {
                        int labelChars = GetLabelCharCount(section);
                        contentChars += labelChars;
                    }
                    
                    int effectiveMaxChars = section.Type == CommentSectionType.Summary && hasExpandableContent
                        ? maxCharsPerLine - 5  // Account for expander
                        : maxCharsPerLine;
                    int contentLines = Math.Max(1, (int)Math.Ceiling((double)contentChars / effectiveMaxChars));
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
            switch (section.Type)
            {
                case CommentSectionType.Param:
                    return (section.Name?.Length ?? 0) + 2; // "Name: "
                case CommentSectionType.TypeParam:
                    return (section.Name?.Length ?? 0) + 4; // "〈Name〉 "
                case CommentSectionType.Returns:
                    return 2; // "↩ "
                case CommentSectionType.Exception:
                    return 4 + (section.Name?.Length ?? 0); // "⚠ Name: "
                case CommentSectionType.Remarks:
                    return 2; // "✎ "
                case CommentSectionType.Example:
                    return 2; // "» "
                case CommentSectionType.Value:
                    return 2; // "= "
                case CommentSectionType.SeeAlso:
                    return 2; // "→ "
                default:
                    return 0;
            }
        }

        private int GetSectionCharCount(IEnumerable<RenderedLine> lines)
        {
            int totalChars = 0;
            foreach (var line in lines)
            {
                if (!line.IsBlank)
                {
                    foreach (var segment in line.Segments)
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
            if (sender is TextBlock textBlock && textBlock.Tag is int startLine)
            {
                // Toggle expanded state
                if (_expandedComments.TryGetValue(startLine, out bool currentState))
                {
                    _expandedComments[startLine] = !currentState;
                }
                else
                {
                    _expandedComments[startLine] = true; // Default was collapsed, now expand
                }

                // Force a re-layout to update the display
                ForceRelayout();
                e.Handled = true;
            }
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

            // Check if caret moved into or out of a comment - if so, trigger re-layout
            var oldLine = e.OldPosition.BufferPosition.GetContainingLine().LineNumber;
            var newLine = e.NewPosition.BufferPosition.GetContainingLine().LineNumber;

            bool wasInComment = IsLineInComment(oldLine);
            bool isInComment = IsLineInComment(newLine);

            if (wasInComment != isInComment || (wasInComment && oldLine != newLine))
            {
                // Force a re-layout by scrolling a tiny amount
                _textView.ViewScroller.ScrollViewportVerticallyByPixels(0.001);
                _textView.ViewScroller.ScrollViewportVerticallyByPixels(-0.001);
            }
        }

        private bool IsLineInComment(int lineNumber)
        {
            foreach (var info in _renderInfos)
            {
                if (lineNumber >= info.StartLine && lineNumber <= info.EndLine)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnRenderedStateChanged(object sender, EventArgs e)
        {
            // Force a re-layout
            if (!_disposed && _textView is { IsClosed: false, InLayout: false })
            {
                var firstLine = _textView.TextViewLines?.FirstVisibleLine;
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
            var background = _textView.Background;
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _textView.LayoutChanged -= OnLayoutChanged;
            _textView.Caret.PositionChanged -= OnCaretPositionChanged;
            _textView.Closed -= OnViewClosed;
            ToggleRenderedCommentsCommand.RenderedCommentsStateChanged -= OnRenderedStateChanged;
        }

        private class CommentRenderInfo
        {
            public int StartLine { get; set; }
            public int EndLine { get; set; }
            public double VerticalScale { get; set; }
            public bool ContainsCaret { get; set; }
            public XmlDocCommentBlock Block { get; set; }
            public RenderedComment RenderedComment { get; set; }
            public double RenderedHeight { get; set; }
            public bool IsExpanded { get; set; }
            public bool HasExpandableContent { get; set; }
        }
    }
}
