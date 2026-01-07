using CommentsVS.Services;

namespace CommentsVS.Test;

[TestClass]
public sealed class LanguageCommentStyleTests
{
    [TestMethod]
    public void GetForContentType_WithCSharp_ReturnsCSharpStyle()
    {
        var result = LanguageCommentStyle.GetForContentType(SupportedContentTypes.CSharp);

        Assert.IsNotNull(result);
        Assert.AreEqual(SupportedContentTypes.CSharp, result.ContentType);
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
        Assert.AreEqual(SupportedContentTypes.CSharp, result.ContentType);
    }

    [TestMethod]
    public void GetForContentType_WithBasic_ReturnsVisualBasicStyle()
    {
        var result = LanguageCommentStyle.GetForContentType(SupportedContentTypes.VisualBasic);

        Assert.IsNotNull(result);
        Assert.AreEqual(SupportedContentTypes.VisualBasic, result.ContentType);
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
        Assert.AreEqual(SupportedContentTypes.VisualBasic, result.ContentType);
    }

    [TestMethod]
    public void GetForContentType_WithCppSlash_ReturnsCppStyle()
    {
        var result = LanguageCommentStyle.GetForContentType(SupportedContentTypes.CPlusPlus);

        Assert.IsNotNull(result);
        Assert.AreEqual(SupportedContentTypes.CPlusPlus, result.ContentType);
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
        Assert.AreEqual(SupportedContentTypes.CPlusPlus, result.ContentType);
    }

    [TestMethod]
    public void GetForContentType_WithFSharp_ReturnsFSharpStyle()
    {
        var result = LanguageCommentStyle.GetForContentType("F#");

        Assert.IsNotNull(result);
        Assert.AreEqual(SupportedContentTypes.FSharp, result.ContentType);
        Assert.AreEqual("///", result.SingleLineDocPrefix);
        Assert.IsNull(result.MultiLineDocStart);
    }

    [TestMethod]
    public void GetForContentType_WithJavaScript_ReturnsJavaScriptStyle()
    {
        var result = LanguageCommentStyle.GetForContentType(SupportedContentTypes.JavaScript);

        Assert.IsNotNull(result);
        Assert.AreEqual(SupportedContentTypes.JavaScript, result.ContentType);
        Assert.AreEqual("///", result.SingleLineDocPrefix);
        Assert.IsNull(result.MultiLineDocStart);
    }

    [TestMethod]
    public void GetForContentType_WithFSharpSymbol_ReturnsFSharpStyle()
    {
        var result = LanguageCommentStyle.GetForContentType("F#");

        Assert.IsNotNull(result);
        Assert.AreEqual(SupportedContentTypes.FSharp, result.ContentType);
    }

    [TestMethod]
    public void GetForContentType_WithTypeScript_ReturnsTypeScriptStyle()
    {
        var result = LanguageCommentStyle.GetForContentType(SupportedContentTypes.TypeScript);

        Assert.IsNotNull(result);
        Assert.AreEqual(SupportedContentTypes.TypeScript, result.ContentType);
        Assert.AreEqual("///", result.SingleLineDocPrefix);
        Assert.IsNull(result.MultiLineDocStart);
    }

    [TestMethod]
    public void GetForContentType_WithRazor_ReturnsRazorStyle()
    {
        var result = LanguageCommentStyle.GetForContentType(SupportedContentTypes.Razor);

        Assert.IsNotNull(result);
        Assert.AreEqual(SupportedContentTypes.Razor, result.ContentType);
        Assert.AreEqual("///", result.SingleLineDocPrefix);
        Assert.IsNull(result.MultiLineDocStart);
    }

    [TestMethod]
    public void GetForContentType_WithSql_ReturnsSqlStyle()
    {
        var result = LanguageCommentStyle.GetForContentType(SupportedContentTypes.Sql);

        Assert.IsNotNull(result);
        Assert.AreEqual(SupportedContentTypes.Sql, result.ContentType);
        Assert.AreEqual("--", result.SingleLineDocPrefix);
        Assert.AreEqual("/*", result.MultiLineDocStart);
        Assert.AreEqual("*/", result.MultiLineDocEnd);
    }

    [TestMethod]
    public void GetForContentType_WithPowerShell_ReturnsPowerShellStyle()
    {
        var result = LanguageCommentStyle.GetForContentType(SupportedContentTypes.PowerShell);

        Assert.IsNotNull(result);
        Assert.AreEqual(SupportedContentTypes.PowerShell, result.ContentType);
        Assert.AreEqual("#", result.SingleLineDocPrefix);
        Assert.AreEqual("<#", result.MultiLineDocStart);
        Assert.AreEqual("#>", result.MultiLineDocEnd);
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
