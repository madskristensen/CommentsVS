namespace CommentsVS.Services
{
    /// <summary>
    /// Defines language-specific comment syntax patterns.
    /// </summary>
    public sealed class LanguageCommentStyle
    {
        /// <summary>
        /// Gets the single-line documentation comment prefix (e.g., "///" for C#).
        /// </summary>
        public string SingleLineDocPrefix { get; }

        /// <summary>
        /// Gets the multi-line documentation comment start (e.g., "/**" for C++).
        /// </summary>
        public string MultiLineDocStart { get; }

        /// <summary>
        /// Gets the multi-line documentation comment end (e.g., "*/" for C++).
        /// </summary>
        public string MultiLineDocEnd { get; }

        /// <summary>
        /// Gets the continuation prefix for multi-line comments (e.g., " * " for C++).
        /// </summary>
        public string MultiLineContinuation { get; }

        /// <summary>
        /// Gets the content type name this style applies to.
        /// </summary>
        public string ContentType { get; }

        private LanguageCommentStyle(
            string contentType,
            string singleLineDocPrefix,
            string multiLineDocStart,
            string multiLineDocEnd,
            string multiLineContinuation)
        {
            ContentType = contentType;
            SingleLineDocPrefix = singleLineDocPrefix;
            MultiLineDocStart = multiLineDocStart;
            MultiLineDocEnd = multiLineDocEnd;
            MultiLineContinuation = multiLineContinuation;
        }

        /// <summary>
        /// C# documentation comment style: /// for single-line.
        /// </summary>
        public static LanguageCommentStyle CSharp { get; } = new LanguageCommentStyle(
            contentType: "CSharp",
            singleLineDocPrefix: "///",
            multiLineDocStart: "/**",
            multiLineDocEnd: "*/",
            multiLineContinuation: " * ");

        /// <summary>
        /// Visual Basic documentation comment style: ''' for single-line.
        /// </summary>
        public static LanguageCommentStyle VisualBasic { get; } = new LanguageCommentStyle(
            contentType: "Basic",
            singleLineDocPrefix: "'''",
            multiLineDocStart: null,
            multiLineDocEnd: null,
            multiLineContinuation: null);

        /// <summary>
        /// C++ documentation comment style: /// for single-line, /** */ for multi-line.
        /// </summary>
        public static LanguageCommentStyle Cpp { get; } = new LanguageCommentStyle(
            contentType: "C/C++",
            singleLineDocPrefix: "///",
            multiLineDocStart: "/**",
            multiLineDocEnd: "*/",
            multiLineContinuation: " * ");

        /// <summary>
        /// Gets the appropriate comment style for a given content type.
        /// </summary>
        /// <param name="contentType">The content type name (e.g., "CSharp", "Basic", "C/C++").</param>
        /// <returns>The matching comment style, or null if not supported.</returns>
        public static LanguageCommentStyle GetForContentType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                return null;
            }

            if (contentType.IndexOf("CSharp", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CSharp;
            }

            if (contentType.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return VisualBasic;
            }

            if (contentType.IndexOf("C/C++", StringComparison.OrdinalIgnoreCase) >= 0 ||
                contentType.IndexOf("C++", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Cpp;
            }

            return null;
        }

        /// <summary>
        /// Gets whether multi-line documentation comments are supported.
        /// </summary>
        public bool SupportsMultiLineDoc => !string.IsNullOrEmpty(MultiLineDocStart);
    }
}
