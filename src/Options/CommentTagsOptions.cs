using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CommentsVS.Options
{
    internal partial class OptionsProvider
    {
        [ComVisible(true)]
        public class CommentTagsOptions : BaseOptionPage<CommentTags> { }
    }

    /// <summary>
    /// Settings for comment tag highlighting (TODO, HACK, NOTE, etc.).
    /// </summary>
    public class CommentTags : BaseOptionModel<CommentTags>
    {
        [Category("Comment Tags")]
        [DisplayName("Enable Comment Tag Highlighting")]
        [Description("When enabled, comment tags like TODO, HACK, NOTE, BUG, FIXME, UNDONE, and REVIEW will be highlighted. Colors can be customized in Tools > Options > Environment > Fonts and Colors.")]
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;
    }
}
