using DotBase.Core;
using DotBase.Tools;
using System.Diagnostics;

namespace DotAsync.InternalLock;

#pragma warning disable DotAsync_Lock1


/// <summary>
/// 
/// Work in progress. Likely can be replaced with a R/W lock.
/// 
/// <para>
/// Similar to R/W locks, idea here is to allow creation of multiple 
/// 'Invoke InvokeLock' tickets, but only a single 'Disposal InvokeLock'.
/// </para>
/// </summary>
internal class InvokeDisposeLock 
    : DisposableBase
    , IInvokeDisposeLock
{
    // Private data >>

    private readonly IDisposableFIFOLock _internalLock;

    private readonly IDisposableFIFOLock _invokeLock;

    private readonly IDisposableFIFOLock _disposalLock;

    private volatile int _ticketCount = 0;

    private LockedTicket _invokeTicket = LockedTicket.FAILED;


    // Implementation >>

    public InvokeDisposeLock(bool enableDispose = false)
    {
        _internalLock = new FastFIFOLock(enableDispose);
        _invokeLock = new AsyncFIFOLock(enableDispose);
        _disposalLock = new AsyncFIFOLock(enableDispose);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var ticket = DisposalLock();
            Debug.Assert(!ticket.Failed);
            if (!ticket.Failed)
            {
                using (ticket)
                {
                    CloseAndDisableInvoke();
                }
            }
        }
        base.Dispose(disposing);
    }

    public void CloseAndDisableInvoke()
    {
        Debug.Assert(_disposalLock.IsOwned);
        _internalLock.Dispose();
        _invokeLock.Dispose();
        _disposalLock.Dispose();
    }

    public LockedTicket InvokeLock()
    {
        var internalTicket = _internalLock.Lock();
        if (internalTicket.Failed)
        {
            return LockedTicket.FAILED;
        }

        using (internalTicket)
        {
            if (_ticketCount == 0)
            {
                Debug.Assert(_invokeTicket.Failed);
                if (_invokeLock.TryLock(out _invokeTicket))
                {
                    Debug.Assert(!_invokeTicket.Failed);
                }
                else
                {
                    return LockedTicket.FAILED;
                }
            }

            return new LockedTicket(++_ticketCount, Exit);
        }
    }

    public async ValueTask<LockedTicket> DisposalLockAsync()
    {
        var priorityTicket = InternalDisposalLock();
        if (priorityTicket.Failed)
        {
            return LockedTicket.FAILED;
        }
        var asyncTicket = await _invokeLock.LockAsync().ConfigureAwait(false);
        return PriorityTicketHandler.LinkedTickets(asyncTicket, priorityTicket);
    }

    public LockedTicket DisposalLock()
    {
        var priorityTicket = InternalDisposalLock();
        if (priorityTicket.Failed)
        {
            return LockedTicket.FAILED;
        }
        var asyncTicket = _invokeLock.Lock();
        return PriorityTicketHandler.LinkedTickets(asyncTicket, priorityTicket);
    }

    private LockedTicket InternalDisposalLock()
    {
        if (!_disposalLock.TryLock(out var ticket))
        {
            return LockedTicket.FAILED;
        }
        InvalidOperationExtension.ThrowIfTrue(ticket.Failed);
        return ticket;
    }

    private void Exit(in LockedTicket lockedTicket)
    {
        var fifoTicket = _internalLock.Lock();
        Debug.Assert(_ticketCount > 0);
        if (fifoTicket.Failed) // It's ok for this lock to fail because it could be disposed.
        {
            // Extremely rare, doing best effort just in case.
            if (--_ticketCount == 0)
            {
                _invokeTicket.Dispose();
                _invokeTicket = LockedTicket.FAILED;
            }
            return;
        }

        using (fifoTicket)
        {
            if (--_ticketCount == 0)
            {
                Debug.Assert(!_invokeTicket.Failed);
                _invokeTicket.Dispose();
                _invokeTicket = LockedTicket.FAILED;
            }
        }
    }
}
