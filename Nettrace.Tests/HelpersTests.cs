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
        ([0b0000_0000], 0),
        ([0b0111_1111], 127),
        ([0b1000_0001, 0b0000_0000], 128),
        ([0b1101_0010, 0b0000_0001], 165),
        ([0b1100_0000, 0b0000_0000], 8192),
        ([0b1111_1111, 0b0111_1111], 16383),
        ([0b1000_0001, 0b1000_0000, 0b0000_0000], 16384),
        ([0b1111_1111, 0b1111_1111, 0b0111_1111], 2_097_151),
        ([0b1000_0001, 0b1000_0000, 0b1000_0000, 0b0000_0000], 2_097_152),
        ([0b1100_0000, 0b1000_0000, 0b1000_0000, 0b0000_0000], 134_217_728),
        ([0b1111_1111, 0b1111_1111, 0b1111_1111, 0b0111_1111], 268_435_455),
        ([0b1000_0001, 0b1000_0000, 0b1000_0000, 0b1000_0000, 0b0000_0000], 268_435_456),
    ];
}
