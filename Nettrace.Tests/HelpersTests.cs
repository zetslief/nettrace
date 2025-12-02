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
    public void TestReadVarInt32(byte[] bytes, int expected)
    {
        var cursor = 0;
        var result = Readers.ReadVarInt32(bytes.AsSpan(), ref cursor);
        Assert.Equal(expected, result);
    }

    public static IEnumerable<object[]> TestData() => PrepareTestData().Select(t => new object[] { t.Span, t.Expected });

    private static IEnumerable<(byte[] Span, int Expected)> PrepareTestData() => [
        ([0xc7, 0x9f, 0x7f], -12345),
        ([0x91, 0x7f], -111),
        ([0x1e], 30),
        ([0xa7, 0xf4, 0x7e], -17881),
        ([0x84, 0xcc, 0x01], 26116),
        ([0xd4, 0xa5, 0xfb, 0xff, 0x79], -1610689836),
        ([0x99, 0xc0, 0xe3, 0x99, 0x05], 1396236313),
        ([0xfa, 0x92, 0x9a, 0xf2, 0x7b], -1102673542),
    ];
}
