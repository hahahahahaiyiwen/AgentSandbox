using AgentSandbox.Core.FileSystem;

namespace AgentSandbox.Core.Shell;

/// <summary>
/// Provides context for shell command execution.
/// </summary>
public interface IShellContext
{
    /// <summary>
    /// The virtual filesystem.
    /// </summary>
    IFileSystem FileSystem { get; }

    /// <summary>
    /// Current working directory.
    /// </summary>
    string CurrentDirectory { get; set; }

    /// <summary>
    /// Environment variables.
    /// </summary>
    IDictionary<string, string> Environment { get; }

    /// <summary>
    /// Resolves a path relative to the current directory.
    /// </summary>
    string ResolvePath(string path);
}
