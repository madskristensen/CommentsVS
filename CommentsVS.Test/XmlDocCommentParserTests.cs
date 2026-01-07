using CommentsVS.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace CommentsVS.Test;

[TestClass]
public sealed class XmlDocCommentParserTests
{
    private static ITextSnapshot CreateSnapshot(string text)
    {
        var contentType = new MockContentType("CSharp");
        var textBuffer = new MockTextBuffer(text, contentType);
        return textBuffer.CurrentSnapshot;
    }

    #region Single-Line Comment Tests

    [TestMethod]
    public void FindAllCommentBlocks_WithSingleLineSummary_FindsBlock()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/// <summary>\n/// Test summary.\n/// </summary>";
        var snapshot = CreateSnapshot(text);

        var blocks = parser.FindAllCommentBlocks(snapshot);

        Assert.HasCount(1, blocks);
        Assert.AreEqual(0, blocks[0].StartLine);
        Assert.AreEqual(2, blocks[0].EndLine);
        Assert.IsFalse(blocks[0].IsMultiLineStyle);
        Assert.IsTrue(blocks[0].XmlContent.Contains("<summary>"));
        Assert.IsTrue(blocks[0].XmlContent.Contains("Test summary."));
    }

    [TestMethod]
    public void FindAllCommentBlocks_WithMultipleSingleLineBlocks_FindsAll()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/// <summary>First</summary>\n\n/// <summary>Second</summary>";
        var snapshot = CreateSnapshot(text);

        var blocks = parser.FindAllCommentBlocks(snapshot);

        Assert.HasCount(2, blocks);
        Assert.IsTrue(blocks[0].XmlContent.Contains("First"));
        Assert.IsTrue(blocks[1].XmlContent.Contains("Second"));
    }

    [TestMethod]
    public void FindAllCommentBlocks_WithIndentation_PreservesIndentation()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "    /// <summary>Indented comment</summary>";
        var snapshot = CreateSnapshot(text);

        var blocks = parser.FindAllCommentBlocks(snapshot);

        Assert.HasCount(1, blocks);
        Assert.AreEqual("    ", blocks[0].Indentation);
    }

    [TestMethod]
    public void FindAllCommentBlocks_WithNoComments_ReturnsEmpty()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "public class Test { }";
        var snapshot = CreateSnapshot(text);

        var blocks = parser.FindAllCommentBlocks(snapshot);

        Assert.IsEmpty(blocks);
    }

    #endregion

    #region Multi-Line Comment Tests

    [TestMethod]
    public void FindAllCommentBlocks_WithMultiLineComment_FindsBlock()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/**\n * <summary>Test</summary>\n */";
        var snapshot = CreateSnapshot(text);

        var blocks = parser.FindAllCommentBlocks(snapshot);

        Assert.HasCount(1, blocks);
        Assert.IsTrue(blocks[0].IsMultiLineStyle);
        Assert.IsTrue(blocks[0].XmlContent.Contains("<summary>"));
    }

    [TestMethod]
    public void FindAllCommentBlocks_WithSingleLineMultiLineComment_FindsBlock()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/** <summary>Test</summary> */";
        var snapshot = CreateSnapshot(text);

        var blocks = parser.FindAllCommentBlocks(snapshot);

        Assert.HasCount(1, blocks);
        Assert.IsTrue(blocks[0].IsMultiLineStyle);
        Assert.AreEqual(0, blocks[0].StartLine);
        Assert.AreEqual(0, blocks[0].EndLine);
    }

    [TestMethod]
    public void FindAllCommentBlocks_WithMultiLineAndContinuation_ExtractsContent()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/**\n * <summary>\n * Test content\n * </summary>\n */";
        var snapshot = CreateSnapshot(text);

        var blocks = parser.FindAllCommentBlocks(snapshot);

        Assert.HasCount(1, blocks);
        Assert.IsTrue(blocks[0].XmlContent.Contains("Test content"));
    }

    #endregion

    #region FindCommentBlockAtPosition Tests

    [TestMethod]
    public void FindCommentBlockAtPosition_InCommentBlock_ReturnsBlock()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/// <summary>Test</summary>";
        var snapshot = CreateSnapshot(text);

        var block = parser.FindCommentBlockAtPosition(snapshot, 10);

        Assert.IsNotNull(block);
        Assert.IsTrue(block.XmlContent.Contains("Test"));
    }

    [TestMethod]
    public void FindCommentBlockAtPosition_OutsideComment_ReturnsNull()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/// <summary>Test</summary>\npublic class Test { }";
        var snapshot = CreateSnapshot(text);

        var block = parser.FindCommentBlockAtPosition(snapshot, 40);

        Assert.IsNull(block);
    }

    [TestMethod]
    public void FindCommentBlockAtPosition_WithInvalidPosition_ReturnsNull()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/// <summary>Test</summary>";
        var snapshot = CreateSnapshot(text);

        var blockNegative = parser.FindCommentBlockAtPosition(snapshot, -1);
        var blockTooLarge = parser.FindCommentBlockAtPosition(snapshot, 1000);

        Assert.IsNull(blockNegative);
        Assert.IsNull(blockTooLarge);
    }

    [TestMethod]
    public void FindCommentBlockAtPosition_WithNullSnapshot_ThrowsArgumentNullException()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);

        Assert.ThrowsException<ArgumentNullException>(() =>
            parser.FindCommentBlockAtPosition(null, 0));
    }

    #endregion

    #region FindCommentBlocksInSpan Tests

    [TestMethod]
    public void FindCommentBlocksInSpan_WithSpanCoveringBlock_FindsBlock()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/// <summary>Test</summary>\npublic class Test { }";
        var snapshot = CreateSnapshot(text);
        var span = new Span(0, 30);

        var blocks = parser.FindCommentBlocksInSpan(snapshot, span);

        Assert.HasCount(1, blocks);
    }

    [TestMethod]
    public void FindCommentBlocksInSpan_WithSpanNotCoveringBlock_ReturnsEmpty()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/// <summary>Test</summary>\npublic class Test { }";
        var snapshot = CreateSnapshot(text);
        var span = new Span(30, 20);

        var blocks = parser.FindCommentBlocksInSpan(snapshot, span);

        Assert.IsEmpty(blocks);
    }

    [TestMethod]
    public void FindCommentBlocksInSpan_WithMultipleBlocks_FindsAll()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/// <summary>First</summary>\n\n/// <summary>Second</summary>";
        var snapshot = CreateSnapshot(text);
        var span = new Span(0, text.Length);

        var blocks = parser.FindCommentBlocksInSpan(snapshot, span);

        Assert.HasCount(2, blocks);
    }

    [TestMethod]
    public void FindCommentBlocksInSpan_WithNullSnapshot_ThrowsArgumentNullException()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);

        Assert.ThrowsException<ArgumentNullException>(() =>
            parser.FindCommentBlocksInSpan(null, new Span(0, 10)));
    }

    #endregion

    #region Visual Basic Style Tests

    [TestMethod]
    public void FindAllCommentBlocks_WithVisualBasicStyle_FindsBlock()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.VisualBasic);
        var text = "''' <summary>\n''' Test summary.\n''' </summary>";
        var snapshot = CreateSnapshot(text);

        var blocks = parser.FindAllCommentBlocks(snapshot);

        Assert.HasCount(1, blocks);
        Assert.IsFalse(blocks[0].IsMultiLineStyle);
        Assert.IsTrue(blocks[0].XmlContent.Contains("Test summary."));
    }

    #endregion

    #region Content Extraction Tests

    [TestMethod]
    public void FindAllCommentBlocks_ExtractsXmlContentCorrectly()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/// <summary>Line 1</summary>\n/// <param name=\"x\">Parameter</param>";
        var snapshot = CreateSnapshot(text);

        var blocks = parser.FindAllCommentBlocks(snapshot);

        Assert.HasCount(1, blocks);
        var content = blocks[0].XmlContent;
        Assert.IsTrue(content.Contains("<summary>Line 1</summary>"));
        Assert.IsTrue(content.Contains("<param name=\"x\">Parameter</param>"));
    }

    [TestMethod]
    public void FindAllCommentBlocks_StripsCommentPrefixes()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/// <summary>Test</summary>";
        var snapshot = CreateSnapshot(text);

        var blocks = parser.FindAllCommentBlocks(snapshot);

        Assert.HasCount(1, blocks);
        // XML content should not contain the /// prefix
        Assert.IsFalse(blocks[0].XmlContent.Contains("///"));
    }

    #endregion

    #region Span Tests

    [TestMethod]
    public void FindAllCommentBlocks_SetsCorrectSpan()
    {
        var parser = new XmlDocCommentParser(LanguageCommentStyle.CSharp);
        var text = "/// <summary>Test</summary>";
        var snapshot = CreateSnapshot(text);

        var blocks = parser.FindAllCommentBlocks(snapshot);

        Assert.HasCount(1, blocks);
        Assert.AreEqual(0, blocks[0].Span.Start);
        Assert.IsTrue(blocks[0].Span.Length > 0);
        Assert.IsTrue(blocks[0].Span.End <= text.Length);
    }

    #endregion
}

// Mock classes for testing
internal class MockContentType : IContentType
{
    private readonly string _typeName;

    public MockContentType(string typeName)
    {
        _typeName = typeName;
    }

    public string TypeName => _typeName;
    public string DisplayName => _typeName;
    public IEnumerable<IContentType> BaseTypes => [];
    public bool IsOfType(string type) => string.Equals(_typeName, type, StringComparison.OrdinalIgnoreCase);
}

internal class MockTextBuffer : ITextBuffer
{
    private readonly MockTextSnapshot _snapshot;

    public MockTextBuffer(string text, IContentType contentType)
    {
        _snapshot = new MockTextSnapshot(text, this, contentType);
    }

    public ITextSnapshot CurrentSnapshot => _snapshot;
    public IContentType ContentType => _snapshot.ContentType;

    // Required interface members (not used in tests)
    public PropertyCollection Properties => throw new NotImplementedException();
    public bool EditInProgress => throw new NotImplementedException();
    public event EventHandler<TextContentChangedEventArgs> Changed { add { } remove { } }
    public event EventHandler<TextContentChangedEventArgs> ChangedLowPriority { add { } remove { } }
    public event EventHandler<TextContentChangedEventArgs> ChangedHighPriority { add { } remove { } }
    public event EventHandler<TextContentChangingEventArgs> Changing { add { } remove { } }
    public event EventHandler PostChanged { add { } remove { } }
    public event EventHandler<ContentTypeChangedEventArgs> ContentTypeChanged { add { } remove { } }
    public event EventHandler<SnapshotSpanEventArgs> ReadOnlyRegionsChanged { add { } remove { } }
    public void ChangeContentType(IContentType newContentType, object editTag) => throw new NotImplementedException();
    public bool CheckEditAccess() => throw new NotImplementedException();
    public ITextEdit CreateEdit(EditOptions options, int? reiteratedVersionNumber, object editTag) => throw new NotImplementedException();
    public ITextEdit CreateEdit() => throw new NotImplementedException();
    public IReadOnlyRegionEdit CreateReadOnlyRegionEdit() => throw new NotImplementedException();
    public ITextSnapshot Delete(Span deleteSpan) => throw new NotImplementedException();
    public NormalizedSpanCollection GetReadOnlyExtents(Span span) => throw new NotImplementedException();
    public ITextSnapshot Insert(int position, string text) => throw new NotImplementedException();
    public bool IsReadOnly(Span span, bool isEdit) => throw new NotImplementedException();
    public bool IsReadOnly(Span span) => throw new NotImplementedException();
    public bool IsReadOnly(int position) => throw new NotImplementedException();
    public ITextSnapshot Replace(Span replaceSpan, string replaceWith) => throw new NotImplementedException();
    public void TakeThreadOwnership() => throw new NotImplementedException();
}

internal class MockTextSnapshot : ITextSnapshot
{
    private readonly string _text;
    private readonly ITextBuffer _buffer;
    private readonly IContentType _contentType;
    private readonly List<ITextSnapshotLine> _lines;

    public MockTextSnapshot(string text, ITextBuffer buffer, IContentType contentType)
    {
        _text = text ?? "";
        _buffer = buffer;
        _contentType = contentType;
        _lines = CreateLines();
    }

    private List<ITextSnapshotLine> CreateLines()
    {
        var lines = new List<ITextSnapshotLine>();
        var lineBreaks = new[] { "\r\n", "\n" };
        var currentPosition = 0;
        var lineNumber = 0;

        while (currentPosition < _text.Length)
        {
            var lineEnd = currentPosition;
            var lineBreakLength = 0;

            // Find the next line break
            for (var i = currentPosition; i < _text.Length; i++)
            {
                if (i + 1 < _text.Length && _text.Substring(i, 2) == "\r\n")
                {
                    lineEnd = i;
                    lineBreakLength = 2;
                    break;
                }
                else if (_text[i] == '\n')
                {
                    lineEnd = i;
                    lineBreakLength = 1;
                    break;
                }
            }

            if (lineBreakLength == 0)
            {
                lineEnd = _text.Length;
            }

            lines.Add(new MockTextSnapshotLine(this, lineNumber++, currentPosition, lineEnd - currentPosition, lineBreakLength));
            currentPosition = lineEnd + lineBreakLength;
        }

        // Handle empty text or text ending without line break
        if (lines.Count == 0 || currentPosition == _text.Length && _text.Length > 0)
        {
            if (lines.Count == 0)
            {
                lines.Add(new MockTextSnapshotLine(this, 0, 0, _text.Length, 0));
            }
        }

        return lines;
    }

    public string GetText() => _text;
    public string GetText(Span span) => _text.Substring(span.Start, span.Length);
    public string GetText(int startIndex, int length) => _text.Substring(startIndex, length);
    public ITextSnapshotLine GetLineFromLineNumber(int lineNumber) => _lines[lineNumber];
    public ITextSnapshotLine GetLineFromPosition(int position)
    {
        foreach (var line in _lines)
        {
            if (position >= line.Start.Position && position <= line.End.Position)
            {
                return line;
            }
        }
        return _lines[_lines.Count - 1];
    }

    public int Length => _text.Length;
    public int LineCount => _lines.Count;
    public IContentType ContentType => _contentType;
    public ITextBuffer TextBuffer => _buffer;

    // Required interface members (not used in tests)
    public char this[int position] => _text[position];
    public IEnumerable<ITextSnapshotLine> Lines => _lines;
    public ITextVersion Version => throw new NotImplementedException();
    public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) => throw new NotImplementedException();
    public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode) => throw new NotImplementedException();
    public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => throw new NotImplementedException();
    public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode) => throw new NotImplementedException();
    public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => throw new NotImplementedException();
    public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode) => throw new NotImplementedException();
    public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => throw new NotImplementedException();
    public int GetLineNumberFromPosition(int position) => GetLineFromPosition(position).LineNumber;
    public char[] ToCharArray(int startIndex, int length) => _text.Substring(startIndex, length).ToCharArray();
    public void Write(TextWriter writer) => writer.Write(_text);
    public void Write(TextWriter writer, Span span) => writer.Write(_text.Substring(span.Start, span.Length));
}

internal class MockTextSnapshotLine : ITextSnapshotLine
{
    private readonly ITextSnapshot _snapshot;
    private readonly int _lineNumber;
    private readonly int _start;
    private readonly int _length;
    private readonly int _lineBreakLength;

    public MockTextSnapshotLine(ITextSnapshot snapshot, int lineNumber, int start, int length, int lineBreakLength)
    {
        _snapshot = snapshot;
        _lineNumber = lineNumber;
        _start = start;
        _length = length;
        _lineBreakLength = lineBreakLength;
    }

    public string GetText() => _snapshot.GetText(_start, _length);
    public string GetTextIncludingLineBreak() => _snapshot.GetText(_start, _length + _lineBreakLength);
    public string GetLineBreakText() => _lineBreakLength > 0 ? _snapshot.GetText(_start + _length, _lineBreakLength) : "";

    public int LineNumber => _lineNumber;
    public ITextSnapshot Snapshot => _snapshot;
    public SnapshotPoint Start => new SnapshotPoint(_snapshot, _start);
    public SnapshotPoint End => new SnapshotPoint(_snapshot, _start + _length);
    public SnapshotPoint EndIncludingLineBreak => new SnapshotPoint(_snapshot, _start + _length + _lineBreakLength);
    public int Length => _length;
    public int LengthIncludingLineBreak => _length + _lineBreakLength;
    public int LineBreakLength => _lineBreakLength;
    public Span Extent => new Span(_start, _length);
    public Span ExtentIncludingLineBreak => new Span(_start, _length + _lineBreakLength);
}
