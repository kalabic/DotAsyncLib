using DotBase.Event;

namespace DotAsync.Model;


public interface IAsyncValue<TValue> 
    : IAsyncValueReader<TValue>
    , IAsyncValueWaiter
{
    InvokeResult AsInvokeResult();

    void Reset();

    void Set();

    void Set(TValue value);

    void Connect(CancellationEventConsumer eventConsumer);
}
