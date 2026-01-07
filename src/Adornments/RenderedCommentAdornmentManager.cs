using System.ComponentModel.Composition;
using CommentsVS.Commands;
using CommentsVS.Services;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;


namespace CommentsVS.Adornments
{
/// <summary>
/// Provides the adornment layer for rendered comments and creates the adornment manager.
/// </summary>
[Export(typeof(IWpfTextViewCreationListener))]
[ContentType(SupportedContentTypes.CSharp)]
[ContentType(SupportedContentTypes.VisualBasic)]
[ContentType(SupportedContentTypes.FSharp)]
[ContentType(SupportedContentTypes.CPlusPlus)]
[ContentType(SupportedContentTypes.TypeScript)]
[ContentType(SupportedContentTypes.JavaScript)]
[ContentType(SupportedContentTypes.Razor)]
[ContentType(SupportedContentTypes.Sql)]
[ContentType(SupportedContentTypes.PowerShell)]
[TextViewRole(PredefinedTextViewRoles.Document)]


internal sealed class RenderedCommentAdornmentManagerProvider : IWpfTextViewCreationListener
{

        [Export(typeof(AdornmentLayerDefinition))]
        [Name("RenderedCommentAdornment")]
        [Order(After = PredefinedAdornmentLayers.Text, Before = PredefinedAdornmentLayers.Caret)]
        internal AdornmentLayerDefinition EditorAdornmentLayer = null;

        [Import]
        internal IOutliningManagerService OutliningManagerService { get; set; }

        [Import]
        internal IViewTagAggregatorFactoryService TagAggregatorFactoryService { get; set; }

        [Import]
        internal IEditorFormatMapService EditorFormatMapService { get; set; }

        public void TextViewCreated(IWpfTextView textView)
        {
            // Create the adornment manager for this view
            IEditorFormatMap formatMap = EditorFormatMapService?.GetEditorFormatMap(textView);
            textView.Properties.GetOrCreateSingletonProperty(
                () => new RenderedCommentAdornmentManager(
                    textView,
                    OutliningManagerService,
                    formatMap));
        }
    }

    /// <summary>
    /// Manages rendered comment adornments that overlay collapsed outlining regions.
    /// </summary>
    internal sealed class RenderedCommentAdornmentManager : IDisposable
    {
        private readonly IWpfTextView _textView;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly IAdornmentLayer _adornmentLayer;
        private readonly IOutliningManager _outliningManager;
        private bool _disposed;

        public RenderedCommentAdornmentManager(
            IWpfTextView textView,
            IOutliningManagerService outliningManagerService,
            IEditorFormatMap editorFormatMap)
        {
            _textView = textView;
            _editorFormatMap = editorFormatMap;
            _adornmentLayer = textView.GetAdornmentLayer("RenderedCommentAdornment");
            _outliningManager = outliningManagerService?.GetOutliningManager(textView);

            _textView.LayoutChanged += OnLayoutChanged;
            _textView.Closed += OnViewClosed;

            if (_outliningManager != null)
            {
                _outliningManager.RegionsCollapsed += OnRegionsCollapsed;
                _outliningManager.RegionsExpanded += OnRegionsExpanded;
            }

            SetRenderingModeHelper.RenderedCommentsStateChanged += OnRenderedStateChanged;
        }

        private void OnRegionsCollapsed(object sender, RegionsCollapsedEventArgs e)
        {
            DeferredUpdateAdornments();
        }

        private void OnRegionsExpanded(object sender, RegionsExpandedEventArgs e)
        {
            DeferredUpdateAdornments();
        }

        private void OnRenderedStateChanged(object sender, EventArgs e)
        {
            DeferredUpdateAdornments();
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            DeferredUpdateAdornments();
        }

        private void DeferredUpdateAdornments()
        {
            // Defer to avoid layout exceptions when called during layout
#pragma warning disable VSTHRD001, VSTHRD110 // Intentional fire-and-forget for UI update
            _textView.VisualElement.Dispatcher.BeginInvoke(
                new Action(UpdateAdornments),
                System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001, VSTHRD110
        }

        private void UpdateAdornments()
        {
            if (_disposed || _textView.IsClosed)
                return;

            _adornmentLayer.RemoveAllAdornments();

            // In Compact/Full mode, IntraTextAdornment handles display
            // This manager is no longer needed for those modes
            // Keep this for potential future overlay needs
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _textView.LayoutChanged -= OnLayoutChanged;
            _textView.Closed -= OnViewClosed;

            if (_outliningManager != null)
            {
                _outliningManager.RegionsCollapsed -= OnRegionsCollapsed;
                _outliningManager.RegionsExpanded -= OnRegionsExpanded;
            }

            SetRenderingModeHelper.RenderedCommentsStateChanged -= OnRenderedStateChanged;
        }
    }
}

