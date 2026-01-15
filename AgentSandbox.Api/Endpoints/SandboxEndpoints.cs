using AgentSandbox.Api.Models;
using AgentSandbox.Core.Sandbox;
using Microsoft.AspNetCore.Mvc;

namespace AgentSandbox.Api.Endpoints;

public static class SandboxEndpoints
{
    public static void MapSandboxEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sandbox")
            .WithTags("Sandbox")
            .WithOpenApi();

        // Create a new sandbox
        group.MapPost("/", CreateSandbox)
            .WithName("CreateSandbox")
            .WithDescription("Creates a new sandbox instance");

        // List all sandboxes
        group.MapGet("/", ListSandboxes)
            .WithName("ListSandboxes")
            .WithDescription("Lists all active sandbox instances");

        // Get sandbox info
        group.MapGet("/{id}", GetSandbox)
            .WithName("GetSandbox")
            .WithDescription("Gets information about a specific sandbox");

        // Delete a sandbox
        group.MapDelete("/{id}", DeleteSandbox)
            .WithName("DeleteSandbox")
            .WithDescription("Destroys a sandbox and releases its resources");

        // Execute a command
        group.MapPost("/{id}/exec", ExecuteCommand)
            .WithName("ExecuteCommand")
            .WithDescription("Executes a shell command in the sandbox");

        // Get command history
        group.MapGet("/{id}/history", GetHistory)
            .WithName("GetCommandHistory")
            .WithDescription("Gets the command execution history");

        // File operations
        group.MapGet("/{id}/fs", ReadFile)
            .WithName("ReadFile")
            .WithDescription("Reads a file from the virtual filesystem");

        group.MapPut("/{id}/fs", WriteFile)
            .WithName("WriteFile")
            .WithDescription("Writes a file to the virtual filesystem");

        group.MapGet("/{id}/ls", ListDirectory)
            .WithName("ListDirectory")
            .WithDescription("Lists contents of a directory");

        // Snapshot operations
        group.MapPost("/{id}/snapshot", CreateSnapshot)
            .WithName("CreateSnapshot")
            .WithDescription("Creates a snapshot of the sandbox state");

        group.MapPost("/{id}/restore", RestoreSnapshot)
            .WithName("RestoreSnapshot")
            .WithDescription("Restores sandbox state from a snapshot");

        // Stats
        group.MapGet("/{id}/stats", GetStats)
            .WithName("GetStats")
            .WithDescription("Gets runtime statistics for the sandbox");
    }

    private static IResult CreateSandbox(
        [FromBody] CreateSandboxRequest? request,
        [FromServices] SandboxManager manager)
    {
        try
        {
            var options = new SandboxOptions();
            
            if (request != null)
            {
                if (request.MaxTotalSize.HasValue)
                    options.MaxTotalSize = request.MaxTotalSize.Value;
                if (request.MaxFileSize.HasValue)
                    options.MaxFileSize = request.MaxFileSize.Value;
                if (request.MaxNodeCount.HasValue)
                    options.MaxNodeCount = request.MaxNodeCount.Value;
                if (!string.IsNullOrEmpty(request.WorkingDirectory))
                    options.WorkingDirectory = request.WorkingDirectory;
                if (request.Environment != null)
                    options.Environment = request.Environment;
            }

            var sandbox = manager.Create(request?.Id, options);
            var stats = sandbox.GetStats();

            return Results.Created($"/api/sandbox/{sandbox.Id}", new SandboxResponse(
                stats.Id,
                stats.CurrentDirectory,
                stats.FileCount,
                stats.TotalSize,
                stats.CreatedAt
            ));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new ErrorResponse(ex.Message, 409));
        }
    }

    private static IResult ListSandboxes([FromServices] SandboxManager manager)
    {
        var sandboxes = manager.GetAllStats()
            .Select(s => new SandboxResponse(
                s.Id,
                s.CurrentDirectory,
                s.FileCount,
                s.TotalSize,
                s.CreatedAt
            ));

        return Results.Ok(sandboxes);
    }

    private static IResult GetSandbox(string id, [FromServices] SandboxManager manager)
    {
        var sandbox = manager.Get(id);
        if (sandbox == null)
            return Results.NotFound(new ErrorResponse($"Sandbox '{id}' not found", 404));

        var stats = sandbox.GetStats();
        return Results.Ok(new SandboxResponse(
            stats.Id,
            stats.CurrentDirectory,
            stats.FileCount,
            stats.TotalSize,
            stats.CreatedAt
        ));
    }

    private static IResult DeleteSandbox(string id, [FromServices] SandboxManager manager)
    {
        if (manager.Destroy(id))
            return Results.NoContent();
        
        return Results.NotFound(new ErrorResponse($"Sandbox '{id}' not found", 404));
    }

    private static IResult ExecuteCommand(
        string id,
        [FromBody] ExecuteCommandRequest request,
        [FromServices] SandboxManager manager)
    {
        var sandbox = manager.Get(id);
        if (sandbox == null)
            return Results.NotFound(new ErrorResponse($"Sandbox '{id}' not found", 404));

        var result = sandbox.Execute(request.Command);

        return Results.Ok(new CommandResponse(
            result.Command,
            result.Stdout,
            result.Stderr,
            result.ExitCode,
            result.Success,
            result.Duration.TotalMilliseconds
        ));
    }

    private static IResult GetHistory(string id, [FromServices] SandboxManager manager)
    {
        var sandbox = manager.Get(id);
        if (sandbox == null)
            return Results.NotFound(new ErrorResponse($"Sandbox '{id}' not found", 404));

        var history = sandbox.GetHistory()
            .Select(r => new CommandResponse(
                r.Command,
                r.Stdout,
                r.Stderr,
                r.ExitCode,
                r.Success,
                r.Duration.TotalMilliseconds
            ));

        return Results.Ok(history);
    }

    private static IResult ReadFile(
        string id,
        [FromQuery] string path,
        [FromServices] SandboxManager manager)
    {
        var sandbox = manager.Get(id);
        if (sandbox == null)
            return Results.NotFound(new ErrorResponse($"Sandbox '{id}' not found", 404));

        try
        {
            var content = sandbox.ReadFile(path);
            var entry = sandbox.FileSystem.GetEntry(path);
            
            return Results.Ok(new FileContentResponse(
                path,
                content,
                entry?.Content.Length ?? 0
            ));
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound(new ErrorResponse($"File '{path}' not found", 404));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message, 400));
        }
    }

    private static IResult WriteFile(
        string id,
        [FromBody] WriteFileRequest request,
        [FromServices] SandboxManager manager)
    {
        var sandbox = manager.Get(id);
        if (sandbox == null)
            return Results.NotFound(new ErrorResponse($"Sandbox '{id}' not found", 404));

        try
        {
            sandbox.WriteFile(request.Path, request.Content);
            return Results.Ok(new { path = request.Path, success = true });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message, 400));
        }
    }

    private static IResult ListDirectory(
        string id,
        [FromQuery] string? path,
        [FromServices] SandboxManager manager)
    {
        var sandbox = manager.Get(id);
        if (sandbox == null)
            return Results.NotFound(new ErrorResponse($"Sandbox '{id}' not found", 404));

        try
        {
            var targetPath = path ?? sandbox.Shell.CurrentDirectory;
            var entries = sandbox.ListDirectory(targetPath)
                .Select(name =>
                {
                    var fullPath = targetPath == "/" ? "/" + name : targetPath + "/" + name;
                    var entry = sandbox.FileSystem.GetEntry(fullPath);
                    return new DirectoryEntry(
                        name,
                        entry?.IsDirectory ?? false,
                        entry?.Content.Length ?? 0,
                        entry?.ModifiedAt ?? DateTime.UtcNow
                    );
                });

            return Results.Ok(new DirectoryListingResponse(targetPath, entries));
        }
        catch (DirectoryNotFoundException)
        {
            return Results.NotFound(new ErrorResponse($"Directory '{path}' not found", 404));
        }
    }

    // In-memory snapshot storage (for demo; use persistent storage in production)
    private static readonly Dictionary<string, SandboxSnapshot> _snapshots = new();

    private static IResult CreateSnapshot(string id, [FromServices] SandboxManager manager)
    {
        var sandbox = manager.Get(id);
        if (sandbox == null)
            return Results.NotFound(new ErrorResponse($"Sandbox '{id}' not found", 404));

        var snapshot = sandbox.CreateSnapshot();
        var snapshotId = Guid.NewGuid().ToString("N")[..12];
        _snapshots[snapshotId] = snapshot;

        return Results.Ok(new SnapshotResponse(
            snapshotId,
            sandbox.Id,
            snapshot.CreatedAt,
            snapshot.FileSystemData.Length
        ));
    }

    private static IResult RestoreSnapshot(
        string id,
        [FromQuery] string snapshotId,
        [FromServices] SandboxManager manager)
    {
        var sandbox = manager.Get(id);
        if (sandbox == null)
            return Results.NotFound(new ErrorResponse($"Sandbox '{id}' not found", 404));

        if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
            return Results.NotFound(new ErrorResponse($"Snapshot '{snapshotId}' not found", 404));

        sandbox.RestoreSnapshot(snapshot);
        return Results.Ok(new { restored = true, snapshotId });
    }

    private static IResult GetStats(string id, [FromServices] SandboxManager manager)
    {
        var sandbox = manager.Get(id);
        if (sandbox == null)
            return Results.NotFound(new ErrorResponse($"Sandbox '{id}' not found", 404));

        var stats = sandbox.GetStats();
        return Results.Ok(new StatsResponse(
            stats.Id,
            stats.FileCount,
            stats.TotalSize,
            stats.CommandCount,
            stats.CurrentDirectory,
            stats.CreatedAt,
            stats.LastActivityAt
        ));
    }
}
