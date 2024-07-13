using Nettrace;

namespace Nettrace.Tests;

public class NettraceReaderTest
{
    const string filePath = "perf.nettrace";

    [Fact]
    public void Test1()
    {
        Assert.True(File.Exists(filePath));
    }
}