using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Classification
{
    /// <summary>
    /// Provides classifiers for comment tags in code files.
    /// </summary>
    [Export(typeof(IClassifierProvider))]
    [ContentType("code")]
    internal sealed class CommentTagClassifierProvider : IClassifierProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry { get; set; }

        public IClassifier GetClassifier(ITextBuffer buffer)
        {
            return buffer.Properties.GetOrCreateSingletonProperty(
                () => new CommentTagClassifier(buffer, ClassificationRegistry));
        }
    }
}
