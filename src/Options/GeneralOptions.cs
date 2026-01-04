using System.ComponentModel;
using System.Runtime.InteropServices;
using CommentsVS.Services;

namespace CommentsVS.Options
{
    /// <summary>
    /// Options provider for the CommentsVS extension.
    /// </summary>
    internal partial class OptionsProvider
    {
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General> { }
    }

    /// <summary>
    /// Settings model for XML Doc Comment formatting options.
    /// </summary>
    public class General : BaseOptionModel<General>
    {
        private const string _reflowCategory = "Comment Reflow";

        [Category(_reflowCategory)]
        [DisplayName("Maximum Line Length")]
        [Description("The maximum line length for XML documentation comments before wrapping to a new line. Default is 120.")]
        [DefaultValue(120)]
        public int MaxLineLength { get; set; } = 120;

        [Category(_reflowCategory)]
        [DisplayName("Enable Reflow on Format Document")]
        [Description("When enabled, XML documentation comments will be reflowed when using Format Document (Ctrl+K, Ctrl+D).")]
        [DefaultValue(true)]
        public bool ReflowOnFormatDocument { get; set; } = true;

        [Category(_reflowCategory)]
        [DisplayName("Enable Reflow on Paste")]
        [Description("When enabled, XML documentation comments will be automatically reflowed when pasting text into a comment block.")]
        [DefaultValue(true)]
        public bool ReflowOnPaste { get; set; } = true;

        [Category(_reflowCategory)]
        [DisplayName("Enable Reflow While Typing")]
        [Description("When enabled, XML documentation comments will be automatically reflowed as you type when a line exceeds the maximum length.")]
        [DefaultValue(true)]
        public bool ReflowOnTyping { get; set; } = true;

        [Category(_reflowCategory)]
        [DisplayName("Use Compact Style for Short Summaries")]
        [Description("When enabled, short summaries that fit on one line will use the compact format: /// <summary>Short text.</summary>")]
        [DefaultValue(true)]
        public bool UseCompactStyleForShortSummaries { get; set; } = true;

        [Category(_reflowCategory)]
        [DisplayName("Preserve Blank Lines")]
        [Description("When enabled, blank lines within XML documentation comments (paragraph separators) will be preserved during reflow.")]
        [DefaultValue(true)]
        public bool PreserveBlankLines { get; set; } = true;

        private const string _outliningCategory = "Comment Outlining";


        [Category(_outliningCategory)]
        [DisplayName("Collapse Comments on File Open")]
        [Description("When enabled, XML documentation comments will be automatically collapsed when opening a file.")]
        [DefaultValue(false)]
        public bool CollapseCommentsOnFileOpen { get; set; } = false;

        private const string _linksCategory = "Issue Links";

        [Category(_linksCategory)]
        [DisplayName("Enable Issue Links")]
        [Description("When enabled, issue references like #123 in comments will become clickable links to the issue on GitHub, GitLab, Bitbucket, or Azure DevOps.")]
        [DefaultValue(true)]
        public bool EnableIssueLinks { get; set; } = true;

        private const string _renderingCategory = "Comment Rendering";

        [Category(_renderingCategory)]
        [DisplayName("Enable Rendered Comments")]
        [Description("When enabled, XML documentation comments will be rendered without XML tags for easier reading. Toggle with Ctrl+M, Ctrl+R.")]
        [DefaultValue(false)]
        public bool EnableRenderedComments { get; set; } = false;

        /// <summary>
        /// Creates a CommentReflowEngine configured with the current options.
        /// </summary>
        public CommentReflowEngine CreateReflowEngine()
        {
            return new CommentReflowEngine(
                MaxLineLength,
                UseCompactStyleForShortSummaries,
                PreserveBlankLines);
        }
    }
}
