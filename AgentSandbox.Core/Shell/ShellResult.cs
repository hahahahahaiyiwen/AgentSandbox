namespace AgentSandbox.Core.Shell;

/// <summary>
/// Result of executing a shell command.
/// </summary>
public class ShellResult
{
    public string Stdout { get; set; } = string.Empty;
    public string Stderr { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public string Command { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }

    public bool Success => ExitCode == 0;

    public static ShellResult Ok(string stdout = "", string command = "") => new()
    {
        Stdout = stdout,
        ExitCode = 0,
        Command = command
    };

    public static ShellResult Error(string stderr, int exitCode = 1, string command = "") => new()
    {
        Stderr = stderr,
        ExitCode = exitCode,
        Command = command
    };
}
