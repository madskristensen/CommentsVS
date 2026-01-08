using System;
using System.Collections.Generic;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace CommentsVS.Classification
{
    /// <summary>
    /// Classifies comment tags (TODO, HACK, NOTE, etc.) for syntax highlighting.
    /// </summary>
    internal sealed class CommentTagClassifier : IClassifier, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private readonly IClassificationTypeRegistryService _registry;

        private readonly IClassificationType _metadataType;
        private bool _disposed;

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        public CommentTagClassifier(ITextBuffer buffer, IClassificationTypeRegistryService registry)
        {
            _buffer = buffer;
            _registry = registry;
            _metadataType = _registry.GetClassificationType(CommentTagClassificationTypes.Metadata);
            _buffer.Changed += OnBufferChanged;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            foreach (ITextChange change in e.Changes)
            {
                ITextSnapshotLine line = e.After.GetLineFromPosition(change.NewPosition);
                ClassificationChanged?.Invoke(this, new ClassificationChangedEventArgs(
                    new SnapshotSpan(e.After, line.Start, line.Length)));
            }
        }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            var result = new List<ClassificationSpan>();

            if (!General.Instance.EnableCommentTagHighlighting)
            {
                return result;
            }

            // Skip large files for performance
            if (span.Snapshot.Length > Constants.MaxFileSize)
            {
                return result;
            }

            var text = span.GetText();

            // Fast pre-check: skip regex if no anchor keywords are present (case-insensitive)
            var hasAnyAnchor = false;
            foreach (var keyword in Constants.GetAllAnchorKeywords())
            {
                if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasAnyAnchor = true;
                    break;
                }
            }
            if (!hasAnyAnchor)
            {
                return result;
            }

            var lineStart = span.Start.Position;

            foreach (System.Text.RegularExpressions.Match match in CommentPatterns.AnchorClassificationRegex.Matches(text))
            {
                System.Text.RegularExpressions.Group tagGroup = match.Groups["tag"];
                if (!tagGroup.Success)
                {
                    continue;
                }

                var tag = tagGroup.Value.TrimEnd(':').ToUpperInvariant();
                IClassificationType classificationType = GetClassificationType(tag);

                if (classificationType != null)
                {
                    var tagSpan = new SnapshotSpan(span.Snapshot, lineStart + tagGroup.Index, tagGroup.Length);
                    result.Add(new ClassificationSpan(tagSpan, classificationType));
                }

                if (_metadataType != null)
                {
                    // Classify the optional metadata right after the anchor.
                    // Examples: TODO(@mads): ...  TODO[#123]: ...  ANCHOR(section-name): ...
                    System.Text.RegularExpressions.Match metaMatch = CommentPatterns.AnchorWithMetadataRegex.Match(text, tagGroup.Index);
                    if (metaMatch.Success && metaMatch.Index == tagGroup.Index)
                    {
                        System.Text.RegularExpressions.Group metaGroup = metaMatch.Groups["metadata"];
                        if (metaGroup.Success && metaGroup.Length > 0)
                        {
                            var metaSpan = new SnapshotSpan(span.Snapshot, lineStart + metaGroup.Index, metaGroup.Length);
                            result.Add(new ClassificationSpan(metaSpan, _metadataType));
                        }
                    }
                }
            }

            return result;
        }

        private IClassificationType GetClassificationType(string tag)
        {
            var typeName = tag switch
            {
                "TODO" => CommentTagClassificationTypes.Todo,
                "HACK" => CommentTagClassificationTypes.Hack,
                "NOTE" => CommentTagClassificationTypes.Note,
                "BUG" => CommentTagClassificationTypes.Bug,
                "FIXME" => CommentTagClassificationTypes.Fixme,
                "UNDONE" => CommentTagClassificationTypes.Undone,
                "REVIEW" => CommentTagClassificationTypes.Review,
                "ANCHOR" => CommentTagClassificationTypes.Anchor,
                _ => null
            };

            if (typeName != null)
            {
                return _registry.GetClassificationType(typeName);
            }

            // Check if it's a custom tag
            if (General.Instance.GetCustomTagsSet().Contains(tag))
            {
                return _registry.GetClassificationType(CommentTagClassificationTypes.Custom);
            }

            return null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _buffer.Changed -= OnBufferChanged;
        }
    }
}
