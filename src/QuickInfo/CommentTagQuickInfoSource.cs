using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

        public Task<QuickInfoItem> GetQuickInfoItemAsync(
            IAsyncQuickInfoSession session,
            CancellationToken cancellationToken)
        {
            if (!CommentTags.Instance.Enabled)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            SnapshotPoint? triggerPoint = session.GetTriggerPoint(textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            ITextSnapshotLine line = triggerPoint.Value.GetContainingLine();
            var lineText = line.GetText();

            // Check if this line is a comment
            if (!_commentLineRegex.IsMatch(lineText))
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            var positionInLine = triggerPoint.Value.Position - line.Start.Position;

            // Find comment tags in the line
            foreach (Match match in _commentTagRegex.Matches(lineText))
            {
                // Check if the trigger point is within this match
                if (positionInLine >= match.Index && positionInLine <= match.Index + match.Length)
                {
                    var tag = match.Groups["tag"].Value.ToUpperInvariant();
                    var description = GetTagDescription(tag);

                    if (!string.IsNullOrEmpty(description))
                    {
                        var span = new SnapshotSpan(line.Start + match.Index, match.Length);
                        ITrackingSpan trackingSpan = textBuffer.CurrentSnapshot.CreateTrackingSpan(
                            span, SpanTrackingMode.EdgeInclusive);

                        return Task.FromResult(new QuickInfoItem(trackingSpan, description));
                    }
                }
            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        private static string GetTagDescription(string tag)
        {
            return tag switch
            {
                "TODO" => "TODO - Task to be completed\n\n" +
                                           "Marks code that needs to be implemented or completed later. " +
                                           "Use for planned features, missing functionality, or deferred work.",
                "HACK" => "HACK - Temporary workaround\n\n" +
                                           "Indicates a quick fix or workaround that should be replaced with a proper solution. " +
                                           "Often used when time constraints force suboptimal code.",
                "NOTE" => "NOTE - Important information\n\n" +
                                           "Highlights important context or explanation about the code. " +
                                           "Use to document non-obvious behavior, assumptions, or decisions.",
                "BUG" => "BUG - Known defect\n\n" +
                                           "Marks a known bug or defect in the code that needs to be fixed. " +
                                           "Include details about the issue and any workarounds.",
                "FIXME" => "FIXME - Code needing repair\n\n" +
                                           "Indicates broken or problematic code that requires fixing. " +
                                           "Similar to BUG but often used for code that works but is incorrect or fragile.",
                "UNDONE" => "UNDONE - Reverted or incomplete change\n\n" +
                                           "Marks code that was started but rolled back or left incomplete. " +
                                           "Use when a feature was partially implemented then abandoned.",
                "REVIEW" => "REVIEW - Needs code review\n\n" +
                                           "Flags code that requires review or discussion before being finalized. " +
                                           "Use for uncertain implementations or code needing a second opinion.",
                _ => null,
            };
        }

        public void Dispose()
        {
        }
    }
}
