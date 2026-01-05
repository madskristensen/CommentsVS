using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CommentsVS.Options;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.QuickInfo
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name("CommentTagQuickInfo")]
    [ContentType("code")]
    [Order(Before = "Default Quick Info Presenter")]
    internal sealed class CommentTagQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(
                () => new CommentTagQuickInfoSource(textBuffer));
        }
    }

    /// <summary>
    /// Provides hover tooltips for comment tags (TODO, HACK, NOTE, etc.) explaining their semantic meaning.
    /// </summary>
    internal sealed class CommentTagQuickInfoSource(ITextBuffer textBuffer) : IAsyncQuickInfoSource
    {
        private static readonly Regex _commentTagRegex = new(
            @"\b(?<tag>TODO|HACK|NOTE|BUG|FIXME|UNDONE|REVIEW)\b:?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _commentLineRegex = new(
            @"^\s*(//|/\*|\*|')",
            RegexOptions.Compiled);

        public async Task<QuickInfoItem> GetQuickInfoItemAsync(
            IAsyncQuickInfoSession session,
            CancellationToken cancellationToken)
        {
            if (!General.Instance.EnableCommentTagHighlighting)
            {
                return null;
            }

            SnapshotPoint? triggerPoint = session.GetTriggerPoint(textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
            {
                return null;
            }

            ITextSnapshotLine line = triggerPoint.Value.GetContainingLine();
            var lineText = line.GetText();

            // Check if this line is a comment
            if (!_commentLineRegex.IsMatch(lineText))
            {
                return null;
            }

            var positionInLine = triggerPoint.Value.Position - line.Start.Position;

            // Find comment tags in the line
            foreach (Match match in _commentTagRegex.Matches(lineText))
            {
                // Check if the trigger point is within this match
                if (positionInLine >= match.Index && positionInLine <= match.Index + match.Length)
                {
                    var tag = match.Groups["tag"].Value.ToUpperInvariant();
                    (var title, var description) = GetTagDescription(tag);

                    if (!string.IsNullOrEmpty(description))
                    {
                        var span = new SnapshotSpan(line.Start + match.Index, match.Length);
                        ITrackingSpan trackingSpan = textBuffer.CurrentSnapshot.CreateTrackingSpan(
                            span, SpanTrackingMode.EdgeInclusive);

                        // Switch to UI thread to create WPF elements
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                        var content = new CommentTagQuickInfoContent(title, description);

                        return new QuickInfoItem(trackingSpan, content);
                    }
                }
            }

            return null;
        }

        private static (string Title, string Description) GetTagDescription(string tag)
        {
            return tag switch
            {
                "TODO" => ("TODO - Task to be completed",
                           "Marks code that needs to be implemented or completed later. " +
                           "Use for planned features, missing functionality, or deferred work."),
                "HACK" => ("HACK - Temporary workaround",
                           "Indicates a quick fix or workaround that should be replaced with a proper solution. " +
                           "Often used when time constraints force suboptimal code."),
                "NOTE" => ("NOTE - Important information",
                           "Highlights important context or explanation about the code. " +
                           "Use to document non-obvious behavior, assumptions, or decisions."),
                "BUG" => ("BUG - Known defect",
                          "Marks a known bug or defect in the code that needs to be fixed. " +
                          "Include details about the issue and any workarounds."),
                "FIXME" => ("FIXME - Code needing repair",
                            "Indicates broken or problematic code that requires fixing. " +
                            "Similar to BUG but often used for code that works but is incorrect or fragile."),
                "UNDONE" => ("UNDONE - Reverted or incomplete change",
                             "Marks code that was started but rolled back or left incomplete. " +
                             "Use when a feature was partially implemented then abandoned."),
                "REVIEW" => ("REVIEW - Needs code review",
                             "Flags code that requires review or discussion before being finalized. " +
                             "Use for uncertain implementations or code needing a second opinion."),
                _ => (null, null),
            };
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Interactive QuickInfo content for comment tags with a clickable link to open the Task List.
    /// </summary>
    internal sealed class CommentTagQuickInfoContent : StackPanel, IInteractiveQuickInfoContent
    {
        public CommentTagQuickInfoContent(string title, string description)
        {
            // Title
            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Children.Add(titleBlock);

            // Description
            var descriptionBlock = new TextBlock
            {
                Text = description,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400
            };
            Children.Add(descriptionBlock);

            // Link to Task List
            var linkBlock = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0)
            };

            var hyperlink = new Hyperlink(new Run("Open Task List"));
            hyperlink.Click += OnTaskListLinkClicked;
            linkBlock.Inlines.Add(hyperlink);
            Children.Add(linkBlock);
        }

        public bool KeepQuickInfoOpen { get; set; }

        public bool IsMouseOverAggregated { get; set; }

        private static void OnTaskListLinkClicked(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await VS.Commands.ExecuteAsync("View.TaskList");
            });
        }
    }
}
