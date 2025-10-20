namespace DotAsync;


/// <summary>
/// 
/// Final status of various 'InvokeSync' function calls.
/// 
/// </summary>
public enum InvokeStatus : int
{
    SUCCESS = 0,

    FAILED = -1,

    CANCELED = -2,

    EXCEPTION = -3,

    TIMEOUT = -4,

    DISPOSED = -5,

    NOT_FOUND = -6,

    BAD_STATE = -7,

    BAD_MESSAGE = -8,
}

public static class InvokeStatusMethods
{
    public static bool IsSuccess(this InvokeStatus status)
    {
        return status == InvokeStatus.SUCCESS;
    }

    public static bool IsError(this InvokeStatus status)
    {
        return status != InvokeStatus.SUCCESS;
    }

    public static ValueTask<InvokeResult> AsValueTask(this InvokeStatus status)
    {
        return ValueTask.FromResult((InvokeResult)status);
    }
}
