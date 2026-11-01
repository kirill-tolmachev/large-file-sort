using Common;
using Microsoft.Win32.SafeHandles;

namespace Sorter;

// Partitions are merged in parallel into per-worker buffers and written in partition order, so the
// output file is written strictly sequentially. A partition that outgrows its buffer waits for its
// turn and streams the rest.
sealed class RunMerger(List<RunFile> runs, Stream output, int readBuffer, int partitionBuffer) : IDisposable
{
    static readonly IComparer<SectionReader> Order = Comparer<SectionReader>.Create(
        (a, b) => LineFormat.Compare(a.Prefix, a.Text, a.Number, b.Prefix, b.Text, b.Number));

    readonly SafeFileHandle[] _handles = runs.Select(run => File.OpenHandle(run.Path, options: FileOptions.RandomAccess)).ToArray();
    readonly object _turn = new();
    int _claimed = -1;
    int _written;

    public void Merge(int partitions, int threads) => Workers.Run(threads, cancellation =>
    {
        using var wake = cancellation.Register(() => { lock (_turn) Monitor.PulseAll(_turn); });
        var readBuffers = runs.Select(_ => new byte[readBuffer]).ToArray();
        var buffer = new byte[partitionBuffer];
        for (int partition; !cancellation.IsCancellationRequested && (partition = Interlocked.Increment(ref _claimed)) < partitions;)
            MergePartition(partition, readBuffers, buffer, cancellation);
    });

    void MergePartition(int partition, byte[][] readBuffers, byte[] buffer, CancellationToken cancellation)
    {
        var queue = new PriorityQueue<SectionReader, SectionReader>(Order);
        for (int i = 0; i < runs.Count; i++)
            Enqueue(new SectionReader(_handles[i], runs[i].Sections[partition], runs[i].Sections[partition + 1], readBuffers[i]));

        int filled = 0;
        bool streaming = false;
        while (queue.TryDequeue(out var reader, out _))
        {
            var line = reader.Line;
            if (!streaming && filled + line.Length > buffer.Length)
            {
                AwaitTurn(partition, cancellation);
                output.Write(buffer, 0, filled);
                streaming = true;
            }
            if (streaming)
                output.Write(line);
            else
            {
                line.CopyTo(buffer.AsSpan(filled));
                filled += line.Length;
            }
            Enqueue(reader);
        }
        if (!streaming)
        {
            AwaitTurn(partition, cancellation);
            output.Write(buffer, 0, filled);
        }
        EndTurn();

        void Enqueue(SectionReader reader)
        {
            if (reader.MoveNext())
                queue.Enqueue(reader, reader);
        }
    }

    void AwaitTurn(int partition, CancellationToken cancellation)
    {
        lock (_turn)
        {
            while (_written != partition)
            {
                cancellation.ThrowIfCancellationRequested();
                Monitor.Wait(_turn);
            }
        }
    }

    void EndTurn()
    {
        lock (_turn)
        {
            _written++;
            Monitor.PulseAll(_turn);
        }
    }

    public void Dispose()
    {
        foreach (var handle in _handles)
            handle.Dispose();
    }
}
