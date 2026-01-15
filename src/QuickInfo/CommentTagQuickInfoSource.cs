using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommentsVS.Options;
using CommentsVS.Services;
using CommentsVS.ToolWindows;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Utilities;


namespace CommentsVS.QuickInfo
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name("CommentTagQuickInfo")]
    [ContentType(SupportedContentTypes.Code)]
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

            var positionInLine = triggerPoint.Value.Position - line.Start.Position;

            // Find the comment portion(s) in the line
            IEnumerable<(int Start, int Length)> commentSpans = CommentSpanHelper.FindCommentSpans(lineText);

            // Check if trigger point is within any comment span
            var isInComment = false;
            var commentText = lineText;
            var commentStartInLine = 0;

            foreach ((var start, var length) in commentSpans)
            {
                if (positionInLine >= start && positionInLine < start + length)
                {
                    isInComment = true;
                    commentText = lineText.Substring(start, length);
                    commentStartInLine = start;
                    break;
                }
            }

            if (!isInComment)
            {
                return null;
            }

            // Find comment tags in the comment portion
            foreach (System.Text.RegularExpressions.Match match in CommentPatterns.CommentTagRegex.Matches(commentText))
            {
                var matchStartInLine = commentStartInLine + match.Index;
                var matchEndInLine = matchStartInLine + match.Length;

                // Check if the trigger point is within this match (adjusted for comment start)
                if (positionInLine >= matchStartInLine && positionInLine <= matchEndInLine)
                {
                    var tag = match.Groups["tag"].Value.ToUpperInvariant();
                    (var title, var description) = GetTagDescription(tag);

                    if (!string.IsNullOrEmpty(description))
                    {
                        var span = new SnapshotSpan(line.Start + matchStartInLine, match.Length);
                        ITrackingSpan trackingSpan = textBuffer.CurrentSnapshot.CreateTrackingSpan(
                            span, SpanTrackingMode.EdgeInclusive);

                        // Parse metadata from the original line text, adjusting match position
                        IReadOnlyList<CommentTagMetadataItem> metadata = TryParseMetadata(lineText, commentStartInLine, match);
                        GitRepositoryInfo repoInfo = await GetRepoInfoAsync().ConfigureAwait(false);
                        ContainerElement content = CreateQuickInfoContent(tag, title, description, metadata, repoInfo);

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
                "ANCHOR" => ("ANCHOR - Named navigation point",
                             "Creates a named location in your code that can be referenced from other files. " +
                             "Use with LINK to build a navigation system within your codebase."),
                "LINK" => ("LINK - File or anchor reference",
                           "Creates a clickable link to another file, line number, or named anchor. " +
                           "Ctrl+Click to navigate to the target location."),
                _ => (null, null),
            };
        }

        public void Dispose()
        {
        }

        private async Task<GitRepositoryInfo> GetRepoInfoAsync()
        {
            if (textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                return await GitRepositoryService.GetRepositoryInfoAsync(document.FilePath).ConfigureAwait(false);
            }

            return null;
        }

        private static ContainerElement CreateQuickInfoContent(string tag, string title, string description, IReadOnlyList<CommentTagMetadataItem> metadata, GitRepositoryInfo repoInfo)
        {
            const int MaxLineLength = 60;

            var elements = new List<object>
            {
                new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, title, ClassifiedTextRunStyle.Bold))
            };

            if (metadata != null && metadata.Count > 0)
            {
                // Blank line before metadata
                elements.Add(new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, string.Empty)));

                foreach (CommentTagMetadataItem item in metadata)
                {
                    var label = item.Kind switch
                    {
                        CommentTagMetadataKind.Owner => "Owner",
                        CommentTagMetadataKind.Issue => "Issue",
                        CommentTagMetadataKind.DueDate => "Due",
                        _ => "Meta",
                    };

                    // For issue references, make the value clickable if we can resolve the URL
                    if (item.Kind == CommentTagMetadataKind.Issue &&
                        repoInfo != null &&
                        int.TryParse(item.Value, out var issueNumber))
                    {
                        var issueUrl = repoInfo.GetIssueUrl(issueNumber);
                        if (!string.IsNullOrEmpty(issueUrl))
                        {
                            elements.Add(new ClassifiedTextElement(
                                new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, $"{label}: ", ClassifiedTextRunStyle.Bold),
                                new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, item.Value, () =>
                                {
                                    Process.Start(new ProcessStartInfo(issueUrl) { UseShellExecute = true });
                                })));
                            continue;
                        }
                    }

                    elements.Add(new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, $"{label}: ", ClassifiedTextRunStyle.Bold),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, item.Value)));
                }
            }

            // Blank line before description
            elements.Add(new ClassifiedTextElement(
                new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, string.Empty)));

            // Wrap description text at max line length for readability
            foreach (var line in WrapText(description, MaxLineLength))
            {
                elements.Add(new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, line)));
            }

            // Add clickable action link based on tag type (skip for LINK which is just navigation syntax)
            if (tag != "LINK")
            {
                // Blank line before link
                elements.Add(new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, string.Empty)));

                elements.Add(new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, "Open Code Anchors", () =>
                    {
                        CodeAnchorsToolWindow.ShowAsync().FireAndForget();
                    })));
            }




            return new ContainerElement(ContainerElementStyle.Stacked, elements);
        }

        private static IEnumerable<string> WrapText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                yield return text ?? string.Empty;
                yield break;
            }

            var words = text.Split(' ');
            var currentLine = string.Empty;

            foreach (var word in words)
            {
                if (currentLine.Length == 0)
                {
                    currentLine = word;
                }
                else if (currentLine.Length + 1 + word.Length <= maxLength)
                {
                    currentLine += " " + word;
                }
                else
                {
                    yield return currentLine;
                    currentLine = word;
                }
            }

            if (currentLine.Length > 0)
            {
                yield return currentLine;
            }
        }

        private static IReadOnlyList<CommentTagMetadataItem> TryParseMetadata(string lineText, int commentStartInLine, Match tagMatch)
        {
            // Attempt to find a prefixed metadata section immediately after the tag token.
            // Examples:
            //   TODO(@user): message
            //   TODO[#1234]: message
            //   TODO(2026-02-01): message
            //   TODO(@user, #1234, 2026-02-01): message
            //   TODO[@user #1234 2026-02-01]: message

            // Adjust match position to be relative to full line
            var adjustedIndex = commentStartInLine + tagMatch.Index;
            System.Text.RegularExpressions.Match match = CommentPatterns.MetadataParseRegex.Match(lineText, adjustedIndex);
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

            internal enum CommentTagMetadataKind
    {
        Owner,
        Issue,
        DueDate,
    }

    internal readonly record struct CommentTagMetadataItem(CommentTagMetadataKind Kind, string Value);
}
