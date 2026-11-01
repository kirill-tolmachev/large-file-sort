using System.Runtime.ExceptionServices;

namespace Sorter;

static class Workers
{
    // The first failure cancels the other workers and is rethrown as it is, so a worker waiting
    // for one that has died does not hang the process.
    public static void Run(int count, Action<CancellationToken> work)
    {
        using var failure = new CancellationTokenSource();
        var tasks = Enumerable.Range(0, count).Select(_ => Task.Run(() =>
        {
            try
            {
                work(failure.Token);
            }
            catch
            {
                failure.Cancel();
                throw;
            }
        })).ToArray();
        try
        {
            Task.WaitAll(tasks);
        }
        catch (AggregateException e)
        {
            var cause = e.InnerExceptions.FirstOrDefault(x => x is not OperationCanceledException) ?? e.InnerExceptions[0];
            ExceptionDispatchInfo.Throw(cause);
        }
    }
}
