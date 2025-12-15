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
        Record(args[1], args[2]);
        break;
    default:
        Writer.Create().WriteUnknownCommandError(args[0]).WriteHelp().Crash();
        break;
}

static void Record(string executableToRun, string outputPath)
{
    var process = Run(executableToRun);
    throw new NotImplementedException(nameof(Record));
}

static Process Run(string projectFilePath)
    => Process.Start("dotnet", $"run --project '{projectFilePath}'");

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
        builder.AppendLine("\trecord [EXECUTABLE_TO_RUN] [OUTPUT_FILE] - runs an executable, attaches even listener to the process and writes the trace to the output file.");
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

