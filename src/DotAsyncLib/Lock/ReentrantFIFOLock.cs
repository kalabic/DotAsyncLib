using System.Collections.Concurrent;

namespace DotAsync.Lock;


/// <summary>
/// 
/// FIFO queued lock with task-aware ownership and optional reentrancy.
/// - Synchronous acquisition only (no async wait). Use with 'using'.
/// - Fair (FIFO) among non-reentrant acquisitions.
/// - Reentrant by the same async flow (owner can enter repeatedly).
/// 
/// <para><c>IMPORTANT: NEVER await while holding this lock.</c></para>
/// 
/// </summary>
public sealed class ReentrantFIFOLock 
    : FIFOLock
{
    private readonly AsyncLocal<OwnerState> _owner = new();

    private struct OwnerState { public long Ticket; public int Depth; }

    /// <summary>  Waiter registry: maps ticket -> waiter </summary>
    private readonly ConcurrentDictionary<long, Waiter> _waiters = new ConcurrentDictionary<long, Waiter>();

    private readonly ReentrantFIFOTicketHandler _handler;


    internal ReentrantFIFOLock(bool enableDispose = false)
        : base(enableDispose)
    {
        _handler = new(this);
    }

    public override LockedValue<T> LockValue<T>(T value)
    {
        return new LockedValue<T>(Lock(), value);
    }

    public override LockedTicket Lock()
    {
        var state = _owner.Value;
        if (state.Depth > 0)
        {
            state.Depth++;
            _owner.Value = state;
            return new LockedTicket(state.Ticket, _handler, reentrant: true);
        }

        long my = GetTicketUsingSpinWait();
        if (my < 0)
        {
            return LockedTicket.FAILED;
        }

        _owner.Value = new OwnerState { Ticket = my, Depth = 1 };
        return new LockedTicket(my, _handler, reentrant: false);
    }

    public override bool TryLock(out LockedTicket scope)
    {
        if (IsDisposed)
        {
            scope = LockedTicket.FAILED;
            return false;
        }

        var state = _owner.Value;
        if (state.Depth > 0)
        {
            state.Depth++;
            _owner.Value = state;
            scope = new LockedTicket(state.Ticket, _handler, reentrant: true);
            return true;
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

        _owner.Value = new OwnerState { Ticket = next, Depth = 1 };
        scope = new LockedTicket(next, _handler, reentrant: false);
        return true;
    }

    /// <summary> TODO: Switch to algorithm used by <see cref="AsyncFIFOLock"/>. </summary>
    public override ValueTask<LockedTicket> LockAsync(bool preserveContext = false)
    {
        if (IsDisposed)
        {
            return ValueTask.FromResult(LockedTicket.FAILED);
        }

        // Reentrant fast path.
        var state = _owner.Value;
        if (state.Depth > 0)
        {
            state.Depth++;
            _owner.Value = state;
            return ValueTask.FromResult(new LockedTicket(state.Ticket, _handler, reentrant: true));
        }

        // Issue a ticket
        long my = Interlocked.Increment(ref _nextTicket) - 1;

        // Fast inline spin: try to acquire quickly
        var spin = new SpinWait();
        int iters = 0;
        while (!IsDisposed && Volatile.Read(ref _serving) != my)
        {
            if (iters < ITERS_SPIN) spin.SpinOnce();
            else if (iters < ITERS_YIELD) Thread.Yield();
            else if (iters < ITERS_SLEEP0) Thread.Sleep(0);
            else
            {
                var waiter = _waiters.GetOrAdd(my, _ => new Waiter(my, preserveContext));
                return new ValueTask<LockedTicket>(waiter.VTS, waiter.VTS.Version);
            }
            iters++;
        }

        if (IsDisposed)
        {
            return ValueTask.FromResult(LockedTicket.FAILED);
        }

        // If we reach here, it's our turn immediately
        _owner.Value = new OwnerState { Ticket = my, Depth = 1 };
        return ValueTask.FromResult(new LockedTicket(my, _handler, reentrant: false));
    }

    internal void Exit(LockedTicket lockedTicket)
    {
        var state = _owner.Value;

        if (state.Depth <= 0)
        {
            throw new SynchronizationLockException("ReentrantFIFOLock: exit without ownership.");
        }

        if (lockedTicket.Reentrant)
        {
            state.Depth--;
            _owner.Value = state;
            return;
        }

        // Outer release
        state.Depth--;
        if (state.Depth > 0)
        {
            _owner.Value = state;
            return;
        }

        // Fully releasing: clear async-local owner and advance the ticket being served.
        _owner.Value = default;

        // increment the serving counter and wake next waiter if present
        long newServing = Interlocked.Increment(ref _serving);

        // Try to wake the waiter that holds ticket == newServing
        if (_waiters.TryRemove(newServing, out var waiter))
        {
            // Run completion under the captured ExecutionContext so the awaiting flow gets the proper Vars
            if (waiter.CapturedContext is not null)
            {
                var captured = waiter.CapturedContext;
                waiter.CapturedContext = null;
                ExecutionContext.Run(captured, _ =>
                {
                    // In the captured ExecutionContext: set owner state so the awaiting continuation sees IsOwned==true
                    _owner.Value = new OwnerState { Ticket = waiter.Ticket, Depth = 1 };

                    // Set the result; the continuation will execute under the captured context.
                    waiter.VTS.SetResult(new LockedTicket(waiter.Ticket, _handler, reentrant: false));
                }, null);
            }
            else
            {
                // No captured context — just set owner and complete (rare).
                _owner.Value = new OwnerState { Ticket = waiter.Ticket, Depth = 1 };
                waiter.VTS.SetResult(new LockedTicket(waiter.Ticket, _handler, reentrant: false));
            }
        }
    }


    //-------------------------------------------------------------------------
    //
    // Implementation specific tools.
    //
    //-------------------------------------------------------------------------

    private readonly struct ReentrantFIFOTicketHandler
        : ITicketHandler
    {
        private readonly ReentrantFIFOLock _main;

        internal ReentrantFIFOTicketHandler(ReentrantFIFOLock main) { _main = main; }

        public void Exit(in LockedTicket lockedTicket)
        {
            _main.Exit(lockedTicket);
        }
    }

    private sealed class Waiter
    {
        public readonly long Ticket;
        public readonly ValueTaskSource<LockedTicket> VTS;
        public ExecutionContext? CapturedContext;

        public Waiter(long ticket, bool preserveContext)
        {
            Ticket = ticket;
            VTS = new ValueTaskSource<LockedTicket>();
            CapturedContext = preserveContext ? ExecutionContext.Capture() : null;
        }
    }
}
