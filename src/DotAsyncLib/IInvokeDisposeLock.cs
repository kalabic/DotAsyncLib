using System.Diagnostics.CodeAnalysis;

namespace DotAsync;


[Experimental("DotAsync_Lock1")]
public interface IInvokeDisposeLock
{
    void CloseAndDisableInvoke();

    LockedTicket DisposalLock();

    LockedTicket InvokeLock();
}
