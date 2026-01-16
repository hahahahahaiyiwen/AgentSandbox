namespace AgentSandbox.Core.FileSystem;

/// <summary>
/// Configuration options for filesystem size limits.
/// </summary>
public class FileSystemOptions
{
    /// <summary>Maximum total size of all files in bytes (null = unlimited).</summary>
    public long? MaxTotalSize { get; set; }
    
    /// <summary>Maximum size of a single file in bytes (null = unlimited).</summary>
    public long? MaxFileSize { get; set; }
    
    /// <summary>Maximum number of files/directories (null = unlimited).</summary>
    public int? MaxNodeCount { get; set; }
}