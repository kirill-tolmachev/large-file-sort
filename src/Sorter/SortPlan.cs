namespace Sorter;

// Three quarters of the budget go to the sort workers, each holding a chunk plus two line
// indexes of about the chunk's size. The remaining quarter buffers the merge, which starts
// only after the sort buffers have been collected: half for reading the runs and half for the
// partitions being assembled, which are sized to fill their buffers about half way.
sealed record SortPlan(int SortWorkers, int ChunkSize, int Partitions, int PartitionBuffer, long ReadBudget)
{
    public const long MinMemoryBudget = 1 << 20;
    const int MaxSortWorkers = 4;
    const int MaxChunkSize = 1 << 30;
    const int MinReadBuffer = 16 << 10;
    const int MaxReadBuffer = 1 << 20;

    public static SortPlan Create(SortOptions options, FileInfo input)
    {
        if (options.MemoryBudget < MinMemoryBudget)
            throw new ArgumentException($"The memory budget must be at least {MinMemoryBudget >> 20} MB");
        int sortWorkers = Math.Min(MaxSortWorkers, options.Threads);
        long sortBudget = options.MemoryBudget / 4 * 3;
        long mergeBudget = options.MemoryBudget / 4;
        int partitionBuffer = (int)(mergeBudget / 2 / options.Threads);
        return new SortPlan(
            sortWorkers,
            (int)Math.Min(sortBudget / sortWorkers / 4, MaxChunkSize),
            (int)(input.Length * 2 / partitionBuffer) + 1,
            partitionBuffer,
            mergeBudget / 2 / options.Threads);
    }

    public int ReadBuffer(int runs) => (int)Math.Clamp(ReadBudget / Math.Max(runs, 1), MinReadBuffer, MaxReadBuffer);
}
