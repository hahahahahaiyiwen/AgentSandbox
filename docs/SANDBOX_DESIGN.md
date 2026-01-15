# Agent Sandbox - Design Document

## Overview

Agent Sandbox is an in-memory isolated execution environment designed for server-side AI agents. It provides a virtual filesystem and Unix-like shell that agents can use to read, write, and manipulate files without affecting the host system.

## Goals

1. **Complete Isolation** - Agents operate in a sandboxed environment with no access to the host filesystem or system resources
2. **Familiar Interface** - Unix-like shell commands and POSIX-style filesystem paths that AI agents already understand
3. **Snapshotting** - Save and restore sandbox state for checkpointing, rollback, and debugging
4. **Resource Limits** - Enforce quotas on file sizes, total storage, and execution time
5. **Dual Usage** - Use as an embedded library or as a standalone REST API server
6. **Extensibility** - Pluggable storage backends and customizable shell commands

## Architecture

```
┌────────────────────────────────────────────────────────────────────────────┐
│                              Agent Sandbox                                  │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │                         SandboxManager                                │  │
│  │  • Create/destroy sandbox instances                                   │  │
│  │  • Session management and cleanup                                     │  │
│  │  • Resource tracking                                                  │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                    │                                       │
│                                    ▼                                       │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │                      AgentSandboxInstance                             │  │
│  │  • Single isolated environment                                        │  │
│  │  • Command execution with timeout                                     │  │
│  │  • Quota enforcement                                                  │  │
│  │  • Snapshot/restore                                                   │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                          │                    │                            │
│            ┌─────────────┘                    └─────────────┐              │
│            ▼                                                ▼              │
│  ┌──────────────────────┐                    ┌──────────────────────────┐  │
│  │    SandboxShell      │                    │   VirtualFileSystem      │  │
│  │                      │───────────────────▶│                          │  │
│  │  • Command parsing   │                    │  • IFileSystem           │  │
│  │  • Built-in commands │                    │  • ISnapshotableFS       │  │
│  │  • Environment vars  │                    │  • IFileSystemStats      │  │
│  │  • Command history   │                    │                          │  │
│  └──────────────────────┘                    └──────────────────────────┘  │
│            │                                              │                │
│            ▼                                              ▼                │
│  ┌──────────────────────┐                    ┌──────────────────────────┐  │
│  │   ShellExtensions    │                    │      IFileStorage        │  │
│  │   (Future)           │                    │                          │  │
│  │  • Custom commands   │                    │  • InMemoryFileStorage   │  │
│  │  • Plugin system     │                    │  • RedisFileStorage      │  │
│  │                      │                    │  • BlobFileStorage       │  │
│  └──────────────────────┘                    └──────────────────────────┘  │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

## Core Components

### SandboxManager

Thread-safe singleton for managing multiple sandbox instances.

```csharp
public class SandboxManager
{
    AgentSandboxInstance Create(string? id = null, SandboxOptions? options = null);
    AgentSandboxInstance? Get(string id);
    AgentSandboxInstance GetOrCreate(string id, SandboxOptions? options = null);
    bool Destroy(string id);
    IEnumerable<string> List();
    void CleanupInactive(TimeSpan? maxInactivity = null);
}
```

### AgentSandboxInstance

Represents a single isolated sandbox environment.

```csharp
public class AgentSandboxInstance
{
    string Id { get; }
    IFileSystem FileSystem { get; }
    SandboxShell Shell { get; }
    SandboxOptions Options { get; }
    
    ShellResult Execute(string command);
    string ReadFile(string path);
    void WriteFile(string path, string content);
    IEnumerable<string> ListDirectory(string path);
    byte[] CreateSnapshot();
    void RestoreSnapshot(byte[] data);
    SandboxStats GetStats();
}
```

### SandboxShell

Unix-like shell emulator with built-in commands.

**Built-in Commands:**
| Command | Description |
|---------|-------------|
| `pwd` | Print working directory |
| `cd` | Change directory |
| `ls` | List directory contents |
| `cat` | Display file contents |
| `echo` | Print text (supports $VAR expansion) |
| `mkdir` | Create directory |
| `rm` | Remove files/directories |
| `cp` | Copy files/directories |
| `mv` | Move/rename files |
| `touch` | Create empty file |
| `head` | Show first N lines |
| `tail` | Show last N lines |
| `wc` | Word/line/char count |
| `grep` | Search file contents |
| `find` | Find files by name |
| `env` | Show environment variables |
| `export` | Set environment variable |
| `clear` | Clear screen |
| `help` | Show available commands |

### VirtualFileSystem

In-memory filesystem with POSIX-like operations. See [FILESYSTEM_DESIGN.md](./FILESYSTEM_DESIGN.md) for detailed interface documentation.

---

## Usage

### As a Library

Embed the sandbox directly in your .NET application:

```csharp
// Create a sandbox instance
var sandbox = new AgentSandboxInstance();

// Execute shell commands
var result = sandbox.Execute("mkdir /project");
var result2 = sandbox.Execute("echo 'Hello World' > /project/hello.txt");
var result3 = sandbox.Execute("cat /project/hello.txt");
Console.WriteLine(result3.Stdout); // "Hello World"

// Direct file operations
sandbox.WriteFile("/config.json", "{\"key\": \"value\"}");
string content = sandbox.ReadFile("/config.json");

// Snapshot for rollback
byte[] snapshot = sandbox.CreateSnapshot();
sandbox.Execute("rm -r /project");
sandbox.RestoreSnapshot(snapshot); // /project is back

// With resource limits
var options = new SandboxOptions
{
    MaxTotalSize = 10 * 1024 * 1024,  // 10 MB
    MaxFileSize = 1 * 1024 * 1024,     // 1 MB
    MaxNodeCount = 1000,
    CommandTimeout = TimeSpan.FromSeconds(30)
};
var limitedSandbox = new AgentSandboxInstance(options: options);
```

### Managing Multiple Sandboxes

```csharp
var manager = new SandboxManager();

// Create sandboxes for different agents
var sandbox1 = manager.Create("agent-1");
var sandbox2 = manager.Create("agent-2");

// Retrieve existing sandbox
var existing = manager.Get("agent-1");

// List all active sandboxes
foreach (var id in manager.List())
{
    Console.WriteLine(id);
}

// Cleanup inactive sandboxes (default: 1 hour)
manager.CleanupInactive();

// Destroy specific sandbox
manager.Destroy("agent-1");
```

### As a REST API Server

Run the sandbox as a standalone HTTP server:

```bash
cd AgentSandbox.Api
dotnet run
```

The server exposes these endpoints:

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/sandbox` | Create new sandbox |
| `GET` | `/api/sandbox` | List all sandboxes |
| `GET` | `/api/sandbox/{id}` | Get sandbox info |
| `DELETE` | `/api/sandbox/{id}` | Destroy sandbox |
| `POST` | `/api/sandbox/{id}/exec` | Execute command |
| `GET` | `/api/sandbox/{id}/history` | Get command history |
| `GET` | `/api/sandbox/{id}/fs?path=/file` | Read file |
| `PUT` | `/api/sandbox/{id}/fs` | Write file |
| `GET` | `/api/sandbox/{id}/ls?path=/` | List directory |
| `POST` | `/api/sandbox/{id}/snapshot` | Create snapshot |
| `POST` | `/api/sandbox/{id}/restore` | Restore snapshot |
| `GET` | `/api/sandbox/{id}/stats` | Get statistics |

**Example API Usage:**

```bash
# Create sandbox
curl -X POST http://localhost:5000/api/sandbox \
  -H "Content-Type: application/json" \
  -d '{"id": "my-sandbox"}'

# Execute command
curl -X POST http://localhost:5000/api/sandbox/my-sandbox/exec \
  -H "Content-Type: application/json" \
  -d '{"command": "echo Hello World"}'

# Read file
curl http://localhost:5000/api/sandbox/my-sandbox/fs?path=/hello.txt

# Write file
curl -X PUT http://localhost:5000/api/sandbox/my-sandbox/fs \
  -H "Content-Type: application/json" \
  -d '{"path": "/data.json", "content": "{\"key\": \"value\"}"}'
```

---

## Configuration

### SandboxOptions

| Option | Default | Description |
|--------|---------|-------------|
| `MaxTotalSize` | 100 MB | Maximum total storage size |
| `MaxFileSize` | 10 MB | Maximum single file size |
| `MaxNodeCount` | 10,000 | Maximum files + directories |
| `CommandTimeout` | 30 sec | Maximum command execution time |

---

## Project Structure

```
AgentSandbox/
├── AgentSandbox.Core/           # Core library
│   ├── FileSystem/              # Virtual filesystem
│   │   ├── IFileSystem.cs       # Filesystem interfaces
│   │   ├── IFileStorage.cs      # Storage abstraction
│   │   ├── FileEntry.cs         # File/directory data class
│   │   ├── VirtualFileSystem.cs # Main implementation
│   │   └── Storage/
│   │       └── InMemoryFileStorage.cs
│   ├── Shell/                   # Shell emulator
│   │   ├── SandboxShell.cs      # Command processor
│   │   └── ShellResult.cs       # Command result
│   └── Sandbox/                 # Sandbox management
│       ├── AgentSandboxInstance.cs
│       ├── SandboxManager.cs
│       └── SandboxOptions.cs
├── AgentSandbox.Api/            # REST API server
│   ├── Program.cs
│   ├── SandboxEndpoints.cs
│   └── ApiModels.cs
├── AgentSandbox.Tests/          # Unit tests
└── docs/
    ├── SANDBOX_DESIGN.md        # This document
    └── FILESYSTEM_DESIGN.md     # Filesystem interfaces
```

---

## Future Considerations

- **Shell Extensions** - Plugin system for custom commands
- **Async FileSystem** - Non-blocking operations for remote storage
- **Process Simulation** - Simulated background processes
- **Network Simulation** - Virtual network stack for agents
- **Multi-tenant Isolation** - Stronger isolation between sandboxes
- **Persistence** - Durable sandbox state across server restarts
