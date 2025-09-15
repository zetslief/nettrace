using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Nettrace;

namespace Explorer.ViewModels;

public sealed class NettraceParser(ILogger<NettraceParser> logger)
{
    private readonly ILogger<NettraceParser> _logger = logger;
    private NettraceReader.NettraceFile? _file = null;

    public event EventHandler? OnFileChanged;

    public NettraceReader.NettraceFile? GetFile()
        => _file;

    public void SetFile(byte[] file)
    {
        using var stream = new MemoryStream(file);
        _file = NettraceReader.Read(stream);
        _logger.LogInformation("{NettraceFile}", _file);
        OnFileChanged?.Invoke(this, EventArgs.Empty);
    }
}
