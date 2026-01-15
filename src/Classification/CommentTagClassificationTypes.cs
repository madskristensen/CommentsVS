namespace CommentsVS.Classification
{
    /// <summary>
    /// Classification type names for comment tags.
    /// </summary>
    internal static class CommentTagClassificationTypes
    {
        public const string Todo = "CommentTag.TODO";
        public const string Hack = "CommentTag.HACK";
        public const string Note = "CommentTag.NOTE";
        public const string Bug = "CommentTag.BUG";
        public const string Fixme = "CommentTag.FIXME";
        public const string Undone = "CommentTag.UNDONE";
        public const string Review = "CommentTag.REVIEW";
        public const string Anchor = "CommentTag.ANCHOR";
        public const string Custom = "CommentTag.Custom";
        public const string Metadata = "CommentTag.Metadata";

                // Prefix-based comment highlighting (Better Comments style)
                public const string PrefixAlert = "CommentPrefix.Alert";
                public const string PrefixQuery = "CommentPrefix.Query";
                public const string PrefixImportant = "CommentPrefix.Important";
                public const string PrefixStrikethrough = "CommentPrefix.Strikethrough";
                public const string PrefixDisabled = "CommentPrefix.Disabled";
                public const string PrefixQuote = "CommentPrefix.Quote";

                // Rendered comment colors (Full/Compact rendering mode)
                public const string RenderedText = "RenderedComment.Text";
                public const string RenderedHeading = "RenderedComment.Heading";
                public const string RenderedCode = "RenderedComment.Code";
                public const string RenderedLink = "RenderedComment.Link";
            }
        }
