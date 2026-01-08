using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using CommentsVS.Services;
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
            foreach (var keyword in Constants.GetAllAnchorKeywords())
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
                foreach (var keyword in Constants.GetAllAnchorKeywords())
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

                foreach (Match match in CommentPatterns.AnchorServiceRegex.Matches(lineText))
                {
                    Group tagGroup = match.Groups["tag"];
                    if (!tagGroup.Success)
                    {
                        continue;
                    }

                    AnchorType? anchorType = AnchorTypeExtensions.ParseWithCustom(tagGroup.Value, out var customTagName);
                    if (anchorType == null)
                    {
                        continue;
                    }

                    // Extract message
                    string message = null;
                    Group messageGroup = match.Groups["message"];
                    if (messageGroup.Success)
                    {
                        message = messageGroup.Value.Trim();
                    }

                    // Extract metadata
                    string rawMetadata = null;
                    string owner = null;
                    string issueReference = null;
                    string anchorId = null;

                    Group metadataGroup = match.Groups["metadata"];
                    if (metadataGroup.Success && metadataGroup.Length > 0)
                    {
                        rawMetadata = metadataGroup.Value;
                        (owner, issueReference, anchorId) = ParseMetadata(rawMetadata, anchorType.Value);
                    }

                    var anchor = new AnchorItem
                    {
                        AnchorType = anchorType.Value,
                        CustomTagName = customTagName,
                        FilePath = filePath,
                        LineNumber = lineNumber + 1, // 1-based line numbers
                        Column = tagGroup.Index,
                        Project = projectName,
                        Message = message,
                        RawMetadata = rawMetadata,
                        Owner = owner,
                        IssueReference = issueReference,
                        AnchorId = anchorId
                    };

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
                return anchors;
            }

            var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

            for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                var lineText = lines[lineNumber];

                // Fast pre-check for this line
                var lineHasAnchor = false;
                foreach (var keyword in Constants.GetAllAnchorKeywords())
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

                foreach (Match match in CommentPatterns.AnchorServiceRegex.Matches(lineText))
                {
                    Group tagGroup = match.Groups["tag"];
                    if (!tagGroup.Success)
                    {
                        continue;
                    }

                    AnchorType? anchorType = AnchorTypeExtensions.ParseWithCustom(tagGroup.Value, out var customTagName);
                    if (anchorType == null)
                    {
                        continue;
                    }

                    // Extract message
                    string message = null;
                    Group messageGroup = match.Groups["message"];
                    if (messageGroup.Success)
                    {
                        message = messageGroup.Value.Trim();
                    }

                    // Extract metadata
                    string rawMetadata = null;
                    string owner = null;
                    string issueReference = null;
                    string anchorId = null;

                    Group metadataGroup = match.Groups["metadata"];
                    if (metadataGroup.Success && metadataGroup.Length > 0)
                    {
                        rawMetadata = metadataGroup.Value;
                        (owner, issueReference, anchorId) = ParseMetadata(rawMetadata, anchorType.Value);
                    }

                    var anchor = new AnchorItem
                    {
                        AnchorType = anchorType.Value,
                        CustomTagName = customTagName,
                        FilePath = filePath,
                        LineNumber = lineNumber + 1, // 1-based line numbers
                        Column = tagGroup.Index,
                        Project = projectName,
                        Message = message,
                        RawMetadata = rawMetadata,
                        Owner = owner,
                        IssueReference = issueReference,
                        AnchorId = anchorId
                    };

                    anchors.Add(anchor);
                }
            }

            return anchors;
        }

        /// <summary>
        /// Parses the metadata string to extract owner, issue reference, and anchor ID.
        /// </summary>
        private (string owner, string issueReference, string anchorId) ParseMetadata(string rawMetadata, AnchorType anchorType)
        {
            if (string.IsNullOrEmpty(rawMetadata))
            {
                return (null, null, null);
            }

            string owner = null;
            string issueReference = null;
            string anchorId = null;

            // Extract owner (@username)
            Match ownerMatch = _ownerRegex.Match(rawMetadata);
            if (ownerMatch.Success)
            {
                owner = ownerMatch.Groups[1].Value;
            }

            // Extract issue reference (#123)
            Match issueMatch = _issueRegex.Match(rawMetadata);
            if (issueMatch.Success)
            {
                issueReference = "#" + issueMatch.Groups[1].Value;
            }

            // For ANCHOR type, the metadata content is the anchor ID
            if (anchorType == AnchorType.Anchor)
            {
                // Strip parentheses/brackets and use as anchor ID
                var content = rawMetadata.Trim('(', ')', '[', ']');
                if (!string.IsNullOrWhiteSpace(content) && owner == null && issueReference == null)
                {
                    anchorId = content;
                }
            }

            return (owner, issueReference, anchorId);
        }
    }
}
