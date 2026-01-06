using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
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
        private IVsEnumWindowSearchFilters _searchFiltersEnum;
        private WindowSearchBooleanOption _matchCaseOption;

        public CodeAnchorsToolWindowPane()
        {
            BitmapImageMoniker = KnownMonikers.Bookmark;
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
        /// Gets the search filters enumerator for anchor type filtering.
        /// </summary>
        public override IVsEnumWindowSearchFilters SearchFiltersEnum
        {
            get
            {
                if (_searchFiltersEnum == null)
                {
                    var filters = new List<IVsWindowSearchFilter>
                    {
                        new WindowSearchSimpleFilter("TODO", "Show only TODO anchors", "type", "TODO"),
                        new WindowSearchSimpleFilter("HACK", "Show only HACK anchors", "type", "HACK"),
                        new WindowSearchSimpleFilter("NOTE", "Show only NOTE anchors", "type", "NOTE"),
                        new WindowSearchSimpleFilter("BUG", "Show only BUG anchors", "type", "BUG"),
                        new WindowSearchSimpleFilter("FIXME", "Show only FIXME anchors", "type", "FIXME"),
                        new WindowSearchSimpleFilter("UNDONE", "Show only UNDONE anchors", "type", "UNDONE"),
                        new WindowSearchSimpleFilter("REVIEW", "Show only REVIEW anchors", "type", "REVIEW"),
                        new WindowSearchSimpleFilter("ANCHOR", "Show only ANCHOR anchors", "type", "ANCHOR"),
                    };
                    _searchFiltersEnum = new WindowSearchFilterEnumerator(filters);
                }
                return _searchFiltersEnum;
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
