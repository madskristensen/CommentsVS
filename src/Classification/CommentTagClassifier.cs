using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        private readonly IReadOnlyList<string> _anchorTags;
        private readonly HashSet<string> _customTags;
        private readonly Regex _anchorRegex;
        private readonly Regex _metadataRegex;

        private readonly IClassificationType _metadataType;
        private bool _disposed;

        // Cached option value to avoid repeated singleton access in hot path
        private bool _enableHighlighting;
        private int _optionCheckCounter;
        private const int OptionCheckInterval = 100; // Re-check option every N calls

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        public CommentTagClassifier(ITextBuffer buffer, IClassificationTypeRegistryService registry)
        {
            _buffer = buffer;
            _registry = registry;
            _metadataType = _registry.GetClassificationType(CommentTagClassificationTypes.Metadata);
            _buffer.Changed += OnBufferChanged;

            // Get file path and cache anchor tags/regex for this file (from .editorconfig or Options)
            string filePath = buffer.GetFileName();
            _anchorTags = EditorConfigSettings.GetAllAnchorTags(filePath);
            _customTags = EditorConfigSettings.GetCustomAnchorTags(filePath);
            _anchorRegex = EditorConfigSettings.GetAnchorClassificationRegex(filePath);
            _metadataRegex = EditorConfigSettings.GetAnchorWithMetadataRegex(filePath);

            // Cache initial option value
            _enableHighlighting = General.Instance.EnableCommentTagHighlighting;
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

            // Periodically refresh cached option value (avoids per-call singleton access)
            if (++_optionCheckCounter >= OptionCheckInterval)
            {
                _optionCheckCounter = 0;
                _enableHighlighting = General.Instance.EnableCommentTagHighlighting;
            }

            if (!_enableHighlighting)
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
            foreach (var keyword in _anchorTags)
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

            foreach (Match match in _anchorRegex.Matches(text))
            {
                Group tagGroup = match.Groups["tag"];
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
                    Match metaMatch = _metadataRegex.Match(text, tagGroup.Index);
                    if (metaMatch.Success && metaMatch.Index == tagGroup.Index)
                    {
                        Group metaGroup = metaMatch.Groups["metadata"];
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

            // Check if it's a custom tag (from .editorconfig or Options page)
            if (_customTags.Contains(tag))
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
