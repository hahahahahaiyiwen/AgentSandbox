# Agent Sandbox Virtual FileSystem

## Overview

The Agent Sandbox provides a fully in-memory virtual filesystem designed for server-side AI agents. It offers complete filesystem isolation, snapshotting capabilities, and a pluggable storage backend for extensibility.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Agent Sandbox Instance                       │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐    ┌─────────────────────────────────────┐ │
│  │  SandboxShell   │───▶│         VirtualFileSystem           │ │
│  │  (CLI Emulator) │    │                                     │ │
│  └─────────────────┘    │  ┌─────────────────────────────────┐│ │
│                         │  │         IFileSystem              ││ │
│                         │  │  • Path operations               ││ │
│                         │  │  • Directory operations          ││ │
│                         │  │  • File read/write operations    ││ │
│                         │  │  • Copy/Move/Delete operations   ││ │
│                         │  └─────────────────────────────────┘│ │
│                         │                  │                   │ │
│                         │                  ▼                   │ │
│                         │  ┌─────────────────────────────────┐│ │
│                         │  │         IFileStorage             ││ │
│                         │  │  • Key-value persistence         ││ │
│                         │  │  • Pluggable backends            ││ │
│                         │  └─────────────────────────────────┘│ │
│                         └─────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

## Core Components

### FileEntry

The data class representing a file or directory node. Combines both metadata and content.

```csharp
public class FileEntry
{
    string Name { get; set; }           // File/directory name
    string Path { get; set; }           // Full normalized path
    bool IsDirectory { get; set; }      // Type discriminator
    byte[] Content { get; set; }        // File content (empty for directories)
    long Size { get; }                  // Computed from Content.Length
    DateTime CreatedAt { get; set; }    // Creation timestamp
    DateTime ModifiedAt { get; set; }   // Last modification timestamp
    int Mode { get; set; }              // Unix-style permissions (e.g., 0644)
}
```

---

### IFileSystem

The primary filesystem interface providing POSIX-like operations.

```csharp
public interface IFileSystem
{
    // Path Operations
    bool Exists(string path);
    bool IsFile(string path);
    bool IsDirectory(string path);
    FileEntry? GetEntry(string path);

    // Directory Operations
    void CreateDirectory(string path);
    IEnumerable<string> ListDirectory(string path);
    IEnumerable<FileEntry> ListEntries(string path);

    // File Read Operations
    byte[] ReadFile(string path);
    string ReadFile(string path, Encoding encoding);
    IEnumerable<string> ReadFileLines(string path, Encoding? encoding = null);
    Stream OpenRead(string path);

    // File Write Operations
    void WriteFile(string path, byte[] content);
    void WriteFile(string path, string content, Encoding? encoding = null);
    void WriteFile(string path, IEnumerable<string> lines, Encoding? encoding = null);
    void AppendToFile(string path, byte[] content);
    void AppendToFile(string path, string content, Encoding? encoding = null);
    Stream OpenWrite(string path, bool append = false);

    // Delete Operations
    void DeleteFile(string path);
    void DeleteDirectory(string path, bool recursive = false);
    void Delete(string path, bool recursive = false);

    // Copy/Move Operations
    void Copy(string source, string destination, bool overwrite = false);
    void Move(string source, string destination, bool overwrite = false);
}
```

---

### ISnapshotableFileSystem

Extension interface for checkpoint/restore capabilities.

```csharp
public interface ISnapshotableFileSystem : IFileSystem
{
    byte[] CreateSnapshot();
    void RestoreSnapshot(byte[] snapshotData);
}
```

---

### IFileSystemStats

Extension interface for filesystem statistics.

```csharp
public interface IFileSystemStats
{
    long TotalSize { get; }       // Sum of all file sizes
    int FileCount { get; }        // Number of files
    int DirectoryCount { get; }   // Number of directories
    int NodeCount { get; }        // Total nodes (files + directories)
}
```

---

### IFileStorage

Low-level storage abstraction for persisting `FileEntry` objects. This is the extensibility point for different storage backends.

```csharp
public interface IFileStorage
{
    // Basic CRUD
    FileEntry? Get(string path);
    void Set(string path, FileEntry entry);
    bool Delete(string path);
    bool Exists(string path);

    // Enumeration
    IEnumerable<string> GetAllPaths();
    IEnumerable<string> GetPathsByPrefix(string prefix);
    IEnumerable<string> GetChildren(string directoryPath);

    // Bulk Operations
    void Clear();
    int Count { get; }
    IEnumerable<KeyValuePair<string, FileEntry>> GetAll();
    void SetMany(IEnumerable<KeyValuePair<string, FileEntry>> entries);
}
```

---

### IAsyncFileStorage

Async version of `IFileStorage` for remote/network backends (Redis, Azure Blob, S3, etc.).

```csharp
public interface IAsyncFileStorage
{
    Task<FileEntry?> GetAsync(string path, CancellationToken ct = default);
    Task SetAsync(string path, FileEntry entry, CancellationToken ct = default);
    Task<bool> DeleteAsync(string path, CancellationToken ct = default);
    Task<bool> ExistsAsync(string path, CancellationToken ct = default);
    Task<IEnumerable<string>> GetPathsByPrefixAsync(string prefix, CancellationToken ct = default);
    Task<IEnumerable<string>> GetChildrenAsync(string directoryPath, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
    Task<IEnumerable<KeyValuePair<string, FileEntry>>> GetAllAsync(CancellationToken ct = default);
    Task SetManyAsync(IEnumerable<KeyValuePair<string, FileEntry>> entries, CancellationToken ct = default);
}
```

---

### ISerializableFileStorage

Extension for storage backends that support serialization.

```csharp
public interface ISerializableFileStorage : IFileStorage
{
    byte[] Serialize();
    void Deserialize(byte[] data);
}
```

---

### FileSystemPath

Static utility class for path manipulation.

```csharp
public static class FileSystemPath
{
    static string Normalize(string path);           // Normalize path (forward slashes, resolve . and ..)
    static string GetParent(string path);           // Get parent directory
    static string GetName(string path);             // Get file/directory name
    static string GetExtension(string path);        // Get extension (with dot)
    static string GetNameWithoutExtension(string path);
    static string Combine(params string[] paths);   // Combine path segments
    static bool IsRoot(string path);                // Check if path is root
    static bool IsChildOf(string childPath, string parentPath);
}
```

**Path Normalization Rules:**
- Backslashes (`\`) converted to forward slashes (`/`)
- Always starts with `/`
- No trailing slashes (except root)
- `.` segments removed
- `..` segments resolved

---

## Usage

### Basic Filesystem Operations

```csharp
// Create filesystem with default in-memory storage
var fs = new VirtualFileSystem();

// Write files
fs.WriteFile("/src/main.cs", "Console.WriteLine(\"Hello\");");
fs.WriteFile("/data/config.json", new byte[] { 0x7B, 0x7D });

// Create directory and list contents
fs.CreateDirectory("/docs");
fs.WriteFile("/docs/README.md", "# Documentation");

foreach (var name in fs.ListDirectory("/docs"))
{
    Console.WriteLine(name);
}

// Read files
string content = fs.ReadFile("/src/main.cs", Encoding.UTF8);
byte[] bytes = fs.ReadFile("/data/config.json");
```

### Snapshot and Restore

```csharp
// Save state before risky operation
var snapshot = fs.CreateSnapshot();

// Make changes
fs.Delete("/src", recursive: true);

// Restore if needed
fs.RestoreSnapshot(snapshot);
```

### Custom Storage Backend

```csharp
// Implement IFileStorage for custom persistence
public class RedisFileStorage : IFileStorage
{
    public FileEntry? Get(string path) { /* Redis GET */ }
    public void Set(string path, FileEntry entry) { /* Redis SET */ }
    // ... other methods
}

// Inject custom storage
var storage = new RedisFileStorage(connectionMultiplexer);
var fs = new VirtualFileSystem(storage);
```

---

## Default Implementations

| Component | Default Class | Description |
|-----------|--------------|-------------|
| `IFileSystem` | `VirtualFileSystem` | Full filesystem with all operations |
| `ISnapshotableFileSystem` | `VirtualFileSystem` | Snapshot/restore support |
| `IFileSystemStats` | `VirtualFileSystem` | Size and count statistics |
| `IFileStorage` | `InMemoryFileStorage` | ConcurrentDictionary-based storage |
| `ISerializableFileStorage` | `InMemoryFileStorage` | JSON serialization support |
