namespace AgentSandbox.Core.FileSystem;

/// <summary>
/// Represents a file or directory entry with both metadata and content.
/// This is the core abstraction for filesystem nodes.
/// </summary>
public class FileEntry
{
    /// <summary>Name of the file or directory.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Full path to the file or directory.</summary>
    public string Path { get; set; } = string.Empty;
    
    /// <summary>Whether this is a directory.</summary>
    public bool IsDirectory { get; set; }
    
    /// <summary>File content as bytes (empty for directories).</summary>
    public byte[] Content { get; set; } = Array.Empty<byte>();
    
    /// <summary>Size in bytes (0 for directories).</summary>
    public long Size => IsDirectory ? 0 : Content.Length;
    
    /// <summary>When the file was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>When the file was last modified.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Unix-style permission mode (e.g., 0644 for files, 0755 for directories).</summary>
    public int Mode { get; set; } = 0644;
}