using DotAsync.AsyncValue;
using DotAsync.InternalTools;
using DotAsync.InternalVTS;
using DotBase.Core;
using DotBase.Event;
using DotBase.Tools;

namespace DotAsync;


/// <summary>
/// 
/// A container for asynchronous/awaitable generic value that can be awaited using
/// functions from <see cref="IAsyncValueWaiter"/> interface.
/// 
/// </summary>
/// <typeparam name="TValue"></typeparam>
public class AValue<TValue>
    : DisposableBase
    , IAsyncValue<TValue>
{
    // Public properties >>

    /// <summary>
    /// 
    /// Attempts to convert the current value to an <see cref="InvokeResult"/>.
    /// Returns <see cref="InvokeResult.FAILED"/> if <typeparamref name="TValue"/> 
    /// is not an unmanaged type or cannot be represented as a 64-bit value.
    /// 
    /// <para>Never throws.</para>
    /// </summary>
    public virtual InvokeResult AsInvokeResult()
    {
        if (GenericType<TValue>.IsUnmanaged)
        {
            return new InvokeResult(GenericTools.ToLong(Value));
        }
        else
        {
            return InvokeResult.FAILED;
        }
    }

    public bool IsCancelled { get { return IsDisposed; } }

    public bool IsSet
    {
        get 
        {
            using (var ticket = _event.Lock())
            {
                if (!ticket.Failed)
                {
                    return _event.IsSet;
                }                    
            }

            return false;
        }
    }

    public TValue Value { get { return _value; } }


    // Private data >>

    private readonly LockedManualResetEvent _event;

    private readonly AsyncValueWaiter<TValue> _valueWaiter;

    private readonly CancellationEventProducer _cancelled;

#pragma warning disable CS8601 // Possible null reference assignment.
    protected TValue _value = default;
#pragma warning restore CS8601 // Possible null reference assignment.

    // Implementation >>

    public AValue()
    {
        _event = new();
        _valueWaiter = new(this, _event);
        _cancelled = new();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            using (var ticket = _event.Lock())
            {
                if (!ticket.Failed)
                {
                    if (!_event.IsSet)
                    {
                        _cancelled.Invoke();
                    }
                }
            }

            _event.Dispose();
            _cancelled.Dispose();
        }
        base.Dispose(disposing);
    }

    public void Connect(CancellationEventConsumer eventConsumer)
    {
        _cancelled.SendTo(eventConsumer);
    }

    public void Reset()
    {
        using (var ticket = _event.Lock())
        {
            if (!ticket.Failed)
            {
                _event.Reset();
            }
        }
    }

    public void Set() 
    {
        using (var ticket = _event.Lock())
        {
            if (!ticket.Failed)
            {
                _event.Set();
            }
        }
    }

    public void Set(TValue value)
    {
        using (var ticket = _event.Lock())
        {
            if (!ticket.Failed)
            {
                _value = value;
                _event.Set();
            }
        }
    }

    public ValueTask<InvokeResult> WaitAsync()
        => _valueWaiter.WaitAsync();

    public ValueTask<InvokeResult> WaitAsync(int timeout)
        => _valueWaiter.WaitAsync(timeout);

    public ValueTask<InvokeResult> WaitAsync(CancellationToken cancellation)
        => _valueWaiter.WaitAsync(cancellation);

    public ValueTask<InvokeResult> WaitAsync(int timeout, CancellationToken cancellation)
        => _valueWaiter.WaitAsync(timeout, cancellation);

    public InvokeResult Wait(int timeout = Timeout.Infinite)
        => _valueWaiter.Wait(timeout);
}
