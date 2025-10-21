using System.Diagnostics;

namespace DotAsync.Lock;


/// <summary>
/// 
/// FIFO queued lock with task-aware ownership and WITHOUT reentrancy.
/// 
/// </summary>
public sealed class FastFIFOLock
    : DisposableFIFOLock
{
    public FastFIFOLock(bool enableDispose = false)
        : base(enableDispose)
    {
    }

    /// <summary>
    /// 
    /// Enter the lock in FIFO order and return a disposable that releases it.
    /// 
    /// </summary>
    public override LockedTicket Lock()
    {
        // First-time acquisition: take a ticket, then wait for our turn.
        long my = GetTicketUsingShortSpinWait();
        if (my < 0)
        {
            return LockedTicket.FAILED;
        }

        // Return a scope that will release on Dispose().
        return new LockedTicket(my, Exit);
    }

    public override ValueTask<LockedTicket> LockAsync(bool preserveContext = false)
    {
        // First-time acquisition: take a ticket, then wait for our turn.
        long my = Interlocked.Increment(ref _nextTicket) - 1;
        if (Volatile.Read(ref _serving) != my)
        {
            var spin = new SpinWait();
            spin.SpinOnce();

            int iters = 0;
            while (!IsDisposed && Volatile.Read(ref _serving) != my)
            {
                if (!spin.NextSpinWillYield)
                {
                    spin.SpinOnce();
                }
                else 
                {
                    if (!preserveContext)
                    {
                        using (ExecutionContext.SuppressFlow())
                        {
                            var task = Task.Run(() =>
                            {
                                SpinWait.SpinUntil(() => !IsDisposed && Volatile.Read(ref _serving) == my);
                                return IsDisposed ? LockedTicket.FAILED : new LockedTicket(my, Exit);
                            });
                            return new ValueTask<LockedTicket>(task);
                        }
                    }
                    else
                    {
                        var task = Task.Run(() =>
                        {
                            SpinWait.SpinUntil(() => !IsDisposed && Volatile.Read(ref _serving) == my);
                            return IsDisposed ? LockedTicket.FAILED : new LockedTicket(my, Exit);
                        });
                        return new ValueTask<LockedTicket>(task);
                    }
                }

                iters++;
            }
        }

        if (IsDisposed)
        {
            return ValueTask.FromResult(LockedTicket.FAILED);
        }

        // Return a scope that will release on Dispose().
        return ValueTask.FromResult(new LockedTicket(my, Exit));
    }

    /// <summary>
    /// 
    /// Non-blocking acquisition.
    /// Returns true if lock acquired, false otherwise.
    /// 
    /// </summary>
    public override bool TryLock(out LockedTicket scope)
    {
        if (IsDisposed)
        {
            scope = LockedTicket.FAILED;
            return false;
        }

        long next = Volatile.Read(ref _nextTicket);
        if (next != Volatile.Read(ref _serving))
        {
            scope = LockedTicket.FAILED;
            return false;
        }

        if (Interlocked.CompareExchange(ref _nextTicket, next + 1, next) != next)
        {
            scope = LockedTicket.FAILED;
            return false;
        }

        Debug.Assert(Volatile.Read(ref _nextTicket) == Volatile.Read(ref _serving) + 1);

        scope = new LockedTicket(next, Exit);
        return true;
    }

    private void Exit(in LockedTicket lockedTicket)
    {
        if (lockedTicket.Ticket == _serving)
        {
            // Fully releasing: clear ownership and advance the ticket being served.
            Interlocked.Increment(ref _serving);
        }
        else
        {
            throw new InvalidOperationException($"Invalid operation on one of {nameof(LockedTicket)} and {nameof(FastFIFOLock)}");
        }
    }
}
