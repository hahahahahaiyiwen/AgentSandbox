using AgentSandbox.Core.FileSystem;
using AgentSandbox.Core.Shell;

namespace AgentSandbox.Tests;

public class SandboxShellTests
{
    private readonly FileSystem _fs;
    private readonly SandboxShell _shell;

    public SandboxShellTests()
    {
        _fs = new FileSystem();
        _shell = new SandboxShell(_fs);
    }

    [Fact]
    public void Pwd_ReturnsCurrentDirectory()
    {
        var result = _shell.Execute("pwd");
        
        Assert.True(result.Success);
        Assert.Equal("/", result.Stdout);
    }

    [Fact]
    public void Cd_ChangesDirectory()
    {
        _fs.CreateDirectory("/mydir");
        
        var result = _shell.Execute("cd /mydir");
        
        Assert.True(result.Success);
        Assert.Equal("/mydir", _shell.CurrentDirectory);
    }

    [Fact]
    public void Cd_FailsForNonexistentDirectory()
    {
        var result = _shell.Execute("cd /nonexistent");
        
        Assert.False(result.Success);
        Assert.Contains("No such file or directory", result.Stderr);
    }

    [Fact]
    public void Mkdir_CreatesDirectory()
    {
        var result = _shell.Execute("mkdir /newdir");
        
        Assert.True(result.Success);
        Assert.True(_fs.Exists("/newdir"));
        Assert.True(_fs.IsDirectory("/newdir"));
    }

    [Fact]
    public void Mkdir_WithP_CreatesParents()
    {
        var result = _shell.Execute("mkdir -p /a/b/c");
        
        Assert.True(result.Success);
        Assert.True(_fs.Exists("/a/b/c"));
    }

    [Fact]
    public void Touch_CreatesEmptyFile()
    {
        var result = _shell.Execute("touch /newfile.txt");
        
        Assert.True(result.Success);
        Assert.True(_fs.Exists("/newfile.txt"));
        Assert.Equal(0, _fs.ReadFile("/newfile.txt", System.Text.Encoding.UTF8).Length);
    }

    [Fact]
    public void Echo_PrintsText()
    {
        var result = _shell.Execute("echo Hello World");
        
        Assert.True(result.Success);
        Assert.Equal("Hello World", result.Stdout);
    }

    [Fact]
    public void Cat_PrintsFileContent()
    {
        _fs.WriteFile("/test.txt", "file content");
        
        var result = _shell.Execute("cat /test.txt");
        
        Assert.True(result.Success);
        Assert.Equal("file content", result.Stdout);
    }

    [Fact]
    public void Ls_ListsDirectoryContents()
    {
        _fs.WriteFile("/a.txt", "a");
        _fs.WriteFile("/b.txt", "b");
        
        var result = _shell.Execute("ls /");
        
        Assert.True(result.Success);
        Assert.Contains("a.txt", result.Stdout);
        Assert.Contains("b.txt", result.Stdout);
    }

    [Fact]
    public void Rm_RemovesFile()
    {
        _fs.WriteFile("/delete.txt", "x");
        
        var result = _shell.Execute("rm /delete.txt");
        
        Assert.True(result.Success);
        Assert.False(_fs.Exists("/delete.txt"));
    }

    [Fact]
    public void Rm_Rf_RemovesDirectoryRecursively()
    {
        _fs.WriteFile("/dir/sub/file.txt", "x");
        
        var result = _shell.Execute("rm -rf /dir");
        
        Assert.True(result.Success);
        Assert.False(_fs.Exists("/dir"));
    }

    [Fact]
    public void Cp_CopiesFile()
    {
        _fs.WriteFile("/source.txt", "content");
        
        var result = _shell.Execute("cp /source.txt /dest.txt");
        
        Assert.True(result.Success);
        Assert.True(_fs.Exists("/dest.txt"));
        Assert.Equal("content", _fs.ReadFile("/dest.txt", System.Text.Encoding.UTF8));
    }

    [Fact]
    public void Mv_MovesFile()
    {
        _fs.WriteFile("/old.txt", "content");
        
        var result = _shell.Execute("mv /old.txt /new.txt");
        
        Assert.True(result.Success);
        Assert.False(_fs.Exists("/old.txt"));
        Assert.True(_fs.Exists("/new.txt"));
    }

    [Fact]
    public void Head_ShowsFirstLines()
    {
        _fs.WriteFile("/lines.txt", "line1\nline2\nline3\nline4\nline5");
        
        var result = _shell.Execute("head -n 2 /lines.txt");
        
        Assert.True(result.Success);
        Assert.Contains("line1", result.Stdout);
        Assert.Contains("line2", result.Stdout);
        Assert.DoesNotContain("line3", result.Stdout);
    }

    [Fact]
    public void Tail_ShowsLastLines()
    {
        _fs.WriteFile("/lines.txt", "line1\nline2\nline3\nline4\nline5");
        
        var result = _shell.Execute("tail -n 2 /lines.txt");
        
        Assert.True(result.Success);
        Assert.Contains("line4", result.Stdout);
        Assert.Contains("line5", result.Stdout);
        Assert.DoesNotContain("line1", result.Stdout);
    }

    [Fact]
    public void Grep_FindsMatchingLines()
    {
        _fs.WriteFile("/search.txt", "apple\nbanana\napricot\ncherry");
        
        var result = _shell.Execute("grep ap /search.txt");
        
        Assert.True(result.Success);
        Assert.Contains("apple", result.Stdout);
        Assert.Contains("apricot", result.Stdout);
        Assert.DoesNotContain("banana", result.Stdout);
    }

    [Fact]
    public void Export_SetsEnvironmentVariable()
    {
        _shell.Execute("export MY_VAR=my_value");
        
        Assert.Equal("my_value", _shell.Environment["MY_VAR"]);
    }

    [Fact]
    public void VariableExpansion_Works()
    {
        _shell.Execute("export NAME=World");
        
        var result = _shell.Execute("echo Hello $NAME");
        
        Assert.Equal("Hello World", result.Stdout);
    }

    [Fact]
    public void RelativePaths_Work()
    {
        _fs.CreateDirectory("/home/user");
        _shell.Execute("cd /home/user");
        _shell.Execute("mkdir mydir");
        _shell.Execute("touch mydir/file.txt");
        
        Assert.True(_fs.Exists("/home/user/mydir"));
        Assert.True(_fs.Exists("/home/user/mydir/file.txt"));
    }

    [Fact]
    public void UnknownCommand_ReturnsError()
    {
        var result = _shell.Execute("unknowncommand");
        
        Assert.False(result.Success);
        Assert.Equal(127, result.ExitCode);
        Assert.Contains("command not found", result.Stderr);
    }

    [Fact]
    public void Help_ListsAvailableCommands()
    {
        var result = _shell.Execute("help");
        
        Assert.True(result.Success);
        Assert.Contains("pwd", result.Stdout);
        Assert.Contains("cd", result.Stdout);
        Assert.Contains("ls", result.Stdout);
    }
}
