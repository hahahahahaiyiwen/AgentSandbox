using AgentSandbox.Core.FileSystem;
using AgentSandbox.Core.Shell;

namespace AgentSandbox.Core;

/// <summary>
/// Represents a sandboxed execution environment with virtual filesystem and shell.
/// </summary>
public class Sandbox : IDisposable
{
    public string Id { get; }
    public FileSystem.FileSystem FileSystem { get; }
    public SandboxShell Shell { get; }
    public SandboxOptions Options { get; }
    public DateTime CreatedAt { get; }
    public DateTime LastActivityAt { get; private set; }
    
    private readonly List<ShellResult> _commandHistory = new();
    private readonly Action<string>? _onDisposed;
    private bool _disposed;

    public Sandbox(string? id = null, SandboxOptions? options = null, Action<string>? onDisposed = null)
    {
        Id = id ?? Guid.NewGuid().ToString("N")[..12];
        Options = options ?? new SandboxOptions();
        _onDisposed = onDisposed;
        
        // Create filesystem with size limits from options
        var fsOptions = new FileSystemOptions
        {
            MaxTotalSize = Options.MaxTotalSize,
            MaxFileSize = Options.MaxFileSize,
            MaxNodeCount = Options.MaxNodeCount
        };
        FileSystem = new FileSystem.FileSystem(fsOptions);
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

        // Register shell extensions
        foreach (var cmd in Options.ShellExtensions)
        {
            Shell.RegisterCommand(cmd);
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
        TotalSize = FileSystem.TotalSize, // in bytes
        CommandCount = _commandHistory.Count,
        CurrentDirectory = Shell.CurrentDirectory,
        CreatedAt = CreatedAt,
        LastActivityAt = LastActivityAt
    };

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Sandbox));
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _commandHistory.Clear();
        
        // Notify manager to remove reference
        _onDisposed?.Invoke(Id);
        
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
