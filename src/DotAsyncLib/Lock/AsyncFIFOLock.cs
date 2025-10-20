using System.Diagnostics;

namespace DotAsync.Lock;

#pragma warning disable CS0420 // CS0420: A reference to a volatile field will not be treated as volatile


/// <summary>
/// 
/// Async FIFO lock using an MCS-style queue (one node per waiter).
/// <list type="bullet">
/// <item>Non-reentrant.</item>
/// <item>Async-friendly: enqueues using <see cref="Interlocked.Exchange"/> immediately.</item>
/// <item>Successor continuations run via TaskCompletionSource; optionally Run() the captured ExecutionContext
///   when completing the successor to preserve Vars semantics for the acquiring flow.</item>
/// </list>
/// </summary>
public sealed class AsyncFIFOLock 
    : FIFOLock
{
    // tail of the queue; null == free
    private volatile McsNode? _tail;


    public AsyncFIFOLock(bool enableDispose = false)
        : base(enableDispose)
    { }

    /// <summary>
    /// 
    /// Enter the lock in FIFO order and return a disposable that releases it.
    /// 
    /// </summary>
    public override LockedValue<T> LockValue<T>(T value)
    {
        return new LockedValue<T>(Lock(), value);
    }

    public override LockedTicket Lock()
    {
        var ticketValueTask = LockAsync().Preserve();
        if (ticketValueTask.IsCompletedSuccessfully)
        {
            return ticketValueTask.Result;
        }
        else if (ticketValueTask.IsCompleted)
        {
            return ticketValueTask.Result;
        }

        var ticketTask = ticketValueTask.AsTask();
        ticketTask.Wait();
        return ticketTask.Result;
    }

    public override bool TryLock(out LockedTicket scope)
    {
        if (IsDisposed)
        {
            scope = LockedTicket.FAILED;
            return false;
        }

        var node = new McsNode(this);

        // Try to set tail from null to this node; if succeeds we acquired immediately
        if (Interlocked.CompareExchange(ref _tail, node, null) == null)
        {
            // Reserve a ticket id (monotonic)
            long my = Interlocked.Increment(ref _nextTicket) - 1;
            Interlocked.Exchange(ref _serving, my);
            node.InitData(my, false);
            scope = node.LockedTicket;
            return true;
        }

        // otherwise fail fast (we did increment _nextTicket; that's acceptable — ticket numbering is monotonic)
        scope = LockedTicket.FAILED;
        return false;
    }

    /// <summary>
    /// 
    /// Lock async (MCS enqueue).
    /// 
    /// <para>If disposed, task will be returned as successful, but ticket in result will be failed.</para>
    /// 
    /// </summary>
    public override ValueTask<LockedTicket> LockAsync(bool preserveContext = false)
    {
        if (IsDisposed)
        {
            return ValueTask.FromResult(LockedTicket.FAILED);
        }

        var node = new McsNode(this);

        // atomic enqueue: prev = Exchange(tail, node)
        var prev = Interlocked.Exchange(ref _tail, node);

        // assign ticket id
        long my = Interlocked.Increment(ref _nextTicket) - 1;
        node.InitData(my, preserveContext);

        if (prev == null)
        {
            // queue was empty -> we own the lock immediately
            Interlocked.Exchange(ref _serving, my);
            return ValueTask.FromResult(node.LockedTicket);
        }

        // link ourselves as successor of prev
        Volatile.Write(ref prev.Next, node);

        // wait for the predecessor to grant ownership by completing our TCS
        return new ValueTask<LockedTicket>(node.VTS, node.VTS.Version);
    }

    /// <summary>
    /// Release path: wake successor if present; otherwise attempt to set tail back to null.
    /// This follows the canonical MCS release algorithm.
    /// </summary>
    public void Exit(LockedTicket ticket)
    {
        // The handler carried the node itself (implements ITicketHandler).
        if (!(ticket.Handler is McsNode node))
        {
            throw new InvalidOperationException("Invalid ticket handler type in AsyncFIFOLock.Exit.");
        }

        // Fast path: if we have no known successor
        var next = Volatile.Read(ref node.Next);
        if (next == null)
        {
            // try to swing tail from node -> null; if success, there was no successor
            if (Interlocked.CompareExchange(ref _tail, null, node) == node)
            {
                // no successor; advance serving to next ticket number
                Interlocked.Exchange(ref _serving, node.Ticket + 1);
                return;
            }

            // someone enqueued but hasn't yet published node.Next; wait for it
            var spin = new SpinWait();
            while ((next = Volatile.Read(ref node.Next)) == null)
            {
                spin.SpinOnce();
            }
        }

        // next != null -> transfer ownership to next
        Debug.Assert(next is not null);
        Interlocked.Exchange(ref _serving, next!.Ticket);

        // Complete the successor's TCS. If it captured ExecutionContext, run under it so Vars/flowing context is correct.
        if (next.CapturedContext is not null)
        {
            var captured = next.CapturedContext;
            next.CapturedContext = null;

            // Running the completion under the captured context is important if callers rely on Vars values
            // to be present as soon as they get the lock.
            ExecutionContext.Run(captured, _ =>
            {
                next.VTS.SetResult(next.LockedTicket);
            }, null);
        }
        else
        {
            next.VTS.SetResult(next.LockedTicket);
        }
    }

    //-------------------------------------------------------------------------
    //
    // Implementation specific tools.
    //
    //-------------------------------------------------------------------------

    private sealed class McsNode : ITicketHandler
    {
        public volatile McsNode? Next;
        public ValueTaskSource<LockedTicket> VTS;
        public LockedTicket LockedTicket;
        public long Ticket;
        public ExecutionContext? CapturedContext;
        private readonly AsyncFIFOLock _owner;


#pragma warning disable CS8618 // CS8618: Non-nullable field 'VTS' must contain a non-null value when exiting constructor.
        public McsNode(AsyncFIFOLock owner)
        {
            _owner = owner;
        }
#pragma warning restore CS8618 // CS8618: Non-nullable field 'VTS' must contain a non-null value when exiting constructor.

        public void InitData(long ticket, bool preserveContext)
        {
            Ticket = ticket;

            VTS = new ValueTaskSource<LockedTicket>();
            LockedTicket = new LockedTicket(ticket, this, reentrant: false);

            // Capture context to allow caller's Vars to be visible when the lock is granted.
            CapturedContext = preserveContext ? ExecutionContext.Capture() : null;
        }

        void ITicketHandler.Exit(in LockedTicket ticket) => _owner.Exit(ticket);
    }
}
