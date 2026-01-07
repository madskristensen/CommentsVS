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
    [ContentType("code")]
    [Name("LinkAnchorCompletion")]
    internal sealed class LinkAnchorCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            return textView.Properties.GetOrCreateSingletonProperty(
                () => new LinkAnchorCompletionSource(textView));
        }
    }

    /// <summary>
    /// Provides IntelliSense completions for LINK anchors, including file paths and anchor names.
    /// </summary>
    internal sealed class LinkAnchorCompletionSource : IAsyncCompletionSource
    {
        private readonly ITextView _textView;
        private string _currentFilePath;
        private string _currentDirectory;
        private bool _initialized;

        private static readonly ImageElement _fileIcon = new(KnownMonikers.Document.ToImageId(), "File");
        private static readonly ImageElement _folderIcon = new(KnownMonikers.FolderClosed.ToImageId(), "Folder");
        private static readonly ImageElement _anchorIcon = new(KnownMonikers.Bookmark.ToImageId(), "Anchor");

        public LinkAnchorCompletionSource(ITextView textView)
        {
            _textView = textView;
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            // Initialize file path on first use
            if (!_initialized)
            {
                Initialize();
            }

            ITextSnapshotLine line = triggerLocation.GetContainingLine();
            string lineText = line.GetText();

            // Only provide completions in comments
            if (!LanguageCommentStyle.IsCommentLine(lineText))
            {
                return CompletionStartData.DoesNotParticipateInCompletion;
            }

            // Check if we're in a LINK context
            int positionInLine = triggerLocation.Position - line.Start.Position;
            string textBeforeCursor = lineText.Substring(0, positionInLine);

            // Look for "LINK:" or "LINK " pattern before cursor
            int linkIndex = textBeforeCursor.LastIndexOf("LINK", System.StringComparison.OrdinalIgnoreCase);
            if (linkIndex < 0)
            {
                return CompletionStartData.DoesNotParticipateInCompletion;
            }

            // Check if there's a colon or space after LINK
            int afterLink = linkIndex + 4;
            if (afterLink >= textBeforeCursor.Length)
            {
                return CompletionStartData.DoesNotParticipateInCompletion;
            }

            string afterLinkText = textBeforeCursor.Substring(afterLink).TrimStart();
            if (string.IsNullOrEmpty(afterLinkText) && textBeforeCursor[afterLink] != ':' && textBeforeCursor[afterLink] != ' ')
            {
                return CompletionStartData.DoesNotParticipateInCompletion;
            }

            // Determine the applicable span (from after LINK: to cursor)
            int startPos = afterLink;
            while (startPos < textBeforeCursor.Length && (textBeforeCursor[startPos] == ':' || textBeforeCursor[startPos] == ' '))
            {
                startPos++;
            }

            var span = new SnapshotSpan(line.Start + startPos, triggerLocation);
            return new CompletionStartData(CompletionParticipation.ProvidesItems, span);
        }

        public Task<CompletionContext> GetCompletionContextAsync(
            IAsyncCompletionSession session,
            CompletionTrigger trigger,
            SnapshotPoint triggerLocation,
            SnapshotSpan applicableToSpan,
            CancellationToken token)
        {
            string currentText = applicableToSpan.GetText();
            var items = new List<CompletionItem>();

            // If starting with #, provide anchor completions
            if (currentText.StartsWith("#"))
            {
                items.AddRange(GetAnchorCompletions(currentText.TrimStart('#')));
            }
            else
            {
                // Provide file path completions
                items.AddRange(GetFilePathCompletions(currentText));
            }

            if (items.Count == 0)
            {
                return Task.FromResult(CompletionContext.Empty);
            }

            return Task.FromResult(new CompletionContext(items.ToImmutableArray()));
        }

        public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
        {
            // Return the suffix as description (contains full path or anchor info)
            return Task.FromResult<object>(item.Suffix ?? item.DisplayText);
        }

        private IEnumerable<CompletionItem> GetFilePathCompletions(string partialPath)
        {
            if (string.IsNullOrEmpty(_currentDirectory))
            {
                yield break;
            }

            string searchDirectory = _currentDirectory;
            string prefix = "";

            // Handle relative path prefixes
            if (partialPath.StartsWith("./") || partialPath.StartsWith(".\\"))
            {
                partialPath = partialPath.Substring(2);
                prefix = "./";
            }
            else if (partialPath.StartsWith("../") || partialPath.StartsWith("..\\"))
            {
                searchDirectory = Path.GetDirectoryName(_currentDirectory);
                partialPath = partialPath.Substring(3);
                prefix = "../";
            }

            // If there's a path separator in the partial path, navigate to that directory
            int lastSep = partialPath.LastIndexOfAny(new[] { '/', '\\' });
            if (lastSep >= 0)
            {
                string subDir = partialPath.Substring(0, lastSep);
                partialPath = partialPath.Substring(lastSep + 1);
                searchDirectory = Path.Combine(searchDirectory, subDir);
                prefix += subDir + "/";
            }

            if (!Directory.Exists(searchDirectory))
            {
                yield break;
            }

            // List directories
            foreach (string dir in SafeGetDirectories(searchDirectory))
            {
                string dirName = Path.GetFileName(dir);
                if (ShouldSkipFolder(dirName))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(partialPath) ||
                    dirName.StartsWith(partialPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    yield return new CompletionItem(
                        dirName + "/",
                        this,
                        _folderIcon,
                        ImmutableArray<CompletionFilter>.Empty,
                        prefix + dirName + "/",
                        prefix + dirName + "/",
                        dirName,
                        dirName,
                        ImmutableArray<ImageElement>.Empty);
                }
            }

            // List files
            foreach (string file in SafeGetFiles(searchDirectory))
            {
                string fileName = Path.GetFileName(file);
                if (string.IsNullOrEmpty(partialPath) ||
                    fileName.StartsWith(partialPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    yield return new CompletionItem(
                        fileName,
                        this,
                        _fileIcon,
                        ImmutableArray<CompletionFilter>.Empty,
                        prefix + fileName,
                        prefix + fileName,
                        fileName,
                        fileName,
                        ImmutableArray<ImageElement>.Empty);
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

                string displayText = "#" + anchor.AnchorId;
                string description = $"{anchor.FileName}:{anchor.LineNumber}";

                yield return new CompletionItem(
                    displayText,
                    this,
                    _anchorIcon,
                    ImmutableArray<CompletionFilter>.Empty,
                    displayText,
                    displayText,
                    anchor.AnchorId,
                    description,
                    ImmutableArray<ImageElement>.Empty);
            }
        }

        private static bool ShouldSkipFolder(string folderName)
        {
            // Skip common non-source folders
            string[] skipFolders = { "node_modules", "bin", "obj", ".git", ".vs", "packages", ".nuget" };
            return skipFolders.Any(f => f.Equals(folderName, System.StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> SafeGetDirectories(string path)
        {
            try
            {
                return Directory.GetDirectories(path);
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private static IEnumerable<string> SafeGetFiles(string path)
        {
            try
            {
                return Directory.GetFiles(path);
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private void Initialize()
        {
            _initialized = true;

            if (_textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                _currentFilePath = document.FilePath;
                _currentDirectory = Path.GetDirectoryName(_currentFilePath);
            }
        }
    }
}
