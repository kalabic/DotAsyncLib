using DotBase.Tools;
using DotBase.Core;
using System.Diagnostics;

namespace DotAsync.Lock;


/// <summary>
/// WIP.
/// <para>Idea is to allow creation of multiple 'Lock' tickets, but only a single 'PriorityLock'.</para>
/// </summary>
public class DisposeLock 
    : DisposableBase
    , ITicketHandler
{
    public bool IsOwned => _ticketCount > 0;

    public int QueueLength => _ticketCount;


    // Private data >>

    private readonly FastFIFOLock _fifoLock;

    private readonly AsyncFIFOLock _asyncLock;

    private readonly AsyncFIFOLock _priorityLock;

    private volatile int _ticketCount = 0;

    private LockedTicket _asyncTicket = LockedTicket.FAILED;


    // Implementation >>

    public DisposeLock(bool enableDispose = false)
    {
        _fifoLock = new(enableDispose);
        _asyncLock = new(enableDispose);
        _priorityLock = new(enableDispose);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var ticket = PriorityLock();
            Debug.Assert(!ticket.Failed);
            if (!ticket.Failed)
            {
                using (ticket)
                {
                    _fifoLock.Dispose();
                    _asyncLock.Dispose();
                    _priorityLock.Dispose();
                }
            }
        }
        base.Dispose(disposing);
    }

    public void DisposeLocked()
    {
        Debug.Assert(_priorityLock.IsOwned);
        _fifoLock.Dispose();
        _asyncLock.Dispose();
        _priorityLock.Dispose();
    }

    public LockedTicket Lock()
    {
        var fifoTicket = _fifoLock.Lock();
        if (fifoTicket.Failed)
        {
            return LockedTicket.FAILED;
        }

        using (fifoTicket)
        {
            if (_ticketCount == 0)
            {
                Debug.Assert(_asyncTicket.Failed);
                if (_asyncLock.TryLock(out _asyncTicket))
                {
                    Debug.Assert(!_asyncTicket.Failed);
                }
                else
                {
                    return LockedTicket.FAILED;
                }
            }

            return new LockedTicket(++_ticketCount, this, false);
        }
    }

    public async ValueTask<LockedTicket> PriorityLockAsync()
    {
        var priorityTicket = InternalPriorityLock();
        if (priorityTicket.Failed)
        {
            return LockedTicket.FAILED;
        }
        var asyncTicket = await _asyncLock.LockAsync().ConfigureAwait(false);
        return new LockedTicket(priorityTicket.Ticket, new PriorityTicketHandler(asyncTicket, priorityTicket), false);
    }

    public LockedTicket PriorityLock()
    {
        var priorityTicket = InternalPriorityLock();
        if (priorityTicket.Failed)
        {
            return LockedTicket.FAILED;
        }
        var asyncTicket = _asyncLock.Lock();
        return new LockedTicket(priorityTicket.Ticket, new PriorityTicketHandler(asyncTicket, priorityTicket), false);
    }

    private LockedTicket InternalPriorityLock()
    {
        if (!_priorityLock.TryLock(out var ticket))
        {
            return LockedTicket.FAILED;
        }
        InvalidOperationExtension.ThrowIfTrue(ticket.Failed);
        return ticket;
    }

    public void Exit(in LockedTicket lockedTicket)
    {
        var fifoTicket = _fifoLock.Lock();
        Debug.Assert(_ticketCount > 0);
        if (fifoTicket.Failed) // It's ok for this lock to fail because it could be disposed.
        {
            // Extremely rare, doing best effort just in case.
            if (--_ticketCount == 0)
            {
                _asyncTicket.Dispose();
                _asyncTicket = LockedTicket.FAILED;
            }
            return;
        }

        using (fifoTicket)
        {
            if (--_ticketCount == 0)
            {
                Debug.Assert(!_asyncTicket.Failed);
                _asyncTicket.Dispose();
                _asyncTicket = LockedTicket.FAILED;
            }
        }
    }
}
