using System.Buffers;

namespace Generator;

sealed class LineGenerator(byte[][] words)
{
    const int BlockSize = 16 << 20;
    const int PoolSize = 10_000;
    const int MaxNumber = 999_999;
    const int MaxWords = 6;
    readonly int _maxLineLength = 32 + MaxWords * (words.Max(word => word.Length) + 1);

    public void Write(string path, long size, int seed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        var rng = new Random(seed);
        var pool = CreatePool(rng);
        int usableBlock = BlockSize - _maxLineLength;
        var blockSeeds = Enumerable.Range(0, (int)((size + usableBlock - 1) / usableBlock)).Select(_ => rng.Next()).ToArray();

        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
        var pending = new Queue<Task<(byte[] Buffer, int Length)>>();
        foreach (int blockSeed in blockSeeds)
        {
            pending.Enqueue(Task.Run(() => FillBlock(new Random(blockSeed), pool)));
            if (pending.Count == Environment.ProcessorCount)
                WriteBlock(pending.Dequeue().GetAwaiter().GetResult());
        }
        while (pending.Count > 0)
            WriteBlock(pending.Dequeue().GetAwaiter().GetResult());

        void WriteBlock((byte[] Buffer, int Length) block)
        {
            long remaining = size - output.Position;
            int length = block.Length <= remaining
                ? block.Length
                : block.Buffer.AsSpan(0, (int)remaining).LastIndexOf((byte)'\n') + 1;
            output.Write(block.Buffer, 0, length);
            ArrayPool<byte>.Shared.Return(block.Buffer);
        }
    }

    byte[][] CreatePool(Random rng)
    {
        Span<byte> phrase = stackalloc byte[_maxLineLength];
        var pool = new byte[PoolSize][];
        for (int i = 0; i < pool.Length; i++)
            pool[i] = phrase[..WritePhrase(phrase, rng)].ToArray();
        return pool;
    }

    (byte[] Buffer, int Length) FillBlock(Random rng, byte[][] pool)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BlockSize);
        int length = 0;
        while (length <= BlockSize - _maxLineLength)
            length += WriteLine(buffer.AsSpan(length), rng, pool);
        return (buffer, length);
    }

    int WriteLine(Span<byte> line, Random rng, byte[][] pool)
    {
        rng.Next(-MaxNumber, MaxNumber + 1).TryFormat(line, out int length);
        ". "u8.CopyTo(line[length..]);
        length += 2;
        if (rng.Next(2) == 0)
        {
            var phrase = pool[rng.Next(pool.Length)];
            phrase.CopyTo(line[length..]);
            length += phrase.Length;
        }
        else
            length += WritePhrase(line[length..], rng);
        line[length++] = (byte)'\n';
        return length;
    }

    int WritePhrase(Span<byte> phrase, Random rng)
    {
        int length = 0;
        for (int count = rng.Next(1, MaxWords + 1); count > 0; count--)
        {
            if (length > 0)
                phrase[length++] = (byte)' ';
            var word = words[rng.Next(words.Length)];
            word.CopyTo(phrase[length..]);
            length += word.Length;
        }
        if (char.IsAsciiLetterLower((char)phrase[0]))
            phrase[0] -= 'a' - 'A';
        return length;
    }
}
