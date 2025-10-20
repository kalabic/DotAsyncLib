namespace DotAsync;


public static class ValueTaskResult
{
    public static readonly ValueTask<InvokeResult> SUCCESS = ValueTask.FromResult(InvokeResult.SUCCESS);

    public static readonly ValueTask<InvokeResult> FAILED = ValueTask.FromResult(InvokeResult.FAILED);

    public static readonly ValueTask<InvokeResult> CANCELED = ValueTask.FromResult(InvokeResult.CANCELED);

    public static readonly ValueTask<InvokeResult> EXCEPTION = ValueTask.FromResult(InvokeResult.EXCEPTION);

    public static readonly ValueTask<InvokeResult> TIMEOUT = ValueTask.FromResult(InvokeResult.TIMEOUT);

    public static readonly ValueTask<InvokeResult> DISPOSED = ValueTask.FromResult(InvokeResult.DISPOSED);

    public static readonly ValueTask<InvokeResult> NOT_FOUND = ValueTask.FromResult(InvokeResult.NOT_FOUND);

    public static readonly ValueTask<InvokeResult> BAD_STATE = ValueTask.FromResult(InvokeResult.BAD_STATE);

    public static readonly ValueTask<InvokeResult> BAD_MESSAGE = ValueTask.FromResult(InvokeResult.BAD_MESSAGE);
}
