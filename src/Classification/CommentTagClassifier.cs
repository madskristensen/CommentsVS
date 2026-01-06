using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using CommentsVS.Options;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Classification
{
    [Export(typeof(IClassifierProvider))]
    [ContentType("code")]
    internal sealed class CommentTagClassifierProvider : IClassifierProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry { get; set; }

        public IClassifier GetClassifier(ITextBuffer buffer)
        {
            return buffer.Properties.GetOrCreateSingletonProperty(
                () => new CommentTagClassifier(buffer, ClassificationRegistry));
        }
    }

    /// <summary>
    /// Classifies comment tags (TODO, HACK, NOTE, etc.) for syntax highlighting.
    /// </summary>
    internal sealed class CommentTagClassifier : IClassifier
    {
        private readonly ITextBuffer _buffer;
        private readonly IClassificationTypeRegistryService _registry;

        private readonly IClassificationType _metadataType;

        /// <summary>
        /// Maximum file size (in characters) to process. Files larger than this are skipped for performance.
        /// </summary>
        private const int _maxFileSize = 150_000;

        /// <summary>
        /// Anchor keywords for fast pre-check before running regex.
        /// </summary>
        private static readonly string[] _anchorKeywords = ["TODO", "HACK", "NOTE", "BUG", "FIXME", "UNDONE", "REVIEW", "ANCHOR"];

        // Regex to match comment anchors - looks for anchor keywords after comment prefixes
        private static readonly Regex _anchorRegex = new(
            @"(?<=//.*)(?<tag>\b(?:TODO|HACK|NOTE|BUG|FIXME|UNDONE|REVIEW|ANCHOR)\b:?)|" +
            @"(?<=/\*.*)(?<tag>\b(?:TODO|HACK|NOTE|BUG|FIXME|UNDONE|REVIEW|ANCHOR)\b:?)|" +
            @"(?<='.*)(?<tag>\b(?:TODO|HACK|NOTE|BUG|FIXME|UNDONE|REVIEW|ANCHOR)\b:?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _anchorWithMetadataRegex = new(
            @"\b(?:TODO|HACK|NOTE|BUG|FIXME|UNDONE|REVIEW|ANCHOR)\b(?<metadata>\s*(?:\([^)]*\)|\[[^\]]*\]))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
            if (span.Snapshot.Length > _maxFileSize)
            {
                return result;
            }

            var text = span.GetText();

            // Fast pre-check: skip regex if no anchor keywords are present (case-insensitive)
            var hasAnyAnchor = false;
            foreach (var keyword in _anchorKeywords)
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
                    Match metaMatch = _anchorWithMetadataRegex.Match(text, tagGroup.Index);
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

            return typeName != null ? _registry.GetClassificationType(typeName) : null;
        }
    }

    /// <summary>
    /// Classification type names for comment tags.
    /// </summary>
    internal static class CommentTagClassificationTypes
    {
        public const string Todo = "CommentTag.TODO";
        public const string Hack = "CommentTag.HACK";
        public const string Note = "CommentTag.NOTE";
        public const string Bug = "CommentTag.BUG";
        public const string Fixme = "CommentTag.FIXME";
        public const string Undone = "CommentTag.UNDONE";
        public const string Review = "CommentTag.REVIEW";
        public const string Anchor = "CommentTag.ANCHOR";

        public const string Metadata = "CommentTag.Metadata";
    }
}
