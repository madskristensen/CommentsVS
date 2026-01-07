using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Classification
{
    /// <summary>
    /// Provides classifiers for prefix-based comment highlighting (Better Comments style).
    /// </summary>
    [Export(typeof(IClassifierProvider))]
    [ContentType("code")]
    internal sealed class PrefixCommentClassifierProvider : IClassifierProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry { get; set; }

        public IClassifier GetClassifier(ITextBuffer buffer)
        {
            return buffer.Properties.GetOrCreateSingletonProperty(
                () => new PrefixCommentClassifier(buffer, ClassificationRegistry));
        }
    }
}
