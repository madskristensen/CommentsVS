using Microsoft.VisualStudio.Text;

namespace CommentsVS.Services;

/// <summary>
/// Stub for XmlDocCommentCache to allow benchmarking without VS dependencies.
/// The real implementation uses VS text buffer properties for caching.
/// </summary>
internal sealed class XmlDocCommentCache
{
    public static XmlDocCommentCache? GetOrCreate(ITextBuffer buffer) => null;
    
    public IReadOnlyList<XmlDocCommentBlock>? GetCommentBlocks(ITextSnapshot snapshot, LanguageCommentStyle commentStyle) => null;
}
