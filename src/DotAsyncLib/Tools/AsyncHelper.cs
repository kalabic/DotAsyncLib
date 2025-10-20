namespace DotAsync.Tools;


public static class AsyncHelper
{
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
