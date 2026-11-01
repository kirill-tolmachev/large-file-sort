using System.Collections.Concurrent;

namespace Sorter;

public static class ExternalSorter
{
    const int WriteBuffer = 1 << 20;

    // The output is assembled under a temporary name and renamed when complete, so a failed sort
    // leaves no partial result and the input may be the output file itself.
    public static void Sort(SortOptions options)
    {
        var plan = SortPlan.Create(options, new FileInfo(options.Input));
        var partial = $"{options.Output}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, WriteBuffer))
                SortInto(options, plan, output);
            File.Move(partial, options.Output, overwrite: true);
        }
        finally
        {
            File.Delete(partial);
        }
    }

    static void SortInto(SortOptions options, SortPlan plan, Stream output)
    {
        var tempDirectory = Directory.CreateDirectory(Path.Combine(options.TempDirectory, $"sort-{Guid.NewGuid():N}"));
        try
        {
            var splitters = Splitters.Sample(options.Input, plan.Partitions);
            var runs = WriteRuns(options.Input, plan, splitters, tempDirectory.FullName);
            // The sort buffers are garbage by now; collecting them before the merge allocates its own
            // keeps the peak working set within the budget.
            GC.Collect();
            using var merger = new RunMerger(runs, output, plan.ReadBuffer(runs.Count), plan.PartitionBuffer);
            merger.Merge(splitters.Length + 1, options.Threads);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    static List<RunFile> WriteRuns(string input, SortPlan plan, Splitter[] splitters, string directory)
    {
        using var reader = new ChunkReader(input, plan.ChunkSize);
        var runs = new ConcurrentBag<RunFile>();
        Workers.Run(plan.SortWorkers, cancellation =>
        {
            var sorter = new ChunkSorter(plan.ChunkSize, splitters);
            while (!cancellation.IsCancellationRequested && reader.TryRead(sorter.Buffer, out int length, out long fileOffset))
                runs.Add(sorter.Sort(length, fileOffset, Path.Combine(directory, Path.GetRandomFileName())));
        });
        return [.. runs];
    }
}
