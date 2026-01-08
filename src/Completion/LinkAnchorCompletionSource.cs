using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommentsVS.Services;
using CommentsVS.ToolWindows;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Completion
{
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(SupportedContentTypes.Code)]
    [Name("LinkAnchorCompletion")]
    internal sealed class LinkAnchorCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            return textView.Properties.GetOrCreateSingletonProperty(
                () => new LinkAnchorCompletionSource(textView));
        }
    }

    [Export(typeof(IAsyncCompletionCommitManagerProvider))]
    [ContentType(SupportedContentTypes.Code)]
    [Name("LinkAnchorCompletionCommitManager")]
    internal sealed class LinkAnchorCompletionCommitManagerProvider : IAsyncCompletionCommitManagerProvider
    {
        public IAsyncCompletionCommitManager GetOrCreate(ITextView textView)
        {
            return textView.Properties.GetOrCreateSingletonProperty(
                () => new LinkAnchorCompletionCommitManager());
        }
    }

    /// <summary>
    /// Handles commit behavior for LINK anchor completions.
    /// </summary>
    internal sealed class LinkAnchorCompletionCommitManager : IAsyncCompletionCommitManager
    {
        public IEnumerable<char> PotentialCommitCharacters => [];

        public bool ShouldCommitCompletion(IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token)
        {
            // Use default commit behavior
            return false;
        }

        public CommitResult TryCommit(IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token)
        {
            // Let VS handle the default commit behavior
            // This will replace the applicable span (entire path from LINK: to cursor) with item.InsertText
            return CommitResult.Unhandled;
        }
    }

    /// <summary>
    /// Provides IntelliSense completions for LINK anchors, including file paths and anchor names.
    /// </summary>
    internal sealed class LinkAnchorCompletionSource(ITextView textView) : IAsyncCompletionSource
    {
        private string _currentFilePath;
        private string _currentDirectory;
        private bool _initialized;

        private static readonly ImageElement _fileIcon = new(KnownMonikers.Document.ToImageId(), "File");
        private static readonly ImageElement _folderIcon = new(KnownMonikers.FolderClosed.ToImageId(), "Folder");
        private static readonly ImageElement _anchorIcon = new(KnownMonikers.Bookmark.ToImageId(), "Anchor");

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            // Initialize file path on first use
            if (!_initialized)
            {
                Initialize();
            }

            ITextSnapshotLine line = triggerLocation.GetContainingLine();
            var lineText = line.GetText();

            // Only provide completions in comments
            if (!LanguageCommentStyle.IsCommentLine(lineText))
            {
                return CompletionStartData.DoesNotParticipateInCompletion;
            }

            // Check if we're in a LINK context
            var positionInLine = triggerLocation.Position - line.Start.Position;
            var textBeforeCursor = lineText.Substring(0, positionInLine);

            // Look for "LINK:" or "LINK " pattern before cursor
            var linkIndex = textBeforeCursor.LastIndexOf("LINK", System.StringComparison.OrdinalIgnoreCase);
            if (linkIndex < 0)
            {
                return CompletionStartData.DoesNotParticipateInCompletion;
            }

            // Check if there's a colon or space after LINK
            var afterLink = linkIndex + 4;
            if (afterLink >= textBeforeCursor.Length)
            {
                return CompletionStartData.DoesNotParticipateInCompletion;
            }

            var afterLinkText = textBeforeCursor.Substring(afterLink).TrimStart();
            if (string.IsNullOrEmpty(afterLinkText) && textBeforeCursor[afterLink] != ':' && textBeforeCursor[afterLink] != ' ')
            {
                return CompletionStartData.DoesNotParticipateInCompletion;
            }

            // Determine the applicable span (from after LINK: to cursor)
            var startPos = afterLink;
            while (startPos < textBeforeCursor.Length && (textBeforeCursor[startPos] == ':' || textBeforeCursor[startPos] == ' '))
            {
                startPos++;
            }

            var span = new SnapshotSpan(line.Start + startPos, triggerLocation);
            return new CompletionStartData(CompletionParticipation.ProvidesItems, span);
        }

        public async Task<CompletionContext> GetCompletionContextAsync(
            IAsyncCompletionSession session,
            CompletionTrigger trigger,
            SnapshotPoint triggerLocation,
            SnapshotSpan applicableToSpan,
            CancellationToken token)
        {
            var currentText = applicableToSpan.GetText();
            var items = new List<CompletionItem>();

            // Get the full text from LINK: to cursor for path context
            ITextSnapshotLine line = triggerLocation.GetContainingLine();
            var lineText = line.GetText();
            var positionInLine = triggerLocation.Position - line.Start.Position;
            var textBeforeCursor = lineText.Substring(0, positionInLine);

            // Find LINK: and extract the full path context
            var linkIndex = textBeforeCursor.LastIndexOf("LINK", System.StringComparison.OrdinalIgnoreCase);
            if (linkIndex >= 0)
            {
                var afterLink = linkIndex + 4;
                while (afterLink < textBeforeCursor.Length && (textBeforeCursor[afterLink] == ':' || textBeforeCursor[afterLink] == ' '))
                {
                    afterLink++;
                }
                var fullPathContext = textBeforeCursor.Substring(afterLink);

                // If starting with #, provide anchor completions
                if (fullPathContext.StartsWith("#"))
                {
                    items.AddRange(GetAnchorCompletions(currentText.TrimStart('#')));
                }
                else
                {
                    // Provide file path completions with full context - run on background thread
                    List<CompletionItem> fileCompletions = await Task.Run(() =>
                        GetFilePathCompletions(fullPathContext).ToList(), token).ConfigureAwait(false);
                    items.AddRange(fileCompletions);
                }
            }

            if (items.Count == 0)
            {
                return CompletionContext.Empty;
            }

            return new CompletionContext(items.ToImmutableArray());
        }

        public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
        {
            // Return the suffix as description (contains full path or anchor info)
            return Task.FromResult<object>(item.Suffix ?? item.DisplayText);
        }

        private IEnumerable<CompletionItem> GetFilePathCompletions(string fullPathContext)
        {
            if (string.IsNullOrEmpty(_currentDirectory))
            {
                yield break;
            }

            var searchDirectory = _currentDirectory;
            var prefix = "";

            // Handle relative path prefixes
            if (fullPathContext.StartsWith("./") || fullPathContext.StartsWith(".\\"))
            {
                fullPathContext = fullPathContext.Substring(2);
                prefix = "./";
            }
            else if (fullPathContext.StartsWith("../") || fullPathContext.StartsWith("..\\"))
            {
                searchDirectory = Path.GetDirectoryName(_currentDirectory);
                fullPathContext = fullPathContext.Substring(3);
                prefix = "../";
            }

            // If there's a path separator in the full path context, navigate to that directory
            var lastSep = fullPathContext.LastIndexOfAny(['/', '\\']);
            var partialName = "";
            if (lastSep >= 0)
            {
                var subDir = fullPathContext.Substring(0, lastSep);
                partialName = fullPathContext.Substring(lastSep + 1);
                searchDirectory = Path.Combine(searchDirectory, subDir);
                prefix += subDir + "/";
            }
            else
            {
                partialName = fullPathContext;
            }

            if (!Directory.Exists(searchDirectory))
            {
                yield break;
            }

            // List directories
            foreach (var dir in SafeGetDirectories(searchDirectory))
            {
                var dirName = Path.GetFileName(dir);
                if (ShouldSkipFolder(dirName))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(partialName) ||
                        dirName.StartsWith(partialName, System.StringComparison.OrdinalIgnoreCase))
                {
                    var insertText = prefix + dirName + "/";
                    yield return new CompletionItem(
                        displayText: dirName + "/",
                        source: this,
                        icon: _folderIcon,
                        filters: ImmutableArray<CompletionFilter>.Empty,
                        suffix: "",
                        insertText: insertText,
                        sortText: insertText,
                        filterText: insertText,
                        attributeIcons: ImmutableArray<ImageElement>.Empty);
                }
            }

            // List files
            foreach (var file in SafeGetFiles(searchDirectory))
            {
                var fileName = Path.GetFileName(file);
                if (string.IsNullOrEmpty(partialName) ||
                    fileName.StartsWith(partialName, System.StringComparison.OrdinalIgnoreCase))
                {
                    var insertText = prefix + fileName;
                    yield return new CompletionItem(
                        displayText: fileName,
                        source: this,
                        icon: _fileIcon,
                        filters: ImmutableArray<CompletionFilter>.Empty,
                        suffix: "",
                        insertText: insertText,
                        sortText: insertText,
                        filterText: insertText,
                        attributeIcons: ImmutableArray<ImageElement>.Empty);
                }
            }
        }

        private IEnumerable<CompletionItem> GetAnchorCompletions(string partialAnchor)
        {
            // Get anchors from the cache
            CodeAnchorsToolWindow toolWindow = CodeAnchorsToolWindow.Instance;
            if (toolWindow?.Cache == null)
            {
                yield break;
            }

            // Get all ANCHOR type anchors
            IReadOnlyList<AnchorItem> allAnchors = toolWindow.Cache.GetAllAnchors();
            IEnumerable<AnchorItem> anchorItems = allAnchors
                .Where(a => a.AnchorType == AnchorType.Anchor && !string.IsNullOrEmpty(a.AnchorId));

            // Filter by partial text
            if (!string.IsNullOrEmpty(partialAnchor))
            {
                anchorItems = anchorItems.Where(a =>
                    a.AnchorId.StartsWith(partialAnchor, System.StringComparison.OrdinalIgnoreCase));
            }

            // Deduplicate by anchor ID
            HashSet<string> seen = new(System.StringComparer.OrdinalIgnoreCase);
            foreach (AnchorItem anchor in anchorItems)
            {
                if (!seen.Add(anchor.AnchorId))
                {
                    continue;
                }

                var displayText = "#" + anchor.AnchorId;
                var description = $"{anchor.FileName}:{anchor.LineNumber}";

                yield return new CompletionItem(
                    displayText: displayText,
                    source: this,
                    icon: _anchorIcon,
                    filters: ImmutableArray<CompletionFilter>.Empty,
                    suffix: description,
                    insertText: displayText,
                    sortText: anchor.AnchorId,
                    filterText: anchor.AnchorId,
                    attributeIcons: ImmutableArray<ImageElement>.Empty);
            }
        }

        private static bool ShouldSkipFolder(string folderName)
        {
            // Skip common non-source folders
            string[] skipFolders = ["node_modules", "bin", "obj", ".git", ".vs", "packages", ".nuget"];
            return skipFolders.Any(f => f.Equals(folderName, System.StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> SafeGetDirectories(string path)
        {
            try
            {
                // Use EnumerateDirectories instead of GetDirectories to avoid loading all at once
                return Directory.EnumerateDirectories(path);
            }
            catch
            {
                return [];
            }
        }

        private static IEnumerable<string> SafeGetFiles(string path)
        {
            try
            {
                // Use EnumerateFiles instead of GetFiles to avoid loading all at once
                return Directory.EnumerateFiles(path);
            }
            catch
            {
                return [];
            }
        }

        private void Initialize()
        {
            _initialized = true;

            if (textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                _currentFilePath = document.FilePath;
                _currentDirectory = Path.GetDirectoryName(_currentFilePath);
            }
        }
    }
}
