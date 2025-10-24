using DotAsync.AsyncValue;

namespace DotAsync.InternalVTS;


internal class WaitableInvokeResultVTS<TValue>
    : AwaitableInvokeResultVTS<TValue>
{
    public WaitableInvokeResultVTS(IAsyncValue<TValue> valueReader,
                                   WaitHandle handle)
        : base(valueReader, handle)
    { }

    public WaitableInvokeResultVTS(IAsyncValue<TValue> valueReader,
                                   WaitHandle handle,
                                   int timeout)
        : base(valueReader, handle, timeout)
    { }

    protected override InvokeResult ComputeCompletionResult(IAsyncValue<TValue> reader, bool timedOut)
    {
        if (reader.IsCancelled)
        {
            return InvokeResult.CANCELED;
        }
        else if (timedOut)
        {
            return InvokeResult.TIMEOUT;
        }
        else if (reader.IsSet)
        {
            return reader.AsInvokeResult();
        }
        else
        {
            // Not disposed, no timeout and no value set?
            return InvokeResult.FAILED;
        }
    }

    protected override InvokeResult DefaultCancelledValue()
    {
        return InvokeResult.CANCELED;
    }
}
