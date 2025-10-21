namespace DotAsync.Lock;


internal delegate void TicketDisposedCallback(in LockedTicket ticket);


/// <summary> 
/// 
/// Disposable wrapper compatible with 'using' syntax; disposing releases the lock.
/// 
/// <see cref="Ticket"/> will be negative if lock failed (it was disposed for example).
/// </summary>
public readonly struct LockedTicket 
    : IDisposable
{
    /// <summary> Disposed locks return failed tickets. </summary>
    internal static readonly LockedTicket FAILED = new LockedTicket(-1, null);


    // Public properties >>

    /// <summary> Disposed locks return failed tickets. </summary>
    public bool Failed { get {  return Ticket < 0; } }

    /// <summary> Negative if lock failed. </summary>
    public readonly long Ticket;


    // Private data >>

    /// <summary> Implementation-specific ticket handler. </summary>
    private readonly TicketDisposedCallback? _handler;


    // Implementation >>

    internal LockedTicket(long ticket, TicketDisposedCallback? handler)
    {
        Ticket = ticket;
        _handler = handler;
    }

    public void Dispose()
    {
        if (_handler != null)
        {
            try 
            {
                _handler(this);
            } finally { }
        }
    }
}
