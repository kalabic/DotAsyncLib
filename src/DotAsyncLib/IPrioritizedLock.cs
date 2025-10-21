using System.Diagnostics.CodeAnalysis;

namespace DotAsync;


public interface IPrioritizedLock 
    : IFIFOLock
{
    LockedTicket PriorityLock();

    ValueTask<LockedTicket> PriorityLockAsync();
}


public interface IDisposablePrioritizedLock
    : IPrioritizedLock
    , IDisposable
{
    [Experimental("DotAsync_Lock0")]
    int DisposeAndWaitEmptyQueue();
}
