namespace DotAsync.Lock;


public interface IFIFOLock
{
    bool IsOwned { get; }

    int QueueLength { get; }

    LockedTicket Lock();

    ValueTask<LockedTicket> LockAsync(bool preserveContext = false);

    bool TryLock(out LockedTicket scope);
}
