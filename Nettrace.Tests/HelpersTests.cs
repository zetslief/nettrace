namespace Nettrace.Tests;

public sealed class HelpersTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public void OneSecond()
    {
        var frequency = 1_000_000_000;
        var sync = 1_000_000;
        var start = DateTime.UtcNow;
        var trace = new NettraceReader.Trace(start, sync, frequency, default, default, default, default);
        var result = Helpers.QpcToUtc(trace, sync + frequency);
        output.WriteLine($"Start: {start} Result: {result}");
        Assert.Equal(TimeSpan.FromSeconds(1), result - start);
    }
}