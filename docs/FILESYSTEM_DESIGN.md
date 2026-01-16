# Agent Sandbox FileSystem Design

## Overview

The FileSystem provides a fully in-memory virtual filesystem designed for isolated agent execution. It offers complete filesystem isolation, snapshotting capabilities, size limits, and a pluggable storage backend for extensibility.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         FileSystem                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                  FileSystem Interface                    │   │
│  │  • Path operations (exists, is file/directory)          │   │
│  │  • Directory operations (create, list)                  │   │
│  │  • File read/write operations                           │   │
│  │  • Copy/Move/Delete operations                          │   │
│  └─────────────────────────────────────────────────────────┘   │
│                            │                                    │
│                            ▼                                    │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                  Storage Interface                       │   │
│  │  • Key-value persistence abstraction                    │   │
│  │  • Pluggable backends (in-memory, Redis, blob, etc.)    │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Core Concepts

### File Entry

A file entry represents a node in the filesystem - either a file or directory. It combines both metadata and content in a single unit:

| Property | Description |
|----------|-------------|
| **Name** | File or directory name |
| **Path** | Full normalized POSIX-style path |
| **IsDirectory** | Type discriminator |
| **Content** | Binary content (empty for directories) |
| **Size** | Computed from content length |
| **CreatedAt** | Creation timestamp |
| **ModifiedAt** | Last modification timestamp |
| **Mode** | Unix-style permissions (e.g., 0644) |

---

### FileSystem Interface

The primary filesystem interface provides POSIX-like operations:

**Path Operations**
- Check if path exists
- Determine if path is file or directory
- Get file entry by path

**Directory Operations**
- Create directories (with parent creation)
- List directory contents (names or full entries)

**File Operations**
- Read file as bytes, text, or lines
- Write file from bytes, text, or lines
- Append content to existing files
- Stream-based read/write access

**Manipulation Operations**
- Delete files and directories (with recursive option)
- Copy files and directories
- Move/rename files and directories

---

### Snapshotable FileSystem

Extension capability for checkpoint/restore:

- **CreateSnapshot**: Serialize entire filesystem state to binary data
- **RestoreSnapshot**: Restore filesystem from previously saved snapshot

Use cases:
- Save state before risky agent operations
- Rollback on errors
- Create checkpoints during long-running tasks

---

### FileSystem Statistics

Provides visibility into filesystem usage:

| Metric | Description |
|--------|-------------|
| **TotalSize** | Sum of all file sizes in bytes |
| **FileCount** | Number of files |
| **DirectoryCount** | Number of directories |
| **NodeCount** | Total nodes (files + directories) |

---

### Size Limits

The filesystem supports configurable limits to prevent unbounded growth:

| Limit | Description |
|-------|-------------|
| **MaxTotalSize** | Maximum total bytes across all files |
| **MaxFileSize** | Maximum size for a single file |
| **MaxNodeCount** | Maximum number of files + directories |

When a limit is exceeded, the write operation fails with an error.

---

### Storage Abstraction

The storage layer abstracts how file entries are persisted:

**Core Operations**
- Get/Set/Delete/Exists for individual entries
- List all paths or paths by prefix
- Get children of a directory

**Bulk Operations**
- Clear all entries
- Get all entries
- Set multiple entries atomically

**Async Support**
- Async versions of all operations for network/remote backends
- Cancellation token support

**Extensibility Points**
- In-memory storage for testing and local execution
- Remote key-value stores (Redis, DynamoDB)
- Cloud blob storage (Azure Blob, S3)
- Database-backed storage

---

### Path Normalization

All paths are normalized to a consistent POSIX-style format:

| Rule | Example |
|------|---------|
| Backslashes converted to forward slashes | `\src\file.txt` → `/src/file.txt` |
| Always starts with `/` | `src/file.txt` → `/src/file.txt` |
| No trailing slashes (except root) | `/src/` → `/src` |
| `.` segments removed | `/src/./file.txt` → `/src/file.txt` |
| `..` segments resolved | `/src/../file.txt` → `/file.txt` |

---

## Design Principles

1. **Isolation**: Complete separation from host filesystem
2. **Simplicity**: POSIX-like semantics familiar to developers
3. **Extensibility**: Pluggable storage backends via clean interface
4. **Observability**: Statistics and quota enforcement
5. **Recoverability**: Snapshot/restore for state management
6. **Safety**: Size limits prevent resource exhaustion
