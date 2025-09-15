using System;
using Microsoft.Extensions.Logging;
using Nettrace;

namespace Explorer.ViewModels;

public sealed class NettraceParser(ILogger<NettraceParser> logger)
{
    private ILogger<NettraceParser> _logger = logger;
    private NettraceReader.NettraceFile? _file = null;

    public event EventHandler? OnFileChanged;

    public NettraceReader.NettraceFile? GetFile()
        => _file;

    public void SetFile(NettraceReader.NettraceFile file)
    {
        _file = file;
        OnFileChanged?.Invoke(this, EventArgs.Empty);
    }
}
