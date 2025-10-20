using DotBase.Core;
using DotAsync.Lock;

namespace DotAsync.Tools;


/// <summary>
/// 
/// Besides providing <see cref="ManualResetEventSlim"/> and access to its <see cref="System.Threading.WaitHandle"/>,
/// this class provides and access to <see cref="Lock.DisposeLock"/> intended to be used by parent class for locking
/// ITS OWN state. This is to simplify and optimize implementation and is used by <see cref="AValue{TValue}"/>
/// and <see cref="InternalVTS.AsyncValueWaiter{TValue}"/>.
/// 
/// </summary>
internal class LockedManualResetEvent 
    : DisposableBase
{
    // Public properties >>

    public bool IsSet { get { return _event.IsSet; } }

    public WaitHandle WaitHandle { get { return _event.WaitHandle; } }


    // Private data >>

    private readonly ManualResetEventSlim _event = new();

    private readonly DisposeLock _disposeLock = new(true);


    // Implementation >>

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            using var ticket = _disposeLock.PriorityLock();
            _disposeLock.DisposeLocked();
            _event.Dispose();
            // base.Dispose also invoked under 'ticket' scope before return.
            base.Dispose(disposing);
            return;
        }
        base.Dispose(disposing);
    }

    public LockedTicket Lock()
        => _disposeLock.Lock();

    public void Set()
        => _event.Set();

    public void Reset()
        => _event.Reset();
}
