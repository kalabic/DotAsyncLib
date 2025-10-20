namespace DotAsync.Model;


public interface IAsyncValueWaiter
{
    ValueTask<InvokeResult> WaitAsync();

    ValueTask<InvokeResult> WaitAsync(int timeout);

    ValueTask<InvokeResult> WaitAsync(CancellationToken cancellation);

    ValueTask<InvokeResult> WaitAsync(int timeout, CancellationToken cancellation);

    InvokeResult Wait(int timeout = Timeout.Infinite);
}
