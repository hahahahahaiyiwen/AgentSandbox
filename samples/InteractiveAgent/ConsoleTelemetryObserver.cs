using AgentSandbox.Core.Telemetry;

namespace AgentSandbox.InteractiveAgent;

/// <summary>
/// A console-based telemetry observer that outputs sandbox events with colored formatting.
/// Useful for development, debugging, and demonstration purposes.
/// </summary>
public class ConsoleTelemetryObserver : ISandboxObserver
{
    private readonly ConsoleTelemetryOptions _options;
    private readonly object _lock = new();

    public ConsoleTelemetryObserver(ConsoleTelemetryOptions? options = null)
    {
        _options = options ?? new ConsoleTelemetryOptions();
    }

    public void OnCommandExecuted(CommandExecutedEvent e)
    {
        if (!_options.ShowCommands) return;

        lock (_lock)
        {
            WritePrefix("CMD", ConsoleColor.Cyan);
            Console.Write($" [{e.CommandName}] ");
            
            if (e.ExitCode == 0)
            {
                WriteColored("OK", ConsoleColor.Green);
            }
            else
            {
                WriteColored($"FAIL({e.ExitCode})", ConsoleColor.Red);
            }
            
            WriteColored($" {e.Duration.TotalMilliseconds:F1}ms", ConsoleColor.DarkGray);
            Console.WriteLine();

            if (_options.ShowCommandDetails)
            {
                WriteColored($"       Command: {e.Command}\n", ConsoleColor.DarkGray);
                if (!string.IsNullOrEmpty(e.TraceId))
                    WriteColored($"       TraceId: {e.TraceId}\n", ConsoleColor.DarkGray);
            }
        }
    }

    public void OnFileChanged(FileChangedEvent e)
    {
        if (!_options.ShowFileChanges) return;

        lock (_lock)
        {
            var color = e.ChangeType switch
            {
                FileChangeType.Created => ConsoleColor.Green,
                FileChangeType.Modified => ConsoleColor.Yellow,
                FileChangeType.Deleted => ConsoleColor.Red,
                FileChangeType.Renamed => ConsoleColor.Magenta,
                _ => ConsoleColor.Gray
            };

            WritePrefix("FILE", color);
            Console.Write($" {e.ChangeType} ");
            WriteColored(e.Path, ConsoleColor.White);
            
            if (e.ChangeType == FileChangeType.Renamed && !string.IsNullOrEmpty(e.OldPath))
            {
                WriteColored($" (from {e.OldPath})", ConsoleColor.DarkGray);
            }
            
            if (e.Bytes.HasValue)
            {
                WriteColored($" [{FormatBytes(e.Bytes.Value)}]", ConsoleColor.DarkGray);
            }
            
            Console.WriteLine();
        }
    }

    public void OnSkillInvoked(SkillInvokedEvent e)
    {
        if (!_options.ShowSkills) return;

        lock (_lock)
        {
            WritePrefix("SKILL", ConsoleColor.Magenta);
            Console.Write($" {e.SkillName}");
            
            if (!string.IsNullOrEmpty(e.ScriptPath))
            {
                WriteColored($" -> {e.ScriptPath}", ConsoleColor.DarkGray);
            }
            
            if (e.Duration.HasValue)
            {
                WriteColored($" {e.Duration.Value.TotalMilliseconds:F1}ms", ConsoleColor.DarkGray);
            }
            
            if (e.Success.HasValue)
            {
                Console.Write(" ");
                if (e.Success.Value)
                    WriteColored("OK", ConsoleColor.Green);
                else
                    WriteColored("FAIL", ConsoleColor.Red);
            }
            
            Console.WriteLine();
        }
    }

    public void OnLifecycleEvent(SandboxLifecycleEvent e)
    {
        if (!_options.ShowLifecycle) return;

        lock (_lock)
        {
            var color = e.LifecycleType switch
            {
                SandboxLifecycleType.Created => ConsoleColor.Green,
                SandboxLifecycleType.Disposed => ConsoleColor.Yellow,
                SandboxLifecycleType.SnapshotCreated => ConsoleColor.Blue,
                SandboxLifecycleType.SnapshotRestored => ConsoleColor.Blue,
                _ => ConsoleColor.Gray
            };

            WritePrefix("LIFECYCLE", color);
            Console.Write($" {e.LifecycleType}");
            WriteColored($" [sandbox:{e.SandboxId[..Math.Min(8, e.SandboxId.Length)]}]", ConsoleColor.DarkGray);
            
            if (!string.IsNullOrEmpty(e.Details))
            {
                WriteColored($" {e.Details}", ConsoleColor.DarkGray);
            }
            
            Console.WriteLine();
        }
    }

    public void OnError(SandboxErrorEvent e)
    {
        if (!_options.ShowErrors) return;

        lock (_lock)
        {
            WritePrefix("ERROR", ConsoleColor.Red);
            Console.Write($" [{e.Category}] ");
            WriteColored(e.Message, ConsoleColor.Red);
            Console.WriteLine();
            
            if (_options.ShowErrorDetails && !string.IsNullOrEmpty(e.ExceptionType))
            {
                WriteColored($"       Type: {e.ExceptionType}\n", ConsoleColor.DarkGray);
            }
        }
    }

    private static void WritePrefix(string prefix, ConsoleColor color)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        WriteColored($"[{timestamp}] ", ConsoleColor.DarkGray);
        WriteColored($"[{prefix}]", color);
    }

    private static void WriteColored(string text, ConsoleColor color)
    {
        var original = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = original;
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
        };
    }
}

/// <summary>
/// Configuration options for ConsoleTelemetryObserver.
/// </summary>
public class ConsoleTelemetryOptions
{
    /// <summary>Show command execution events. Default: true.</summary>
    public bool ShowCommands { get; set; } = true;

    /// <summary>Show detailed command info (full command, trace ID). Default: false.</summary>
    public bool ShowCommandDetails { get; set; } = false;

    /// <summary>Show file change events. Default: true.</summary>
    public bool ShowFileChanges { get; set; } = true;

    /// <summary>Show skill invocation events. Default: true.</summary>
    public bool ShowSkills { get; set; } = true;

    /// <summary>Show lifecycle events (created, disposed). Default: true.</summary>
    public bool ShowLifecycle { get; set; } = true;

    /// <summary>Show error events. Default: true.</summary>
    public bool ShowErrors { get; set; } = true;

    /// <summary>Show detailed error info (exception type). Default: true.</summary>
    public bool ShowErrorDetails { get; set; } = true;
}
