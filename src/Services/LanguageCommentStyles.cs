using Microsoft.VisualStudio.Utilities;
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
            contentType: ContentTypes.CSharp,
            singleLineDocPrefix: "///",
            multiLineDocStart: "/**",
            multiLineDocEnd: "*/",
            multiLineContinuation: " * ");

        /// <summary>
        /// Visual Basic documentation comment style: ''' for single-line.
        /// </summary>
        public static LanguageCommentStyle VisualBasic { get; } = new LanguageCommentStyle(
            contentType: ContentTypes.VisualBasic,
            singleLineDocPrefix: "'''",
            multiLineDocStart: null,
            multiLineDocEnd: null,
            multiLineContinuation: null);

        /// <summary>
        /// C++ documentation comment style: /// for single-line, /** */ for multi-line.
        /// </summary>
        public static LanguageCommentStyle Cpp { get; } = new LanguageCommentStyle(
            contentType: ContentTypes.CPlusPlus,
            singleLineDocPrefix: "///",
            multiLineDocStart: "/**",
            multiLineDocEnd: "*/",
            multiLineContinuation: " * ");

        /// <summary>
        /// F# documentation comment style: /// for single-line.
        /// </summary>
        public static LanguageCommentStyle FSharp { get; } = new LanguageCommentStyle(
            contentType: "FSharp",
            singleLineDocPrefix: "///",
            multiLineDocStart: null,
            multiLineDocEnd: null,
            multiLineContinuation: null);

        /// <summary>
        /// TypeScript documentation comment style: /// for single-line.
        /// </summary>
        public static LanguageCommentStyle TypeScript { get; } = new LanguageCommentStyle(
            contentType: "TypeScript",
            singleLineDocPrefix: "///",
            multiLineDocStart: null,
            multiLineDocEnd: null,
            multiLineContinuation: null);

        /// <summary>
        /// JavaScript documentation comment style: /// for single-line.
        /// </summary>
        public static LanguageCommentStyle JavaScript { get; } = new LanguageCommentStyle(
            contentType: "JavaScript",
            singleLineDocPrefix: "///",
            multiLineDocStart: null,
            multiLineDocEnd: null,
            multiLineContinuation: null);

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

            if (contentType.IndexOf(ContentTypes.CSharp, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CSharp;
            }

            if (contentType.IndexOf(ContentTypes.VisualBasic, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return VisualBasic;
            }


            if (contentType.IndexOf(ContentTypes.CPlusPlus, StringComparison.OrdinalIgnoreCase) >= 0 ||
                contentType.IndexOf("C++", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Cpp;
            }

            if (contentType.IndexOf(ContentTypes.FSharp, StringComparison.OrdinalIgnoreCase) >= 0 ||
                contentType.IndexOf("FSharp", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FSharp;
            }

            if (contentType.IndexOf("TypeScript", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TypeScript;
            }

            if (contentType.IndexOf("JavaScript", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return JavaScript;
            }

            return null;
        }



        /// <summary>
        /// Gets the appropriate comment style for a given content type.
        /// </summary>
        /// <param name="contentType">The content type.</param>
        /// <returns>The matching comment style, or null if not supported.</returns>
        public static LanguageCommentStyle GetForContentType(IContentType contentType)
        {
            if (contentType == null)
            {
                return null;
            }

            if (contentType.IsOfType("CSharp"))
            {
                return CSharp;
            }

            if (contentType.IsOfType("Basic"))
            {
                return VisualBasic;
            }

            if (contentType.IsOfType("C/C++"))
            {
                return Cpp;
            }

            if (contentType.IsOfType("F#") || contentType.IsOfType("FSharp"))
            {
                return FSharp;
            }

            if (contentType.IsOfType("TypeScript"))
            {
                return TypeScript;
            }

            if (contentType.IsOfType("JavaScript"))
            {
                return JavaScript;
            }

            return null;
        }


        /// <summary>
        /// Gets whether multi-line documentation comments are supported.
        /// </summary>
        public bool SupportsMultiLineDoc => !string.IsNullOrEmpty(MultiLineDocStart);

        /// <summary>
        /// Regex pattern to match comment line prefixes across all supported languages.
        /// Matches: //, /*, *, ' (VB)
        /// </summary>
        public static readonly System.Text.RegularExpressions.Regex CommentLineRegex = new(
            @"^\s*(//|/\*|\*|')",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// Checks if a line of text is a comment line.
        /// </summary>
        /// <param name="lineText">The text of the line to check.</param>
        /// <returns>True if the line starts with a comment prefix.</returns>
        public static bool IsCommentLine(string lineText)
        {
            return !string.IsNullOrEmpty(lineText) && CommentLineRegex.IsMatch(lineText);
        }
    }
}
