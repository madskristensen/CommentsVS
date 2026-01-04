using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using CommentsVS.Options;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Handlers
{
    [Export(typeof(IMouseProcessorProvider))]
    [Name("IssueReferenceClickHandler")]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class IssueReferenceMouseProcessorProvider : IMouseProcessorProvider
    {
        public IMouseProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            return wpfTextView.Properties.GetOrCreateSingletonProperty(
                () => new IssueReferenceMouseProcessor(wpfTextView));
        }
    }

    /// <summary>
    /// Handles mouse clicks on issue references (#123) to open them in the browser.
    /// </summary>
    internal sealed class IssueReferenceMouseProcessor(
        IWpfTextView textView) : MouseProcessorBase
    {
        private GitRepositoryInfo _repoInfo;
        private bool _repoInfoInitialized;

        private static readonly Regex _issueReferenceRegex = new(
            @"#(?<number>\d+)\b",
            RegexOptions.Compiled);

        private static readonly Regex _commentLineRegex = new(
            @"^\s*(//|/\*|\*|')",
            RegexOptions.Compiled);

        public override void PreprocessMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!General.Instance.EnableIssueLinks)
            {
                return;
            }

            // Check for Ctrl+Click
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            // Initialize repo info lazily
            if (!_repoInfoInitialized)
            {
                InitializeRepoInfo();
            }

            if (_repoInfo == null)
            {
                return;
            }

            // Get the position under the mouse
            SnapshotPoint? position = GetMousePosition(e);
            if (position == null)
            {
                return;
            }

            // Check if we clicked on an issue reference
            var url = GetIssueUrlAtPosition(position.Value);
            if (!string.IsNullOrEmpty(url))
            {
                // Open the URL in the default browser
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    e.Handled = true;
                }
                catch
                {
                    // Ignore errors opening browser
                }
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

        private string GetIssueUrlAtPosition(SnapshotPoint position)
        {
            ITextSnapshotLine line = position.GetContainingLine();
            var lineText = line.GetText();

            // Check if this line is a comment
            if (!_commentLineRegex.IsMatch(lineText))
            {
                return null;
            }

            var positionInLine = position.Position - line.Start.Position;

            // Find issue references in the line
            foreach (Match match in _issueReferenceRegex.Matches(lineText))
            {
                // Check if the click position is within this match
                if (positionInLine >= match.Index && positionInLine <= match.Index + match.Length)
                {
                    if (int.TryParse(match.Groups["number"].Value, out var issueNumber))
                    {
                        return _repoInfo.GetIssueUrl(issueNumber);
                    }
                }
            }

            return null;
        }

        private void InitializeRepoInfo()
        {
            _repoInfoInitialized = true;

            if (textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                _repoInfo = GitRepositoryService.GetRepositoryInfo(document.FilePath);
            }
        }
    }
}
