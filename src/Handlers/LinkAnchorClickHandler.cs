using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommentsVS.Services;
using CommentsVS.ToolWindows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.TextManager.Interop;
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
    internal sealed class LinkAnchorMouseProcessor : MouseProcessorBase
    {
        private readonly IWpfTextView _textView;
        private string _currentFilePath;
        private bool _filePathInitialized;

        public LinkAnchorMouseProcessor(IWpfTextView textView)
        {
            _textView = textView;
        }

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
            Point point = e.GetPosition(_textView.VisualElement);
            ITextViewLine line = _textView.TextViewLines.GetTextViewLineContainingYCoordinate(point.Y + _textView.ViewportTop);

            if (line == null)
            {
                return null;
            }

            return line.GetBufferPositionFromXCoordinate(point.X + _textView.ViewportLeft);
        }

        private LinkAnchorInfo GetLinkAtPosition(SnapshotPoint position)
        {
            ITextSnapshotLine line = position.GetContainingLine();
            string lineText = line.GetText();

            // Check if this line is a comment
            if (!LanguageCommentStyle.IsCommentLine(lineText))
            {
                return null;
            }

            int positionInLine = position.Position - line.Start.Position;
            return LinkAnchorParser.GetLinkAtPosition(lineText, positionInLine);
        }

        private void NavigateToLink(LinkAnchorInfo link)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string targetPath;
            int targetLine = 0;
            int? targetEndLine = null;

            if (link.IsLocalAnchor)
            {
                // Local anchor - search in current file
                targetPath = _currentFilePath;
                targetLine = FindAnchorLine(_currentFilePath, link.AnchorName);
            }
            else
            {
                // Resolve the file path
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
                    targetLine = FindAnchorLine(targetPath, link.AnchorName);
                }
            }

            // Navigate to the target
            OpenFileAtLine(targetPath, targetLine, targetEndLine);
        }

        private int FindAnchorLine(string filePath, string anchorName)
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

            // Fallback: scan the file directly for the anchor
            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(filePath);
                    string anchorPattern = $"ANCHOR({anchorName})";
                    string anchorPatternAlt = $"ANCHOR[id={anchorName}]";

                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        if (line.IndexOf(anchorPattern, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            line.IndexOf(anchorPatternAlt, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return i + 1; // 1-based line number
                        }
                    }
                }
                catch
                {
                    // Ignore file read errors
                }
            }

            return 0;
        }

        private void OpenFileAtLine(string filePath, int lineNumber, int? endLineNumber = null)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Open the file
                VsShellUtilities.OpenDocument(
                    ServiceProvider.GlobalProvider,
                    filePath,
                    Guid.Empty,
                    out _,
                    out _,
                    out IVsWindowFrame windowFrame,
                    out IVsTextView vsTextView);

                if (windowFrame != null)
                {
                    windowFrame.Show();
                }

                // Navigate to line if specified
                if (vsTextView != null && lineNumber > 0)
                {
                    if (endLineNumber.HasValue && endLineNumber.Value > lineNumber)
                    {
                        // Select the range from start line to end line
                        int startLine = lineNumber - 1; // 0-based
                        int endLine = endLineNumber.Value - 1; // 0-based

                        // Get the length of the last line to select to end of it
                        vsTextView.GetBuffer(out IVsTextLines textLines);
                        int endLineLength = 0;
                        if (textLines != null)
                        {
                            textLines.GetLengthOfLine(endLine, out endLineLength);
                        }

                        // Set selection from start of first line to end of last line
                        vsTextView.SetSelection(startLine, 0, endLine, endLineLength);
                        vsTextView.CenterLines(startLine, endLine - startLine + 1);
                    }
                    else
                    {
                        // Single line - just position caret
                        vsTextView.SetCaretPos(lineNumber - 1, 0);
                        vsTextView.CenterLines(lineNumber - 1, 1);
                    }
                }
            });
        }

        private static void ShowStatusMessage(string message)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (ServiceProvider.GlobalProvider.GetService(typeof(SVsStatusbar)) is IVsStatusbar statusBar)
                {
                    statusBar.SetText(message);
                }
            });
        }

        private void InitializeFilePath()
        {
            _filePathInitialized = true;

            if (_textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                _currentFilePath = document.FilePath;
            }
        }
    }
}
