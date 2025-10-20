namespace DotAsync.Lock;


/// <summary> 
/// 
/// Disposable wrapper to use with 'using'; disposing releases the lock.
/// 
/// <see cref="Ticket"/> will be negative if lock failed (it was disposed for example).
/// </summary>
public readonly struct LockedTicket 
    : IDisposable
{
    /// <summary> FIFO locks from here return failed tickets after being disposed. </summary>
    public static readonly LockedTicket FAILED = new LockedTicket(-1, null, false);

    public bool Failed { get {  return Ticket < 0; } }

    /// <summary> Negative if lock failed. </summary>
    public readonly long Ticket;

    /// <summary> Implementation-specific ticket handler. </summary>
    internal readonly ITicketHandler? Handler;

    public readonly bool Reentrant;

    internal LockedTicket(long ticket, ITicketHandler? handler, bool reentrant)
    {
        Ticket = ticket;
        Handler = handler;
        Reentrant = reentrant;
    }

    public void Dispose()
    {
        Handler?.Exit(this);
    }
}
