using System.IO;

namespace CommentsVS.Services
{
    /// <summary>
    /// Path helpers for filtering files to a workspace root.
    /// </summary>
    public static class WorkspacePathFilter
    {
        /// <summary>
        /// Returns true when <paramref name="filePath"/> is physically located inside <paramref name="rootPath"/>.
        /// </summary>
        public static bool IsFileWithinRoot(string filePath, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(rootPath))
            {
                return false;
            }

            try
            {
                var fullFilePath = Path.GetFullPath(filePath);
                var fullRootPath = EnsureTrailingSeparator(Path.GetFullPath(rootPath));

                return fullFilePath.StartsWith(fullRootPath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }
}
