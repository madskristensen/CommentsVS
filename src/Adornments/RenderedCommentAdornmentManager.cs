using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Adornments
{
/// <summary>
/// Provides the adornment layer for rendered comments and creates the adornment manager.
/// </summary>
[Export(typeof(IWpfTextViewCreationListener))]
[ContentType("CSharp")]
[ContentType("Basic")]
[TextViewRole(PredefinedTextViewRoles.Document)]
internal sealed class RenderedCommentAdornmentManagerProvider : IWpfTextViewCreationListener
{
    [Export(typeof(AdornmentLayerDefinition))]
    [Name("RenderedCommentAdornment")]
    [Order(After = PredefinedAdornmentLayers.Text, Before = PredefinedAdornmentLayers.Caret)]
    internal AdornmentLayerDefinition EditorAdornmentLayer = null;

    [Import]
        internal IOutliningManagerService OutliningManagerService { get; set; }

        [Import]
        internal IViewTagAggregatorFactoryService TagAggregatorFactoryService { get; set; }

        public void TextViewCreated(IWpfTextView textView)
        {
            // Create the adornment manager for this view
            textView.Properties.GetOrCreateSingletonProperty(
                () => new RenderedCommentAdornmentManager(
                    textView, 
                    OutliningManagerService,
                    TagAggregatorFactoryService));
        }
    }

    /// <summary>
    /// Manages rendered comment adornments that overlay collapsed outlining regions.
    /// </summary>
    internal sealed class RenderedCommentAdornmentManager : IDisposable
    {
        private readonly IWpfTextView _textView;
        private readonly IAdornmentLayer _adornmentLayer;
        private readonly IOutliningManager _outliningManager;
        private readonly ITagAggregator<IOutliningRegionTag> _outliningTagAggregator;
        private readonly HashSet<int> _expandedComments = new HashSet<int>(); // Track expanded state by start line
        private bool _disposed;

        public RenderedCommentAdornmentManager(
            IWpfTextView textView,
            IOutliningManagerService outliningManagerService,
            IViewTagAggregatorFactoryService tagAggregatorFactoryService)
        {
            _textView = textView;
            _adornmentLayer = textView.GetAdornmentLayer("RenderedCommentAdornment");
            _outliningManager = outliningManagerService?.GetOutliningManager(textView);
            _outliningTagAggregator = tagAggregatorFactoryService?.CreateTagAggregator<IOutliningRegionTag>(textView);

            _textView.LayoutChanged += OnLayoutChanged;
            _textView.Closed += OnViewClosed;

            if (_outliningManager != null)
            {
                _outliningManager.RegionsCollapsed += OnRegionsCollapsed;
                _outliningManager.RegionsExpanded += OnRegionsExpanded;
            }

            ToggleRenderedCommentsCommand.RenderedCommentsStateChanged += OnRenderedStateChanged;
        }

        private void OnRegionsCollapsed(object sender, RegionsCollapsedEventArgs e)
        {
            DeferredUpdateAdornments();
        }

        private void OnRegionsExpanded(object sender, RegionsExpandedEventArgs e)
        {
            DeferredUpdateAdornments();
        }

        private void OnRenderedStateChanged(object sender, EventArgs e)
        {
            DeferredUpdateAdornments();
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            DeferredUpdateAdornments();
        }

        private void DeferredUpdateAdornments()
        {
            // Defer to avoid layout exceptions when called during layout
#pragma warning disable VSTHRD001, VSTHRD110 // Intentional fire-and-forget for UI update
            _textView.VisualElement.Dispatcher.BeginInvoke(
                new Action(UpdateAdornments),
                System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001, VSTHRD110
        }

        private void UpdateAdornments()
        {
            if (_disposed || _textView.IsClosed)
                return;

            _adornmentLayer.RemoveAllAdornments();

            if (!General.Instance.EnableRenderedComments || _outliningManager == null)
                return;

            ITextSnapshot snapshot = _textView.TextSnapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);
            if (commentStyle == null)
                return;

            var parser = new XmlDocCommentParser(commentStyle);
            IReadOnlyList<XmlDocCommentBlock> commentBlocks = parser.FindAllCommentBlocks(snapshot);

            foreach (XmlDocCommentBlock block in commentBlocks)
            {
                try
                {
                    // Check if this region is collapsed
                    var adjustedStart = block.Span.Start + block.Indentation.Length;
                    var adjustedSpan = new SnapshotSpan(snapshot, adjustedStart, block.Span.End - adjustedStart);

                    ICollapsible collapsible = FindCollapsedRegion(adjustedSpan);
                    if (collapsible == null || !collapsible.IsCollapsed)
                        continue;


                    // Get the visual position of the collapsed region
                    ITextViewLine line = _textView.TextViewLines.GetTextViewLineContainingBufferPosition(adjustedSpan.Start);
                    if (line == null)
                        continue;

                    var lineHeight = line.Height;

                    // Create and position the adornment
                    UIElement adornment = CreateRenderedCommentElement(block, lineHeight);
                    
                    // Position at the start of the collapsed region
                    TextBounds bounds = line.GetCharacterBounds(adjustedSpan.Start);
                    Canvas.SetLeft(adornment, bounds.Left);
                    Canvas.SetTop(adornment, line.Top);

                    _adornmentLayer.AddAdornment(
                        AdornmentPositioningBehavior.TextRelative,
                        adjustedSpan,
                        null,
                        adornment,
                        null);
                }
                catch
                {
                    // Skip blocks that fail
                }
            }
        }

        private ICollapsible FindCollapsedRegion(SnapshotSpan span)
        {
            if (_outliningManager == null)
                return null;

            IEnumerable<ICollapsible> regions = _outliningManager.GetAllRegions(span);
            foreach (ICollapsible region in regions)
            {
                if (region.IsCollapsed)
                {
                    SnapshotSpan regionSpan = region.Extent.GetSpan(span.Snapshot);
                    if (regionSpan.Start == span.Start)
                        return region;
                }
            }
            return null;
        }



        private UIElement CreateRenderedCommentElement(XmlDocCommentBlock block, double lineHeight)
        {
            RenderedComment renderedComment = XmlDocCommentRenderer.Render(block);

            var fontSize = _textView.FormattedLineSource?.DefaultTextProperties?.FontRenderingEmSize ?? 13.0;
            var fontFamily = _textView.FormattedLineSource?.DefaultTextProperties?.Typeface?.FontFamily
                ?? new FontFamily("Consolas");

            // Use gray color to reduce visual noise (similar to collapsed outlining text)
            var textBrush = new SolidColorBrush(Color.FromRgb(155, 155, 155)); // Gray
            var linkBrush = new SolidColorBrush(Color.FromRgb(86, 156, 214)); // VS blue for links
            var paramBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128)); // Darker gray for params
            var returnsBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));
            var exceptionBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));

            // Get background from view
            Brush bgBrush = _textView.Background is SolidColorBrush solidBrush ? solidBrush : Brushes.White;

            // Check if this comment is expanded
            var isExpanded = _expandedComments.Contains(block.StartLine);
            var hasMoreContent = renderedComment.HasAdditionalSections;

            // Create container with explicit height to fully cover the outlining UI including border
            // Add extra pixels to cover the collapsed region border
            var container = new Border
            {
                Background = bgBrush,
                Height = lineHeight + 4, // Extra height to cover the outlining border
                VerticalAlignment = VerticalAlignment.Top,
                ClipToBounds = false
            };

            var innerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = bgBrush,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 0)
            };

            // Main content panel
            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Background = bgBrush
            };

            if (isExpanded && hasMoreContent)
            {
                // Show all sections
                RenderAllSections(contentPanel, renderedComment, fontSize, fontFamily, lineHeight,
                    textBrush, linkBrush, paramBrush, returnsBrush, exceptionBrush, bgBrush);
            }
            else
            {
                // Show only summary
                var summaryBlock = new TextBlock
                {
                    FontFamily = fontFamily,
                    FontSize = fontSize,
                    Foreground = textBrush,
                    Background = bgBrush,
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalAlignment = VerticalAlignment.Center
                };

                RenderedCommentSection summary = renderedComment.Summary;
                if (summary != null)
                {
                    RenderSectionContent(summaryBlock, summary.ProseLines, textBrush, linkBrush, paramBrush);
                }

                contentPanel.Children.Add(summaryBlock);
            }

            innerPanel.Children.Add(contentPanel);

            // Add expand/collapse indicator if there's more content
            if (hasMoreContent)
            {
                var expanderText = isExpanded ? " ▼" : " ▶";
                var expander = new TextBlock
                {
                    Text = expanderText,
                    FontFamily = fontFamily,
                    FontSize = fontSize * 0.85,
                    Foreground = new SolidColorBrush(Color.FromRgb(86, 156, 214)), // Keep blue for visibility
                    Background = bgBrush,
                    VerticalAlignment = VerticalAlignment.Top,
                    Cursor = Cursors.Hand,
                    ToolTip = isExpanded ? "Collapse" : "Expand to show more",
                    Tag = block.StartLine,
                    Margin = new Thickness(2, 0, 0, 0)
                };
                expander.MouseLeftButtonDown += OnExpanderClicked;
                innerPanel.Children.Add(expander);
            }

            // Add padding at the end to fully cover the outlining "[...]" placeholder
            var padding = new Border
            {
                Width = 60, // Extra width to cover any remaining outlining text
                Background = bgBrush
            };
            innerPanel.Children.Add(padding);

            container.Child = innerPanel;
            return container;
        }

        private void RenderAllSections(StackPanel panel, RenderedComment comment, double fontSize, 
            FontFamily fontFamily, double lineHeight, Brush textBrush, Brush linkBrush, 
            Brush paramBrush, Brush returnsBrush, Brush exceptionBrush, Brush bgBrush)
        {
            var isFirst = true;
            foreach (RenderedCommentSection section in comment.Sections)
            {
                if (section.IsEmpty)
                    continue;

                // Add spacing between sections
                if (!isFirst)
                {
                    panel.Children.Add(new Border { Height = lineHeight * 0.3, Background = bgBrush });
                }

                var sectionBlock = new TextBlock
                {
                    FontFamily = fontFamily,
                    FontSize = fontSize,
                    Foreground = textBrush,
                    Background = bgBrush,
                    TextWrapping = TextWrapping.NoWrap
                };

                // Add label for non-summary sections
                if (section.Type != CommentSectionType.Summary)
                {
                    var (label, labelBrush) = GetSectionLabel(section, textBrush, paramBrush, returnsBrush, exceptionBrush, linkBrush);
                    if (!string.IsNullOrEmpty(label))
                    {
                        sectionBlock.Inlines.Add(new Run(label) 
                        { 
                            Foreground = labelBrush,
                            FontWeight = FontWeights.SemiBold
                        });
                    }
                }

                RenderSectionContent(sectionBlock, section.Lines, textBrush, linkBrush, paramBrush);
                panel.Children.Add(sectionBlock);
                isFirst = false;
            }
        }

        private (string label, Brush brush) GetSectionLabel(RenderedCommentSection section, 
            Brush textBrush, Brush paramBrush, Brush returnsBrush, Brush exceptionBrush, Brush linkBrush)
        {
            return section.Type switch
            {
                CommentSectionType.Param => ($"{section.Name}: ", paramBrush),
                CommentSectionType.TypeParam => ($"<{section.Name}>: ", paramBrush),
                CommentSectionType.Returns => ("Returns: ", returnsBrush),
                CommentSectionType.Exception => ($"Throws {section.Name}: ", exceptionBrush),
                CommentSectionType.Remarks => ("Remarks: ", textBrush),
                CommentSectionType.Example => ("Example: ", textBrush),
                CommentSectionType.Value => ("Value: ", textBrush),
                CommentSectionType.SeeAlso => ("See: ", linkBrush),
                _ => (null, textBrush),
            };
        }

        private void OnExpanderClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.Tag is int startLine)
            {
                // Toggle expanded state
                if (_expandedComments.Contains(startLine))
                {
                    _expandedComments.Remove(startLine);
                }
                else
                {
                    _expandedComments.Add(startLine);
                }

                // Force refresh
                UpdateAdornments();
                e.Handled = true;
            }
        }

        private void RenderSectionContent(TextBlock textBlock, IEnumerable<RenderedLine> lines, Brush textBrush, Brush linkBrush, Brush paramBrush)
        {
            var firstSegment = true;
            foreach (RenderedLine line in lines)
            {
                if (line.IsBlank)
                    continue;

                if (!firstSegment)
                {
                    textBlock.Inlines.Add(new Run(" ") { Foreground = textBrush });
                }
                firstSegment = false;

                foreach (RenderedSegment segment in line.Segments)
                {
                    textBlock.Inlines.Add(CreateInline(segment, textBrush, linkBrush, paramBrush));
                }
            }
        }

        private Inline CreateInline(RenderedSegment segment, Brush textBrush, Brush linkBrush, Brush paramBrush)
        {
            switch (segment.Type)
            {
                case RenderedSegmentType.Link:
                    return new Run(segment.Text) { Foreground = linkBrush };

                case RenderedSegmentType.ParamRef:
                case RenderedSegmentType.TypeParamRef:
                    return new Run(segment.Text)
                    {
                        Foreground = paramBrush,
                        FontStyle = FontStyles.Italic
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
                    return new Run(segment.Text)
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(214, 157, 133)),
                        FontFamily = new FontFamily("Consolas")
                    };

                default:
                    return new Run(segment.Text) { Foreground = textBrush };
            }
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
            _textView.Closed -= OnViewClosed;

            if (_outliningManager != null)
            {
                _outliningManager.RegionsCollapsed -= OnRegionsCollapsed;
                _outliningManager.RegionsExpanded -= OnRegionsExpanded;
            }

            _outliningTagAggregator?.Dispose();
            ToggleRenderedCommentsCommand.RenderedCommentsStateChanged -= OnRenderedStateChanged;
        }
    }
}
