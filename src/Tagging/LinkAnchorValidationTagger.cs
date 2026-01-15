using System.Collections.Generic;
using System.ComponentModel.Composition;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Tagging
{
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(IErrorTag))]
    [ContentType(SupportedContentTypes.Code)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class LinkAnchorValidationTaggerProvider : IViewTaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
            {
                return null;
            }

            return buffer.Properties.GetOrCreateSingletonProperty(
                () => new LinkAnchorValidationTagger(buffer)) as ITagger<T>;
        }
    }

    /// <summary>
    /// Creates error tags (squiggles) for invalid LINK anchors (missing files or anchors).
    /// </summary>
    internal sealed class LinkAnchorValidationTagger : ITagger<IErrorTag>, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private string _currentFilePath;
        private ITextDocument _document;
        private bool _disposed;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public LinkAnchorValidationTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;
            TryInitializeDocument();
        }

        private void TryInitializeDocument()
        {
            if (_document != null)
            {
                return;
            }

            if (_buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                _document = document;
                _currentFilePath = document.FilePath;
                _document.FileActionOccurred += OnFileActionOccurred;
            }
        }

        private void OnFileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            // Update file path if document was renamed or saved to a new location
            if (e.FileActionType == FileActionTypes.DocumentRenamed)
            {
                _currentFilePath = e.FilePath;
                RaiseTagsChangedForEntireBuffer();
            }
        }

        private void RaiseTagsChangedForEntireBuffer()
        {
            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _buffer.Changed -= OnBufferChanged;

            if (_document != null)
            {
                _document.FileActionOccurred -= OnFileActionOccurred;
            }
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            foreach (ITextChange change in e.Changes)
            {
                ITextSnapshotLine line = e.After.GetLineFromPosition(change.NewPosition);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                    new SnapshotSpan(e.After, line.Start, line.Length)));
            }
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {
                yield break;
            }

            ITextSnapshot snapshot = spans[0].Snapshot;

            // Skip large files for performance
            if (snapshot.Length > Constants.MaxFileSize)
            {
                yield break;
            }

            // Try to initialize document if not yet available
            TryInitializeDocument();

            // Can't validate without knowing the current file
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                yield break;
            }

            var resolver = new FilePathResolver(_currentFilePath);

            // Process each line that intersects with the requested spans
            foreach (SnapshotSpan span in spans)
            {
                // Get the full line(s) that this span intersects
                var startLineNumber = span.Start.GetContainingLine().LineNumber;
                var endLineNumber = span.End.GetContainingLine().LineNumber;

                for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
                {
                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNumber);
                    var text = line.GetText();

                    // Fast pre-check: skip if no LINK keyword
                    if (text.IndexOf("LINK", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    // Check if this line is a comment
                    if (!LanguageCommentStyle.IsCommentLine(text))
                    {
                        continue;
                    }

                    IReadOnlyList<LinkAnchorInfo> links = LinkAnchorParser.Parse(text);
                    foreach (LinkAnchorInfo link in links)
                    {
                        // Skip local anchors for now (would need to scan current file)
                        if (link.IsLocalAnchor)
                        {
                            continue;
                        }

                        // Try to resolve the file path
                        if (!resolver.TryResolve(link.FilePath, out _))
                        {
                            // File not found - create a warning squiggle on the target portion only
                            var tagSpan = new SnapshotSpan(snapshot, line.Start + link.TargetStartIndex, link.TargetLength);
                            var errorTag = new ErrorTag(PredefinedErrorTypeNames.Warning, $"File not found: {link.FilePath}");
                            yield return new TagSpan<IErrorTag>(tagSpan, errorTag);
                        }
                    }
                }
            }
        }
    }
}
