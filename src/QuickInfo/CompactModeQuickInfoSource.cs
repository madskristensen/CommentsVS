using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.QuickInfo
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name("CompactModeQuickInfo")]
    [ContentType("CSharp")]
    [ContentType("Basic")]
    [Order(Before = "Default Quick Info Presenter")]
    internal sealed class CompactModeQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        [Import]
        internal IOutliningManagerService OutliningManagerService { get; set; }

        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(
                () => new CompactModeQuickInfoSource(textBuffer, OutliningManagerService));
        }
    }

    /// <summary>
    /// Provides hover tooltips for collapsed XML doc comments in Compact mode,
    /// showing the full rendered view.
    /// </summary>
    internal sealed class CompactModeQuickInfoSource : IAsyncQuickInfoSource
    {
        private readonly ITextBuffer _textBuffer;
        private readonly IOutliningManagerService _outliningManagerService;

        public CompactModeQuickInfoSource(ITextBuffer textBuffer, IOutliningManagerService outliningManagerService)
        {
            _textBuffer = textBuffer;
            _outliningManagerService = outliningManagerService;
        }

        public Task<QuickInfoItem> GetQuickInfoItemAsync(
            IAsyncQuickInfoSession session,
            CancellationToken cancellationToken)
        {
            // Show tooltip in Compact mode only (Full mode shows all details inline)
            RenderingMode renderingMode = General.Instance.CommentRenderingMode;
            if (renderingMode != RenderingMode.Compact)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            SnapshotPoint? triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            ITextSnapshot snapshot = triggerPoint.Value.Snapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);
            if (commentStyle == null)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            var parser = new XmlDocCommentParser(commentStyle);
            XmlDocCommentBlock block = parser.FindCommentBlockAtPosition(snapshot, triggerPoint.Value.Position);

            if (block == null)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            // Render the comment in full format
            RenderedComment renderedComment = XmlDocCommentRenderer.Render(block);
            
            // Only show tooltip if there's content not visible in compact inline view:
            // 1. Additional sections beyond summary (params, returns, remarks, etc.)
            // 2. Summary was truncated (>100 chars in compact mode)
            // 3. Summary has list content not shown inline
            bool hasAdditionalSections = renderedComment.HasAdditionalSections;
            
            string strippedSummary = XmlDocCommentRenderer.GetStrippedSummary(block);
            bool summaryTruncated = !string.IsNullOrEmpty(strippedSummary) && strippedSummary.Length > 100;
            
            RenderedCommentSection summarySection = renderedComment.Summary;
            bool summaryHasListContent = summarySection != null && summarySection.ListContentStartIndex >= 0;

            if (!hasAdditionalSections && !summaryTruncated && !summaryHasListContent)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            // Create the tooltip content
            FrameworkElement tooltipContent = CreateFullRenderingTooltip(renderedComment);
            
            if (tooltipContent == null)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            ITrackingSpan trackingSpan = snapshot.CreateTrackingSpan(block.Span, SpanTrackingMode.EdgeInclusive);
            return Task.FromResult(new QuickInfoItem(trackingSpan, tooltipContent));
        }

        /// <summary>
        /// Creates a formatted tooltip showing the full rendered view of the comment.
        /// </summary>
        private FrameworkElement CreateFullRenderingTooltip(RenderedComment renderedComment)
        {
            if (renderedComment?.Sections == null || renderedComment.Sections.Count == 0)
            {
                return null;
            }

            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                MaxWidth = 600
            };

            // Color scheme matching Visual Studio tooltips
            var textBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));
            var headingBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));
            var linkBrush = new SolidColorBrush(Color.FromRgb(86, 156, 214));
            var codeBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
            var paramBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));

            var isFirst = true;
            foreach (RenderedCommentSection section in renderedComment.Sections)
            {
                if (section.IsEmpty)
                {
                    continue;
                }

                if (!isFirst)
                {
                    panel.Children.Add(new TextBlock { Height = 8 }); // Spacing between sections
                }
                isFirst = false;

                // Add section heading (except for summary)
                if (!string.IsNullOrEmpty(section.Heading) && section.Type != CommentSectionType.Summary)
                {
                    var headingBlock = new TextBlock
                    {
                        FontWeight = FontWeights.Bold,
                        Foreground = headingBrush,
                        Margin = new Thickness(0, 0, 0, 4),
                        TextWrapping = TextWrapping.Wrap
                    };
                    headingBlock.Inlines.Add(new Run(section.Heading));
                    panel.Children.Add(headingBlock);
                }

                // Add section content
                foreach (RenderedLine line in section.Lines)
                {
                    if (line.IsBlank)
                    {
                        panel.Children.Add(new TextBlock { Height = 4 }); // Blank line spacing
                        continue;
                    }

                    var textBlock = new TextBlock
                    {
                        Foreground = textBrush,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 2)
                    };

                    foreach (RenderedSegment segment in line.Segments)
                    {
                        textBlock.Inlines.Add(CreateInline(segment, textBrush, linkBrush, codeBrush, paramBrush));
                    }

                    panel.Children.Add(textBlock);
                }
            }

            return panel;
        }

        private Inline CreateInline(RenderedSegment segment, Brush defaultBrush, Brush linkBrush,
            Brush codeBrush, Brush paramBrush)
        {
            return segment.Type switch
            {
                RenderedSegmentType.Heading => new Run(segment.Text)
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = defaultBrush
                },
                RenderedSegmentType.Link => new Run(segment.Text)
                {
                    Foreground = linkBrush,
                    TextDecorations = TextDecorations.Underline
                },
                RenderedSegmentType.ParamRef or RenderedSegmentType.TypeParamRef =>
                    new Run(segment.Text)
                    {
                        Foreground = paramBrush,
                        FontStyle = FontStyles.Italic
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
                _ => new Run(segment.Text) { Foreground = defaultBrush }
            };
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}
