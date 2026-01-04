using CommentsVS.Services;

namespace CommentsVS.Test;

[TestClass]
public sealed class LanguageCommentStyleTests
{
    [TestMethod]
    public void GetForContentType_WithCSharp_ReturnsCSharpStyle()
    {
        var result = LanguageCommentStyle.GetForContentType("CSharp");

        Assert.IsNotNull(result);
        Assert.AreEqual("CSharp", result.ContentType);
        Assert.AreEqual("///", result.SingleLineDocPrefix);
        Assert.AreEqual("/**", result.MultiLineDocStart);
        Assert.AreEqual("*/", result.MultiLineDocEnd);
        Assert.AreEqual(" * ", result.MultiLineContinuation);
    }

    [TestMethod]
    public void GetForContentType_WithCSharpCaseInsensitive_ReturnsCSharpStyle()
    {
        var result = LanguageCommentStyle.GetForContentType("csharp");

        Assert.IsNotNull(result);
        Assert.AreEqual("CSharp", result.ContentType);
    }

    [TestMethod]
    public void GetForContentType_WithBasic_ReturnsVisualBasicStyle()
    {
        var result = LanguageCommentStyle.GetForContentType("Basic");

        Assert.IsNotNull(result);
        Assert.AreEqual("Basic", result.ContentType);
        Assert.AreEqual("'''", result.SingleLineDocPrefix);
        Assert.IsNull(result.MultiLineDocStart);
        Assert.IsNull(result.MultiLineDocEnd);
        Assert.IsNull(result.MultiLineContinuation);
    }

    [TestMethod]
    public void GetForContentType_WithBasicCaseInsensitive_ReturnsVisualBasicStyle()
    {
        var result = LanguageCommentStyle.GetForContentType("basic");

        Assert.IsNotNull(result);
        Assert.AreEqual("Basic", result.ContentType);
    }

    [TestMethod]
    public void GetForContentType_WithCppSlash_ReturnsCppStyle()
    {
        var result = LanguageCommentStyle.GetForContentType("C/C++");

        Assert.IsNotNull(result);
        Assert.AreEqual("C/C++", result.ContentType);
        Assert.AreEqual("///", result.SingleLineDocPrefix);
        Assert.AreEqual("/**", result.MultiLineDocStart);
        Assert.AreEqual("*/", result.MultiLineDocEnd);
        Assert.AreEqual(" * ", result.MultiLineContinuation);
    }

    [TestMethod]
    public void GetForContentType_WithCppOnly_ReturnsCppStyle()
    {
        var result = LanguageCommentStyle.GetForContentType("C++");

        Assert.IsNotNull(result);
        Assert.AreEqual("C/C++", result.ContentType);
    }

    [TestMethod]
    public void GetForContentType_WithNull_ReturnsNull()
    {
        var result = LanguageCommentStyle.GetForContentType((string?)null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetForContentType_WithEmptyString_ReturnsNull()
    {
        var result = LanguageCommentStyle.GetForContentType(string.Empty);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetForContentType_WithUnknownContentType_ReturnsNull()
    {
        var result = LanguageCommentStyle.GetForContentType("Python");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void CSharpStyle_SupportsMultiLineDoc_ReturnsTrue()
    {
        Assert.IsTrue(LanguageCommentStyle.CSharp.SupportsMultiLineDoc);
    }

    [TestMethod]
    public void VisualBasicStyle_SupportsMultiLineDoc_ReturnsFalse()
    {
        Assert.IsFalse(LanguageCommentStyle.VisualBasic.SupportsMultiLineDoc);
    }

    [TestMethod]
    public void CppStyle_SupportsMultiLineDoc_ReturnsTrue()
    {
        Assert.IsTrue(LanguageCommentStyle.Cpp.SupportsMultiLineDoc);
    }
}
