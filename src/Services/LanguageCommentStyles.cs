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
            contentType: SupportedContentTypes.CSharp,
            singleLineDocPrefix: "///",
            multiLineDocStart: "/**",
            multiLineDocEnd: "*/",
            multiLineContinuation: " * ");

        /// <summary>
        /// Visual Basic documentation comment style: ''' for single-line.
        /// </summary>
        public static LanguageCommentStyle VisualBasic { get; } = new LanguageCommentStyle(
            contentType: SupportedContentTypes.VisualBasic,
            singleLineDocPrefix: "'''",
            multiLineDocStart: null,
            multiLineDocEnd: null,
            multiLineContinuation: null);

        /// <summary>
        /// C++ documentation comment style: /// for single-line, /** */ for multi-line.
        /// </summary>
        public static LanguageCommentStyle Cpp { get; } = new LanguageCommentStyle(
            contentType: SupportedContentTypes.CPlusPlus,
            singleLineDocPrefix: "///",
            multiLineDocStart: "/**",
            multiLineDocEnd: "*/",
            multiLineContinuation: " * ");

        /// <summary>
        /// F# documentation comment style: /// for single-line.
        /// </summary>
        public static LanguageCommentStyle FSharp { get; } = new LanguageCommentStyle(
            contentType: SupportedContentTypes.FSharp,
            singleLineDocPrefix: "///",
            multiLineDocStart: null,
            multiLineDocEnd: null,
            multiLineContinuation: null);

        /// <summary>
        /// TypeScript documentation comment style: /// for single-line.
        /// </summary>
        public static LanguageCommentStyle TypeScript { get; } = new LanguageCommentStyle(
            contentType: SupportedContentTypes.TypeScript,
            singleLineDocPrefix: "///",
            multiLineDocStart: null,
            multiLineDocEnd: null,
            multiLineContinuation: null);

        /// <summary>
        /// JavaScript documentation comment style: /// for single-line.
        /// </summary>
        public static LanguageCommentStyle JavaScript { get; } = new LanguageCommentStyle(
            contentType: SupportedContentTypes.JavaScript,
            singleLineDocPrefix: "///",
            multiLineDocStart: null,
            multiLineDocEnd: null,
            multiLineContinuation: null);

        /// <summary>
        /// Razor/Blazor documentation comment style: /// for single-line (C# sections).
        /// </summary>
        public static LanguageCommentStyle Razor { get; } = new LanguageCommentStyle(
            contentType: SupportedContentTypes.Razor,
            singleLineDocPrefix: "///",
            multiLineDocStart: null,
            multiLineDocEnd: null,
            multiLineContinuation: null);

        /// <summary>
        /// SQL comment style: -- for single-line.
        /// </summary>
        public static LanguageCommentStyle Sql { get; } = new LanguageCommentStyle(
            contentType: SupportedContentTypes.Sql,
            singleLineDocPrefix: "--",
            multiLineDocStart: "/*",
            multiLineDocEnd: "*/",
            multiLineContinuation: null);

        /// <summary>
        /// PowerShell comment style: # for single-line.
        /// </summary>
        public static LanguageCommentStyle PowerShell { get; } = new LanguageCommentStyle(
            contentType: SupportedContentTypes.PowerShell,
            singleLineDocPrefix: "#",
            multiLineDocStart: "<#",
            multiLineDocEnd: "#>",
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

            // Check Razor before CSharp since "RazorCSharp" contains "CSharp"
            if (contentType.IndexOf(SupportedContentTypes.Razor, StringComparison.OrdinalIgnoreCase) >= 0 ||
                contentType.IndexOf("Razor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Razor;
            }

            if (contentType.IndexOf(SupportedContentTypes.CSharp, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CSharp;
            }

            if (contentType.IndexOf(SupportedContentTypes.VisualBasic, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return VisualBasic;
            }


            if (contentType.IndexOf(SupportedContentTypes.CPlusPlus, StringComparison.OrdinalIgnoreCase) >= 0 ||
                contentType.IndexOf("C++", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Cpp;
            }

            if (contentType.IndexOf(SupportedContentTypes.FSharp, StringComparison.OrdinalIgnoreCase) >= 0 ||
                contentType.IndexOf("F#", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FSharp;
            }

            if (contentType.IndexOf(SupportedContentTypes.TypeScript, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TypeScript;
            }

            if (contentType.IndexOf(SupportedContentTypes.JavaScript, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return JavaScript;
            }

            if (contentType.IndexOf(SupportedContentTypes.Sql, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Sql;
            }

            if (contentType.IndexOf(SupportedContentTypes.PowerShell, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PowerShell;
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

            if (contentType.IsOfType(SupportedContentTypes.CSharp))
            {
                return CSharp;
            }

            if (contentType.IsOfType(SupportedContentTypes.VisualBasic))
            {
                return VisualBasic;
            }

            if (contentType.IsOfType(SupportedContentTypes.CPlusPlus))
            {
                return Cpp;
            }

            if (contentType.IsOfType("F#") || contentType.IsOfType(SupportedContentTypes.FSharp))
            {
                return FSharp;
            }

            if (contentType.IsOfType(SupportedContentTypes.TypeScript))
            {
                return TypeScript;
            }

            if (contentType.IsOfType(SupportedContentTypes.JavaScript))
            {
                return JavaScript;
            }

            if (contentType.IsOfType(SupportedContentTypes.Razor) || contentType.IsOfType("Razor"))
            {
                return Razor;
            }

            if (contentType.IsOfType(SupportedContentTypes.Sql))
            {
                return Sql;
            }

            if (contentType.IsOfType(SupportedContentTypes.PowerShell))
            {
                return PowerShell;
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
