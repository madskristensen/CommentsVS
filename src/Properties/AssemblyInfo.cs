using CommentsVS;

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Allows the test project to exercise internal types directly instead of duplicating
// production logic in mirrored test-only copies.
[assembly: InternalsVisibleTo("CommentsVS.Test")]

[assembly: AssemblyTitle(Vsix.Name)]
[assembly: AssemblyDescription(Vsix.Description)]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany(Vsix.Author)]
[assembly: AssemblyProduct(Vsix.Name)]
[assembly: AssemblyCopyright(Vsix.Author)]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: AssemblyVersion(Vsix.Version)]
[assembly: AssemblyFileVersion(Vsix.Version)]

namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}