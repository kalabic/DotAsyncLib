using DotBase.Core;
using DotBase.Tools;
using System.Diagnostics.CodeAnalysis;

namespace DotAsync.InternalLock;


internal abstract class DisposableFIFOLock
    : DisposableBase
    , IDisposableFIFOLock
{
    protected const int ITERS_SPIN   = 100;
    protected const int ITERS_YIELD  = 200;
    protected const int ITERS_SLEEP0 = 400;


    //-------------------------------------------------------------------------
    //
    // Public properties and interfaces.
    //
    //-------------------------------------------------------------------------

    public virtual bool IsOwned => Volatile.Read(ref _nextTicket) > Volatile.Read(ref _serving);

    public virtual int QueueLength { get { return (int)(Interlocked.Read(ref _nextTicket) - Interlocked.Read(ref _serving)); } }

    /// <summary> If disposed, returned ticket will be in 'failed' state. </summary>
    public abstract LockedTicket Lock();

    /// <summary> If disposed, task will be returned as successful, but ticket in result will be in 'failed' state. </summary>
    public abstract ValueTask<LockedTicket> LockAsync(bool preserveContext = false);

    /// <summary> If immediate lock acquisition fails or if disposed, returned ticket will be null. </summary>
    public abstract bool TryLock(out LockedTicket scope);


    //-------------------------------------------------------------------------
    //
    // Private data.
    //
    //-------------------------------------------------------------------------

    /// <summary> The new ticket to hand out. Starts at 0. </summary>
    protected long _nextTicket;

    /// <summary> The ticket to be served. Starts at 0. </summary>
    protected long _serving;

    private bool _enableDispose;


    //-------------------------------------------------------------------------
    //
    // Implementation.
    //
    //-------------------------------------------------------------------------

    protected DisposableFIFOLock(bool enableDispose)
    {
        _enableDispose = enableDispose;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            InvalidOperationExtension.ThrowIfFalse(_enableDispose);
        }
        base.Dispose(disposing);
    }

    protected long GetTicketUsingSpinWait()
    {
        long ticket = Interlocked.Increment(ref _nextTicket) - 1;
        var spin = new SpinWait();
        int iters = 0;
        while (!IsDisposed && Volatile.Read(ref _serving) != ticket)
        {
            if (iters < ITERS_SPIN) spin.SpinOnce();
            else if (iters < ITERS_YIELD) Thread.Yield();
            else if (iters < ITERS_SLEEP0) Thread.Sleep(0);
            else Thread.Sleep(1);
            iters++;
        }

        if (IsDisposed)
        {
            if (Volatile.Read(ref _serving) == ticket)
            {
                // Fully releasing: clear ownership and advance the ticket being served.
                Interlocked.Increment(ref _serving);
            }
            return -1;
        }
        else
        {
            return ticket;
        }
    }

    protected long GetTicketUsingShortSpinWait()
    {
        long ticket = Interlocked.Increment(ref _nextTicket) - 1;
        var spin = new SpinWait();
        int iters = 0;
        while (!IsDisposed && Volatile.Read(ref _serving) != ticket)
        {
            if (!spin.NextSpinWillYield) spin.SpinOnce();
            else if (iters < ITERS_YIELD) Thread.Yield();
            else if (iters < ITERS_SLEEP0) Thread.Sleep(0);
            iters++;
        }

        if (IsDisposed)
        {
            if (Volatile.Read(ref _serving) == ticket)
            {
                // Fully releasing: clear ownership and advance the ticket being served.
                Interlocked.Increment(ref _serving);
            }
            return -1;
        }
        else
        {
            return ticket;
        }
    }

    [Experimental("DotAsync_Lock0")]
    public int DisposeAndWaitEmptyQueue()
    {
        int length = QueueLength;
        Dispose();
        var spin = new SpinWait();
        int iters = 0;
        while (QueueLength > 0)
        {
            if (!spin.NextSpinWillYield) spin.SpinOnce();
            else if (iters < ITERS_YIELD) Thread.Yield();
            else if (iters < ITERS_SLEEP0) Thread.Sleep(0);
            iters++;
        }
        return length;
    }
}
