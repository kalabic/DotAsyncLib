using DotBase.Tools;
using DotBase.Core;

namespace DotAsync.Lock;


public class PriorityFIFOLock
    : DisposableBase
{
    private readonly AsyncFIFOLock _asyncLock;

    private readonly AsyncFIFOLock _priorityLock;

    public PriorityFIFOLock(bool enableDispose = false)
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

    public int DisposeAndWaitEmptyQueue()
    {
        int length = _priorityLock.DisposeAndWaitEmptyQueue();
        length += _asyncLock.DisposeAndWaitEmptyQueue();
        return length;
    }

    public LockedTicket TryLock()
    {
        if (!_asyncLock.TryLock(out var asyncTicket))
        {
            return LockedTicket.FAILED;
        }
        InvalidOperationExtension.ThrowIfTrue(asyncTicket.Failed);

        if (!_priorityLock.TryLock(out var priorityTicket))
        {
            asyncTicket.Dispose();
            return LockedTicket.FAILED;
        }
        InvalidOperationExtension.ThrowIfTrue(priorityTicket.Failed);

        return new LockedTicket(priorityTicket.Ticket, new PriorityTicketHandler(asyncTicket, priorityTicket), false);
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

        return new LockedTicket(priorityTicket.Ticket, new PriorityTicketHandler(asyncTicket, priorityTicket), false);
    }

    public async ValueTask<LockedTicket> LockAsync()
    {
        var asyncTicket = await _asyncLock.LockAsync().ConfigureAwait(false);
        if (asyncTicket.Failed)
        {
            return asyncTicket;
        }

        var priorityTicket = await _priorityLock.LockAsync().ConfigureAwait(false);
        if (priorityTicket.Failed)
        {
            asyncTicket.Dispose();
            return priorityTicket;
        }

        return new LockedTicket(priorityTicket.Ticket, new PriorityTicketHandler(asyncTicket, priorityTicket), false);
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
