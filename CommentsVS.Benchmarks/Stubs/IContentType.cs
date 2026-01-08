namespace Microsoft.VisualStudio.Utilities;

/// <summary>
/// Stub for IContentType to allow benchmarking without VS dependencies.
/// </summary>
public interface IContentType
{
    string TypeName { get; }
    bool IsOfType(string type);
}
