using DotAsync.Model;

namespace DotAsync.InternalVTS;


internal class CancellableInvokeResultVTS<TValue>
    : AwaitableInvokeResultVTS<TValue>
{
    // Private data >>

    private readonly CancellationToken _cancellation;

    private readonly CancellationTokenRegistration _ctr;


    // Implementation >>

    public CancellableInvokeResultVTS(IAsyncValue<TValue> valueReader,
                                            WaitHandle handle,
                                            CancellationToken cancellation)
        : base(valueReader, handle)
    {
        _cancellation = cancellation;

        if (_cancellation.CanBeCanceled && !IsCompleted) // << Base registration can (and often does) complete the task before it returns.
        {
            _ctr = _cancellation.UnsafeRegister(CancellationCallback, null);

            using var ticket = _unregisterLock.Lock();
            UnregisterEarlyCancellation();
        }
        else
        {
            _ctr = default;
        }
    }

    public CancellableInvokeResultVTS(IAsyncValue<TValue> valueReader,
                                            WaitHandle handle,
                                            int timeout,
                                            CancellationToken cancellation)
        : base(valueReader, handle, timeout)
    {
        _cancellation = cancellation;

        if (_cancellation.CanBeCanceled && !IsCompleted) // << Base registration can (and often does) complete the task before it returns.
        {
            _ctr = _cancellation.UnsafeRegister(CancellationCallback, null);

            using var ticket = _unregisterLock.Lock();
            UnregisterEarlyCancellation();
        }
        else
        {
            _ctr = default;
        }
    }

    private void UnregisterEarlyCancellation()
    {
        if (IsCompleted)
        {
            _readerHandle?.Unregister();
            _readerHandle = null;
            _ctr.Dispose();
            UnregisterEarlyCompletion();
        }
    }

    protected override void Unregister()
    {
        if (_ctr != default) _ctr.Dispose();
        base.Unregister();
    }

    protected override InvokeResult ComputeCompletionResult(IAsyncValue<TValue> reader, bool timedOut)
    {
        if (reader.IsCancelled)
        {
            return InvokeResult.CANCELED;
        }
        else if (timedOut || _cancellation.IsCancellationRequested)
        {
            return InvokeResult.CANCELED;
        }
        else if (reader.IsSet)
        {
            return reader.AsInvokeResult();
        }
        else
        {
            // Not disposed, not cancelled and no value set?
            return InvokeResult.FAILED;
        }
    }
}
