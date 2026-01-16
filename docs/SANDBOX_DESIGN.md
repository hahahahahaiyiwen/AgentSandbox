# Sandbox Instance Design

This document describes the internal design of the Agent Sandbox Instance, including the Shell and Shell Extensions system.

## Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│                        Agent Sandbox Instance                             │
│                                                                           │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                          Public API                                  │ │
│  │                                                                      │ │
│  │  Execute(command) ──────────────────────────> ShellResult           │ │
│  │  CreateSnapshot() ──────────────────────────> SandboxSnapshot       │ │
│  │  RestoreSnapshot(snapshot)                                          │ │
│  │  GetStats() ────────────────────────────────> SandboxStats          │ │
│  │  GetHistory() ──────────────────────────────> List<ShellResult>     │ │
│  │                                                                      │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│                                   │                                       │
│                                   ▼                                       │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                         Sandbox Shell                                │ │
│  │                                                                      │ │
│  │   ┌─────────────────────┐    ┌────────────────────────────────────┐ │ │
│  │   │   Built-in Commands │    │       Shell Extensions             │ │ │
│  │   │                     │    │                                    │ │ │
│  │   │   pwd, cd, ls       │    │   curl  - HTTP requests           │ │ │
│  │   │   cat, head, tail   │    │   (custom extensions)             │ │ │
│  │   │   mkdir, rm, cp, mv │    │                                    │ │ │
│  │   │   grep, find, wc    │    │                                    │ │ │
│  │   │   echo, env, export │    │                                    │ │ │
│  │   └─────────────────────┘    └────────────────────────────────────┘ │ │
│  │                                                                      │ │
│  │   Features:                                                          │ │
│  │   • Command parsing with quote handling                             │ │
│  │   • Environment variable expansion ($VAR)                           │ │
│  │   • Output redirection (>, >>)                                      │ │
│  │                                                                      │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│                                   │                                       │
│                                   ▼                                       │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                          FileSystem                                  │ │
│  │                                                                      │ │
│  │   • POSIX-like file/directory operations                            │ │
│  │   • Size limits (total size, file size, node count)                 │ │
│  │   • Snapshot/restore for state persistence                          │ │
│  │   • Pluggable storage backend                                       │ │
│  │                                                                      │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## Sandbox Instance

The primary execution environment. Encapsulates filesystem, shell, and all state.

### Public API

| Method | Description |
|--------|-------------|
| Execute(command) | Execute a shell command, returns result |
| CreateSnapshot() | Capture entire sandbox state |
| RestoreSnapshot(snapshot) | Restore to a saved state |
| GetStats() | Get runtime statistics |
| GetHistory() | Get command execution history |

### Configuration

| Option | Default | Description |
|--------|---------|-------------|
| MaxTotalSize | 100 MB | Maximum total filesystem size |
| MaxFileSize | 10 MB | Maximum single file size |
| MaxNodeCount | 10,000 | Maximum files + directories |
| CommandTimeout | 30 sec | Command execution timeout |
| WorkingDirectory | `/` | Initial working directory |
| Environment | empty | Initial environment variables |

### Snapshot

Captures complete sandbox state for persistence or rollback.

**Contains**:
- Complete filesystem state (all files and directories)
- Current working directory
- Environment variables
- Timestamp

---

## Shell

Interprets and executes commands against the virtual filesystem.

### Shell Interface

| Method | Description |
|--------|-------------|
| Execute(commandLine) | Parse and execute a command string |
| RegisterCommand(command) | Register a custom command extension |
| GetAvailableCommands() | List all available commands |

### Built-in Commands (19 commands)

| Category | Commands |
|----------|----------|
| Navigation | `pwd`, `cd`, `ls` |
| File Content | `cat`, `head`, `tail`, `wc`, `grep` |
| File Operations | `touch`, `cp`, `mv`, `rm` |
| Directory | `mkdir`, `find` |
| Output | `echo` |
| Environment | `env`, `export` |
| Utility | `clear`, `help` |

### Shell Features

**Command Parsing**:
- Quote handling (single and double quotes)
- Whitespace-separated arguments
- Quote escaping within strings

**Variable Expansion**:
- `$VAR` expands to environment variable value
- `$HOME`, `$PWD`, `$PATH` available by default
- Custom variables via `export VAR=value`

**Output Redirection**:
- `>` overwrites file with command output
- `>>` appends command output to file
- Redirection applies after command execution

**Exit Codes**:
- 0 = success
- 1 = general error
- 127 = command not found

---

## Shell Extensions

Pluggable commands that add capabilities beyond file operations.

### Extension Interface

| Property/Method | Description |
|-----------------|-------------|
| Name | Primary command name (e.g., "curl") |
| Aliases | Alternative names for the command |
| Description | Short help text |
| Usage | Usage examples |
| Execute(args, context) | Run command with arguments and context |

### Shell Context

Provided to extensions during execution:

| Property | Description |
|----------|-------------|
| FileSystem | Virtual filesystem access |
| CurrentDirectory | Current working directory |
| Environment | Environment variables dictionary |
| ResolvePath(path) | Resolve relative paths to absolute |

### Creating Extensions

Extensions implement a simple interface:
1. Define `Name` - the command name users type
2. Define `Description` - shown in help output
3. Implement `Execute(args, context)` - command logic

Extensions receive:
- Parsed arguments (command name excluded)
- Shell context for filesystem and environment access

Extensions return:
- ShellResult with stdout, stderr, and exit code

### Built-in Extensions

**curl** - HTTP client command

Supports common curl syntax:
| Option | Description |
|--------|-------------|
| `-X, --request` | HTTP method (GET, POST, PUT, DELETE) |
| `-H, --header` | Add header (repeatable) |
| `-d, --data` | Request body data |
| `-o, --output` | Write response to file |
| `-s, --silent` | Silent mode |
| `-i, --include` | Include response headers |
| `-L, --location` | Follow redirects |

---

## Command Result

Returned from every command execution.

| Property | Description |
|----------|-------------|
| Stdout | Standard output text |
| Stderr | Standard error text |
| ExitCode | 0 = success, non-zero = failure |
| Success | Convenience boolean (ExitCode == 0) |
| Command | The executed command string |
| Duration | Execution time |

---

## Design Principles

### Single Execute Interface

All agent interactions go through `Execute(command)`. No direct filesystem API exposure.

Benefits:
- Uniform interface for all operations
- Built-in command logging and history
- Natural support for output redirection
- Easy to add policies and auditing

### Resource Limits at FileSystem Level

Size and count limits enforced at filesystem layer, not shell.

Benefits:
- Limits apply to all write operations
- Extensions cannot bypass quotas
- Consistent enforcement

### Extension via Composition

Commands added by registering implementations, not inheritance.

Benefits:
- Multiple extensions from different sources
- Easy testing of individual commands
- Clean separation of concerns

---

## Security

- **No Host Access**: Virtual filesystem is completely isolated
- **No Network by Default**: HTTP only if curl extension registered
- **Resource Limits**: Prevents resource exhaustion
- **Command Allowlist**: Only registered commands available
- **State Isolation**: Each sandbox independent
