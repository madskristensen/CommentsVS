namespace Microsoft.VisualStudio.Text;

/// <summary>
/// Stub for Microsoft.VisualStudio.Text.Span to allow benchmarking without VS dependencies.
/// </summary>
public readonly struct Span(int start, int length)
{
    public int Start { get; } = start;
    public int Length { get; } = length;
    public int End => Start + Length;
}
