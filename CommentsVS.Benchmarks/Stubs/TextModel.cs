namespace Microsoft.VisualStudio.Text;

/// <summary>
/// Stub for ITextBuffer to allow benchmarking without VS dependencies.
/// </summary>
public interface ITextBuffer
{
    ITextSnapshot CurrentSnapshot { get; }
}

/// <summary>
/// Stub for ITextSnapshot to allow benchmarking without VS dependencies.
/// </summary>
public interface ITextSnapshot
{
    int LineCount { get; }
    int Length { get; }
    ITextSnapshotLine GetLineFromLineNumber(int lineNumber);
    ITextSnapshotLine GetLineFromPosition(int position);
    Microsoft.VisualStudio.Utilities.IContentType ContentType { get; }
}

/// <summary>
/// Stub for ITextSnapshotLine.
/// </summary>
public interface ITextSnapshotLine
{
    int LineNumber { get; }
    SnapshotPoint Start { get; }
    SnapshotPoint End { get; }
    string GetText();
}

/// <summary>
/// Stub for SnapshotPoint.
/// </summary>
public struct SnapshotPoint
{
    public int Position { get; set; }
}
