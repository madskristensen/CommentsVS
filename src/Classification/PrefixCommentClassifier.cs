using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Classification
{
    /// <summary>
    /// Classifies comments with special prefixes (!, ?, *, //, -, >) for enhanced highlighting.
    /// Implements "Better Comments" style visual differentiation.
    /// </summary>
    internal sealed class PrefixCommentClassifier : IClassifier
    {
        private readonly ITextBuffer _buffer;
        private readonly Regex _commentRegex;
        private readonly string _fastCheckPattern;

        private readonly IClassificationType _alertType;
        private readonly IClassificationType _queryType;
        private readonly IClassificationType _importantType;
        private readonly IClassificationType _strikethroughType;
        private readonly IClassificationType _disabledType;
        private readonly IClassificationType _quoteType;

        // Regex patterns for different comment styles
        // C-style: // ! text (C#, C++, TypeScript, JavaScript, Razor, F#)
        private static readonly Regex _cStyleRegex = new(
            @"(?<prefix>//)\s*(?<marker>[!?*\->]|//)\s*(?<content>.*?)$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Hash-style: # ! text (PowerShell)
        private static readonly Regex _hashStyleRegex = new(
            @"(?<prefix>#)\s*(?<marker>[!?*\->])\s*(?<content>.*?)$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // VB-style: ' ! text
        private static readonly Regex _vbStyleRegex = new(
            @"(?<prefix>')\s*(?<marker>[!?*\->])\s*(?<content>.*?)$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // SQL-style: -- ! text
        private static readonly Regex _sqlStyleRegex = new(
            @"(?<prefix>--)\s*(?<marker>[!?*\->])\s*(?<content>.*?)$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        public PrefixCommentClassifier(ITextBuffer buffer, IClassificationTypeRegistryService registry)
        {
            _buffer = buffer;

            // Determine the appropriate regex based on content type
            (_commentRegex, _fastCheckPattern) = GetCommentPatternForContentType(buffer);

            _alertType = registry.GetClassificationType(CommentTagClassificationTypes.PrefixAlert);
            _queryType = registry.GetClassificationType(CommentTagClassificationTypes.PrefixQuery);
            _importantType = registry.GetClassificationType(CommentTagClassificationTypes.PrefixImportant);
            _strikethroughType = registry.GetClassificationType(CommentTagClassificationTypes.PrefixStrikethrough);
            _disabledType = registry.GetClassificationType(CommentTagClassificationTypes.PrefixDisabled);
            _quoteType = registry.GetClassificationType(CommentTagClassificationTypes.PrefixQuote);

            _buffer.Changed += OnBufferChanged;
        }

        private static (Regex regex, string fastCheck) GetCommentPatternForContentType(ITextBuffer buffer)
        {
            IContentType contentType = buffer.ContentType;

            // VB uses ' for comments
            if (contentType.IsOfType(SupportedContentTypes.VisualBasic))
            {
                return (_vbStyleRegex, "'");
            }

            // SQL uses -- for comments
            if (contentType.IsOfType(SupportedContentTypes.Sql))
            {
                return (_sqlStyleRegex, "--");
            }

            // PowerShell uses # for comments
            if (contentType.IsOfType(SupportedContentTypes.PowerShell))
            {
                return (_hashStyleRegex, "#");
            }

            // C#, C++, TypeScript, JavaScript, F#, Razor all use // for comments
            return (_cStyleRegex, "//");
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
            if (span.Snapshot.Length > Constants.MaxFileSize)
            {
                return result;
            }

            var text = span.GetText();

            // Fast pre-check: skip if no comment delimiter is present
            if (!text.Contains(_fastCheckPattern))
            {
                return result;
            }

            var lineStart = span.Start.Position;

            foreach (Match match in _commentRegex.Matches(text))
            {
                Group markerGroup = match.Groups["marker"];
                Group contentGroup = match.Groups["content"];

                if (!markerGroup.Success)
                {
                    continue;
                }

                var marker = markerGroup.Value;
                IClassificationType classificationType = GetClassificationType(marker);

                if (classificationType != null && contentGroup.Success)
                {
                    // Classify the marker and the content together
                    var startIndex = markerGroup.Index;
                    var length = (contentGroup.Index + contentGroup.Length) - markerGroup.Index;

                    if (length > 0)
                    {
                        var prefixSpan = new SnapshotSpan(span.Snapshot, lineStart + startIndex, length);
                        result.Add(new ClassificationSpan(prefixSpan, classificationType));
                    }
                }
            }

            return result;
        }

        private IClassificationType GetClassificationType(string marker)
        {
            return marker switch
            {
                "!" => _alertType,
                "?" => _queryType,
                "*" => _importantType,
                "//" => _strikethroughType,
                "-" => _disabledType,
                ">" => _quoteType,
                _ => null
            };
        }
    }
}
