using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Search task for filtering anchors based on search query.
    /// </summary>
    internal sealed class CodeAnchorsSearchTask(
        uint dwCookie,
        IVsSearchQuery pSearchQuery,
        IVsSearchCallback pSearchCallback,
        CodeAnchorsToolWindowPane pane)
        : VsSearchTask(dwCookie, pSearchQuery, pSearchCallback)
    {
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread - VsSearchTask handles marshalling
        protected override void OnStartSearch()
        {
            ErrorCode = VSConstants.S_OK;
            uint resultCount = 0;

            try
            {
                // SearchQuery is marshalled by VsSearchTask base class
                var searchString = SearchQuery.SearchString ?? string.Empty;
                var matchCase = pane.MatchCaseOption.Value;

                // Extract type filter if present (e.g., type:"TODO")
                string typeFilter = null;
                var filterPattern = "type:\"";
                var filterIndex = searchString.IndexOf(filterPattern, StringComparison.OrdinalIgnoreCase);
                if (filterIndex >= 0)
                {
                    var startIndex = filterIndex + filterPattern.Length;
                    var endIndex = searchString.IndexOf('"', startIndex);
                    if (endIndex > startIndex)
                    {
                        typeFilter = searchString.Substring(startIndex, endIndex - startIndex);
                        // Remove the filter from the search string
                        searchString = searchString.Remove(filterIndex, endIndex - filterIndex + 1).Trim();
                    }
                }

                // Apply the search on the UI thread using JoinableTaskFactory
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    if (pane.Content is CodeAnchorsControl control)
                    {
                        resultCount = control.ApplySearchFilter(searchString, typeFilter, matchCase);
                    }
                });

                SearchResults = resultCount;
            }
            catch (Exception)
            {
                ErrorCode = VSConstants.E_FAIL;
            }

            base.OnStartSearch();
        }
#pragma warning restore VSTHRD010

        protected override void OnStopSearch()
        {
            SearchResults = 0;
        }
    }
}
