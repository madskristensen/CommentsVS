using System.Collections.Generic;
using System.Text.RegularExpressions;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Service for scanning documents and finding anchors in comments.
    /// </summary>
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
            if (buffer == null)
            {
                return [];
            }

            ITextSnapshot snapshot = buffer.CurrentSnapshot;

            // Get all anchor tags and regex for this file (built-in + custom from .editorconfig/Options)
            IReadOnlyList<string> anchorTags = EditorConfigSettings.GetAllAnchorTags(filePath);
            Regex anchorRegex = EditorConfigSettings.GetAnchorServiceRegex(filePath);

            // Scan line-by-line without allocating full text copy
            return ScanSnapshotLines(snapshot, anchorTags, anchorRegex, filePath, projectName);
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
            if (string.IsNullOrEmpty(text))
            {
                return [];
            }

            // Get all anchor tags and regex for this file (built-in + custom from .editorconfig/Options)
            IReadOnlyList<string> anchorTags = EditorConfigSettings.GetAllAnchorTags(filePath);
            Regex anchorRegex = EditorConfigSettings.GetAnchorServiceRegex(filePath);

            // Scan using line reader to avoid Split() allocation
            return ScanTextLines(text, anchorTags, anchorRegex, filePath, projectName);
        }

        /// <summary>
        /// Scans snapshot lines without allocating full text.
        /// </summary>
        private IReadOnlyList<AnchorItem> ScanSnapshotLines(
            ITextSnapshot snapshot,
            IReadOnlyList<string> anchorTags,
            Regex anchorRegex,
            string filePath,
            string projectName)
        {
            var anchors = new List<AnchorItem>();
            var lineCount = snapshot.LineCount;

            for (var lineNumber = 0; lineNumber < lineCount; lineNumber++)
            {
                var lineText = snapshot.GetLineFromLineNumber(lineNumber).GetText();

                // Fast pre-check for this line
                if (!ContainsAnyKeyword(lineText, anchorTags))
                {
                    continue;
                }

                foreach (Match match in anchorRegex.Matches(lineText))
                {
                    AnchorItem anchor = CreateAnchorFromMatch(match, filePath, projectName, lineNumber);
                    if (anchor != null)
                    {
                        anchors.Add(anchor);
                    }
                }
            }

            return anchors;
        }

        /// <summary>
        /// Scans text lines without allocating a string array via Split().
        /// Uses StringReader for memory-efficient line enumeration.
        /// </summary>
        private IReadOnlyList<AnchorItem> ScanTextLines(
            string text,
            IReadOnlyList<string> anchorTags,
            Regex anchorRegex,
            string filePath,
            string projectName)
        {
            var anchors = new List<AnchorItem>();
            var lineNumber = 0;

            using (var reader = new System.IO.StringReader(text))
            {
                string lineText;
                while ((lineText = reader.ReadLine()) != null)
                {
                    // Fast pre-check for this line
                    if (ContainsAnyKeyword(lineText, anchorTags))
                    {
                        foreach (Match match in anchorRegex.Matches(lineText))
                        {
                            AnchorItem anchor = CreateAnchorFromMatch(match, filePath, projectName, lineNumber);
                            if (anchor != null)
                            {
                                anchors.Add(anchor);
                            }
                        }
                    }

                    lineNumber++;
                }
            }

            return anchors;
        }

        /// <summary>
        /// Checks if the text contains any of the specified keywords (case-insensitive).
        /// </summary>
        private static bool ContainsAnyKeyword(string text, IReadOnlyList<string> keywords)
        {
            foreach (var keyword in keywords)
            {
                if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Creates an AnchorItem from a regex match, or returns null if the match is invalid.
        /// </summary>
        private AnchorItem CreateAnchorFromMatch(Match match, string filePath, string projectName, int lineNumber)
        {
            Group tagGroup = match.Groups["tag"];
            if (!tagGroup.Success)
            {
                return null;
            }

            AnchorType? anchorType = AnchorTypeExtensions.ParseWithCustom(tagGroup.Value, filePath, out var customTagName);
            if (anchorType == null)
            {
                return null;
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

            return new AnchorItem
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
