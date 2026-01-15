using System.Text;
using System.Text.Json;

namespace AgentSandbox.Core.FileSystem;

/// <summary>
/// In-memory virtual filesystem for agent sandboxing.
/// Thread-safe and serializable for snapshotting.
/// Implements IFileSystem, ISnapshotableFileSystem, and IFileSystemStats.
/// Uses IFileStorage for pluggable storage backends.
/// </summary>
public class VirtualFileSystem : IFileSystem, ISnapshotableFileSystem, IFileSystemStats
{
    private readonly IFileStorage _storage;
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new VirtualFileSystem with in-memory storage.
    /// </summary>
    public VirtualFileSystem() : this(new InMemoryFileStorage())
    {
    }

    /// <summary>
    /// Creates a new VirtualFileSystem with the specified storage backend.
    /// </summary>
    /// <param name="storage">Storage backend for persisting file entries.</param>
    public VirtualFileSystem(IFileStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        
        // Initialize root directory if not exists
        if (!_storage.Exists("/"))
        {
            _storage.Set("/", new FileEntry
            {
                Path = "/",
                Name = "/",
                IsDirectory = true,
                Mode = 0755
            });
        }
    }

    #region IFileSystem - Path Operations
    
    /// <inheritdoc />
    public bool Exists(string path)
    {
        path = FileSystemPath.Normalize(path);
        return _storage.Exists(path);
    }
    
    /// <inheritdoc />
    public bool IsFile(string path)
    {
        path = FileSystemPath.Normalize(path);
        var entry = _storage.Get(path);
        return entry != null && !entry.IsDirectory;
    }

    /// <inheritdoc />
    public bool IsDirectory(string path)
    {
        path = FileSystemPath.Normalize(path);
        var entry = _storage.Get(path);
        return entry != null && entry.IsDirectory;
    }
    
    /// <inheritdoc />
    public FileEntry? GetEntry(string path)
    {
        path = FileSystemPath.Normalize(path);
        return _storage.Get(path);
    }

    #endregion

    #region IFileSystem - Directory Operations
    
    /// <inheritdoc />
    public void CreateDirectory(string path)
    {
        path = FileSystemPath.Normalize(path);
        if (path == "/") return;

        lock (_lock)
        {
            var existing = _storage.Get(path);
            if (existing != null)
            {
                if (!existing.IsDirectory)
                    throw new InvalidOperationException($"Path exists as a file: {path}");
                return;
            }

            // Create parent directories
            var parent = FileSystemPath.GetParent(path);
            if (!Exists(parent))
            {
                CreateDirectory(parent);
            }

            _storage.Set(path, new FileEntry
            {
                Path = path,
                Name = FileSystemPath.GetName(path),
                IsDirectory = true,
                Mode = 0755
            });
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> ListDirectory(string path)
    {
        path = FileSystemPath.Normalize(path);
        
        var info = _storage.Get(path);
        if (info == null)
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        
        if (!info.IsDirectory)
            throw new InvalidOperationException($"Not a directory: {path}");

        return _storage.GetChildren(path)
            .Select(childPath => FileSystemPath.GetName(childPath))
            .OrderBy(name => name);
    }

    #endregion

    #region IFileSystem - File Read Operations
    
    /// <inheritdoc />
    public byte[] ReadFile(string path)
    {
        path = FileSystemPath.Normalize(path);
        
        var entry = _storage.Get(path);
        if (entry == null)
            throw new FileNotFoundException($"File not found: {path}");
        
        if (entry.IsDirectory)
            throw new InvalidOperationException($"Cannot read directory: {path}");
        
        return entry.Content;
    }
    
    /// <inheritdoc />
    public string ReadFile(string path, Encoding encoding)
    {
        return encoding.GetString(ReadFile(path));
    }
    
    /// <inheritdoc />
    public IEnumerable<string> ReadFileLines(string path, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var text = ReadFile(path, encoding);
        return text.Split('\n');
    }
    
    /// <inheritdoc />
    public Stream OpenRead(string path)
    {
        var bytes = ReadFile(path);
        return new MemoryStream(bytes, writable: false);
    }

    #endregion

    #region IFileSystem - File Write Operations
    
    /// <inheritdoc />
    public void WriteFile(string path, byte[] content)
    {
        path = FileSystemPath.Normalize(path);
        
        lock (_lock)
        {
            var parent = FileSystemPath.GetParent(path);
            if (!Exists(parent))
            {
                CreateDirectory(parent);
            }

            var existing = _storage.Get(path);
            if (existing != null)
            {
                if (existing.IsDirectory)
                    throw new InvalidOperationException($"Cannot write to directory: {path}");
                
                existing.Content = content;
                existing.ModifiedAt = DateTime.UtcNow;
                _storage.Set(path, existing);
            }
            else
            {
                _storage.Set(path, new FileEntry
                {
                    Path = path,
                    Name = FileSystemPath.GetName(path),
                    IsDirectory = false,
                    Content = content,
                    Mode = 0644
                });
            }
        }
    }

    /// <inheritdoc />
    public void WriteFile(string path, string content, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        WriteFile(path, encoding.GetBytes(content));
    }
    
    /// <inheritdoc />
    public void WriteFile(string path, IEnumerable<string> lines, Encoding? encoding = null)
    {
        WriteFile(path, string.Join("\n", lines), encoding);
    }
    
    /// <inheritdoc />
    public void AppendToFile(string path, byte[] content)
    {
        path = FileSystemPath.Normalize(path);
        
        lock (_lock)
        {
            var existing = _storage.Get(path);
            if (existing != null)
            {
                if (existing.IsDirectory)
                    throw new InvalidOperationException($"Cannot append to directory: {path}");
                
                var newContent = new byte[existing.Content.Length + content.Length];
                existing.Content.CopyTo(newContent, 0);
                content.CopyTo(newContent, existing.Content.Length);
                existing.Content = newContent;
                existing.ModifiedAt = DateTime.UtcNow;
                _storage.Set(path, existing);
            }
            else
            {
                WriteFile(path, content);
            }
        }
    }
    
    /// <inheritdoc />
    public void AppendToFile(string path, string content, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        AppendToFile(path, encoding.GetBytes(content));
    }
    
    /// <inheritdoc />
    public Stream OpenWrite(string path, bool append = false)
    {
        return new VirtualFileWriteStream(this, path, append);
    }

    #endregion

    #region IFileSystem - Delete Operations
    
    /// <inheritdoc />
    public void DeleteFile(string path)
    {
        path = FileSystemPath.Normalize(path);
        
        var info = _storage.Get(path);
        if (info == null)
            throw new FileNotFoundException($"File not found: {path}");
        
        if (info.IsDirectory)
            throw new InvalidOperationException($"Path is a directory, use DeleteDirectory: {path}");
        
        _storage.Delete(path);
    }
    
    /// <inheritdoc />
    public void DeleteDirectory(string path, bool recursive = false)
    {
        path = FileSystemPath.Normalize(path);
        
        if (path == "/")
            throw new InvalidOperationException("Cannot delete root directory");
        
        var info = _storage.Get(path);
        if (info == null)
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        
        if (!info.IsDirectory)
            throw new InvalidOperationException($"Path is not a directory: {path}");
        
        lock (_lock)
        {
            var children = ListDirectory(path).ToList();
            if (children.Count > 0 && !recursive)
                throw new InvalidOperationException($"Directory not empty: {path}");

            if (recursive)
            {
                foreach (var child in children)
                {
                    Delete(FileSystemPath.Combine(path, child), recursive: true);
                }
            }

            _storage.Delete(path);
        }
    }

    /// <inheritdoc />
    public void Delete(string path, bool recursive = false)
    {
        path = FileSystemPath.Normalize(path);
        
        if (path == "/")
            throw new InvalidOperationException("Cannot delete root directory");

        lock (_lock)
        {
            var info = _storage.Get(path);
            if (info == null)
                throw new FileNotFoundException($"Path not found: {path}");

            if (info.IsDirectory)
            {
                DeleteDirectory(path, recursive);
            }
            else
            {
                DeleteFile(path);
            }
        }
    }

    #endregion

    #region IFileSystem - Copy/Move Operations

    /// <inheritdoc />
    public void Copy(string source, string destination, bool overwrite = false)
    {
        source = FileSystemPath.Normalize(source);
        destination = FileSystemPath.Normalize(destination);

        var entry = _storage.Get(source);
        if (entry == null)
            throw new FileNotFoundException($"Source not found: {source}");
        
        if (!overwrite && Exists(destination))
            throw new InvalidOperationException($"Destination already exists: {destination}");

        lock (_lock)
        {
            if (entry.IsDirectory)
            {
                CreateDirectory(destination);
                foreach (var child in ListDirectory(source))
                {
                    Copy(FileSystemPath.Combine(source, child), FileSystemPath.Combine(destination, child), overwrite);
                }
            }
            else
            {
                WriteFile(destination, entry.Content);
            }
        }
    }

    /// <inheritdoc />
    public void Move(string source, string destination, bool overwrite = false)
    {
        Copy(source, destination, overwrite);
        Delete(source, recursive: true);
    }

    #endregion

    #region ISnapshotableFileSystem
    
    /// <inheritdoc />
    public byte[] CreateSnapshot()
    {
        if (_storage is ISerializableFileStorage serializable)
        {
            return serializable.Serialize();
        }
        
        // Fallback: serialize via GetAll
        var snapshot = _storage.GetAll()
            .ToDictionary(
                kvp => kvp.Key,
                kvp => new FileEntry
                {
                    Name = kvp.Value.Name,
                    Path = kvp.Value.Path,
                    IsDirectory = kvp.Value.IsDirectory,
                    Content = kvp.Value.Content,
                    CreatedAt = kvp.Value.CreatedAt,
                    ModifiedAt = kvp.Value.ModifiedAt,
                    Mode = kvp.Value.Mode
                });
        
        return JsonSerializer.SerializeToUtf8Bytes(snapshot, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }
    
    /// <inheritdoc />
    public void RestoreSnapshot(byte[] snapshotData)
    {
        if (_storage is ISerializableFileStorage serializable)
        {
            serializable.Deserialize(snapshotData);
            return;
        }
        
        // Fallback: deserialize and populate storage
        var snapshot = JsonSerializer.Deserialize<Dictionary<string, FileEntry>>(snapshotData);
        if (snapshot == null) return;

        lock (_lock)
        {
            _storage.Clear();
            foreach (var kvp in snapshot)
            {
                _storage.Set(kvp.Key, kvp.Value);
            }
        }
    }

    #endregion

    #region IFileSystemStats
    
    /// <inheritdoc />
    public long TotalSize
    {
        get
        {
            long total = 0;
            foreach (var kvp in _storage.GetAll())
            {
                if (!kvp.Value.IsDirectory)
                {
                    total += kvp.Value.Content.Length;
                }
            }
            return total;
        }
    }
    
    /// <inheritdoc />
    public int FileCount => _storage.GetAll().Count(kvp => !kvp.Value.IsDirectory);
    
    /// <inheritdoc />
    public int DirectoryCount => _storage.GetAll().Count(kvp => kvp.Value.IsDirectory);
    
    /// <inheritdoc />
    public int NodeCount => _storage.Count;

    #endregion
}

/// <summary>
/// A writable stream that writes to the virtual filesystem on dispose.
/// </summary>
internal class VirtualFileWriteStream : MemoryStream
{
    private readonly VirtualFileSystem _fs;
    private readonly string _path;
    private readonly bool _append;
    private bool _disposed;

    public VirtualFileWriteStream(VirtualFileSystem fs, string path, bool append)
    {
        _fs = fs;
        _path = path;
        _append = append;
        
        // If appending, load existing content
        if (append && fs.Exists(path) && fs.IsFile(path))
        {
            var existing = fs.ReadFile(path);
            Write(existing, 0, existing.Length);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
            _fs.WriteFile(_path, ToArray());
        }
        base.Dispose(disposing);
    }
}