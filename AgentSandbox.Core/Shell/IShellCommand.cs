namespace AgentSandbox.Core.Shell;

/// <summary>
/// Represents a shell command that can be registered and executed.
/// </summary>
public interface IShellCommand
{
    /// <summary>
    /// The primary command name (e.g., "curl", "git").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Alternative names for this command.
    /// </summary>
    IReadOnlyList<string> Aliases => Array.Empty<string>();

    /// <summary>
    /// Short description for help text.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Usage examples for help text.
    /// </summary>
    string Usage => $"{Name} [options]";

    /// <summary>
    /// Executes the command with the given arguments.
    /// </summary>
    /// <param name="args">Command arguments (excluding the command name).</param>
    /// <param name="context">Shell context providing filesystem, environment, etc.</param>
    /// <returns>The result of command execution.</returns>
    ShellResult Execute(string[] args, IShellContext context);
}
