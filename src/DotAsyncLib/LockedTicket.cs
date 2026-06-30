using DotBase.Log;
using System.Runtime.CompilerServices;

namespace DotAsync;


internal delegate void TicketReleaseHandler(in LockedTicket ticket);


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
    private readonly TicketReleaseHandler? _handler;


    // Implementation >>

    internal LockedTicket(long ticket, TicketReleaseHandler? handler)
    {
        Ticket = ticket;
        _handler = handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Dispose()
    {
        if (_handler != null)
        {
            try 
            {
                _handler(this);
            } 
            catch (Exception ex) 
            {
                LiteLog.Log.ExceptionOccurred("Lock ticket handler threw exception.", ex);
            }
        }
    }
}
