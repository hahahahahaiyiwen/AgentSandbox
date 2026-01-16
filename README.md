# Agent Sandbox

An in-memory agent sandbox with virtual filesystem and command-line interface for server-side AI agents. Built with .NET 8.

## Features

- **In-Memory Virtual Filesystem**: Full POSIX-like filesystem that never touches disk
- **Sandboxed Shell**: Unix-style CLI emulator with 18+ commands
- **Thread-Safe**: Concurrent access support with proper locking
- **Snapshots**: Save and restore complete sandbox state
- **Resource Limits**: Configurable max file size, total storage, and node count
- **REST API**: Full HTTP API for remote sandbox management
- **Zero Dependencies**: Pure .NET implementation, no external services required

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    AgentSandbox.Api                         │
│            (REST endpoints, Swagger UI)                     │
├─────────────────────────────────────────────────────────────┤
│                    AgentSandbox.Core                        │
│  ┌─────────────────┐ ┌─────────────────┐ ┌───────────────┐  │
│  │ VirtualFileSystem│ │  SandboxShell   │ │SandboxManager │  │
│  │   (in-memory)   │ │ (CLI emulator)  │ │  (sessions)   │  │
│  └─────────────────┘ └─────────────────┘ └───────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## Quick Start

### Run the API Server

```bash
cd AgentSandbox
dotnet run --project AgentSandbox.Api
```

Navigate to `http://localhost:5000/swagger` to explore the API.

### Use as a Library

```csharp
using AgentSandbox.Core.Sandbox;

// Create a sandbox
var sandbox = new AgentSandboxInstance();

// Execute shell commands
var result = sandbox.Execute("mkdir -p /workspace/src");
sandbox.Execute("echo 'console.log(\"Hello\")' > /workspace/src/app.js");

// Read/write files directly
sandbox.WriteFile("/data/config.json", "{\"key\": \"value\"}");
var content = sandbox.ReadFile("/data/config.json");

// List directories
var files = sandbox.ListDirectory("/workspace/src");

// Create snapshots for checkpointing
var snapshot = sandbox.CreateSnapshot();
// ... make changes ...
sandbox.RestoreSnapshot(snapshot); // Rollback

// Get statistics
var stats = sandbox.GetStats();
Console.WriteLine($"Files: {stats.FileCount}, Size: {stats.TotalSize} bytes");
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/sandbox` | Create new sandbox |
| GET | `/api/sandbox` | List all sandboxes |
| GET | `/api/sandbox/{id}` | Get sandbox info |
| DELETE | `/api/sandbox/{id}` | Destroy sandbox |
| POST | `/api/sandbox/{id}/exec` | Execute command |
| GET | `/api/sandbox/{id}/history` | Get command history |
| GET | `/api/sandbox/{id}/fs?path=` | Read file |
| PUT | `/api/sandbox/{id}/fs` | Write file |
| GET | `/api/sandbox/{id}/ls?path=` | List directory |
| POST | `/api/sandbox/{id}/snapshot` | Create snapshot |
| POST | `/api/sandbox/{id}/restore?snapshotId=` | Restore snapshot |
| GET | `/api/sandbox/{id}/stats` | Get statistics |

### Example: Create and Use Sandbox via API

```bash
# Create a sandbox
curl -X POST http://localhost:5000/api/sandbox \
  -H "Content-Type: application/json" \
  -d '{"id": "agent-1", "workingDirectory": "/workspace"}'

# Execute a command
curl -X POST http://localhost:5000/api/sandbox/agent-1/exec \
  -H "Content-Type: application/json" \
  -d '{"command": "echo Hello World"}'

# Write a file
curl -X PUT http://localhost:5000/api/sandbox/agent-1/fs \
  -H "Content-Type: application/json" \
  -d '{"path": "/workspace/test.txt", "content": "file content"}'

# Read a file
curl "http://localhost:5000/api/sandbox/agent-1/fs?path=/workspace/test.txt"

# List directory
curl "http://localhost:5000/api/sandbox/agent-1/ls?path=/workspace"
```

## Supported Shell Commands

| Command | Description | Example |
|---------|-------------|---------|
| `pwd` | Print working directory | `pwd` |
| `cd` | Change directory | `cd /home/user` |
| `ls` | List directory contents | `ls -la /path` |
| `cat` | Display file contents | `cat file.txt` |
| `echo` | Print text | `echo Hello $USER` |
| `mkdir` | Create directory | `mkdir -p /a/b/c` |
| `rm` | Remove file/directory | `rm -rf /dir` |
| `cp` | Copy file/directory | `cp src.txt dest.txt` |
| `mv` | Move/rename | `mv old.txt new.txt` |
| `touch` | Create empty file | `touch file.txt` |
| `head` | Show first N lines | `head -n 10 file.txt` |
| `tail` | Show last N lines | `tail -n 5 file.txt` |
| `wc` | Count lines/words/bytes | `wc file.txt` |
| `grep` | Search pattern in files | `grep -i pattern file.txt` |
| `find` | Find files | `find /path -name "*.txt"` |
| `env` | Show environment | `env` |
| `export` | Set environment variable | `export KEY=value` |
| `help` | Show available commands | `help` |

## Configuration

```csharp
var options = new SandboxOptions
{
    MaxTotalSize = 100 * 1024 * 1024,  // 100 MB total storage
    MaxFileSize = 10 * 1024 * 1024,     // 10 MB per file
    MaxNodeCount = 10000,                // Max files/directories
    CommandTimeout = TimeSpan.FromSeconds(30),
    WorkingDirectory = "/workspace",
    Environment = new Dictionary<string, string>
    {
        ["PROJECT"] = "MyAgent",
        ["DEBUG"] = "true"
    }
};

var sandbox = new AgentSandboxInstance("my-agent", options);
```

## Multi-Sandbox Management

```csharp
var manager = new SandboxManager();

// Create multiple isolated sandboxes
var sandbox1 = manager.Create("agent-1");
var sandbox2 = manager.Create("agent-2");

// Get existing sandbox
var existing = manager.Get("agent-1");

// Get or create (idempotent)
var sandbox = manager.GetOrCreate("agent-3");

// List all active sandboxes
foreach (var id in manager.List())
{
    var stats = manager.Get(id)?.GetStats();
    Console.WriteLine($"{id}: {stats?.FileCount} files");
}

// Cleanup inactive sandboxes (default: 1 hour timeout)
int cleaned = manager.CleanupInactive();

// Destroy specific sandbox
manager.Destroy("agent-1");
```

## Use Cases

1. **AI Agent Execution**: Provide agents with isolated file/command environments
2. **Code Sandboxing**: Execute untrusted code without filesystem risk
3. **Testing**: Create reproducible test environments with snapshots
4. **Simulation**: Simulate filesystem operations for training/evaluation
5. **Multi-tenant Services**: Isolate per-user/per-session state

## Project Structure

```
AgentSandbox/
├── AgentSandbox.sln
├── nuget.config
├── AgentSandbox.Core/           # Core library
│   ├── FileSystem/
│   │   └── VirtualFileSystem.cs # In-memory VFS
│   ├── Shell/
│   │   ├── ShellResult.cs       # Command result model
│   │   └── SandboxShell.cs      # CLI emulator
│   └── Sandbox/
│       ├── AgentSandboxInstance.cs  # Main sandbox class
│       └── SandboxManager.cs        # Multi-sandbox manager
├── AgentSandbox.Api/            # REST API
│   ├── Endpoints/
│   │   └── SandboxEndpoints.cs  # API routes
│   ├── Models/
│   │   └── ApiModels.cs         # DTOs
│   └── Program.cs
└── AgentSandbox.Tests/          # Unit tests
    ├── VirtualFileSystemTests.cs
    ├── SandboxShellTests.cs
    └── SandboxManagerTests.cs
```

## Building

```bash
dotnet build
dotnet test
```

## Building and Referencing as a NuGet Package

### Build the Package

```bash
# Pack the core library
dotnet pack AgentSandbox.Core -c Release -o ./nupkgs

# Pack the Semantic Kernel integration (optional)
dotnet pack AgentSandbox.SemanticKernel -c Release -o ./nupkgs
```

### Reference in Your Project

**Option 1: Local Package Source**

Add a local NuGet source pointing to the `nupkgs` folder:

```bash
# Add local source
dotnet nuget add source ./path/to/AgentSandbox/nupkgs --name LocalPackages

# Add package reference
dotnet add package AgentSandbox.Core
dotnet add package AgentSandbox.SemanticKernel  # For Semantic Kernel integration
```

**Option 2: Direct Project Reference**

Reference the project directly in your `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="../AgentSandbox/AgentSandbox.Core/AgentSandbox.Core.csproj" />
  <!-- For Semantic Kernel integration -->
  <ProjectReference Include="../AgentSandbox/AgentSandbox.SemanticKernel/AgentSandbox.SemanticKernel.csproj" />
</ItemGroup>
```

**Option 3: nuget.config for Local Feed**

Create or update `nuget.config` in your solution root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="LocalPackages" value="./path/to/AgentSandbox/nupkgs" />
  </packageSources>
</configuration>
```

Then add the package reference:

```bash
dotnet add package AgentSandbox.Core
```

## License

MIT
