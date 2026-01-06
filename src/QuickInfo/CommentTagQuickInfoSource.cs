using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
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

        private static readonly Regex _metadataRegex = new(
            @"(?<tag>TODO|HACK|NOTE|BUG|FIXME|UNDONE|REVIEW)(?:\s*(?:\((?<metaParen>[^)]*)\)|\[(?<metaBracket>[^\]]*)\]))?\s*: ?",
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
                        var metadata = TryParseMetadata(lineText, match);
                        var content = new CommentTagQuickInfoContent(title, description, metadata);

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

        private static IReadOnlyList<CommentTagMetadataItem>? TryParseMetadata(string lineText, Match tagMatch)
        {
            // Attempt to find a prefixed metadata section immediately after the tag token.
            // Examples:
            //   TODO(@user): message
            //   TODO[#1234]: message
            //   TODO(2026-02-01): message
            //   TODO(@user, #1234, 2026-02-01): message
            //   TODO[@user #1234 2026-02-01]: message

            var match = _metadataRegex.Match(lineText, tagMatch.Index);
            if (!match.Success || match.Index != tagMatch.Index)
            {
                return null;
            }

            var meta = match.Groups["metaParen"].Success ? match.Groups["metaParen"].Value :
                       match.Groups["metaBracket"].Success ? match.Groups["metaBracket"].Value :
                       null;

            if (string.IsNullOrWhiteSpace(meta))
            {
                return null;
            }

            return ParseMetadataTokens(meta);
        }

        private static IReadOnlyList<CommentTagMetadataItem> ParseMetadataTokens(string metadata)
        {
            var items = new List<CommentTagMetadataItem>();

            foreach (var token in Regex.Split(metadata, @"[\s,;]+"))
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (token[0] == '@' && token.Length > 1)
                {
                    items.Add(new CommentTagMetadataItem(CommentTagMetadataKind.Owner, token.Substring(1)));
                    continue;
                }

                if (token[0] == '#')
                {
                    var id = token.Length > 1 ? token.Substring(1) : string.Empty;
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        items.Add(new CommentTagMetadataItem(CommentTagMetadataKind.Issue, id));
                    }

                    continue;
                }

                if (DateTime.TryParseExact(token, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    items.Add(new CommentTagMetadataItem(CommentTagMetadataKind.DueDate, token));
                }
            }

            return items;
        }
    }

    /// <summary>
    /// Interactive QuickInfo content for comment tags with a clickable link to open the Task List.
    /// </summary>
    internal sealed class CommentTagQuickInfoContent : StackPanel, IInteractiveQuickInfoContent
    {
        public CommentTagQuickInfoContent(string title, string description, IReadOnlyList<CommentTagMetadataItem>? metadata)
        {
            // Title
            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Children.Add(titleBlock);

            if (metadata is { Count: > 0 })
            {
                var metadataBlock = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 6),
                    MaxWidth = 400
                };

                LineBreak trailingLineBreak = null;

                foreach (var item in metadata)
                {
                    var label = item.Kind switch
                    {
                        CommentTagMetadataKind.Owner => "Owner",
                        CommentTagMetadataKind.Issue => "Issue",
                        CommentTagMetadataKind.DueDate => "Due",
                        _ => "Meta",
                    };

                    metadataBlock.Inlines.Add(new Run($"{label}: ") { FontWeight = FontWeights.SemiBold });
                    metadataBlock.Inlines.Add(new Run(item.Value));
                    trailingLineBreak = new LineBreak();
                    metadataBlock.Inlines.Add(trailingLineBreak);
                }

                // Remove the last line break.
                if (trailingLineBreak != null)
                {
                    metadataBlock.Inlines.Remove(trailingLineBreak);
                }

                Children.Add(metadataBlock);
            }

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
            VS.Commands.ExecuteAsync("View.TaskList").FireAndForget();
        }
    }

    internal enum CommentTagMetadataKind
    {
        Owner,
        Issue,
        DueDate,
    }

    internal readonly record struct CommentTagMetadataItem(CommentTagMetadataKind Kind, string Value);
}
