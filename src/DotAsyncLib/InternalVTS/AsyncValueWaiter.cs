using DotAsync.AsyncValue;
using DotAsync.InternalTools;

namespace DotAsync.InternalVTS;


internal class AsyncValueWaiter<TValue>
    : IAsyncValueWaiter
{
    private readonly IAsyncValue<TValue> _reader;

    private readonly LockedManualResetEvent _eventLock;

    public AsyncValueWaiter(IAsyncValue<TValue> reader, LockedManualResetEvent eventLock)
    {
        _reader = reader;
        _eventLock = eventLock;
    }

    public ValueTask<InvokeResult> WaitAsync(int timeout)
    {
        return WaitAsync(timeout, CancellationToken.None);
    }

    public ValueTask<InvokeResult> WaitAsync(CancellationToken cancellation)
    {
        return WaitAsync(Timeout.Infinite, cancellation);
    }

    public ValueTask<InvokeResult> WaitAsync()
    {
        using (var ticket = _eventLock.Lock())
        {
            if (ticket.Failed)
                return ValueTaskResult.DISPOSED;

            if (_reader.IsCancelled)
                return ValueTaskResult.CANCELED;

            if (_reader.IsSet)
                return ValueTask.FromResult(_reader.AsInvokeResult());

            WaitHandle? handle = _eventLock.WaitHandle;
            if (handle is null)
                return ValueTaskResult.DISPOSED;

            WaitHandle safeHandle = handle;

            var taskSource = new WaitableInvokeResultVTS<TValue>(_reader, safeHandle);
            if (taskSource.IsCompleted)
            {
                return ValueTask.FromResult(taskSource.GetResult(taskSource.Version));
            }

            return new ValueTask<InvokeResult>(taskSource, taskSource.Version);
        }
    }

    public ValueTask<InvokeResult> WaitAsync(int timeout, CancellationToken cancellation)
    {
        using (var ticket = _eventLock.Lock())
        {
            if (ticket.Failed)
                return ValueTaskResult.DISPOSED;

            if (_reader.IsCancelled)
                return ValueTaskResult.CANCELED;

            if (cancellation.IsCancellationRequested)
                return ValueTaskResult.CANCELED;

            if (_reader.IsSet)
                return ValueTask.FromResult(_reader.AsInvokeResult());

            WaitHandle? handle = _eventLock.WaitHandle;
            if (handle is null)
                return ValueTaskResult.DISPOSED;

            WaitHandle safeHandle = handle;

            var taskSource = new CancellableInvokeResultVTS<TValue>(
                _reader, safeHandle, timeout, cancellation);
            if (taskSource.IsCompleted)
            {
                return ValueTask.FromResult(taskSource.GetResult(taskSource.Version));
            }

            return new ValueTask<InvokeResult>(taskSource, taskSource.Version);
        }
    }

    /// <summary> TODO: Improve. </summary>
    public InvokeResult Wait(int timeout = Timeout.Infinite)
    {
        var waitValueTask = WaitAsync(timeout).Preserve();
        var waitTask = waitValueTask.AsTask();

        try
        {
            if (!waitTask.IsCompleted)
            {
                waitTask.Wait(timeout);
            }

            if (!waitTask.IsCompleted)
            {
                return InvokeResult.TIMEOUT;
            }

            // This will rethrow if the task faulted, so catch and map exceptions below.
            return waitTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return InvokeResult.CANCELED;
        }
        catch (Exception)
        {
            return InvokeResult.EXCEPTION;
        }
    }
}
