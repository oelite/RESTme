using System.IO;

namespace OElite;

public static class StreamUtils
{
    public static byte[] ToBytes(this Stream streamObj)
    {
        if (streamObj is MemoryStream stream)
            return stream.ToArray();

        using var memoryStream = new MemoryStream();
        streamObj.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}