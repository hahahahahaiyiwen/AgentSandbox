namespace AgentSandbox.Core.Shell;

/// <summary>
/// Represents a sandboxed shell that can execute commands and be extended with custom commands.
/// </summary>
public interface ISandboxShell
{
    /// <summary>
    /// Executes a command string.
    /// </summary>
    /// <param name="commandLine">The command line to execute.</param>
    /// <returns>The result of command execution.</returns>
    ShellResult Execute(string commandLine);

    /// <summary>
    /// Registers a shell command extension.
    /// </summary>
    /// <param name="command">The command to register.</param>
    void RegisterCommand(IShellCommand command);

    /// <summary>
    /// Registers multiple shell command extensions.
    /// </summary>
    /// <param name="commands">The commands to register.</param>
    void RegisterCommands(IEnumerable<IShellCommand> commands);

    /// <summary>
    /// Gets all available command names (built-in and extensions).
    /// </summary>
    /// <returns>Enumerable of command names.</returns>
    IEnumerable<string> GetAvailableCommands();
}
