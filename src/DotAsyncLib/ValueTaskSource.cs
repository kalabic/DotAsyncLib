using System.Threading.Tasks.Sources;

namespace DotAsync;


/// <summary>
/// 
/// This version:
/// <list type="bullet">
/// <item>Provides <see cref="IsCompleted"/> property.</item>
/// <item><see cref="SetResult(T)"/> and <see cref="Reset"/> are under lock.</item>
/// <item>Will NOT throw if <see cref="SetResult(T)"/> is invoked multiple times. Result is set only the first time it is invoked.</item>
/// </list>
/// 
/// </summary>
internal class ValueTaskSource<T> : IValueTaskSource<T>
{
    // Public properties >>

    /// <summary>
    /// Set to true after <see cref="SetResult(T)"/> was invoked. Set to false after <see cref="Reset"/>.
    /// </summary>
    public bool IsCompleted { get { return _completed; } }

    public bool RunContinuationsAsynchronously { get { return _core.RunContinuationsAsynchronously; } }

    public short Version { get { return _core.Version; } }


    // Private data >>

    private ManualResetValueTaskSourceCore<T> _core;

    private readonly IFIFOLock _lock;

    private bool _completed;


    // Implementation >>

    public ValueTaskSource()
        : this(AsyncTools.NewDisposableFastLock())
    { }

    public ValueTaskSource(IFIFOLock fastLock)
    {
        _core = new()
        {
            RunContinuationsAsynchronously = true
        };
        _lock = fastLock;
        _completed = false;
    }

    public void Reset()
    {
        using var ticket = _lock.Lock();
        _completed = false;
        _core.Reset();
    }

    public void SetResult(T result)
    {
        using var ticket = _lock.Lock();
        if (!_completed)
        {
            _completed = true;
            _core.SetResult(result);
        }
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return _core.GetStatus(token);
    }

    public T GetResult(short token)
    {
        return _core.GetResult(token);
    }

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _core.OnCompleted(continuation, state, token, flags);
    }
}
