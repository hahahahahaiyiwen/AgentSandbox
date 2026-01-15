namespace AgentSandbox.Core.FileSystem;

/// <summary>
/// Path utility methods for filesystem implementations.
/// </summary>
public static class FileSystemPath
{
    /// <summary>
    /// Normalizes a path to use forward slashes and resolves . and ..
    /// </summary>
    public static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path)) return "/";
        
        path = path.Replace('\\', '/');
        if (!path.StartsWith('/')) path = "/" + path;
        
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new Stack<string>();
        
        foreach (var part in parts)
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (stack.Count > 0) stack.Pop();
            }
            else
            {
                stack.Push(part);
            }
        }
        
        var result = "/" + string.Join("/", stack.Reverse());
        return result == "" ? "/" : result;
    }
    
    /// <summary>
    /// Gets the parent directory path.
    /// </summary>
    public static string GetParent(string path)
    {
        path = Normalize(path);
        if (path == "/") return "/";
        
        var lastSlash = path.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : path[..lastSlash];
    }
    
    /// <summary>
    /// Gets the file or directory name from a path.
    /// </summary>
    public static string GetName(string path)
    {
        path = Normalize(path);
        if (path == "/") return "/";
        
        var lastSlash = path.LastIndexOf('/');
        return path[(lastSlash + 1)..];
    }
    
    /// <summary>
    /// Gets the file extension including the dot, or empty string if none.
    /// </summary>
    public static string GetExtension(string path)
    {
        var name = GetName(path);
        var dotIndex = name.LastIndexOf('.');
        return dotIndex < 0 ? "" : name[dotIndex..];
    }
    
    /// <summary>
    /// Gets the file name without extension.
    /// </summary>
    public static string GetNameWithoutExtension(string path)
    {
        var name = GetName(path);
        var dotIndex = name.LastIndexOf('.');
        return dotIndex < 0 ? name : name[..dotIndex];
    }
    
    /// <summary>
    /// Combines path segments.
    /// </summary>
    public static string Combine(params string[] paths)
    {
        if (paths.Length == 0) return "/";
        
        var result = paths[0];
        for (int i = 1; i < paths.Length; i++)
        {
            var segment = paths[i];
            if (string.IsNullOrEmpty(segment)) continue;
            
            if (segment.StartsWith('/') || segment.StartsWith('\\'))
            {
                result = segment;
            }
            else
            {
                result = result.TrimEnd('/', '\\') + "/" + segment;
            }
        }
        
        return Normalize(result);
    }
    
    /// <summary>
    /// Checks if a path is the root.
    /// </summary>
    public static bool IsRoot(string path) => Normalize(path) == "/";
    
    /// <summary>
    /// Checks if child path is under parent path.
    /// </summary>
    public static bool IsChildOf(string childPath, string parentPath)
    {
        childPath = Normalize(childPath);
        parentPath = Normalize(parentPath);
        
        if (parentPath == "/") return childPath != "/";
        return childPath.StartsWith(parentPath + "/");
    }
}
