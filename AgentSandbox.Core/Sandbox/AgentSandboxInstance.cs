using AgentSandbox.Core.FileSystem;
using AgentSandbox.Core.Shell;

namespace AgentSandbox.Core.Sandbox;

/// <summary>
/// Configuration options for a sandbox instance.
/// </summary>
public class SandboxOptions
{
    /// <summary>Maximum total size of all files in bytes (default: 100MB).</summary>
    public long MaxTotalSize { get; set; } = 100 * 1024 * 1024;
    
    /// <summary>Maximum size of a single file in bytes (default: 10MB).</summary>
    public long MaxFileSize { get; set; } = 10 * 1024 * 1024;
    
    /// <summary>Maximum number of files/directories (default: 10000).</summary>
    public int MaxNodeCount { get; set; } = 10000;
    
    /// <summary>Command execution timeout (default: 30 seconds).</summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>Initial environment variables.</summary>
    public Dictionary<string, string> Environment { get; set; } = new();
    
    /// <summary>Initial working directory.</summary>
    public string WorkingDirectory { get; set; } = "/";
}

/// <summary>
/// Represents a sandboxed execution environment with virtual filesystem and shell.
/// </summary>
public class AgentSandboxInstance : IDisposable
{
    public string Id { get; }
    public VirtualFileSystem FileSystem { get; }
    public SandboxShell Shell { get; }
    public SandboxOptions Options { get; }
    public DateTime CreatedAt { get; }
    public DateTime LastActivityAt { get; private set; }
    
    private readonly List<ShellResult> _commandHistory = new();
    private bool _disposed;

    public AgentSandboxInstance(string? id = null, SandboxOptions? options = null)
    {
        Id = id ?? Guid.NewGuid().ToString("N")[..12];
        Options = options ?? new SandboxOptions();
        FileSystem = new VirtualFileSystem();
        Shell = new SandboxShell(FileSystem);
        CreatedAt = DateTime.UtcNow;
        LastActivityAt = CreatedAt;

        // Apply initial environment
        foreach (var kvp in Options.Environment)
        {
            Shell.Execute($"export {kvp.Key}={kvp.Value}");
        }

        // Set initial working directory
        if (Options.WorkingDirectory != "/")
        {
            FileSystem.CreateDirectory(Options.WorkingDirectory);
            Shell.Execute($"cd {Options.WorkingDirectory}");
        }
    }

    /// <summary>
    /// Executes a shell command in the sandbox.
    /// </summary>
    public ShellResult Execute(string command)
    {
        ThrowIfDisposed();
        LastActivityAt = DateTime.UtcNow;
        
        var result = Shell.Execute(command);
        _commandHistory.Add(result);
        
        return result;
    }

    /// <summary>
    /// Reads a file from the virtual filesystem.
    /// </summary>
    public string ReadFile(string path)
    {
        ThrowIfDisposed();
        LastActivityAt = DateTime.UtcNow;
        return FileSystem.ReadFile(path, System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// Writes a file to the virtual filesystem with size validation.
    /// </summary>
    public void WriteFile(string path, string content)
    {
        ThrowIfDisposed();
        LastActivityAt = DateTime.UtcNow;

        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        
        if (bytes.Length > Options.MaxFileSize)
            throw new InvalidOperationException($"File size {bytes.Length} exceeds maximum {Options.MaxFileSize}");

        if (FileSystem.TotalSize + bytes.Length > Options.MaxTotalSize)
            throw new InvalidOperationException($"Total storage would exceed maximum {Options.MaxTotalSize}");

        if (FileSystem.NodeCount >= Options.MaxNodeCount && !FileSystem.Exists(path))
            throw new InvalidOperationException($"Maximum node count {Options.MaxNodeCount} reached");

        FileSystem.WriteFile(path, content);
    }

    /// <summary>
    /// Lists contents of a directory.
    /// </summary>
    public IEnumerable<string> ListDirectory(string path)
    {
        ThrowIfDisposed();
        LastActivityAt = DateTime.UtcNow;
        return FileSystem.ListDirectory(path);
    }

    /// <summary>
    /// Gets command execution history.
    /// </summary>
    public IReadOnlyList<ShellResult> GetHistory() => _commandHistory.AsReadOnly();

    /// <summary>
    /// Creates a snapshot of the entire sandbox state.
    /// </summary>
    public SandboxSnapshot CreateSnapshot()
    {
        ThrowIfDisposed();
        return new SandboxSnapshot
        {
            Id = Id,
            FileSystemData = FileSystem.CreateSnapshot(),
            CurrentDirectory = Shell.CurrentDirectory,
            Environment = new Dictionary<string, string>(Shell.Environment),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Restores sandbox state from a snapshot.
    /// </summary>
    public void RestoreSnapshot(SandboxSnapshot snapshot)
    {
        ThrowIfDisposed();
        FileSystem.RestoreSnapshot(snapshot.FileSystemData);
        Shell.Execute($"cd {snapshot.CurrentDirectory}");
        
        foreach (var kvp in snapshot.Environment)
        {
            Shell.Execute($"export {kvp.Key}={kvp.Value}");
        }
        
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets sandbox statistics.
    /// </summary>
    public SandboxStats GetStats() => new()
    {
        Id = Id,
        FileCount = FileSystem.NodeCount,
        TotalSize = FileSystem.TotalSize,
        CommandCount = _commandHistory.Count,
        CurrentDirectory = Shell.CurrentDirectory,
        CreatedAt = CreatedAt,
        LastActivityAt = LastActivityAt
    };

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AgentSandboxInstance));
    }

    public void Dispose()
    {
        _disposed = true;
        _commandHistory.Clear();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Snapshot of sandbox state for persistence/restoration.
/// </summary>
public class SandboxSnapshot
{
    public string Id { get; set; } = string.Empty;
    public byte[] FileSystemData { get; set; } = Array.Empty<byte>();
    public string CurrentDirectory { get; set; } = "/";
    public Dictionary<string, string> Environment { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Runtime statistics for a sandbox.
/// </summary>
public class SandboxStats
{
    public string Id { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long TotalSize { get; set; }
    public int CommandCount { get; set; }
    public string CurrentDirectory { get; set; } = "/";
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
}
