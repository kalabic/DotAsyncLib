namespace DotAsync.AsyncValue;


public interface IAsyncValueHolder
{
    IAsyncValueWaiter GetWaiter();
}
