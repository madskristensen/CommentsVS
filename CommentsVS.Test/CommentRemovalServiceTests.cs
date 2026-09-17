using CommentsVS.Services;

namespace CommentsVS.Test;

/// <summary>
/// Tests for CommentRemovalService's pure text-based predicate methods (internal, exposed via
/// InternalsVisibleTo). These exercise the real production algorithms directly, without needing
/// ITextSnapshotLine/VS Shell types.
/// </summary>
[TestClass]
public sealed class CommentRemovalServiceTests
{
    #region IsTextEmpty

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("<!--")]
    [DataRow("-->")]
    [DataRow("<%%>")]
    [DataRow("<%")]
    [DataRow("%>")]
    [DataRow("<!-- -->")]
    [DataRow("<!---->")]
    public void IsTextEmpty_EmptyOrCommentRemnant_ReturnsTrue(string text)
    {
        Assert.IsTrue(CommentRemovalService.IsTextEmpty(text));
    }

    [TestMethod]
    [DataRow("var x = 1;")]
    [DataRow("some text")]
    [DataRow("<!-- not fully closed")]
    public void IsTextEmpty_NonEmptyContent_ReturnsFalse(string text)
    {
        Assert.IsFalse(CommentRemovalService.IsTextEmpty(text));
    }

    #endregion

    #region IsXmlDocCommentText

    [TestMethod]
    [DataRow("///", "CSharp")]
    [DataRow("/// <summary>", "CSharp")]
    [DataRow("///", "FSharp")]
    public void IsXmlDocCommentText_CSharpOrFSharpTripleSlash_ReturnsTrue(string text, string contentType)
    {
        Assert.IsTrue(CommentRemovalService.IsXmlDocCommentText(text, contentType));
    }

    [TestMethod]
    public void IsXmlDocCommentText_VBTripleQuote_ReturnsTrue()
    {
        Assert.IsTrue(CommentRemovalService.IsXmlDocCommentText("''' <summary>", "Basic"));
    }

    [TestMethod]
    public void IsXmlDocCommentText_CSharpDoubleSlash_ReturnsFalse()
    {
        Assert.IsFalse(CommentRemovalService.IsXmlDocCommentText("// regular comment", "CSharp"));
    }

    [TestMethod]
    public void IsXmlDocCommentText_VBSingleQuote_ReturnsFalse()
    {
        Assert.IsFalse(CommentRemovalService.IsXmlDocCommentText("' regular comment", "Basic"));
    }

    [TestMethod]
    public void IsXmlDocCommentText_UnrelatedContentType_ReturnsFalse()
    {
        Assert.IsFalse(CommentRemovalService.IsXmlDocCommentText("/// looks like doc comment", "PlainText"));
    }

    #endregion

    #region ContainsAnchorCommentInText

    [TestMethod]
    [DataRow("// TODO: fix this")]
    [DataRow("// HACK: workaround")]
    [DataRow("/* BUG: crash */")]
    [DataRow("' NOTE: important")]
    [DataRow("<!-- FIXME: broken -->")]
    public void ContainsAnchorCommentInText_BuiltInKeyword_ReturnsTrue(string text)
    {
        Assert.IsTrue(CommentRemovalService.ContainsAnchorCommentInText(text, string.Empty));
    }

    [TestMethod]
    public void ContainsAnchorCommentInText_UppercaseBareKeyword_ReturnsTrue()
    {
        Assert.IsTrue(CommentRemovalService.ContainsAnchorCommentInText("// TODO fix this", string.Empty));
    }

    [TestMethod]
    public void ContainsAnchorCommentInText_LowercaseBareKeyword_ReturnsFalse()
    {
        Assert.IsFalse(CommentRemovalService.ContainsAnchorCommentInText("// todo fix this", string.Empty));
    }

    [TestMethod]
    public void ContainsAnchorCommentInText_LowercaseKeywordWithColon_ReturnsTrue()
    {
        Assert.IsTrue(CommentRemovalService.ContainsAnchorCommentInText("// todo: fix this", string.Empty));
    }

    [TestMethod]
    public void ContainsAnchorCommentInText_NoKeyword_ReturnsFalse()
    {
        Assert.IsFalse(CommentRemovalService.ContainsAnchorCommentInText("// just a comment", string.Empty));
    }

    [TestMethod]
    public void ContainsAnchorCommentInText_NoComment_ReturnsFalse()
    {
        Assert.IsFalse(CommentRemovalService.ContainsAnchorCommentInText("var todo = \"TODO\";", string.Empty));
    }

    [TestMethod]
    public void ContainsAnchorCommentInText_CustomTag_ReturnsTrue()
    {
        Assert.IsTrue(CommentRemovalService.ContainsAnchorCommentInText("// PERF: slow query", "PERF"));
    }

    [TestMethod]
    public void ContainsAnchorCommentInText_CustomTagCommaSeparated_ReturnsTrue()
    {
        Assert.IsTrue(CommentRemovalService.ContainsAnchorCommentInText("// SECURITY: check this", "PERF,SECURITY"));
    }

    [TestMethod]
    public void ContainsAnchorCommentInText_CustomTagNotConfigured_ReturnsFalse()
    {
        Assert.IsFalse(CommentRemovalService.ContainsAnchorCommentInText("// PERF: slow query", string.Empty));
    }

    #endregion
}
