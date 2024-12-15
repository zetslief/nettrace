using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;

namespace Explorer.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private string? filePath = "./../perf.nettrace";
    private string metadataBlockContent = string.Empty;
    private string eventBlockContext = string.Empty;

    public MainWindowViewModel()
    {
        WelcomeCommand = ReactiveCommand.Create(OnCommand);
    }

    public ICommand WelcomeCommand { get; }

    public string? FilePath
    {
        get => filePath;
        set => this.RaiseAndSetIfChanged(ref filePath, value);
    }

    public string MetadataBlockContent
    {
        get => metadataBlockContent;
        set => this.RaiseAndSetIfChanged(ref metadataBlockContent, value);
    }

    public string EventBlockContent
    {
        get => metadataBlockContent;
        set => this.RaiseAndSetIfChanged(ref metadataBlockContent, value);
    }

    private void OnCommand()
    {
        if (filePath is null)
        {
            return;
        }

        var path = Path.GetFullPath(filePath);

        if (!File.Exists(path))
        {
            MetadataBlockContent = $"File not found: {path}";
        }

        var nettrace = Nettrace.NettraceReader.Read(File.Open(path, FileMode.Open));
        MetadataBlockContent = nettrace.MetadataBlocks.Select(b => b.ToString()).Aggregate((block, acc) => $"{acc}{Environment.NewLine}{block}");
        EventBlockContent = nettrace.EventBlocks.Select(b => b.ToString()).Aggregate((block, acc) => $"{acc}{Environment.NewLine}{block}");
    }
}