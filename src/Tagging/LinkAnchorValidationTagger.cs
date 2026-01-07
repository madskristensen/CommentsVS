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
    [ContentType("code")]
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
    internal sealed class LinkAnchorValidationTagger : ITagger<IErrorTag>
    {
        private readonly ITextBuffer _buffer;
        private string _currentFilePath;
        private bool _filePathInitialized;

        /// <summary>
        /// Maximum file size (in characters) to process. Files larger than this are skipped for performance.
        /// </summary>
        private const int _maxFileSize = 150_000;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public LinkAnchorValidationTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;
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

            // Skip large files for performance
            if (spans[0].Snapshot.Length > _maxFileSize)
            {
                yield break;
            }

            // Initialize file path lazily
            if (!_filePathInitialized)
            {
                InitializeFilePath();
            }

            // Can't validate without knowing the current file
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                yield break;
            }

            var resolver = new FilePathResolver(_currentFilePath);

            foreach (SnapshotSpan span in spans)
            {
                string text = span.GetText();

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
                        var tagSpan = new SnapshotSpan(span.Snapshot, span.Start + link.TargetStartIndex, link.TargetLength);
                        var errorTag = new ErrorTag(PredefinedErrorTypeNames.Warning, $"File not found: {link.FilePath}");
                        yield return new TagSpan<IErrorTag>(tagSpan, errorTag);
                    }
                }
            }
        }

        private void InitializeFilePath()
        {
            _filePathInitialized = true;

            if (_buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                _currentFilePath = document.FilePath;
            }
        }
    }
}
