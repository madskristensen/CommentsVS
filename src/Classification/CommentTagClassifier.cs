using System;
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

        // Regex to match comment tags - looks for tag keywords after comment prefixes
        private static readonly Regex TagRegex = new Regex(
            @"(?<=//.*)(?<tag>\b(?:TODO|HACK|NOTE|BUG|FIXME|UNDONE|REVIEW)\b:?)|" +
            @"(?<=/\*.*)(?<tag>\b(?:TODO|HACK|NOTE|BUG|FIXME|UNDONE|REVIEW)\b:?)|" +
            @"(?<='.*)(?<tag>\b(?:TODO|HACK|NOTE|BUG|FIXME|UNDONE|REVIEW)\b:?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        public CommentTagClassifier(ITextBuffer buffer, IClassificationTypeRegistryService registry)
        {
            _buffer = buffer;
            _registry = registry;
            _buffer.Changed += OnBufferChanged;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            foreach (ITextChange change in e.Changes)
            {
                var line = e.After.GetLineFromPosition(change.NewPosition);
                ClassificationChanged?.Invoke(this, new ClassificationChangedEventArgs(
                    new SnapshotSpan(e.After, line.Start, line.Length)));
            }
        }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            var result = new List<ClassificationSpan>();

            if (!CommentTags.Instance.Enabled)
            {
                return result;
            }

            string text = span.GetText();
            int lineStart = span.Start.Position;

            foreach (Match match in TagRegex.Matches(text))
            {
                Group tagGroup = match.Groups["tag"];
                if (!tagGroup.Success)
                {
                    continue;
                }

                string tag = tagGroup.Value.TrimEnd(':').ToUpperInvariant();
                IClassificationType classificationType = GetClassificationType(tag);

                if (classificationType != null)
                {
                    var tagSpan = new SnapshotSpan(span.Snapshot, lineStart + tagGroup.Index, tagGroup.Length);
                    result.Add(new ClassificationSpan(tagSpan, classificationType));
                }
            }

            return result;
        }

        private IClassificationType GetClassificationType(string tag)
        {
            switch (tag)
            {
                case "TODO":
                    return _registry.GetClassificationType(CommentTagClassificationTypes.Todo);
                case "HACK":
                    return _registry.GetClassificationType(CommentTagClassificationTypes.Hack);
                case "NOTE":
                    return _registry.GetClassificationType(CommentTagClassificationTypes.Note);
                case "BUG":
                    return _registry.GetClassificationType(CommentTagClassificationTypes.Bug);
                case "FIXME":
                    return _registry.GetClassificationType(CommentTagClassificationTypes.Fixme);
                case "UNDONE":
                    return _registry.GetClassificationType(CommentTagClassificationTypes.Undone);
                case "REVIEW":
                    return _registry.GetClassificationType(CommentTagClassificationTypes.Review);
                default:
                    return null;
            }
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
    }
}
