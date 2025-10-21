using DotAsync.AsyncValue;

namespace DotAsync.InternalVTS;


internal abstract class AwaitableInvokeResultVTS<TValue>
    : AwaitableValueVTS<TValue, InvokeResult>
{
    public AwaitableInvokeResultVTS(IAsyncValue<TValue> valueReader,
                           WaitHandle handle)
        : base(valueReader, handle)
    { }

    public AwaitableInvokeResultVTS(IAsyncValue<TValue> valueReader,
                           WaitHandle handle,
                           int timeout)
        : base(valueReader, handle, timeout)
    { }

    protected override InvokeResult DefaultCancelledValue()
    {
        return InvokeResult.CANCELED;
    }

    protected override InvokeResult DefaultDisposedValue()
    {
        return InvokeResult.DISPOSED;
    }
}
