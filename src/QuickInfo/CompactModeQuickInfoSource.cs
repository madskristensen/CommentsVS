using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Utilities;
using static CommentsVS.Services.RenderedSegment;


namespace CommentsVS.QuickInfo
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name("CompactModeQuickInfo")]
    [ContentType(SupportedContentTypes.VisualBasic)]
    [ContentType(SupportedContentTypes.CSharp)]
    [ContentType(SupportedContentTypes.FSharp)]
    [ContentType(SupportedContentTypes.CPlusPlus)]
    [ContentType(SupportedContentTypes.TypeScript)]
    [ContentType(SupportedContentTypes.JavaScript)]
    [ContentType(SupportedContentTypes.Razor)]
    [ContentType(SupportedContentTypes.Sql)]
    [ContentType(SupportedContentTypes.PowerShell)]
    [Order(Before = "Default Quick Info Presenter")]

    internal sealed class CompactModeQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {


        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(
                () => new CompactModeQuickInfoSource(textBuffer));
        }
    }

    /// <summary>
    /// Provides hover tooltips for collapsed XML doc comments in Compact mode,
    /// showing the full rendered view.
    /// </summary>
    internal sealed class CompactModeQuickInfoSource : IAsyncQuickInfoSource
    {
        private readonly ITextBuffer _textBuffer;
        private readonly string _filePath;

        public CompactModeQuickInfoSource(ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;
            if (textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                _filePath = document.FilePath;
            }
        }

        public async Task<QuickInfoItem> GetQuickInfoItemAsync(
            IAsyncQuickInfoSession session,
            CancellationToken cancellationToken)
        {
            // Show tooltip in Compact mode only (Full mode shows all details inline)
            RenderingMode renderingMode = General.Instance.CommentRenderingMode;
            if (renderingMode != RenderingMode.Compact)
            {
                return null;
            }

            SnapshotPoint? triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
            {
                return null;
            }

            ITextSnapshot snapshot = triggerPoint.Value.Snapshot;
            var commentStyle = LanguageCommentStyle.GetForContentType(snapshot.ContentType);
            if (commentStyle == null)
            {
                return null;
            }

            var parser = new XmlDocCommentParser(commentStyle);
            XmlDocCommentBlock block = parser.FindCommentBlockAtPosition(snapshot, triggerPoint.Value.Position);

            if (block == null)
            {
                return null;
            }

            // Get repo info for issue reference resolution (async is fine here since we're in an async method)
            GitRepositoryInfo repoInfo = null;
            if (!string.IsNullOrEmpty(_filePath))
            {
                repoInfo = GitRepositoryService.TryGetCachedRepositoryInfo(_filePath)
                    ?? await GitRepositoryService.GetRepositoryInfoAsync(_filePath).ConfigureAwait(false);
            }

            // Render the comment in full format
            RenderedComment renderedComment = XmlDocCommentRenderer.Render(block, repoInfo);

            // Only show tooltip if there's content not visible in compact inline view:
            // 1. Additional sections beyond summary (params, returns, remarks, etc.)
            // 2. Summary has list content not shown inline
            // Note: Summary is no longer truncated - it word-wraps in compact mode
            var hasAdditionalSections = renderedComment.HasAdditionalSections;

            RenderedCommentSection summarySection = renderedComment.Summary;
            var summaryHasListContent = summarySection != null && summarySection.ListContentStartIndex >= 0;

            if (!hasAdditionalSections && !summaryHasListContent)
            {
                return null;
            }

            // Create the tooltip content using ContainerElement for proper theme support
            ContainerElement tooltipContent = CreateFullRenderingTooltip(renderedComment);

            if (tooltipContent == null)
            {
                return null;
            }

            ITrackingSpan trackingSpan = snapshot.CreateTrackingSpan(block.Span, SpanTrackingMode.EdgeInclusive);
            return new QuickInfoItem(trackingSpan, tooltipContent);
        }

        /// <summary>
        /// Creates a formatted tooltip showing the full rendered view of the comment.
        /// </summary>
        private static ContainerElement CreateFullRenderingTooltip(RenderedComment renderedComment)
        {
            if (renderedComment?.Sections == null || renderedComment.Sections.Count == 0)
            {
                return null;
            }

            var elements = new List<object>();

            var isFirst = true;
            foreach (RenderedCommentSection section in renderedComment.Sections)
            {
                if (section.IsEmpty)
                {
                    continue;
                }

                if (!isFirst)
                {
                    // Spacing between sections
                    elements.Add(new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, string.Empty)));
                }
                isFirst = false;

                // Add section heading (except for summary)
                if (!string.IsNullOrEmpty(section.Heading) && section.Type != CommentSectionType.Summary)
                {
                    elements.Add(new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, section.Heading, ClassifiedTextRunStyle.Bold)));
                }

                // Add section content
                foreach (RenderedLine line in section.Lines)
                {
                    if (line.IsBlank)
                    {
                        elements.Add(new ClassifiedTextElement(
                            new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, string.Empty)));
                        continue;
                    }

                    var runs = new List<ClassifiedTextRun>();
                    foreach (RenderedSegment segment in line.Segments)
                    {
                        runs.Add(CreateClassifiedTextRun(segment));
                    }

                    elements.Add(new ClassifiedTextElement(runs));
                }
            }

            return new ContainerElement(ContainerElementStyle.Stacked, elements);
        }

        private static ClassifiedTextRun CreateClassifiedTextRun(RenderedSegment segment)
        {
            return segment.Type switch
            {
                RenderedSegmentType.Heading => new ClassifiedTextRun(
                    PredefinedClassificationTypeNames.Text, segment.Text, ClassifiedTextRunStyle.Bold),
                RenderedSegmentType.Link => new ClassifiedTextRun(
                    PredefinedClassificationTypeNames.Text, segment.Text, ClassifiedTextRunStyle.Underline),
                RenderedSegmentType.ParamRef or RenderedSegmentType.TypeParamRef => new ClassifiedTextRun(
                    PredefinedClassificationTypeNames.Identifier, segment.Text, ClassifiedTextRunStyle.Italic),
                RenderedSegmentType.Bold => new ClassifiedTextRun(
                    PredefinedClassificationTypeNames.Text, segment.Text, ClassifiedTextRunStyle.Bold),
                RenderedSegmentType.Italic => new ClassifiedTextRun(
                    PredefinedClassificationTypeNames.Text, segment.Text, ClassifiedTextRunStyle.Italic),
                RenderedSegmentType.Code => new ClassifiedTextRun(
                    PredefinedClassificationTypeNames.String, segment.Text),
                _ => new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, segment.Text)
            };
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}
