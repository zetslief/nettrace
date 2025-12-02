using Nettrace;
using System.Collections.Generic;

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

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestReadVarInt(byte[] bytes, int expected)
    {
        var cursor = 0;
        var result = Readers.ReadVarUInt32(bytes.AsSpan(), ref cursor);
        Assert.Equal(expected, result);
        cursor = 0;
        var longResult = Readers.ReadVarUInt64(bytes.AsSpan(), ref cursor);
        Assert.Equal(expected, longResult);
    }

    public static IEnumerable<object[]> TestData() => PrepareTestData().Select(t => new object[] { t.Span, t.Expected });

    private static IEnumerable<(byte[] Span, int Expected)> PrepareTestData() => [
        ([0x1e], 30),
        ([0x84, 0xcc, 0x01], 26116),
        ([0x99, 0xc0, 0xe3, 0x99, 0x05], 1396236313),
    ];
}
