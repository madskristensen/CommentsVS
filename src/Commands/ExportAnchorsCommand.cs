using System.Collections.Generic;
using System.IO;
using System.Windows;
using CommentsVS.Services;
using CommentsVS.ToolWindows;
using Microsoft.Win32;

namespace CommentsVS.Commands
{
    /// <summary>
    /// Command to export anchors as TSV (tab-separated values) to clipboard.
    /// </summary>
    [Command(PackageIds.ExportAsTsv)]
    internal sealed class ExportAsTsvCommand : BaseCommand<ExportAsTsvCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ExportAnchorsHelper.CopyToClipboardAsync(AnchorExportFormat.Tsv);
        }
    }

    /// <summary>
    /// Command to export anchors as CSV to clipboard.
    /// </summary>
    [Command(PackageIds.ExportAsCsv)]
    internal sealed class ExportAsCsvCommand : BaseCommand<ExportAsCsvCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ExportAnchorsHelper.CopyToClipboardAsync(AnchorExportFormat.Csv);
        }
    }

    /// <summary>
    /// Command to export anchors as Markdown to clipboard.
    /// </summary>
    [Command(PackageIds.ExportAsMarkdown)]
    internal sealed class ExportAsMarkdownCommand : BaseCommand<ExportAsMarkdownCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ExportAnchorsHelper.CopyToClipboardAsync(AnchorExportFormat.Markdown);
        }
    }

    /// <summary>
    /// Command to export anchors as JSON to clipboard.
    /// </summary>
    [Command(PackageIds.ExportAsJson)]
    internal sealed class ExportAsJsonCommand : BaseCommand<ExportAsJsonCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ExportAnchorsHelper.CopyToClipboardAsync(AnchorExportFormat.Json);
        }
    }

    /// <summary>
    /// Command to export anchors to a file with a save dialog.
    /// </summary>
    [Command(PackageIds.ExportToFile)]
    internal sealed class ExportToFileCommand : BaseCommand<ExportToFileCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ExportAnchorsHelper.ExportToFileAsync();
        }
    }

    /// <summary>
    /// Helper class for export operations.
    /// </summary>
    internal static class ExportAnchorsHelper
    {
        /// <summary>
        /// Copies the filtered anchors to clipboard in the specified format.
        /// </summary>
        public static async Task CopyToClipboardAsync(AnchorExportFormat format)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IReadOnlyList<AnchorItem> anchors = GetFilteredAnchors();
            if (anchors == null || anchors.Count == 0)
            {
                await VS.StatusBar.ShowMessageAsync("No anchors to export");
                return;
            }

            var content = AnchorExporter.Export(anchors, format);
            Clipboard.SetText(content);

            var formatName = format switch
            {
                AnchorExportFormat.Tsv => "TSV",
                AnchorExportFormat.Csv => "CSV",
                AnchorExportFormat.Markdown => "Markdown",
                AnchorExportFormat.Json => "JSON",
                _ => "text"
            };

            await VS.StatusBar.ShowMessageAsync($"Copied {anchors.Count} anchor(s) as {formatName} to clipboard");
        }

        /// <summary>
        /// Exports the filtered anchors to a file using a save dialog.
        /// </summary>
        public static async Task ExportToFileAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IReadOnlyList<AnchorItem> anchors = GetFilteredAnchors();
            if (anchors == null || anchors.Count == 0)
            {
                await VS.StatusBar.ShowMessageAsync("No anchors to export");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Code Anchors",
                Filter = AnchorExporter.GetAllFormatsFileFilter(),
                DefaultExt = ".tsv",
                FileName = "code-anchors"
            };

            if (dialog.ShowDialog() == true)
            {
                var extension = Path.GetExtension(dialog.FileName);
                AnchorExportFormat format = AnchorExporter.GetFormatFromExtension(extension);
                var content = AnchorExporter.Export(anchors, format);

                File.WriteAllText(dialog.FileName, content);

                await VS.StatusBar.ShowMessageAsync($"Exported {anchors.Count} anchor(s) to {dialog.FileName}");
            }
        }

                        private static IReadOnlyList<AnchorItem> GetFilteredAnchors()
                        {
                            ThreadHelper.ThrowIfNotOnUIThread();

                            if (CodeAnchorsToolWindow.Instance?.Control == null)
                            {
                                return null;
                            }

                            return CodeAnchorsToolWindow.Instance.Control.FilteredAnchors;
                        }
                    }
                }
