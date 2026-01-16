using AgentSandbox.Core;

Sandbox sandbox = new Sandbox();

while (true)
{
    Console.Write("SandboxShell > ");

    string? command = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(command))
        continue;

    if (command.Trim().ToLower() == "exit")
        break;

    var result = sandbox.Execute(command);

    if (result != null)
    {
        Console.WriteLine("================================");
        Console.WriteLine("Command: " + result.Command);

        if (result.Success)
        {
            Console.WriteLine("Output: " + result.Stdout);
        }
        else
        {
            Console.WriteLine("Error: " + result.Stderr);
        }

        Console.WriteLine("Duration: " + result.Duration.TotalMilliseconds + " ms");
        Console.WriteLine("================================");
    }

    var stats = sandbox.GetStats();

    Console.WriteLine("Sandbox stats: " + stats.CommandCount + " commands executed, " + stats.FileCount + " files created.");
}