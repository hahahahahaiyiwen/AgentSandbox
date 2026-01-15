namespace AgentSandbox.Core.FileSystem;

/// <summary>
/// Storage backend interface for persisting file system entries.
/// Implementations can be in-memory, disk-based, remote KV stores, etc.
/// </summary>
public interface IFileStorage
{
    #region Basic Operations
    
    /// <summary>
    /// Gets a file entry by its path.
    /// </summary>
    /// <param name="path">Normalized path to the file/directory.</param>
    /// <returns>The file entry, or null if not found.</returns>
    FileEntry? Get(string path);
    
    /// <summary>
    /// Stores a file entry at the given path.
    /// </summary>
    /// <param name="path">Normalized path to store at.</param>
    /// <param name="entry">The file entry to store.</param>
    void Set(string path, FileEntry entry);
    
    /// <summary>
    /// Deletes a file entry at the given path.
    /// </summary>
    /// <param name="path">Normalized path to delete.</param>
    /// <returns>True if deleted, false if not found.</returns>
    bool Delete(string path);
    
    /// <summary>
    /// Checks if a path exists in storage.
    /// </summary>
    /// <param name="path">Normalized path to check.</param>
    /// <returns>True if exists.</returns>
    bool Exists(string path);
    
    #endregion
    
    #region Enumeration
    
    /// <summary>
    /// Gets all paths in storage.
    /// </summary>
    /// <returns>All stored paths.</returns>
    IEnumerable<string> GetAllPaths();
    
    /// <summary>
    /// Gets all paths that start with a given prefix.
    /// </summary>
    /// <param name="prefix">Path prefix to match.</param>
    /// <returns>Matching paths.</returns>
    IEnumerable<string> GetPathsByPrefix(string prefix);
    
    /// <summary>
    /// Gets immediate children of a directory path.
    /// </summary>
    /// <param name="directoryPath">Normalized directory path.</param>
    /// <returns>Child paths (full paths, not just names).</returns>
    IEnumerable<string> GetChildren(string directoryPath);
    
    #endregion
    
    #region Bulk Operations
    
    /// <summary>
    /// Clears all entries from storage.
    /// </summary>
    void Clear();
    
    /// <summary>
    /// Gets the count of all entries.
    /// </summary>
    int Count { get; }
    
    /// <summary>
    /// Gets all entries as key-value pairs.
    /// </summary>
    /// <returns>All path-entry pairs.</returns>
    IEnumerable<KeyValuePair<string, FileEntry>> GetAll();
    
    /// <summary>
    /// Sets multiple entries at once (batch operation).
    /// </summary>
    /// <param name="entries">Entries to set.</param>
    void SetMany(IEnumerable<KeyValuePair<string, FileEntry>> entries);
    
    #endregion
}

/// <summary>
/// Extended storage interface with async support for remote backends.
/// </summary>
public interface IAsyncFileStorage
{
    /// <summary>
    /// Gets a file entry by its path asynchronously.
    /// </summary>
    Task<FileEntry?> GetAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stores a file entry at the given path asynchronously.
    /// </summary>
    Task SetAsync(string path, FileEntry entry, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a file entry at the given path asynchronously.
    /// </summary>
    Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a path exists in storage asynchronously.
    /// </summary>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all paths that start with a given prefix asynchronously.
    /// </summary>
    Task<IEnumerable<string>> GetPathsByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets immediate children of a directory path asynchronously.
    /// </summary>
    Task<IEnumerable<string>> GetChildrenAsync(string directoryPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clears all entries from storage asynchronously.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all entries as key-value pairs asynchronously.
    /// </summary>
    Task<IEnumerable<KeyValuePair<string, FileEntry>>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets multiple entries at once asynchronously (batch operation).
    /// </summary>
    Task SetManyAsync(IEnumerable<KeyValuePair<string, FileEntry>> entries, CancellationToken cancellationToken = default);
}

/// <summary>
/// Storage interface that supports serialization for snapshots.
/// </summary>
public interface ISerializableFileStorage : IFileStorage
{
    /// <summary>
    /// Serializes all storage contents to bytes.
    /// </summary>
    /// <returns>Serialized data.</returns>
    byte[] Serialize();
    
    /// <summary>
    /// Deserializes and restores storage contents from bytes.
    /// </summary>
    /// <param name="data">Serialized data from Serialize().</param>
    void Deserialize(byte[] data);
}
