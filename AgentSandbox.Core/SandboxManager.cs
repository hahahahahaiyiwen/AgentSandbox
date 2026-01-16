using System.Collections.Concurrent;

namespace AgentSandbox.Core;

/// <summary>
/// Manages multiple sandbox instances. Thread-safe singleton for server-side usage.
/// </summary>
public class SandboxManager
{
    private readonly ConcurrentDictionary<string, Sandbox> _sandboxes = new();
    private readonly SandboxOptions _defaultOptions;
    private readonly TimeSpan _inactivityTimeout;

    public SandboxManager(SandboxOptions? defaultOptions = null, TimeSpan? inactivityTimeout = null)
    {
        _defaultOptions = defaultOptions ?? new SandboxOptions();
        _inactivityTimeout = inactivityTimeout ?? TimeSpan.FromHours(1);
    }

    /// <summary>
    /// Creates a new sandbox instance.
    /// </summary>
    public Sandbox Create(string? id = null, SandboxOptions? options = null)
    {
        var sandbox = new Sandbox(id, options ?? _defaultOptions, OnSandboxDisposed);
        
        if (!_sandboxes.TryAdd(sandbox.Id, sandbox))
        {
            sandbox.Dispose();
            throw new InvalidOperationException($"Sandbox with ID '{sandbox.Id}' already exists");
        }
        
        return sandbox;
    }

    /// <summary>
    /// Gets an existing sandbox by ID.
    /// </summary>
    public Sandbox? Get(string id)
    {
        return _sandboxes.TryGetValue(id, out var sandbox) ? sandbox : null;
    }

    /// <summary>
    /// Gets or creates a sandbox with the given ID.
    /// </summary>
    public Sandbox GetOrCreate(string id, SandboxOptions? options = null)
    {
        return _sandboxes.GetOrAdd(id, _ => new Sandbox(id, options ?? _defaultOptions, OnSandboxDisposed));
    }

    /// <summary>
    /// Destroys a sandbox and releases its resources.
    /// </summary>
    public bool Destroy(string id)
    {
        if (_sandboxes.TryRemove(id, out var sandbox))
        {
            sandbox.Dispose();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Called when a sandbox is disposed directly (not via Destroy).
    /// </summary>
    private void OnSandboxDisposed(string id)
    {
        _sandboxes.TryRemove(id, out _);
    }

    /// <summary>
    /// Lists all active sandbox IDs.
    /// </summary>
    public IEnumerable<string> List() => _sandboxes.Keys;

    /// <summary>
    /// Gets statistics for all sandboxes.
    /// </summary>
    public IEnumerable<SandboxStats> GetAllStats() => 
        _sandboxes.Values.Select(s => s.GetStats());

    /// <summary>
    /// Cleans up inactive sandboxes.
    /// </summary>
    public int CleanupInactive()
    {
        var cutoff = DateTime.UtcNow - _inactivityTimeout;
        var toRemove = _sandboxes
            .Where(kvp => kvp.Value.LastActivityAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            Destroy(id);
        }

        return toRemove.Count;
    }

    /// <summary>
    /// Gets total count of active sandboxes.
    /// </summary>
    public int Count => _sandboxes.Count;
}
