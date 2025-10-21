using System.Collections.Frozen;

namespace Nettrace;

public static class NettraceFileExtensions
{
    public static FrozenDictionary<int, NettraceReader.EventBlob<NettraceReader.MetadataEvent>> BuildMetadataCache(this NettraceReader.NettraceFile file)
        => file.MetadataBlocks
            .SelectMany(mb => mb.EventBlobs)
            .ToFrozenDictionary(b => b.Payload.Header.MetaDataId, b => b);
}
