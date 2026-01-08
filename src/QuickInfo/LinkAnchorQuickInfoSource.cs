using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using CommentsVS.Services;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.QuickInfo
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name("LinkAnchorQuickInfo")]
    [ContentType(SupportedContentTypes.Code)]
    [Order(Before = "Default Quick Info Presenter")]
    internal sealed class LinkAnchorQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(
                () => new LinkAnchorQuickInfoSource(textBuffer));
        }
    }

    /// <summary>
    /// Provides hover tooltips for LINK anchors showing the resolved path and navigation hint.
    /// </summary>
    internal sealed class LinkAnchorQuickInfoSource(ITextBuffer textBuffer) : IAsyncQuickInfoSource
    {
        private string _currentFilePath;
        private bool _filePathInitialized;

        public Task<QuickInfoItem> GetQuickInfoItemAsync(
            IAsyncQuickInfoSession session,
            CancellationToken cancellationToken)
        {
            SnapshotPoint? triggerPoint = session.GetTriggerPoint(textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            // Initialize file path lazily
            if (!_filePathInitialized)
            {
                InitializeFilePath();
            }

            ITextSnapshotLine line = triggerPoint.Value.GetContainingLine();
            var lineText = line.GetText();

            // Check if this line is a comment
            if (!LanguageCommentStyle.IsCommentLine(lineText))
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            var positionInLine = triggerPoint.Value.Position - line.Start.Position;

            // Find LINK reference at position (only matches within the clickable target portion)
            LinkAnchorInfo link = LinkAnchorParser.GetLinkAtPosition(lineText, positionInLine);
            if (link == null)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            // Create the tooltip - apply tracking span only to the target portion
            var span = new SnapshotSpan(line.Start + link.TargetStartIndex, link.TargetLength);
            ITrackingSpan trackingSpan = textBuffer.CurrentSnapshot.CreateTrackingSpan(
                span, SpanTrackingMode.EdgeInclusive);

            var tooltip = BuildTooltip(link);

            return Task.FromResult(new QuickInfoItem(trackingSpan, tooltip));
        }

        private string BuildTooltip(LinkAnchorInfo link)
        {
            var sb = new System.Text.StringBuilder();

            if (link.IsLocalAnchor)
            {
                sb.AppendLine($"🔗 Local Anchor: #{link.AnchorName}");
                sb.AppendLine("Jump to anchor in current file");
            }
            else
            {
                // Resolve the path
                string resolvedPath = null;
                var fileExists = false;

                if (!string.IsNullOrEmpty(_currentFilePath))
                {
                    var resolver = new FilePathResolver(_currentFilePath);
                    fileExists = resolver.TryResolve(link.FilePath, out resolvedPath);
                }

                if (fileExists)
                {
                    sb.AppendLine($"📁 {resolvedPath}");
                }
                else
                {
                    sb.AppendLine($"⚠️ File not found: {link.FilePath}");
                    if (!string.IsNullOrEmpty(resolvedPath))
                    {
                        sb.AppendLine($"Resolved path: {resolvedPath}");
                    }
                }

                if (link.HasLineRange)
                {
                    sb.AppendLine($"Lines: {link.LineNumber}-{link.EndLineNumber}");
                }
                else if (link.HasLineNumber)
                {
                    sb.AppendLine($"Line: {link.LineNumber}");
                }

                if (link.HasAnchor)
                {
                    sb.AppendLine($"Anchor: #{link.AnchorName}");
                }
            }

            sb.AppendLine();
            sb.Append("Ctrl+Click to navigate");

            return sb.ToString();
        }

        private void InitializeFilePath()
        {
            _filePathInitialized = true;

            if (textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                _currentFilePath = document.FilePath;
            }
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}
