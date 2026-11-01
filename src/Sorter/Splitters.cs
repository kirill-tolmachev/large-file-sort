using Common;

namespace Sorter;

readonly record struct Splitter(ulong Prefix, long Number, byte[] Text);

static class Splitters
{
    const int SampleBlocks = 256;
    const int SampleBlockSize = 64 << 10;

    public static Splitter[] Sample(string path, int partitions)
    {
        using var file = File.OpenHandle(path);
        long length = RandomAccess.GetLength(file);
        long step = Math.Max(length / SampleBlocks, SampleBlockSize);
        var block = new byte[SampleBlockSize];
        var samples = new List<Splitter>();
        for (long offset = 0; offset < length; offset += step)
        {
            var data = block.AsSpan(0, RandomAccess.Read(file, block, offset));
            int start = offset == 0 ? 0 : data.IndexOf((byte)'\n') + 1;
            int end = data.LastIndexOf((byte)'\n');
            if (start > end)
                continue;
            var lines = data[start..end];
            foreach (var range in lines.Split((byte)'\n'))
            {
                var line = lines[range];
                if (LineFormat.TryParse(line, out long number, out var text))
                    samples.Add(new Splitter(LineFormat.Prefix(line[text]), number, line[text].ToArray()));
            }
        }
        if (samples.Count == 0)
            return [];
        samples.Sort((a, b) => LineFormat.Compare(a.Prefix, a.Text, a.Number, b.Prefix, b.Text, b.Number));
        return Enumerable.Range(1, partitions - 1).Select(i => samples[(int)((long)i * samples.Count / partitions)]).ToArray();
    }
}
