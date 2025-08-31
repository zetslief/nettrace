namespace Nettrace;

public static class Helpers
{
    public static DateTime QpcToUtc(NettraceReader.Trace trace, long qpcTimestamp)
        => trace.DateTime + TimeSpan.FromTicks((qpcTimestamp - trace.SynTimeQpc) * 1_0_000_000 / trace.QpcFrequency);
}
