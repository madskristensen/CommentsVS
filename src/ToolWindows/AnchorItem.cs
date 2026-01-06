using System.IO;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Represents a single anchor found in a source file.
    /// </summary>
    public class AnchorItem
    {
        /// <summary>
        /// Gets or sets the type of anchor (TODO, HACK, ANCHOR, etc.).
        /// </summary>
        public AnchorType AnchorType { get; set; }

        /// <summary>
        /// Gets or sets the message/description text following the anchor keyword.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the full file path where the anchor is located.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets the file name without directory path.
        /// </summary>
        public string FileName => string.IsNullOrEmpty(FilePath) ? string.Empty : Path.GetFileName(FilePath);

        /// <summary>
        /// Gets or sets the 1-based line number where the anchor is located.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Gets or sets the 0-based column/character position where the anchor starts.
        /// </summary>
        public int Column { get; set; }

        /// <summary>
        /// Gets or sets the project name containing this anchor.
        /// </summary>
        public string Project { get; set; }

        /// <summary>
        /// Gets or sets the raw metadata string from parentheses or brackets, e.g., "(@mads)" or "[#123]".
        /// </summary>
        public string RawMetadata { get; set; }

        /// <summary>
        /// Gets or sets the owner/assignee extracted from metadata (e.g., "@mads").
        /// </summary>
        public string Owner { get; set; }

        /// <summary>
        /// Gets or sets the issue reference extracted from metadata (e.g., "#123").
        /// </summary>
        public string IssueReference { get; set; }

        /// <summary>
        /// Gets or sets the anchor identifier for ANCHOR types (e.g., "section-name" from "ANCHOR(section-name)").
        /// </summary>
        public string AnchorId { get; set; }

        /// <summary>
        /// Gets the display text for the anchor type column.
        /// </summary>
        public string TypeDisplayName => AnchorType.GetDisplayName();

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
