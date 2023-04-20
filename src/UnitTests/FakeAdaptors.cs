using Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests;

public class FakeFileListProcessor : IFileListProcessor
{
    public Task Copy(FileCopyBatch batch)
    {
        return Task.CompletedTask;
    }
}

public class FakeChunkManager : IFileResultManager
{
    public Task ProcessChunk(FileCopyBatch fileCopyBatch)
    {
        return Task.CompletedTask;
    }
}
