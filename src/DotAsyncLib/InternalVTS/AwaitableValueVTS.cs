using DotBase.Event;
using System.Diagnostics;
using System.Threading.Tasks.Sources;
using DotAsync.AsyncValue;

namespace DotAsync.InternalVTS;


/// <summary>
/// 
/// This class is a link between an object implementing <see cref="IAsyncValue{TValue}"/> interface
/// and a <see cref="ValueTaskSource{TCompletedValue}"/>.
/// <para>There are two generic type parameters, so a type conversion from actual
/// awaited value to a type returned by ValueTaskSource can be made.</para>
/// 
/// </summary>
/// <typeparam name="TValue">Parameter passed to <see cref="IAsyncValue{TValue}"/>.</typeparam>
/// <typeparam name="TCompletedValue">Parameter passed to <see cref="ValueTaskSource{TCompletedValue}"/></typeparam>
internal abstract class AwaitableValueVTS<TValue, TCompletedValue>
    : IValueTaskSource<TCompletedValue>
{
    // Public properties >>

    public bool IsCompleted { get { return _vts.IsCompleted; } }

    public short Version { get { { return _vts.Version; } } }


    // Private data >>

    protected readonly IFIFOLock _lock = AsyncTools.NewDisposableFastLock();

    /// <summary> Needed because 'Unregister' function can and will be invoked sometimes (very rare) during construction. </summary>
    protected readonly IFIFOLock _unregisterLock = AsyncTools.NewDisposableFastLock();

    protected ValueTaskSource<TCompletedValue> _vts;

    protected CState _cstate;

    protected ReaderRegistrationHandle? _readerHandle;

    private readonly CancellationEventConsumer _cancelled;


    // Abstract functions >>

    protected abstract TCompletedValue DefaultCancelledValue();

    protected abstract TCompletedValue DefaultDisposedValue();

    protected abstract TCompletedValue ComputeCompletionResult(IAsyncValue<TValue> reader, bool timedOut);


    // Implementation >>

    /// <summary>
    /// 
    /// A weak reference is used for <paramref name="valueReader"/> othervise it
    /// could be kept alive just because we are waiting for it here (and so it
    /// would never change its state making this object wait for it forever).
    /// 
    /// </summary>
    public AwaitableValueVTS(IAsyncValue<TValue> valueReader, WaitHandle handle)
    {
        ArgumentNullException.ThrowIfNull(valueReader);
        ArgumentNullException.ThrowIfNull(handle);
        _vts = new(_lock);
        _cstate = new();
        _readerHandle = new(valueReader, handle, ComputeCompletionCallback);
        _cancelled = new();

        using (var ticket = _unregisterLock.Lock())
        {
            if (!IsCompleted)
            {
                _cancelled.AddHandler(HandleCancellation);
                valueReader.Connect(_cancelled);
            }
            else
            {
                UnregisterEarlyCompletion();
            }

            _cstate.IsConstructed = true;
        }
    }

    public AwaitableValueVTS(IAsyncValue<TValue> valueReader, WaitHandle handle, int timeout)
    {
        ArgumentNullException.ThrowIfNull(valueReader);
        ArgumentNullException.ThrowIfNull(handle);
        _vts = new(_lock);
        _cstate = new();
        _readerHandle = new(valueReader, handle, timeout, ComputeCompletionCallback);
        _cancelled = new();

        using (var ticket = _unregisterLock.Lock())
        {
            if (!IsCompleted)
            {
                _cancelled.AddHandler(HandleCancellation);
                valueReader.Connect(_cancelled);
            }
            else
            {
                UnregisterEarlyCompletion();
            }

            _cstate.IsConstructed = true;
        }
    }

    protected void UnregisterEarlyCompletion()
    {
        _readerHandle?.Unregister();
        _readerHandle = null;
        _cstate.IsDisposed = true;

        // This cleanup is needed when invoked from derived classes (i.e. CancellableInvokeResultVTS).
        _cancelled.RemoveHandler(HandleCancellation);
        _cancelled.Dispose();
    }

    protected virtual void Unregister()
    {
        using (var ticket = _unregisterLock.Lock())
        {
            if (_cstate.IsConstructed && !_cstate.IsDisposed)
            {
                _readerHandle?.Unregister();
                _readerHandle = null;
                _cstate.IsDisposed = true;

                _cancelled.RemoveHandler(HandleCancellation);
                _cancelled.Dispose();

                // Works only for early completion:
                // _vts?.Reset();
                // _vts = null;
            }
        }
    }

    protected void ComputeCompletionCallback(object? state, bool timedOut)
    {
        // All the extra safety checks here exist because it seems that registered
        // system callback also keeps weak reference to this object, but still
        // (in rare occasions) invokes it after the cleanup.

        try
        {
            // This is a weak reference, so the object may no longer exist.
            var reader = _readerHandle?.Reader();
            TCompletedValue value = (reader is not null) 
                ? ComputeCompletionResult(reader, timedOut) 
                : DefaultDisposedValue();
            _vts.SetResult(value);
            Unregister();
        }
        catch(Exception ex) { Debug.Fail(ex.Message); }
    }


    /// <summary> Compatible with <see cref="CancellationToken"/> notifications. </summary>
    protected void CancellationCallback(object? state)
        => HandleCancellation(state, null);


    /// <summary> Compatible with <see cref="CancellationEventProducer"/>. </summary>
    protected void HandleCancellation(object? s, CancellationEvent? ev)
    {
        TCompletedValue value = DefaultCancelledValue();
        _vts.SetResult(value);
        Unregister();
    }

    public TCompletedValue GetResult(short token)
    {
        return _vts.GetResult(token);
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return  _vts.GetStatus(token);
    }

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _vts.OnCompleted(continuation, state, token, flags);
    }



    //-------------------------------------------------------------------------
    //
    // Private classes & utilities.
    //
    //-------------------------------------------------------------------------

    protected struct CState
    {
        public bool  IsConstructed;

        public bool  IsDisposed;
    }

    protected class ReaderRegistrationHandle
    {
        private WeakReference<IAsyncValue<TValue>?>? _reader;

        private WaitHandle? _handle;

        private RegisteredWaitHandle? _registration;

        public ReaderRegistrationHandle(IAsyncValue<TValue> valueReader, 
                                        WaitHandle handle, 
                                        WaitOrTimerCallback callBack)
        {
            _reader = new(valueReader);
            _handle = handle;
            _registration = ThreadPool.RegisterWaitForSingleObject(_handle, callBack, null, Timeout.Infinite, true);
        }

        public ReaderRegistrationHandle(IAsyncValue<TValue> valueReader,
                                        WaitHandle handle,
                                        int timeout,
                                        WaitOrTimerCallback callBack)
        {
            _reader = new(valueReader);
            _handle = handle;
            _registration = ThreadPool.RegisterWaitForSingleObject(_handle, callBack, null, timeout, true);
        }

        public void Unregister()
        {
            // Yes, trying really hard to help GC here.
            _registration?.Unregister(null);
            _registration = null;
            _handle = null;
            _reader?.SetTarget(null);
            _reader = null;
        }

        /// <summary>
        /// 
        /// It seems that registered system callback also keep weak reference to
        /// this object, but still (in rare occasions) invokes it after the cleanup.
        /// This function tries to handle this case when it happens.
        /// 
        /// </summary>
        /// <returns></returns>
        public IAsyncValue<TValue>? Reader()
        {
            try
            {
                var keepReader = _reader;
                if (keepReader is not null)
                {
                    if (keepReader.TryGetTarget(out var reader))
                    {
                        return reader;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
