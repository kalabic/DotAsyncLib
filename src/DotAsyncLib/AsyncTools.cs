using DotAsync.InternalLock;

namespace DotAsync;

#pragma warning disable DotAsync_Lock1


public static class AsyncTools
{
    /// <summary> 
    /// 
    /// Creates a disposable asynchronous FIFO lock.
    /// 
    /// </summary>
    /// <returns>A new instance of a FIFO lock that can be awaited and disposed.</returns>
    public static IDisposableFIFOLock NewDisposableLock()
    {
        return new AsyncFIFOLock();
    }

    /// <summary>
    /// 
    /// Creates a disposable, fast non-fair lock optimized for low-contention scenarios.
    /// 
    /// </summary>
    /// <returns>A new fast asynchronous lock.</returns>
    public static IDisposableFIFOLock NewDisposableFastLock()
    {
        return new FastFIFOLock();
    }

    /// <summary>
    /// 
    /// Creates a disposable, reentrant asynchronous lock.
    /// 
    /// </summary>
    /// <returns>A new reentrant asynchronous lock instance.</returns>
    public static IDisposableFIFOLock NewDisposableReentrantLock()
    {
        return new ReentrantFIFOLock();
    }

    /// <summary>
    /// 
    /// Creates a disposable lock designed for synchronized disposal operations.
    /// 
    /// </summary>
    /// <param name="enableDispose">If true, the lock participates in disposal coordination.</param>
    /// <returns>An <see cref="IInvokeDisposeLock"/> instance.</returns>
    public static IInvokeDisposeLock NewInvokeDisposeLock(bool enableDispose = false)
    {
        return new InvokeDisposeLock(enableDispose);
    }

    /// <summary>
    /// 
    /// Creates a disposable prioritized asynchronous lock that grants access based on request priority.
    /// 
    /// </summary>
    /// <param name="enableDispose">If true, the lock participates in disposal coordination.</param>
    /// <returns>An <see cref="IDisposablePrioritizedLock"/> instance.</returns>
    public static IDisposablePrioritizedLock NewDisposablePrioritizedLock(bool enableDispose = false)
    {
        return new PrioritizedLock(enableDispose);
    }

    /// <summary>
    /// 
    /// Awaits two asynchronous <see cref="ValueTask{InvokeResult}"/> instances in sequence,
    /// returning the result of the second only if the first completes successfully.
    /// 
    /// </summary>
    /// <param name="first">The first asynchronous operation to await.</param>
    /// <param name="second">The second asynchronous operation to await if the first succeeds.</param>
    /// <returns>
    /// A <see cref="ValueTask{InvokeResult}"/> that resolves to the first failure result
    /// or the result of the second task if both succeed.
    /// </returns>
    public static ValueTask<InvokeResult> AwaitValueTaskPair(ValueTask<InvokeResult> first,
                                                             ValueTask<InvokeResult> second)
    {
        if (first.IsCompletedSuccessfully)
        {
            // Fast sync path: first already completed synchronously
            var req = first.Result;

            // If result is a success return second task, or a failed first one.
            return req ? second : first;
        }
        else if (first.IsCompleted)
        {
            // Fast sync path: first failed synchronously
            return first;
        }

        // Slow path: need an async state machine
        return WaitAsync();

        async ValueTask<InvokeResult> WaitAsync()
        {
            var req = await first.ConfigureAwait(false);
            if (req != InvokeResult.SUCCESS)
            {
                return req;
            }

            if (second.IsCompleted)
            {
                return second.Result;
            }

            return await second.ConfigureAwait(false);
        }
    }
}
