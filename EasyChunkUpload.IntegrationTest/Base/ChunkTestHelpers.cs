using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EasyChunkUpload.IntegrationTest.Base;

public class ChunkTestHelpers
{

    public static byte[] GenerateTestChunk(int size)
    {
        var random = new Random();
        var buffer = new byte[size];
        random.NextBytes(buffer);
        return buffer;
    }
    
}
