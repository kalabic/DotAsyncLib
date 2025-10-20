namespace DotAsync.Lock;


/// <summary>
/// 
/// Disposable wrapper to use with 'using'; disposing releases the lock.
/// Holds a value payload for convenience.
/// 
/// </summary>
public readonly struct LockedValue<T> : IDisposable
{
    private readonly LockedTicket _ticket;

    public readonly T Value { get; }

    internal LockedValue(LockedTicket ticket, T value)
    {
        _ticket = ticket;
        Value = value;
    }

    public void Dispose()
    {
        _ticket.Dispose();
    }
}
