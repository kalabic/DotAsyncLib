using DotBase.Core;
using DotBase.Tools;
using System.Diagnostics.CodeAnalysis;

namespace DotAsync.InternalLock;


#pragma warning disable DotAsync_Lock0


internal class PrioritizedLock
    : DisposableBase
    , IDisposablePrioritizedLock
{
    // Public properties >>

    public bool IsOwned { get { return _priorityLock.IsOwned; } }

    public int QueueLength { get { return _priorityLock.QueueLength; } }


    // Private data >>

    private readonly IDisposableFIFOLock _asyncLock;

    private readonly IDisposableFIFOLock _priorityLock;


    // Implementation >>

    public PrioritizedLock(bool enableDispose = false)
    {
        _asyncLock = new AsyncFIFOLock(enableDispose);
        _priorityLock = new AsyncFIFOLock(enableDispose);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _priorityLock.Dispose();
            _asyncLock.Dispose();
        }
        base.Dispose(disposing);
    }

    [Experimental("DotAsync_Lock0")]
    public int DisposeAndWaitEmptyQueue()
    {
        int length = _priorityLock.DisposeAndWaitEmptyQueue();
        length += _asyncLock.DisposeAndWaitEmptyQueue();
        return length;
    }

    public bool TryLock(out LockedTicket scope)
    {
        if (!_asyncLock.TryLock(out var asyncTicket))
        {
            scope = LockedTicket.FAILED;
            return false;
        }
        InvalidOperationExtension.ThrowIfTrue(asyncTicket.Failed);

        if (!_priorityLock.TryLock(out var priorityTicket))
        {
            asyncTicket.Dispose();
            scope = LockedTicket.FAILED;
            return false;
        }
        InvalidOperationExtension.ThrowIfTrue(priorityTicket.Failed);

        scope = PriorityTicketHandler.LinkedTickets(asyncTicket, priorityTicket);
        return true;
    }

    public LockedTicket Lock()
    {
        var asyncTicket = _asyncLock.Lock();
        if (asyncTicket.Failed)
        {
            return asyncTicket;
        }

        var priorityTicket = _priorityLock.Lock();
        if (priorityTicket.Failed)
        {
            asyncTicket.Dispose();
            return priorityTicket;
        }

        return PriorityTicketHandler.LinkedTickets(asyncTicket, priorityTicket);
    }

    public async ValueTask<LockedTicket> LockAsync(bool preserveContext = false)
    {
        var asyncTicket = await _asyncLock.LockAsync(preserveContext).ConfigureAwait(false);
        if (asyncTicket.Failed)
        {
            return asyncTicket;
        }

        var priorityTicket = await _priorityLock.LockAsync(preserveContext).ConfigureAwait(false);
        if (priorityTicket.Failed)
        {
            asyncTicket.Dispose();
            return priorityTicket;
        }

        return PriorityTicketHandler.LinkedTickets(asyncTicket, priorityTicket);
    }

    public LockedTicket PriorityLock()
    {
        return _priorityLock.Lock();
    }

    public ValueTask<LockedTicket> PriorityLockAsync()
    {
        return _priorityLock.LockAsync();
    }
}
