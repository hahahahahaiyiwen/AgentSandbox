using AgentSandbox.Core;
using Microsoft.Extensions.Logging;

namespace AgentSandbox.Extensions.Observability;

/// <summary>
/// Extension methods for adding structured logging to sandboxes.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Adds structured logging to the sandbox using the provided logger.
    /// </summary>
    /// <param name="sandbox">The sandbox instance.</param>
    /// <param name="logger">The logger to use.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <returns>The subscription disposable (dispose to unsubscribe).</returns>
    public static IDisposable AddLogging(
        this Sandbox sandbox,
        ILogger logger,
        LoggingObserverOptions? options = null)
    {
        var observer = new LoggingSandboxObserver(logger, options);
        return sandbox.Subscribe(observer);
    }

    /// <summary>
    /// Adds structured logging to the sandbox using the provided logger factory.
    /// </summary>
    /// <param name="sandbox">The sandbox instance.</param>
    /// <param name="loggerFactory">The logger factory to create a logger from.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <returns>The subscription disposable (dispose to unsubscribe).</returns>
    public static IDisposable AddLogging(
        this Sandbox sandbox,
        ILoggerFactory loggerFactory,
        LoggingObserverOptions? options = null)
    {
        var logger = loggerFactory.CreateLogger<Sandbox>();
        return sandbox.AddLogging(logger, options);
    }

    /// <summary>
    /// Adds structured logging to the sandbox with configuration action.
    /// </summary>
    /// <param name="sandbox">The sandbox instance.</param>
    /// <param name="logger">The logger to use.</param>
    /// <param name="configure">Action to configure options.</param>
    /// <returns>The subscription disposable (dispose to unsubscribe).</returns>
    public static IDisposable AddLogging(
        this Sandbox sandbox,
        ILogger logger,
        Action<LoggingObserverOptions> configure)
    {
        var options = new LoggingObserverOptions();
        configure(options);
        return sandbox.AddLogging(logger, options);
    }
}
