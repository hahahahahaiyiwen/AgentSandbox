using System.Collections.Concurrent;
using System.Text.Json;

namespace AgentSandbox.Core.FileSystem;

/// <summary>
/// In-memory implementation of IFileStorage using ConcurrentDictionary.
/// Thread-safe and suitable for single-instance scenarios.
/// </summary>
public class InMemoryFileStorage : IFileStorage, ISerializableFileStorage
{
    private readonly ConcurrentDictionary<string, FileEntry> _store = new();

    public InMemoryFileStorage()
    {
    }

    #region IFileStorage Implementation

    /// <inheritdoc />
    public FileEntry? Get(string path)
    {
        return _store.TryGetValue(path, out var entry) ? entry : null;
    }

    /// <inheritdoc />
    public void Set(string path, FileEntry entry)
    {
        _store[path] = entry;
    }

    /// <inheritdoc />
    public bool Delete(string path)
    {
        return _store.TryRemove(path, out _);
    }

    /// <inheritdoc />
    public bool Exists(string path)
    {
        return _store.ContainsKey(path);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAllPaths()
    {
        return _store.Keys;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetPathsByPrefix(string prefix)
    {
        return _store.Keys.Where(k => k.StartsWith(prefix));
    }

    /// <inheritdoc />
    public IEnumerable<string> GetChildren(string directoryPath)
    {
        var prefix = directoryPath == "/" ? "/" : directoryPath + "/";
        
        return _store.Keys
            .Where(k => k != directoryPath && k.StartsWith(prefix))
            .Where(k =>
            {
                var remainder = k[prefix.Length..];
                return !remainder.Contains('/'); // Only immediate children
            });
    }

    /// <inheritdoc />
    public void Clear()
    {
        _store.Clear();
    }

    /// <inheritdoc />
    public int Count => _store.Count;

    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, FileEntry>> GetAll()
    {
        return _store;
    }

    /// <inheritdoc />
    public void SetMany(IEnumerable<KeyValuePair<string, FileEntry>> entries)
    {
        foreach (var entry in entries)
        {
            Set(entry.Key, entry.Value);
        }
    }

    #endregion

    #region ISerializableFileStorage Implementation

    /// <inheritdoc />
    public byte[] Serialize()
    {
        return JsonSerializer.SerializeToUtf8Bytes(_store, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    /// <inheritdoc />
    public void Deserialize(byte[] data)
    {
        var snapshot = JsonSerializer.Deserialize<Dictionary<string, FileEntry>>(data);
        if (snapshot == null) return;

        _store.Clear();
        foreach (var kvp in snapshot)
        {
            _store[kvp.Key] = kvp.Value;
        }
    }

    #endregion
}
