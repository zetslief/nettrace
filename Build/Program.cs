using System.Diagnostics;
using System.Text;

Console.Write($"[{Environment.GetCommandLineArgs()[0]}]");
foreach (var arg in args)
{
    Console.Write($" [{arg}]");
}
Console.WriteLine("");

if (args.Length < 1) Writer.Create().WriteHelp().Crash();

switch (args[0])
{
    case "record":
        if (args.Length < 3) Writer.Create().WriteInvalidNumberOfArgsError(2, args.Length - 1)
            .WriteHelp().Crash();
        Runner.Record(args[1], args[2]);
        break;
    default:
        Writer.Create().WriteUnknownCommandError(args[0]).WriteHelp().Crash();
        break;
}

public static class Runner
{
    public static void Record(string projectPath, string outputPath)
    {
        var (buildSuccess, maybeOutput, maybeProjectDllPath) = BuildProject(projectPath);
        Console.WriteLine($"BuildOutput: {maybeOutput}");
        if (!buildSuccess) throw new InvalidOperationException($"Build failed: {projectPath}.");
        Debug.Assert(maybeProjectDllPath is not null);
        var (runSuccess, maybeProjectProcess) = RunExecutable(maybeProjectDllPath.WithoutExtension());
        if (!runSuccess) throw new InvalidOperationException($"Failed to run: {maybeProjectDllPath}");
        Debug.Assert(maybeProjectProcess is not null);
        using Process projectProcess = maybeProjectProcess;
        {
            Console.WriteLine($"{maybeProjectDllPath} started: {projectProcess.Id}");
            projectProcess.Kill();
        }
        throw new NotImplementedException(nameof(Record));
    }

    static (bool Success, string? Output, string? DllName) BuildProject(string projectFilePath)
    {
        // TODO: add exception handling.
        using var buildProcess = Process.Start(new ProcessStartInfo("dotnet")
        {
            Arguments = $"build {Path.Join(Environment.CurrentDirectory, projectFilePath)}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
        });
        if (buildProcess is null) return (false, "no output - dotnet process failed to start", null);
        var output = buildProcess.StandardOutput.ReadToEnd();
        buildProcess.WaitForExit(TimeSpan.FromSeconds(10));
        if (buildProcess.ExitCode != 0) return (false, output, null);

        // Example output:
        // profileme -> profileme/bin/Debug/net10.0/profileme.dll
        var projectFileName = Path.GetFileNameWithoutExtension(projectFilePath);
        foreach (var line in output.Split(Environment.NewLine))
        {
            if (!line.Contains($"{projectFileName} ->")) continue;
            var lineSpan = line.AsSpan();
            var indexOfArrow = lineSpan.IndexOf('>');
            if (indexOfArrow < 0) continue;
            int dllNameStart = indexOfArrow + 2;
            if (dllNameStart >= lineSpan.Length) continue;
            return (true, output, new string(lineSpan[dllNameStart..]));
        }
        return (false, output, null);
    }

    static (bool Success, Process? Process) RunExecutable(string executablePath)
    {
        var runProcess = Process.Start(new ProcessStartInfo(executablePath)
        {
            RedirectStandardOutput = true,
        });
        if (runProcess is null) return (false, null);
        return (true, runProcess);
    }
}

public sealed class Writer
{
    private readonly StringBuilder _builder = new();

    public static Writer Create() => new();

    public Writer WriteUnknownCommandError(string command)
    {
        Console.WriteLine($"ERROR: unknown command '{command}'");
        return this;
    }

    public Writer WriteInvalidNumberOfArgsError(int expected, int actual)
    {
        Console.WriteLine($"ERROR: invalid number of arguments provided to the command. Expected {expected}. Actual: {actual}");
        return this;
    }

    public Writer WriteHelp()
    {
        Console.WriteLine(Help(_builder));
        _builder.Clear();
        return this;
    }

    private static string Help(StringBuilder builder)
    {
        builder.AppendLine("Build: a set of helper tools to run/record/read traces.");
        builder.AppendLine("\tBuild [COMMAND] [OPTIONS]");
        builder.AppendLine("COMMANDS:");
        builder.AppendLine("\trecord [PROJECT] [OUTPUT_FILE] - runs an executable project, attaches event listener to the process and writes the trace to the output file.");
        return builder.ToString();
    }
}

public static class WriterExtensions
{
    extension(Writer writer)
    {
        public void Crash() => Environment.Exit(1);
    }
}


public static class PathHelpers
{
    extension(string path)
    {
        public string WithoutExtension() => path[..^4];
    }
}
