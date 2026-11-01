namespace Sorter;

public sealed record SortOptions(string Input, string Output)
{
    public long MemoryBudget { get; init; } = 2L << 30;
    public string TempDirectory { get; init; } = Path.GetDirectoryName(Path.GetFullPath(Output))!;
    public int Threads { get; init; } = Environment.ProcessorCount;
}
