using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommentsVS.ToolWindows;

namespace CommentsVS.Services
{
    /// <summary>
    /// Export format options for code anchors.
    /// </summary>
    public enum AnchorExportFormat
    {
        Tsv,
        Csv,
        Markdown,
        Json
    }

    /// <summary>
    /// Exports code anchors to various formats (TSV, CSV, Markdown, JSON).
    /// </summary>
    public static class AnchorExporter
    {
        private static readonly string[] _headers =
        [
            "Type", "Message", "File", "Path", "Line", "Project", "Owner", "Issue", "Anchor ID"
        ];

        /// <summary>
        /// Exports anchors to the specified format.
        /// </summary>
        /// <param name="anchors">The anchors to export.</param>
        /// <param name="format">The export format.</param>
        /// <returns>The formatted string representation of the anchors.</returns>
        public static string Export(IEnumerable<AnchorItem> anchors, AnchorExportFormat format)
        {
            List<AnchorItem> items = anchors?.ToList() ?? [];

            return format switch
            {
                AnchorExportFormat.Tsv => ExportAsTsv(items),
                AnchorExportFormat.Csv => ExportAsCsv(items),
                AnchorExportFormat.Markdown => ExportAsMarkdown(items),
                AnchorExportFormat.Json => ExportAsJson(items),
                _ => ExportAsTsv(items)
            };
        }

        /// <summary>
        /// Exports anchors as tab-separated values (TSV).
        /// </summary>
        private static string ExportAsTsv(List<AnchorItem> anchors)
        {
            var sb = new StringBuilder();

            // Header row
            sb.AppendLine(string.Join("\t", _headers));

            // Data rows
            foreach (AnchorItem anchor in anchors)
            {
                sb.AppendLine(string.Join("\t", GetRowValues(anchor)));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports anchors as comma-separated values (CSV).
        /// </summary>
        private static string ExportAsCsv(List<AnchorItem> anchors)
        {
            var sb = new StringBuilder();

            // Header row
            sb.AppendLine(string.Join(",", _headers));

            // Data rows
            foreach (AnchorItem anchor in anchors)
            {
                IEnumerable<string> values = GetRowValues(anchor).Select(EscapeCsvField);
                sb.AppendLine(string.Join(",", values));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports anchors as a Markdown table.
        /// </summary>
        private static string ExportAsMarkdown(List<AnchorItem> anchors)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Code Anchors");
            sb.AppendLine();

            // Header row
            sb.AppendLine("| " + string.Join(" | ", _headers) + " |");

            // Separator row
            sb.AppendLine("|" + string.Join("|", _headers.Select(_ => "------")) + "|");

            // Data rows
            foreach (AnchorItem anchor in anchors)
            {
                IEnumerable<string> values = GetRowValues(anchor).Select(EscapeMarkdownCell);
                sb.AppendLine("| " + string.Join(" | ", values) + " |");
            }

            sb.AppendLine();
            sb.AppendLine($"*Exported from Comment Studio — {anchors.Count} anchor{(anchors.Count == 1 ? "" : "s")}*");

            return sb.ToString();
        }

        /// <summary>
        /// Exports anchors as JSON.
        /// </summary>
        private static string ExportAsJson(List<AnchorItem> anchors)
        {
            var sb = new StringBuilder();

            sb.AppendLine("{");
            sb.AppendLine($"  \"exportedAt\": \"{DateTime.UtcNow:O}\",");
            sb.AppendLine($"  \"count\": {anchors.Count},");
            sb.AppendLine("  \"anchors\": [");

            for (var i = 0; i < anchors.Count; i++)
            {
                AnchorItem anchor = anchors[i];
                var comma = i < anchors.Count - 1 ? "," : "";

                sb.AppendLine("    {");
                sb.AppendLine($"      \"type\": {JsonEscape(anchor.TypeDisplayName)},");
                sb.AppendLine($"      \"message\": {JsonEscape(anchor.Message)},");
                sb.AppendLine($"      \"file\": {JsonEscape(anchor.FileName)},");
                sb.AppendLine($"      \"path\": {JsonEscape(anchor.FilePath)},");
                sb.AppendLine($"      \"line\": {anchor.LineNumber},");
                sb.AppendLine($"      \"project\": {JsonEscape(anchor.Project)},");
                sb.AppendLine($"      \"owner\": {JsonEscape(anchor.Owner)},");
                sb.AppendLine($"      \"issue\": {JsonEscape(anchor.IssueReference)},");
                sb.AppendLine($"      \"anchorId\": {JsonEscape(anchor.AnchorId)}");
                sb.AppendLine($"    }}{comma}");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Gets the file filter string for the save file dialog based on format.
        /// </summary>
        public static string GetFileFilter(AnchorExportFormat format)
        {
            return format switch
            {
                AnchorExportFormat.Tsv => "Tab-Separated Values (*.tsv)|*.tsv",
                AnchorExportFormat.Csv => "Comma-Separated Values (*.csv)|*.csv",
                AnchorExportFormat.Markdown => "Markdown (*.md)|*.md",
                AnchorExportFormat.Json => "JSON (*.json)|*.json",
                _ => "All Files (*.*)|*.*"
            };
        }

        /// <summary>
        /// Gets the combined file filter for all supported formats.
        /// </summary>
        public static string GetAllFormatsFileFilter()
        {
            return "Tab-Separated Values (*.tsv)|*.tsv|" +
                   "Comma-Separated Values (*.csv)|*.csv|" +
                   "Markdown (*.md)|*.md|" +
                   "JSON (*.json)|*.json";
        }

        /// <summary>
        /// Gets the export format from a file extension.
        /// </summary>
        public static AnchorExportFormat GetFormatFromExtension(string extension)
        {
            return extension?.ToLowerInvariant() switch
            {
                ".tsv" => AnchorExportFormat.Tsv,
                ".csv" => AnchorExportFormat.Csv,
                ".md" => AnchorExportFormat.Markdown,
                ".json" => AnchorExportFormat.Json,
                _ => AnchorExportFormat.Tsv
            };
        }

        private static string[] GetRowValues(AnchorItem anchor)
        {
            return
            [
                anchor.TypeDisplayName ?? "",
                anchor.Message ?? "",
                anchor.FileName ?? "",
                anchor.FilePath ?? "",
                anchor.LineNumber.ToString(),
                anchor.Project ?? "",
                anchor.Owner ?? "",
                anchor.IssueReference ?? "",
                anchor.AnchorId ?? ""
            ];
        }

        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return "";
            }

            // If field contains comma, quote, or newline, wrap in quotes and escape internal quotes
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }

        private static string EscapeMarkdownCell(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            // Escape pipe characters in Markdown tables
            return value.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
        }

        private static string JsonEscape(string value)
        {
            if (value == null)
            {
                return "null";
            }

            var escaped = value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");

            return $"\"{escaped}\"";
        }
    }
}
