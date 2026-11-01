namespace Sorter;

sealed class ChunkReader(string path, int chunkSize) : IDisposable
{
    readonly FileStream _file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 0, FileOptions.SequentialScan);
    readonly Lock _gate = new();

    // Each chunk ends on a line boundary; the cut-off tail is simply re-read as the start of the next chunk.
    public bool TryRead(byte[] buffer, out int length, out long fileOffset)
    {
        lock (_gate)
        {
            fileOffset = _file.Position;
            int read = _file.ReadAtLeast(buffer.AsSpan(0, chunkSize), chunkSize, throwOnEndOfStream: false);
            bool endOfFile = read < chunkSize;
            if (endOfFile && read > 0 && buffer[read - 1] != '\n')
                buffer[read++] = (byte)'\n';
            length = endOfFile ? read : buffer.AsSpan(0, read).LastIndexOf((byte)'\n') + 1;
            if (length == 0 && read > 0)
                throw new InvalidDataException($"Line at byte {fileOffset} is longer than the {chunkSize:N0} byte chunk");
            _file.Position = fileOffset + length;
            return length > 0;
        }
    }

    public void Dispose() => _file.Dispose();
}
