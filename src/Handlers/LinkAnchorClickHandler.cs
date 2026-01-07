using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommentsVS.Services;
using CommentsVS.ToolWindows;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Handlers
{
    [Export(typeof(IMouseProcessorProvider))]
    [Name("LinkAnchorClickHandler")]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class LinkAnchorMouseProcessorProvider : IMouseProcessorProvider
    {
        public IMouseProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            return wpfTextView.Properties.GetOrCreateSingletonProperty(
                () => new LinkAnchorMouseProcessor(wpfTextView));
        }
    }

    /// <summary>
    /// Handles mouse clicks on LINK anchors to navigate to the target file/line/anchor.
    /// </summary>
    internal sealed class LinkAnchorMouseProcessor(IWpfTextView textView) : MouseProcessorBase
    {
        private string _currentFilePath;
        private bool _filePathInitialized;

        public override void PreprocessMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Check for Ctrl+Click
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            // Initialize file path lazily
            if (!_filePathInitialized)
            {
                InitializeFilePath();
            }

            // Get the position under the mouse
            SnapshotPoint? position = GetMousePosition(e);
            if (position == null)
            {
                return;
            }

            // Check if we clicked on a LINK reference
            LinkAnchorInfo link = GetLinkAtPosition(position.Value);
            if (link != null)
            {
                NavigateToLink(link);
                e.Handled = true;
            }
        }

        private SnapshotPoint? GetMousePosition(MouseButtonEventArgs e)
        {
            Point point = e.GetPosition(textView.VisualElement);
            ITextViewLine line = textView.TextViewLines.GetTextViewLineContainingYCoordinate(point.Y + textView.ViewportTop);

            if (line == null)
            {
                return null;
            }

            return line.GetBufferPositionFromXCoordinate(point.X + textView.ViewportLeft);
        }

        private LinkAnchorInfo GetLinkAtPosition(SnapshotPoint position)
        {
            ITextSnapshotLine line = position.GetContainingLine();
            var lineText = line.GetText();

            // Check if this line is a comment
            if (!LanguageCommentStyle.IsCommentLine(lineText))
            {
                return null;
            }

            var positionInLine = position.Position - line.Start.Position;
            return LinkAnchorParser.GetLinkAtPosition(lineText, positionInLine);
        }

        private void NavigateToLink(LinkAnchorInfo link)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Run navigation asynchronously to avoid blocking UI thread on file I/O
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                string targetPath;
                var targetLine = 0;
                int? targetEndLine = null;

                if (link.IsLocalAnchor)
                {
                    // Local anchor - search in current file
                    targetPath = _currentFilePath;
                    targetLine = await FindAnchorLineAsync(_currentFilePath, link.AnchorName).ConfigureAwait(false);
                }
                else
                {
                    // Resolve the file path (this is fast, doesn't need async)
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var resolver = new FilePathResolver(_currentFilePath);
                    if (!resolver.TryResolve(link.FilePath, out targetPath))
                    {
                        // File not found - show message
                        ShowStatusMessage($"File not found: {link.FilePath}");
                        return;
                    }

                    // Determine target line
                    if (link.HasLineNumber)
                    {
                        targetLine = link.LineNumber.Value;
                        if (link.HasLineRange)
                        {
                            targetEndLine = link.EndLineNumber.Value;
                        }
                    }
                    else if (link.HasAnchor)
                    {
                        targetLine = await FindAnchorLineAsync(targetPath, link.AnchorName).ConfigureAwait(false);
                    }
                }

                // Navigate to the target
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                OpenFileAtLine(targetPath, targetLine, targetEndLine);
            }).FireAndForget();
        }

        private async Task<int> FindAnchorLineAsync(string filePath, string anchorName)
        {
            if (string.IsNullOrEmpty(anchorName))
            {
                return 0;
            }

            // First, check the solution anchor cache via the tool window
            CodeAnchorsToolWindow toolWindow = CodeAnchorsToolWindow.Instance;
            if (toolWindow?.Cache != null)
            {
                System.Collections.Generic.IReadOnlyList<AnchorItem> anchors = toolWindow.Cache.GetAnchorsForFile(filePath);
                AnchorItem matchingAnchor = anchors.FirstOrDefault(a =>
                    a.AnchorType == AnchorType.Anchor &&
                    string.Equals(a.AnchorId, anchorName, StringComparison.OrdinalIgnoreCase));

                if (matchingAnchor != null)
                {
                    return matchingAnchor.LineNumber;
                }
            }

            // Fallback: scan the file directly for the anchor on a background thread
            if (File.Exists(filePath))
            {
                try
                {
                    // Read file asynchronously on background thread
                    var lines = await Task.Run(() => File.ReadAllLines(filePath)).ConfigureAwait(false);
                    var anchorPattern = $"ANCHOR({anchorName})";
                    var anchorPatternAlt = $"ANCHOR[id={anchorName}]";

                    for (var i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (line.IndexOf(anchorPattern, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            line.IndexOf(anchorPatternAlt, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return i + 1; // 1-based line number
                        }
                    }
                }
                catch (Exception ex)
                {
                    ex.Log();
                }
            }

            return 0;
        }

        /// <summary>
        /// File extensions that should be opened with the default system application
        /// instead of Visual Studio's text editor.
        /// </summary>
        private static readonly HashSet<string> _nonTextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".tiff", ".tif", ".webp",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".zip", ".rar", ".7z", ".tar", ".gz",
            ".exe", ".dll", ".pdb",
            ".mp3", ".mp4", ".wav", ".avi", ".mov", ".mkv"
        };

        private void OpenFileAtLine(string filePath, int lineNumber, int? endLineNumber = null)
        {
            // Check if this is a non-text file that should be opened externally
            var extension = System.IO.Path.GetExtension(filePath);
            if (_nonTextExtensions.Contains(extension))
            {
                OpenFileExternally(filePath);
                return;
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    // Use toolkit to open the document
                    DocumentView docView = await VS.Documents.OpenAsync(filePath);
                    if (docView?.TextView == null)
                    {
                        return;
                    }

                    // Navigate to line if specified
                    if (lineNumber > 0)
                    {
                        ITextSnapshot snapshot = docView.TextBuffer.CurrentSnapshot;
                        var targetLine = Math.Min(lineNumber - 1, snapshot.LineCount - 1);

                        if (endLineNumber.HasValue && endLineNumber.Value > lineNumber)
                        {
                            // Select the range from start line to end line
                            var endLine = Math.Min(endLineNumber.Value - 1, snapshot.LineCount - 1);

                            ITextSnapshotLine startSnapshotLine = snapshot.GetLineFromLineNumber(targetLine);
                            ITextSnapshotLine endSnapshotLine = snapshot.GetLineFromLineNumber(endLine);

                            var selectionSpan = new SnapshotSpan(startSnapshotLine.Start, endSnapshotLine.End);
                            docView.TextView.Selection.Select(selectionSpan, isReversed: false);
                            docView.TextView.ViewScroller.EnsureSpanVisible(selectionSpan, EnsureSpanVisibleOptions.AlwaysCenter);
                        }
                        else
                        {
                            // Single line - just position caret
                            ITextSnapshotLine snapshotLine = snapshot.GetLineFromLineNumber(targetLine);
                            docView.TextView.Caret.MoveTo(snapshotLine.Start);
                            docView.TextView.ViewScroller.EnsureSpanVisible(
                                new SnapshotSpan(snapshotLine.Start, 0),
                                EnsureSpanVisibleOptions.AlwaysCenter);
                        }
                    }
                }
                catch (Exception ex) when (ex.HResult == unchecked((int)0x8007006E) || ex is System.IO.FileLoadException)
                {
                    // File cannot be opened in VS text editor - open externally
                    OpenFileExternally(filePath);
                }
            }).FireAndForget();
        }

        private static void OpenFileExternally(string filePath)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch
            {
                ShowStatusMessage($"Cannot open file: {filePath}");
            }
        }

        private static void ShowStatusMessage(string message)
        {
            VS.StatusBar.ShowMessageAsync(message).FireAndForget();
        }

        private void InitializeFilePath()
        {
            _filePathInitialized = true;

            if (textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                _currentFilePath = document.FilePath;
            }
        }
    }
}
