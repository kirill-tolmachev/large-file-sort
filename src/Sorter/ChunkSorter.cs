using System.Collections.Concurrent;
using Common;

namespace Sorter;

sealed record RunFile(string Path, long[] Sections);

sealed class ChunkSorter(int chunkSize, Splitter[] splitters)
{
    public byte[] Buffer { get; } = new byte[chunkSize];
    LineRef[] _lines = [];
    LineRef[] _sorted = [];
    int[] _buckets = [];

    // Lines are grouped by splitter range first, so the groups can be sorted in parallel and
    // written out as the run's sections without any further comparison.
    public RunFile Sort(int length, long fileOffset, string path)
    {
        var data = Buffer.AsSpan(0, length);
        int count = data.Count((byte)'\n');
        Ensure(ref _lines, count);
        Ensure(ref _sorted, count);
        Ensure(ref _buckets, count);
        Index(data, fileOffset, _lines.AsSpan(0, count));
        var starts = Partition(count);
        Parallel.For(0, starts.Length - 1, bucket =>
            _sorted.AsSpan(starts[bucket], starts[bucket + 1] - starts[bucket]).Sort(new LineRefComparer(Buffer)));
        return WriteRun(path, starts);
    }

    // Headroom keeps later chunks with slightly more lines from reallocating the indexes.
    static void Ensure<T>(ref T[] array, int count)
    {
        if (array.Length < count)
            array = new T[count + count / 8];
    }

    static void Index(ReadOnlySpan<byte> data, long fileOffset, Span<LineRef> lines)
    {
        int start = 0;
        foreach (ref var line in lines)
        {
            int length = data[start..].IndexOf((byte)'\n') + 1;
            if (!LineFormat.TryParse(data.Slice(start, length - 1), out long number, out var text))
                throw new InvalidDataException($"Malformed line at byte {fileOffset + start} of the input");
            var (textOffset, textLength) = text.GetOffsetAndLength(length - 1);
            ulong prefix = LineFormat.Prefix(data.Slice(start + textOffset, textLength));
            line = new LineRef(prefix, number, start, length, textOffset, textLength);
            start += length;
        }
    }

    int[] Partition(int count)
    {
        Parallel.ForEach(Partitioner.Create(0, count), range =>
        {
            for (int i = range.Item1; i < range.Item2; i++)
                _buckets[i] = BucketOf(_lines[i]);
        });
        var starts = new int[splitters.Length + 2];
        for (int i = 0; i < count; i++)
            starts[_buckets[i] + 1]++;
        for (int bucket = 1; bucket < starts.Length; bucket++)
            starts[bucket] += starts[bucket - 1];
        var next = (int[])starts.Clone();
        for (int i = 0; i < count; i++)
            _sorted[next[_buckets[i]]++] = _lines[i];
        return starts;
    }

    int BucketOf(in LineRef line)
    {
        int low = 0, high = splitters.Length;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (Compare(line, splitters[middle]) >= 0)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    int Compare(in LineRef line, in Splitter splitter) =>
        LineFormat.Compare(
            line.Prefix, Buffer.AsSpan(line.Start + line.TextOffset, line.TextLength), line.Number,
            splitter.Prefix, splitter.Text, splitter.Number);

    RunFile WriteRun(string path, int[] starts)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 20);
        var sections = new long[starts.Length];
        for (int bucket = 0; bucket + 1 < starts.Length; bucket++)
        {
            foreach (ref readonly var line in _sorted.AsSpan(starts[bucket], starts[bucket + 1] - starts[bucket]))
                file.Write(Buffer, line.Start, line.Length);
            sections[bucket + 1] = file.Position;
        }
        return new RunFile(path, sections);
    }
}
