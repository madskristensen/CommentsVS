using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using CommentsVS.Services;

namespace CommentsVS.Options
{
    /// <summary>
    /// Rendering mode for XML documentation comments.
    /// </summary>
    public enum RenderingMode
    {
        /// <summary>
        /// Show raw XML comments with standard Visual Studio syntax.
        /// </summary>
        Off = 0,

        /// <summary>
        /// Use outlining with stripped XML tags when collapsed.
        /// </summary>
        Compact = 1,

        /// <summary>
        /// Replace comments with rich formatted rendering optimized for reading.
        /// </summary>
        Full = 2
    }

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
        private HashSet<string> _cachedExtensions;
        private string _lastExtensionsValue;
        private HashSet<string> _cachedFolders;
        private string _lastFoldersValue;
        private HashSet<string> _cachedCustomTags;
        private string _lastCustomTagsValue;

        private const string _reflowCategory = "Comment Reflow";

        [Category(_reflowCategory)]
        [DisplayName("Maximum line length")]
        [Description("The maximum line length for XML documentation comments before wrapping to a new line. Default is 120.")]
        [DefaultValue(120)]
        public int MaxLineLength { get; set; } = 120;

        [Category(_reflowCategory)]
        [DisplayName("Enable reflow on Format Document")]
        [Description("When enabled, XML documentation comments will be reflowed when using Format Document (Ctrl+K, Ctrl+D).")]
        [DefaultValue(true)]
        public bool ReflowOnFormatDocument { get; set; } = true;

        [Category(_reflowCategory)]
        [DisplayName("Enable reflow on paste")]
        [Description("When enabled, XML documentation comments will be automatically reflowed when pasting text into a comment block.")]
        [DefaultValue(true)]
        public bool ReflowOnPaste { get; set; } = true;

        [Category(_reflowCategory)]
        [DisplayName("Enable reflow while typing")]
        [Description("When enabled, XML documentation comments will be automatically reflowed as you type when a line exceeds the maximum length.")]
        [DefaultValue(true)]
        public bool ReflowOnTyping { get; set; } = true;

        [Category(_reflowCategory)]
        [DisplayName("Use compact style for short summaries")]
        [Description("When enabled, short summaries that fit on one line will use the compact format: /// <summary>Short text.</summary>")]
        [DefaultValue(true)]
        public bool UseCompactStyleForShortSummaries { get; set; } = true;

        [Category(_reflowCategory)]
        [DisplayName("Preserve blank lines")]
        [Description("When enabled, blank lines within XML documentation comments (paragraph separators) will be preserved during reflow.")]
        [DefaultValue(true)]
        public bool PreserveBlankLines { get; set; } = true;

        private const string _outliningCategory = "Comment Outlining";

        [Category(_outliningCategory)]
        [DisplayName("Collapsed by default")]
        [Description("When enabled, XML documentation comments will be automatically collapsed when opening a file. Only applies when Rendering Mode is Off or Compact.")]
        [DefaultValue(false)]
        public bool CollapseCommentsOnFileOpen { get; set; } = false;

        private const string _tagsCategory = "Comment Tags";

        [Category(_tagsCategory)]
        [DisplayName("Enable comment tag highlighting")]
        [Description("When enabled, comment tags like TODO, HACK, NOTE, BUG, FIXME, UNDONE, and REVIEW will be highlighted. Colors can be customized in Tools > Options > Environment > Fonts and Colors.")]
        [DefaultValue(true)]
        public bool EnableCommentTagHighlighting { get; set; } = true;

        [Category(_tagsCategory)]
        [DisplayName("Enable prefix highlighting")]
        [Description("When enabled, comments with special prefixes (!, ?, *, //, -, >) will be highlighted with different colors (Better Comments style). Colors can be customized in Tools > Options > Environment > Fonts and Colors.")]
        [DefaultValue(true)]
        public bool EnablePrefixHighlighting { get; set; } = true;

        [Category(_tagsCategory)]
        [DisplayName("Custom tags")]
        [Description("Comma-separated list of custom comment tags to highlight. Example: PERF, SECURITY, DEBT, REFACTOR. All custom tags share the same color, customizable in Tools > Options > Environment > Fonts and Colors under 'Comment Tag - Custom'.")]
        [DefaultValue("")]
        public string CustomTags { get; set; } = "";

        private const string _linksCategory = "Issue Links";

        [Category(_linksCategory)]
        [DisplayName("Enable issue links")]
        [Description("When enabled, issue references like #123 in comments will become clickable links to the issue on GitHub, GitLab, Bitbucket, or Azure DevOps.")]
        [DefaultValue(true)]
        public bool EnableIssueLinks { get; set; } = true;

        private const string _renderingCategory = "Comment Rendering";

        [Category(_renderingCategory)]
        [DisplayName("Rendering mode")]
        [Description("Controls how XML documentation comments are displayed. Off: Raw XML syntax. Compact: Outlining with stripped tags. Full: Rich formatted rendering. Toggle with Ctrl+M, Ctrl+R.")]
        [DefaultValue(RenderingMode.Off)]
        [TypeConverter(typeof(EnumConverter))]
        public RenderingMode CommentRenderingMode { get; set; } = RenderingMode.Off;

        private const string _anchorsCategory = "Code Anchors";

        [Category(_anchorsCategory)]
        [DisplayName("Scan solution on load")]
        [Description("When enabled, the entire solution will be scanned for code anchors (TODO, HACK, etc.) when a solution is loaded.")]
        [DefaultValue(true)]
        public bool ScanSolutionOnLoad { get; set; } = true;

        [Category(_anchorsCategory)]
        [DisplayName("File extensions to scan")]
        [Description("Comma-separated list of file extensions to scan for code anchors. Example: .cs,.vb,.js,.ts")]
        [DefaultValue(".cs,.vb,.fs,.cpp,.c,.h,.hpp,.cc,.cxx,.js,.ts,.jsx,.tsx,.css,.scss,.less,.html,.htm,.xml,.xaml,.json,.yaml,.yml,.ps1,.psm1,.sql,.md,.razor,.cshtml,.vbhtml,.py,.rb,.go,.rs,.java,.kt,.swift,.php")]
        public string FileExtensionsToScan { get; set; } = ".cs,.vb,.fs,.cpp,.c,.h,.hpp,.cc,.cxx,.js,.ts,.jsx,.tsx,.css,.scss,.less,.html,.htm,.xml,.xaml,.json,.yaml,.yml,.ps1,.psm1,.sql,.md,.razor,.cshtml,.vbhtml,.py,.rb,.go,.rs,.java,.kt,.swift,.php";

        [Category(_anchorsCategory)]
        [DisplayName("Folders to ignore")]
        [Description("Comma-separated list of folder names to ignore when scanning for code anchors.")]
        [DefaultValue("node_modules,bin,obj,.git,.vs,.svn,packages,.nuget,bower_components,vendor,dist,build,out,target,.idea,.vscode,__pycache__,.pytest_cache,coverage,.nyc_output")]
        public string FoldersToIgnore { get; set; } = "node_modules,bin,obj,.git,.vs,.svn,packages,.nuget,bower_components,vendor,dist,build,out,target,.idea,.vscode,__pycache__,.pytest_cache,coverage,.nyc_output";

        /// <summary>
        /// Gets the file extensions to scan as a HashSet for fast lookup.
        /// The result is cached and only recalculated when the setting changes.
        /// </summary>
        public HashSet<string> GetFileExtensionsSet()
        {
            if (_cachedExtensions == null || _lastExtensionsValue != FileExtensionsToScan)
            {
                _lastExtensionsValue = FileExtensionsToScan;
                _cachedExtensions = ParseExtensions(FileExtensionsToScan);
            }
            return _cachedExtensions;
        }

        private static HashSet<string> ParseExtensions(string extensionsToScan)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(extensionsToScan))
            {
                foreach (var ext in extensionsToScan.Split([','], StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = ext.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        // Ensure extension starts with a dot
                        extensions.Add(trimmed.StartsWith(".") ? trimmed : "." + trimmed);
                    }
                }
            }
            return extensions;
        }

        /// <summary>
        /// Gets the folders to ignore as a HashSet for fast lookup.
        /// The result is cached and only recalculated when the setting changes.
        /// </summary>
        public HashSet<string> GetIgnoredFoldersSet()
        {
            if (_cachedFolders == null || _lastFoldersValue != FoldersToIgnore)
            {
                _lastFoldersValue = FoldersToIgnore;
                _cachedFolders = ParseFolders(FoldersToIgnore);
            }
            return _cachedFolders;
        }

        private static HashSet<string> ParseFolders(string foldersToIgnore)
        {
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(foldersToIgnore))
            {
                foreach (var folder in foldersToIgnore.Split([','], StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = folder.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        folders.Add(trimmed);
                    }
                }
            }
            return folders;
        }

        /// <summary>
        /// Gets the custom tags as a HashSet for fast lookup.
        /// The result is cached and only recalculated when the setting changes.
        /// Tags are normalized to uppercase.
        /// </summary>
        public HashSet<string> GetCustomTagsSet()
        {
            if (_cachedCustomTags == null || _lastCustomTagsValue != CustomTags)
            {
                _lastCustomTagsValue = CustomTags;
                _cachedCustomTags = ParseCustomTags(CustomTags);
            }
            return _cachedCustomTags;
        }

        private static HashSet<string> ParseCustomTags(string customTags)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(customTags))
            {
                foreach (var tag in customTags.Split([','], StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = tag.Trim().ToUpperInvariant();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        tags.Add(trimmed);
                    }
                }
            }
            return tags;
        }

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
