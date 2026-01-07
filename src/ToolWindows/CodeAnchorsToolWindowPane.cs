using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;

namespace CommentsVS.ToolWindows
{
    /// <summary>
    /// Pane for the Code Anchors tool window with integrated VS search support.
    /// </summary>
    [Guid("8B0B8A6E-5E7F-4B6E-9F8A-1C2D3E4F5A6B")]
    public class CodeAnchorsToolWindowPane : ToolWindowPane
    {
        private IVsEnumWindowSearchOptions _searchOptionsEnum;
        private WindowSearchBooleanOption _matchCaseOption;

        public CodeAnchorsToolWindowPane()
        {
            BitmapImageMoniker = KnownMonikers.CodeReviewDashboard;
            ToolBar = new System.ComponentModel.Design.CommandID(PackageGuids.CommentsVS, PackageIds.CodeAnchorsToolbar);
            ToolBarLocation = (int)VSTWT_LOCATION.VSTWT_TOP;
        }

        /// <summary>
        /// Gets the control hosted in this tool window.
        /// </summary>
        private CodeAnchorsControl Control => Content as CodeAnchorsControl;

        /// <summary>
        /// Gets a value indicating whether search is enabled for this tool window.
        /// </summary>
        public override bool SearchEnabled => true;

        /// <summary>
        /// Gets the match case search option.
        /// </summary>
        public WindowSearchBooleanOption MatchCaseOption
        {
            get
            {
                if (_matchCaseOption == null)
                {
                    _matchCaseOption = new WindowSearchBooleanOption("Match case", "Match case", false);
                }
                return _matchCaseOption;
            }
        }

        /// <summary>
        /// Gets the search options enumerator.
        /// </summary>
        public override IVsEnumWindowSearchOptions SearchOptionsEnum
        {
            get
            {
                if (_searchOptionsEnum == null)
                {
                    var options = new List<IVsWindowSearchOption> { MatchCaseOption };
                    _searchOptionsEnum = new WindowSearchOptionEnumerator(options);
                }
                return _searchOptionsEnum;
            }
        }

        /// <summary>
        /// Creates a search task for the given query.
        /// </summary>
        public override IVsSearchTask CreateSearch(uint dwCookie, IVsSearchQuery pSearchQuery, IVsSearchCallback pSearchCallback)
        {
            if (pSearchQuery == null || pSearchCallback == null)
            {
                return null;
            }

            return new CodeAnchorsSearchTask(dwCookie, pSearchQuery, pSearchCallback, this);
        }

        /// <summary>
        /// Clears the current search and restores all anchors.
        /// </summary>
        public override void ClearSearch()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Control?.ClearSearchFilter();
        }
    }
}
