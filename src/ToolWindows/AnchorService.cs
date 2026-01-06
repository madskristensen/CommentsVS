using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Text;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Service for scanning documents and finding anchors in comments.
    /// </summary>
    [Export(typeof(AnchorService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AnchorService
    {
        /// <summary>
        /// All anchor keywords for detection.
        /// </summary>
        private static readonly string[] _anchorKeywords = ["TODO", "HACK", "NOTE", "BUG", "FIXME", "UNDONE", "REVIEW", "ANCHOR"];

        /// <summary>
        /// Regex to match anchors in comments. Captures the anchor keyword and optional trailing colon.
        /// Supports C-style (// and /* */), VB-style ('), and HTML-style (<!-- -->) comments.
        /// </summary>
        private static readonly Regex _anchorRegex = new(
            @"(?<prefix>//|/\*|'|<!--)\s*(?<tag>\b(?:TODO|HACK|NOTE|BUG|FIXME|UNDONE|REVIEW|ANCHOR)\b)\s*(?<metadata>(?:\([^)]*\)|\[[^\]]*\]))?\s*:?\s*(?<message>.*?)(?:\*/|-->|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Regex to extract owner from metadata (e.g., @mads from (@mads) or [@mads]).
        /// </summary>
        private static readonly Regex _ownerRegex = new(
            @"@(\w+)",
            RegexOptions.Compiled);

        /// <summary>
        /// Regex to extract issue reference from metadata (e.g., #123 from [#123]).
        /// </summary>
        private static readonly Regex _issueRegex = new(
            @"#(\d+)",
            RegexOptions.Compiled);

        /// <summary>
        /// Scans a text buffer for anchors.
        /// </summary>
        /// <param name="buffer">The text buffer to scan.</param>
        /// <param name="filePath">The file path associated with the buffer.</param>
        /// <param name="projectName">The project name (optional).</param>
        /// <returns>A list of anchors found in the buffer.</returns>
        public IReadOnlyList<AnchorItem> ScanBuffer(ITextBuffer buffer, string filePath, string projectName = null)
        {
            var anchors = new List<AnchorItem>();

            if (buffer == null)
            {
                return anchors;
            }

            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            var fullText = snapshot.GetText();

            // Fast pre-check: skip if no anchor keywords are present
            var hasAnyAnchor = false;
            foreach (var keyword in _anchorKeywords)
            {
                if (fullText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasAnyAnchor = true;
                    break;
                }
            }

            if (!hasAnyAnchor)
            {
                return anchors;
            }

            // Scan each line for anchors
            for (var lineNumber = 0; lineNumber < snapshot.LineCount; lineNumber++)
            {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNumber);
                var lineText = line.GetText();

                // Fast pre-check for this line
                var lineHasAnchor = false;
                foreach (var keyword in _anchorKeywords)
                {
                    if (lineText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        lineHasAnchor = true;
                        break;
                    }
                }

                if (!lineHasAnchor)
                {
                    continue;
                }

                foreach (Match match in _anchorRegex.Matches(lineText))
                {
                    Group tagGroup = match.Groups["tag"];
                    if (!tagGroup.Success)
                    {
                        continue;
                    }

                    AnchorType? anchorType = AnchorTypeExtensions.Parse(tagGroup.Value);
                    if (anchorType == null)
                    {
                        continue;
                    }

                    var anchor = new AnchorItem
                    {
                        AnchorType = anchorType.Value,
                        FilePath = filePath,
                        LineNumber = lineNumber + 1, // 1-based line numbers
                        Column = tagGroup.Index,
                        Project = projectName
                    };

                    // Extract message
                    Group messageGroup = match.Groups["message"];
                    if (messageGroup.Success)
                    {
                        anchor.Message = messageGroup.Value.Trim();
                    }

                    // Extract metadata
                    Group metadataGroup = match.Groups["metadata"];
                    if (metadataGroup.Success && metadataGroup.Length > 0)
                    {
                        anchor.RawMetadata = metadataGroup.Value;
                        ParseMetadata(anchor);
                    }

                    anchors.Add(anchor);
                }
            }

            return anchors;
        }

        /// <summary>
        /// Scans plain text content for anchors.
        /// </summary>
        /// <param name="text">The text content to scan.</param>
        /// <param name="filePath">The file path associated with the text.</param>
        /// <param name="projectName">The project name (optional).</param>
        /// <returns>A list of anchors found in the text.</returns>
        public IReadOnlyList<AnchorItem> ScanText(string text, string filePath, string projectName = null)
        {
            var anchors = new List<AnchorItem>();

            if (string.IsNullOrEmpty(text))
            {
                return anchors;
            }

            // Fast pre-check: skip if no anchor keywords are present
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
                return anchors;
            }

            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                var lineText = lines[lineNumber];

                // Fast pre-check for this line
                var lineHasAnchor = false;
                foreach (var keyword in _anchorKeywords)
                {
                    if (lineText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        lineHasAnchor = true;
                        break;
                    }
                }

                if (!lineHasAnchor)
                {
                    continue;
                }

                foreach (Match match in _anchorRegex.Matches(lineText))
                {
                    Group tagGroup = match.Groups["tag"];
                    if (!tagGroup.Success)
                    {
                        continue;
                    }

                    AnchorType? anchorType = AnchorTypeExtensions.Parse(tagGroup.Value);
                    if (anchorType == null)
                    {
                        continue;
                    }

                    var anchor = new AnchorItem
                    {
                        AnchorType = anchorType.Value,
                        FilePath = filePath,
                        LineNumber = lineNumber + 1, // 1-based line numbers
                        Column = tagGroup.Index,
                        Project = projectName
                    };

                    // Extract message
                    Group messageGroup = match.Groups["message"];
                    if (messageGroup.Success)
                    {
                        anchor.Message = messageGroup.Value.Trim();
                    }

                    // Extract metadata
                    Group metadataGroup = match.Groups["metadata"];
                    if (metadataGroup.Success && metadataGroup.Length > 0)
                    {
                        anchor.RawMetadata = metadataGroup.Value;
                        ParseMetadata(anchor);
                    }

                    anchors.Add(anchor);
                }
            }

            return anchors;
        }

        /// <summary>
        /// Parses the metadata string to extract owner, issue reference, and anchor ID.
        /// </summary>
        private void ParseMetadata(AnchorItem anchor)
        {
            if (string.IsNullOrEmpty(anchor.RawMetadata))
            {
                return;
            }

            // Extract owner (@username)
            Match ownerMatch = _ownerRegex.Match(anchor.RawMetadata);
            if (ownerMatch.Success)
            {
                anchor.Owner = ownerMatch.Groups[1].Value;
            }

            // Extract issue reference (#123)
            Match issueMatch = _issueRegex.Match(anchor.RawMetadata);
            if (issueMatch.Success)
            {
                anchor.IssueReference = "#" + issueMatch.Groups[1].Value;
            }

            // For ANCHOR type, the metadata content is the anchor ID
            if (anchor.AnchorType == AnchorType.Anchor)
            {
                // Strip parentheses/brackets and use as anchor ID
                var content = anchor.RawMetadata.Trim('(', ')', '[', ']');
                if (!string.IsNullOrWhiteSpace(content) && anchor.Owner == null && anchor.IssueReference == null)
                {
                    anchor.AnchorId = content;
                }
            }
        }
    }
}
