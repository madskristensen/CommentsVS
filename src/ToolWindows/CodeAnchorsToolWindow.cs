using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Tool window for displaying code anchors (TODO, HACK, ANCHOR, etc.) from open documents.
    /// </summary>
    public class CodeAnchorsToolWindow : BaseToolWindow<CodeAnchorsToolWindow>
    {
        private CodeAnchorsControl _control;
        private readonly AnchorService _anchorService = new AnchorService();

        /// <summary>
        /// Gets the current instance of the tool window (set after CreateAsync is called).
        /// </summary>
        public static CodeAnchorsToolWindow Instance { get; private set; }

        public override string GetTitle(int toolWindowId) => "Code Anchors";

        public override Type PaneType => typeof(Pane);

        public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            Instance = this;

            _control = new CodeAnchorsControl();
            _control.AnchorActivated += OnAnchorActivated;
            _control.RefreshRequested += OnRefreshRequested;

            // Initial scan of open documents
            await ScanOpenDocumentsAsync();

            // Subscribe to document events
            VS.Events.DocumentEvents.Opened += OnDocumentOpened;
            VS.Events.DocumentEvents.Closed += OnDocumentClosed;
            VS.Events.DocumentEvents.Saved += OnDocumentSaved;

            return _control;
        }

        /// <summary>
        /// Scans all currently open documents for anchors.
        /// </summary>
        public async Task ScanOpenDocumentsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _control?.ClearAnchors();

            var allAnchors = new List<AnchorItem>();

            // Get the running document table
            IVsRunningDocumentTable rdt = await VS.Services.GetRunningDocumentTableAsync();
            if (rdt == null)
            {
                return;
            }

            rdt.GetRunningDocumentsEnum(out IEnumRunningDocuments enumDocs);
            if (enumDocs == null)
            {
                return;
            }

            uint[] cookies = new uint[1];
            while (enumDocs.Next(1, cookies, out uint fetched) == 0 && fetched == 1)
            {
                rdt.GetDocumentInfo(
                    cookies[0],
                    out uint flags,
                    out uint readLocks,
                    out uint editLocks,
                    out string filePath,
                    out IVsHierarchy hierarchy,
                    out uint itemId,
                    out IntPtr docData);

                if (string.IsNullOrEmpty(filePath))
                {
                    continue;
                }

                // Get project name
                string projectName = null;
                if (hierarchy != null)
                {
                    hierarchy.GetProperty((uint)Microsoft.VisualStudio.VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_Name, out object projNameObj);
                    projectName = projNameObj as string;
                }

                // Try to get text from the document
                string documentText = await GetDocumentTextAsync(filePath);
                if (!string.IsNullOrEmpty(documentText))
                {
                    var anchors = _anchorService.ScanText(documentText, filePath, projectName);
                    allAnchors.AddRange(anchors);
                }
            }

            _control?.UpdateAnchors(allAnchors);
        }

        /// <summary>
        /// Scans a single document for anchors and updates the display.
        /// </summary>
        public async Task ScanDocumentAsync(string filePath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (string.IsNullOrEmpty(filePath) || _control == null)
            {
                return;
            }

            // Remove existing anchors for this file
            _control.RemoveAnchorsForFile(filePath);

            // Get project name
            string projectName = await GetProjectNameForFileAsync(filePath);

            // Get document text
            string documentText = await GetDocumentTextAsync(filePath);
            if (!string.IsNullOrEmpty(documentText))
            {
                var anchors = _anchorService.ScanText(documentText, filePath, projectName);
                _control.AddAnchors(anchors);
            }
        }

        /// <summary>
        /// Navigates to the next anchor in the list.
        /// </summary>
        public async Task NavigateToNextAnchorAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            AnchorItem anchor = _control?.SelectNextAnchor();
            if (anchor != null)
            {
                await NavigateToAnchorAsync(anchor);
            }
        }

        /// <summary>
        /// Navigates to the previous anchor in the list.
        /// </summary>
        public async Task NavigateToPreviousAnchorAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            AnchorItem anchor = _control?.SelectPreviousAnchor();
            if (anchor != null)
            {
                await NavigateToAnchorAsync(anchor);
            }
        }

        private async void OnAnchorActivated(object sender, AnchorItem anchor)
        {
            await NavigateToAnchorAsync(anchor);
        }

        private async void OnRefreshRequested(object sender, EventArgs e)
        {
            await ScanOpenDocumentsAsync();
        }

        private async void OnDocumentOpened(string filePath)
        {
            await ScanDocumentAsync(filePath);
        }

        private void OnDocumentClosed(string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _control?.RemoveAnchorsForFile(filePath);
        }

        private async void OnDocumentSaved(string filePath)
        {
            await ScanDocumentAsync(filePath);
        }

        private async Task NavigateToAnchorAsync(AnchorItem anchor)
        {
            if (anchor == null)
            {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Open the document
            DocumentView docView = await VS.Documents.OpenAsync(anchor.FilePath);
            if (docView?.TextView == null)
            {
                return;
            }

            // Navigate to the line
            try
            {
                ITextSnapshot snapshot = docView.TextView.TextSnapshot;
                if (anchor.LineNumber > 0 && anchor.LineNumber <= snapshot.LineCount)
                {
                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(anchor.LineNumber - 1);
                    SnapshotPoint point = line.Start.Add(Math.Min(anchor.Column, line.Length));

                    docView.TextView.Caret.MoveTo(point);
                    docView.TextView.ViewScroller.EnsureSpanVisible(
                        new SnapshotSpan(point, 0),
                        Microsoft.VisualStudio.Text.Editor.EnsureSpanVisibleOptions.AlwaysCenter);
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        private async Task<string> GetDocumentTextAsync(string filePath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                // Try to get from open document first
                DocumentView docView = await VS.Documents.GetDocumentViewAsync(filePath);
                if (docView?.TextView != null)
                {
                    return docView.TextView.TextSnapshot.GetText();
                }

                // Fall back to reading from disk
                if (System.IO.File.Exists(filePath))
                {
                    return System.IO.File.ReadAllText(filePath);
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        private async Task<string> GetProjectNameForFileAsync(string filePath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                PhysicalFile file = await PhysicalFile.FromFileAsync(filePath);
                Project project = file?.ContainingProject;
                return project?.Name;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Pane for the Code Anchors tool window.
        /// </summary>
        [Guid("8B0B8A6E-5E7F-4B6E-9F8A-1C2D3E4F5A6B")]
        public class Pane : ToolWindowPane
        {
            public Pane()
            {
                BitmapImageMoniker = KnownMonikers.Bookmark;
            }
        }
    }
}
