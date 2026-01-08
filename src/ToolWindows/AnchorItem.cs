using System.IO;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Represents a single anchor found in a source file.
    /// </summary>
    public sealed record AnchorItem
    {
        /// <summary>
        /// Gets the type of anchor (TODO, HACK, ANCHOR, etc.).
        /// </summary>
        public AnchorType AnchorType { get; init; }

        /// <summary>
        /// Gets the custom tag name when AnchorType is Custom (e.g., "PERF", "SECURITY").
        /// </summary>
        public string CustomTagName { get; init; }

        /// <summary>
        /// Gets the message/description text following the anchor keyword.
        /// </summary>
        public string Message { get; init; }

        /// <summary>
        /// Gets the full file path where the anchor is located.
        /// </summary>
        public string FilePath { get; init; }

        /// <summary>
        /// Gets the file name without directory path.
        /// </summary>
        public string FileName => string.IsNullOrEmpty(FilePath) ? string.Empty : Path.GetFileName(FilePath);

        /// <summary>
        /// Gets the 1-based line number where the anchor is located.
        /// </summary>
        public int LineNumber { get; init; }

        /// <summary>
        /// Gets the 0-based column/character position where the anchor starts.
        /// </summary>
        public int Column { get; init; }

        /// <summary>
        /// Gets the project name containing this anchor.
        /// </summary>
        public string Project { get; init; }

        /// <summary>
        /// Gets the raw metadata string from parentheses or brackets, e.g., "(@mads)" or "[#123]".
        /// </summary>
        public string RawMetadata { get; init; }

        /// <summary>
        /// Gets the owner/assignee extracted from metadata (e.g., "@mads").
        /// </summary>
        public string Owner { get; init; }

        /// <summary>
        /// Gets the issue reference extracted from metadata (e.g., "#123").
        /// </summary>
        public string IssueReference { get; init; }

        /// <summary>
        /// Gets the anchor identifier for ANCHOR types (e.g., "section-name" from "ANCHOR(section-name)").
        /// </summary>
        public string AnchorId { get; init; }

        /// <summary>
        /// Gets the display text for the anchor type column.
        /// </summary>
        public string TypeDisplayName => AnchorType == AnchorType.Custom && !string.IsNullOrEmpty(CustomTagName)
            ? CustomTagName
            : AnchorType.GetDisplayName();

        /// <summary>
        /// Gets the formatted metadata for display in the tool window.
        /// Shows owner, issue reference, and/or anchor ID in a readable format.
        /// </summary>
        public string MetadataDisplay
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();

                if (!string.IsNullOrEmpty(Owner))
                {
                    parts.Add($"@{Owner}");
                }

                if (!string.IsNullOrEmpty(IssueReference))
                {
                    parts.Add(IssueReference);
                }

                if (!string.IsNullOrEmpty(AnchorId))
                {
                    parts.Add(AnchorId);
                }

                return parts.Count > 0 ? string.Join(" ", parts) : string.Empty;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this anchor has any metadata.
        /// </summary>
        public bool HasMetadata => !string.IsNullOrEmpty(Owner) || 
                                   !string.IsNullOrEmpty(IssueReference) || 
                                   !string.IsNullOrEmpty(AnchorId);

        /// <summary>
        /// Gets the full text representation for display purposes.
        /// </summary>
        public string FullText
        {
            get
            {
                var text = $"{AnchorType.GetDisplayName()}";
                if (!string.IsNullOrEmpty(RawMetadata))
                {
                    text += $" {RawMetadata}";
                }
                if (!string.IsNullOrEmpty(Message))
                {
                    text += $": {Message}";
                }
                return text;
            }
        }

        public override string ToString()
        {
            return $"{TypeDisplayName} - {FileName}:{LineNumber} - {Message}";
        }
    }
}
