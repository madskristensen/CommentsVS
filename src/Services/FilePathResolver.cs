using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CommentsVS.Services
{
    /// <summary>
    /// Resolves file paths from LINK anchor references.
    /// </summary>
    /// <remarks>
    /// Supports path resolution types:
    /// <list type="bullet">
    /// <item>Relative paths (<c>./</c>, <c>../</c>) - resolved from current file location</item>
    /// <item>Solution-relative (<c>/</c> or <c>~/</c>) - resolved from solution root</item>
    /// <item>Project-relative (<c>@/</c>) - resolved from current project root</item>
    /// <item>Plain paths - resolved from current file location</item>
    /// </list>
    /// </remarks>
    public class FilePathResolver
    {
        private readonly string _currentFilePath;
        private readonly string _solutionDirectory;
        private readonly string _projectDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="FilePathResolver"/> class.
        /// </summary>
        /// <param name="currentFilePath">The full path of the current file.</param>
        /// <param name="solutionDirectory">The solution directory (optional).</param>
        /// <param name="projectDirectory">The project directory (optional).</param>
        public FilePathResolver(string currentFilePath, string solutionDirectory = null, string projectDirectory = null)
        {
            _currentFilePath = currentFilePath;
            _solutionDirectory = solutionDirectory;
            _projectDirectory = projectDirectory;
        }

        /// <summary>
        /// Creates a FilePathResolver for the given file path, automatically detecting solution and project directories.
        /// </summary>
        /// <param name="currentFilePath">The full path of the current file.</param>
        /// <returns>A configured FilePathResolver instance.</returns>
        public static FilePathResolver Create(string currentFilePath)
        {
            string solutionDir = null;
            string projectDir = null;

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                solutionDir = GetSolutionDirectory();
                projectDir = GetProjectDirectory(currentFilePath);
            });

            return new FilePathResolver(currentFilePath, solutionDir, projectDir);
        }

        /// <summary>
        /// Resolves a path from a LINK reference to an absolute file path.
        /// </summary>
        /// <param name="linkPath">The path from the LINK reference.</param>
        /// <returns>The resolved absolute path, or null if resolution fails.</returns>
        public string Resolve(string linkPath)
        {
            if (string.IsNullOrWhiteSpace(linkPath))
            {
                return null;
            }

            // Normalize path separators
            string normalizedPath = linkPath.Replace('/', Path.DirectorySeparatorChar);

            string basePath;
            string relativePath;

            // Determine base path based on prefix
            if (normalizedPath.StartsWith("~/") || normalizedPath.StartsWith("~\\"))
            {
                // Solution-relative (~/path)
                basePath = _solutionDirectory;
                relativePath = normalizedPath.Substring(2);
            }
            else if (normalizedPath.StartsWith("/") || normalizedPath.StartsWith("\\"))
            {
                // Solution-relative (/path)
                basePath = _solutionDirectory;
                relativePath = normalizedPath.Substring(1);
            }
            else if (normalizedPath.StartsWith("@/") || normalizedPath.StartsWith("@\\"))
            {
                // Project-relative (@/path)
                basePath = _projectDirectory;
                relativePath = normalizedPath.Substring(2);
            }
            else
            {
                // Relative to current file (including ./ and ../)
                basePath = Path.GetDirectoryName(_currentFilePath);
                relativePath = normalizedPath;
            }

            if (string.IsNullOrEmpty(basePath))
            {
                return null;
            }

            try
            {
                string combinedPath = Path.Combine(basePath, relativePath);
                return Path.GetFullPath(combinedPath);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Resolves a path and checks if the file exists.
        /// </summary>
        /// <param name="linkPath">The path from the LINK reference.</param>
        /// <param name="resolvedPath">The resolved absolute path.</param>
        /// <returns>True if the file exists, false otherwise.</returns>
        public bool TryResolve(string linkPath, out string resolvedPath)
        {
            resolvedPath = Resolve(linkPath);
            return !string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath);
        }

        /// <summary>
        /// Validates whether a resolved path exists.
        /// </summary>
        /// <param name="resolvedPath">The resolved absolute path.</param>
        /// <returns>True if the file exists, false otherwise.</returns>
        public static bool FileExists(string resolvedPath)
        {
            return !string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath);
        }

        /// <summary>
        /// Gets the solution directory from the current VS instance.
        /// </summary>
        private static string GetSolutionDirectory()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution)) is IVsSolution solution)
            {
                solution.GetSolutionInfo(out string solutionDir, out _, out _);
                return solutionDir;
            }

            return null;
        }

        /// <summary>
        /// Gets the project directory for the file containing the link.
        /// </summary>
        private static string GetProjectDirectory(string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            if (ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution)) is IVsSolution solution)
            {
                // Find the project containing this file
                solution.GetProjectOfUniqueName(filePath, out IVsHierarchy hierarchy);
                if (hierarchy != null)
                {
                    hierarchy.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_ProjectDir, out object projectDirObj);
                    return projectDirObj as string;
                }
            }

            // Fallback: walk up directory tree looking for .csproj/.vbproj
            string dir = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir))
            {
                string[] projectFiles = Directory.GetFiles(dir, "*.csproj");
                if (projectFiles.Length > 0)
                {
                    return dir;
                }

                projectFiles = Directory.GetFiles(dir, "*.vbproj");
                if (projectFiles.Length > 0)
                {
                    return dir;
                }

                dir = Path.GetDirectoryName(dir);
            }

            return null;
        }

        /// <summary>
        /// Gets the current file's directory.
        /// </summary>
        public string CurrentFileDirectory => Path.GetDirectoryName(_currentFilePath);

        /// <summary>
        /// Gets the solution directory.
        /// </summary>
        public string SolutionDirectory => _solutionDirectory;

        /// <summary>
        /// Gets the project directory.
        /// </summary>
        public string ProjectDirectory => _projectDirectory;
    }
}
