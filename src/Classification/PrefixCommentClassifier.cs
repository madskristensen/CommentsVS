using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CommentsVS.Options;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace CommentsVS.Classification
{
    /// <summary>
    /// Classifies comments with special prefixes (!, ?, *, //, -, >) for enhanced highlighting.
    /// Implements "Better Comments" style visual differentiation.
    /// </summary>
    internal sealed class PrefixCommentClassifier : IClassifier
    {
        private readonly ITextBuffer _buffer;
        private readonly IClassificationTypeRegistryService _registry;

        private readonly IClassificationType _alertType;
        private readonly IClassificationType _queryType;
        private readonly IClassificationType _importantType;
        private readonly IClassificationType _strikethroughType;
        private readonly IClassificationType _disabledType;
        private readonly IClassificationType _quoteType;

        /// <summary>
        /// Maximum file size (in characters) to process. Files larger than this are skipped for performance.
        /// </summary>
        private const int _maxFileSize = 150_000;

        // Regex to match comment prefixes after the comment delimiter
        // Matches: // ! text, // ? text, // * text, // // text, // - text, // > text
        // Also supports: # ! text, ' ! text (for other languages)
        private static readonly Regex _prefixRegex = new(
            @"(?<prefix>//|#|')\s*(?<marker>[!?*\->]|//)\s*(?<content>.*?)$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        public PrefixCommentClassifier(ITextBuffer buffer, IClassificationTypeRegistryService registry)
        {
            _buffer = buffer;
            _registry = registry;

            _alertType = _registry.GetClassificationType(CommentTagClassificationTypes.PrefixAlert);
            _queryType = _registry.GetClassificationType(CommentTagClassificationTypes.PrefixQuery);
            _importantType = _registry.GetClassificationType(CommentTagClassificationTypes.PrefixImportant);
            _strikethroughType = _registry.GetClassificationType(CommentTagClassificationTypes.PrefixStrikethrough);
            _disabledType = _registry.GetClassificationType(CommentTagClassificationTypes.PrefixDisabled);
            _quoteType = _registry.GetClassificationType(CommentTagClassificationTypes.PrefixQuote);

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

            General options = General.Instance;
            if (!options.EnablePrefixHighlighting)
            {
                return result;
            }

            // Skip large files for performance
            if (span.Snapshot.Length > _maxFileSize)
            {
                return result;
            }

            var text = span.GetText();

            // Fast pre-check: skip if no comment delimiter is present
            if (!text.Contains("//") && !text.Contains("#") && !text.Contains("'"))
            {
                return result;
            }

            var lineStart = span.Start.Position;

            foreach (Match match in _prefixRegex.Matches(text))
            {
                Group markerGroup = match.Groups["marker"];
                Group contentGroup = match.Groups["content"];

                if (!markerGroup.Success)
                {
                    continue;
                }

                var marker = markerGroup.Value;
                IClassificationType classificationType = GetClassificationType(marker, options);

                if (classificationType != null && contentGroup.Success)
                {
                    // Classify the marker and the content together
                    int startIndex = markerGroup.Index;
                    int length = (contentGroup.Index + contentGroup.Length) - markerGroup.Index;

                    if (length > 0)
                    {
                        var prefixSpan = new SnapshotSpan(span.Snapshot, lineStart + startIndex, length);
                        result.Add(new ClassificationSpan(prefixSpan, classificationType));
                    }
                }
            }

            return result;
        }

        private IClassificationType GetClassificationType(string marker, General options)
        {
            return marker switch
            {
                "!" when options.EnableAlertPrefix => _alertType,
                "?" when options.EnableQueryPrefix => _queryType,
                "*" when options.EnableImportantPrefix => _importantType,
                "//" when options.EnableStrikethroughPrefix => _strikethroughType,
                "-" when options.EnableDisabledPrefix => _disabledType,
                ">" when options.EnableQuotePrefix => _quoteType,
                _ => null
            };
        }
    }
}
